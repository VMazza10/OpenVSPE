using System.Net;
using System.Net.Sockets;
using OpenVSPE.Core.Interfaces;
using OpenVSPE.Core.Logging;
using OpenVSPE.Core.Models;

namespace OpenVSPE.Printing
{
    /// <summary>
    /// Canal que recebe dados via TCP e envia diretamente para uma impressora
    /// instalada no Windows em modo RAW (sem processamento pelo driver).
    /// Útil para impressoras USB que não possuem driver de porta COM virtual.
    /// </summary>
    public class PrinterChannel : IChannel
    {
        private readonly LogManager _log;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private readonly List<TcpClient> _clientesAtivos = new();
        private readonly object _lockClientes = new();
        private Task? _listenTask;
        private int _trabalhosEnviados;

        public ChannelConfig Config { get; }
        public ChannelStatus Status { get; private set; } = ChannelStatus.Parado;
        public string? UltimoErro { get; private set; }
        public int ConexoesAtivas
        {
            get { lock (_lockClientes) return _clientesAtivos.Count; }
        }

        public PrinterChannel(ChannelConfig config, LogManager log)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Config.NomeImpressora))
                throw new InvalidOperationException("Nome da impressora não configurado.");

            // Valida se a impressora existe (pula se for SCREEN para depuração)
            if (Config.NomeImpressora != "SCREEN" && !RawPrinterHelper.PrinterExists(Config.NomeImpressora))
            {
                throw new InvalidOperationException($"A impressora '{Config.NomeImpressora}' não foi encontrada no Windows.\nVerifique o nome exato no Painel de Controle -> Dispositivos e Impressoras.");
            }

            Status = ChannelStatus.Iniciando;
            UltimoErro = null;
            _trabalhosEnviados = 0;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                var ip = IPAddress.Parse(Config.IpEscuta);
                _tcpListener = new TcpListener(ip, Config.PortaTcp);
                _tcpListener.Start();

                Status = ChannelStatus.Ativo;
                _log.Info($"Canal iniciado: Impressora '{Config.NomeImpressora}' -> TCP {Config.IpEscuta}:{Config.PortaTcp}", Config.Nome);

                _listenTask = Task.Run(() => AceitarConexoesAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                Status = ChannelStatus.Erro;
                UltimoErro = ex.Message;
                _log.Erro($"Falha ao iniciar canal: {ex.Message}", Config.Nome, ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            _log.Info("Parando canal de impressora...", Config.Nome);
            Status = ChannelStatus.Parado;

            _cts?.Cancel();

            lock (_lockClientes)
            {
                foreach (var client in _clientesAtivos)
                {
                    try { client.Close(); } catch { }
                }
                _clientesAtivos.Clear();
            }

            try { _tcpListener?.Stop(); } catch { }

            if (_listenTask != null)
            {
                try { await _listenTask; } catch { }
            }

            _log.Info($"Canal parado. Total de trabalhos enviados: {_trabalhosEnviados}", Config.Nome);
        }

        private async Task AceitarConexoesAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener!.AcceptTcpClientAsync(ct);
                    var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
                    var ipCliente = endpoint?.Address.ToString() ?? "desconhecido";

                    // Verifica restrição de IP
                    if (Config.IpsPermitidos.Count > 0 && !Config.IpsPermitidos.Contains(ipCliente))
                    {
                        _log.Aviso($"Conexão rejeitada de IP não autorizado: {ipCliente}", Config.Nome);
                        client.Close();
                        continue;
                    }

                    _log.Info($"Nova conexão TCP de {ipCliente}:{endpoint?.Port}", Config.Nome);
                    lock (_lockClientes) _clientesAtivos.Add(client);

                    // Processa o trabalho de impressão em background
                    _ = Task.Run(() => ProcessarTrabalhoAsync(client, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        _log.Erro($"Erro ao aceitar conexão: {ex.Message}", Config.Nome, ex);
                        await Task.Delay(1000, ct);
                    }
                }
            }
        }

        private async Task ProcessarTrabalhoAsync(TcpClient client, CancellationToken ct)
        {
            var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
            var clienteInfo = $"{endpoint?.Address}:{endpoint?.Port}";

            try
            {
                using var networkStream = client.GetStream();
                using var msPrint = new MemoryStream(); // Dados reais de impressão
                var buffer = new byte[8192];
                bool apenasStatus = false;

                // Lê todos os dados até o cliente fechar a conexão ou timeout de inatividade
                var ctsRead = CancellationTokenSource.CreateLinkedTokenSource(ct);
                ctsRead.CancelAfter(5000); // 5 segundos de espera máxima por novos dados

                while (client.Connected && !ct.IsCancellationRequested)
                {
                    try
                    {
                        var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, ctsRead.Token);
                        if (bytesRead == 0) break; // Cliente fechou a conexão graciosamente

                        // Sempre loga bytes completos de pacotes curtos para diagnóstico
                        var hexRecebido = bytesRead <= 5
                            ? string.Join(" ", buffer.Take(bytesRead).Select(b => $"0x{b:X2}"))
                            : $"0x{buffer[0]:X2}... ({bytesRead} bytes)";

                        // === FAKE ACK: Intercepta comandos de status do PDV ===
                        // O PDV envia consultas de status e ESPERA resposta antes de imprimir
                        // Se não receber resposta, reporta "Com Erro" e fica em loop

                        bool ehConsultaStatus = false;

                        // 1) DLE EOT (0x10 0x04 n) — Status em tempo real da impressora
                        if (bytesRead <= 5 && buffer[0] == 0x10 && bytesRead >= 2 && buffer[1] == 0x04)
                        {
                            ehConsultaStatus = true;
                            // 0x16 = "online, sem erro, papel presente" para Bematech ESC/POS
                            byte[] resp = { 0x16 };
                            try
                            {
                                await networkStream.WriteAsync(resp, 0, resp.Length, ct);
                                await networkStream.FlushAsync(ct);
                                _log.Info($"DLE EOT [{hexRecebido}] → ACK 0x16 (online)", Config.Nome);
                            }
                            catch { }
                        }
                        // 2) GS r n (0x1D 0x72 n) — Consulta sensor de papel
                        else if (bytesRead <= 5 && buffer[0] == 0x1D && bytesRead >= 2 && buffer[1] == 0x72)
                        {
                            ehConsultaStatus = true;
                            // 0x00 = Papel presente, sem fim de papel (mais padrão que 0x12)
                            byte[] resp = { 0x00 };
                            try
                            {
                                await networkStream.WriteAsync(resp, 0, resp.Length, ct);
                                await networkStream.FlushAsync(ct);
                                _log.Info($"GS r (sensor papel) [{hexRecebido}] → ACK 0x00 (papel OK)", Config.Nome);
                            }
                            catch { }
                        }
                        // 3) ESC v (0x1B 0x76) — Consulta status básico (padrão Bematech/Epson)
                        else if (bytesRead <= 5 && buffer[0] == 0x1B && bytesRead >= 2 && buffer[1] == 0x76)
                        {
                            ehConsultaStatus = true;
                            // 0x00 = Impressora pronta/online
                            byte[] resp = { 0x00 };
                            try
                            {
                                await networkStream.WriteAsync(resp, 0, resp.Length, ct);
                                await networkStream.FlushAsync(ct);
                                _log.Info($"ESC v (status) [{hexRecebido}] → ACK 0x00 (pronto)", Config.Nome);
                            }
                            catch { }
                        }
                        // 4) ENQ (0x05) — Status de hardware (muito usado por drivers Bematech antigos)
                        else if (bytesRead == 1 && buffer[0] == 0x05)
                        {
                            ehConsultaStatus = true;
                            // 0x00 = OK / Pronto
                            byte[] resp = { 0x00 };
                            try
                            {
                                await networkStream.WriteAsync(resp, 0, resp.Length, ct);
                                await networkStream.FlushAsync(ct);
                                _log.Info($"ENQ (status hardware) [{hexRecebido}] → ACK 0x00", Config.Nome);
                            }
                            catch { }
                        }
                        // 5) GS 0xF8 n (0x1D 0xF8 n) — Consulta Status Exclusiva BEMATECH (BemaESC)
                        // Identificado nos logs do cliente: [0x1D 0xF8 0x31]
                        else if (bytesRead <= 5 && buffer[0] == 0x1D && bytesRead >= 2 && (byte)buffer[1] == 0xF8)
                        {
                            ehConsultaStatus = true;
                            // Resposta padrão Bematech para status OK (Online/Papel OK)
                            // Geralmente 0x00 ou o próprio byte de status ESC/POS
                            byte[] resp = { 0x00 }; 
                            try
                            {
                                await networkStream.WriteAsync(resp, 0, resp.Length, ct);
                                await networkStream.FlushAsync(ct);
                                _log.Info($"BemaESC Status [{hexRecebido}] → ACK 0x00", Config.Nome);
                            }
                            catch { }
                        }
                        // 6) GS a n (0x1D 0x61 n) — Configura ASB (Automatic Status Back)
                        else if (bytesRead <= 5 && buffer[0] == 0x1D && bytesRead >= 2 && buffer[1] == 0x61)
                        {
                            ehConsultaStatus = true; // Sem resposta necessária, apenas ignora para não travar
                            _log.Info($"GS a (ASB config) [{hexRecebido}] → ignorado (sem resposta)", Config.Nome);
                        }
                        // 6) DLE genérico (0x10) — Se cair aqui é DLE mas não EOT
                        else if (bytesRead <= 5 && buffer[0] == 0x10)
                        {
                            ehConsultaStatus = true;
                            byte[] resp = { 0x16 }; // Bematech online
                            try
                            {
                                await networkStream.WriteAsync(resp, 0, resp.Length, ct);
                                await networkStream.FlushAsync(ct);
                                _log.Info($"DLE genérico [{hexRecebido}] → ACK 0x16", Config.Nome);
                            }
                            catch { }
                        }

                        if (ehConsultaStatus)
                        {
                            apenasStatus = true; // não acumula no payload de impressão
                        }
                        else
                        {
                            // Dados reais de impressão (ou corte GS V, etc.) — acumula normalmente
                            apenasStatus = false;
                            msPrint.Write(buffer, 0, bytesRead);
                            if (bytesRead <= 5)
                                _log.Info($"Dados curtos [{hexRecebido}] acumulados para impressão", Config.Nome);
                        }

                        // Recebeu dados, reseta o timeout para aguardar rapidamente o restante (se houver)
                        // 300ms é tempo de sobra pra rede local. Promove "impressão instantânea"
                        ctsRead.CancelAfter(300);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout alcançado (cliente parou de enviar ou conexão cortada abruptamente)
                        break;
                    }
                    catch (IOException)
                    {
                        break; // Desconexão subjacente
                    }
                }

                var dados = msPrint.ToArray();
                if (dados.Length > 0)
                {
                    _log.Info($"Recebido trabalho de impressão: {dados.Length} bytes de {clienteInfo} (início: 0x{dados[0]:X2})", Config.Nome);

                    // Removido PurgeQueue automático para não cancelar impressões anteriores (ex: Bematech 4200)

                    // Envia para a impressora com tentativa de retry
                    var sucesso = false;
                    for (int tentativa = 1; tentativa <= 3; tentativa++)
                    {
                        try
                        {
                            if (Config.NomeImpressora == "SCREEN")
                            {
                                var tempPath = Path.Combine(Path.GetTempPath(), $"openvspe_print_{Guid.NewGuid():N}.txt");
                                File.WriteAllBytes(tempPath, dados);
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "notepad.exe",
                                    Arguments = tempPath,
                                    UseShellExecute = true
                                });
                                sucesso = true;
                            }
                            else
                            {
                                sucesso = RawPrinterHelper.SendRawData(Config.NomeImpressora!, dados);
                            }

                            if (sucesso)
                            {
                                _trabalhosEnviados++;
                                _log.Info($"Trabalho enviado com sucesso (total: {_trabalhosEnviados})", Config.Nome);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Erro($"Tentativa {tentativa}/3 falhou: {ex.Message}", Config.Nome);
                            if (tentativa < 3)
                                await Task.Delay(1000 * tentativa, ct);
                        }
                    }

                    if (!sucesso)
                    {
                        _log.Erro($"Falha ao enviar trabalho após 3 tentativas", Config.Nome);
                    }
                }
                else
                {
                    if (apenasStatus)
                        _log.Info($"Conexão de {clienteInfo} era apenas checagem de status (sem dados para imprimir)", Config.Nome);
                    else
                        _log.Aviso($"Conexão de {clienteInfo} não enviou dados", Config.Nome);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.Erro($"Erro ao processar trabalho de {clienteInfo}: {ex.Message}", Config.Nome, ex);
            }
            finally
            {
                lock (_lockClientes) _clientesAtivos.Remove(client);
                try { client.Close(); } catch { }
                _log.Info($"Conexão encerrada: {clienteInfo}", Config.Nome);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
