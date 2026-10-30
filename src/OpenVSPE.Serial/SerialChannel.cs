using RJCP.IO.Ports;
using System.Net;
using System.Net.Sockets;
using OpenVSPE.Core.Interfaces;
using OpenVSPE.Core.Logging;
using OpenVSPE.Core.Models;

namespace OpenVSPE.Serial
{
    /// <summary>
    /// Canal que faz ponte bidirecional entre uma porta COM serial e um servidor TCP.
    /// Funciona para balanças (COM física) e impressoras com COM virtual (Epson, Elgin).
    /// </summary>
    public class SerialChannel : IChannel
    {
        private readonly LogManager _log;
        private SerialPortStream? _serialPort;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private readonly List<TcpClient> _clientesAtivos = new();
        private readonly object _lockClientes = new();
        private Task? _listenTask;

        public ChannelConfig Config { get; }
        public ChannelStatus Status { get; private set; } = ChannelStatus.Parado;
        public string? UltimoErro { get; private set; }
        public int ConexoesAtivas
        {
            get
            {
                lock (_lockClientes) return _clientesAtivos.Count;
            }
        }

        public SerialChannel(ChannelConfig config, LogManager log)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Config.PortaCom))
                throw new InvalidOperationException("Porta COM não configurada.");

            Status = ChannelStatus.Iniciando;
            UltimoErro = null;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Abre a porta serial
                AbrirPortaSerial();

                // Inicia o listener TCP
                var ip = IPAddress.Parse(Config.IpEscuta);
                _tcpListener = new TcpListener(ip, Config.PortaTcp);
                _tcpListener.Start();

                Status = ChannelStatus.Ativo;
                _log.Info($"Canal iniciado: {Config.PortaCom} -> TCP {Config.IpEscuta}:{Config.PortaTcp}", Config.Nome);

                // Task que aceita conexões TCP em loop
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
            _log.Info("Parando canal...", Config.Nome);
            Status = ChannelStatus.Parado;

            _cts?.Cancel();

            // Fecha todos os clientes TCP
            lock (_lockClientes)
            {
                foreach (var client in _clientesAtivos)
                {
                    try { client.Close(); } catch { }
                }
                _clientesAtivos.Clear();
            }

            // Para o listener TCP
            try { _tcpListener?.Stop(); } catch { }

            // Fecha a porta serial
            try { _serialPort?.Close(); } catch { }
            _serialPort?.Dispose();
            _serialPort = null;

            if (_listenTask != null)
            {
                try { await _listenTask; } catch { }
            }

            _log.Info("Canal parado.", Config.Nome);
        }

        private void AbrirPortaSerial()
        {
            _serialPort?.Dispose();
            
            // Tenta combinações de DTR/RTS e Timeouts que costumam bugar drivers específicos
            var tentativas = new[]
            {
                new { Dtr = true, Timeout = true },
                new { Dtr = false, Timeout = true },
                new { Dtr = false, Timeout = false }
            };

            Exception? ultimoErro = null;

            foreach (var t in tentativas)
            {
                try
                {
                    _serialPort = new SerialPortStream
                    {
                        PortName = Config.PortaCom!,
                        BaudRate = Config.BaudRate,
                        DataBits = Config.DataBits,
                        StopBits = Config.StopBits switch {
                            OpenVSPE.Core.Models.StopBits.One => RJCP.IO.Ports.StopBits.One,
                            OpenVSPE.Core.Models.StopBits.Two => RJCP.IO.Ports.StopBits.Two,
                            OpenVSPE.Core.Models.StopBits.OnePointFive => RJCP.IO.Ports.StopBits.One5,
                            _ => RJCP.IO.Ports.StopBits.One
                        },
                        Parity = Config.Paridade switch {
                            OpenVSPE.Core.Models.Parity.None => RJCP.IO.Ports.Parity.None,
                            OpenVSPE.Core.Models.Parity.Odd => RJCP.IO.Ports.Parity.Odd,
                            OpenVSPE.Core.Models.Parity.Even => RJCP.IO.Ports.Parity.Even,
                            OpenVSPE.Core.Models.Parity.Mark => RJCP.IO.Ports.Parity.Mark,
                            OpenVSPE.Core.Models.Parity.Space => RJCP.IO.Ports.Parity.Space,
                            _ => RJCP.IO.Ports.Parity.None
                        },
                        Handshake = Config.ControleFluxo switch {
                            OpenVSPE.Core.Models.Handshake.None => RJCP.IO.Ports.Handshake.None,
                            OpenVSPE.Core.Models.Handshake.XOnXOff => RJCP.IO.Ports.Handshake.None,
                            OpenVSPE.Core.Models.Handshake.RequestToSend => RJCP.IO.Ports.Handshake.Rts,
                            OpenVSPE.Core.Models.Handshake.RequestToSendXOnXOff => RJCP.IO.Ports.Handshake.None,
                            _ => RJCP.IO.Ports.Handshake.None
                        },
                        ReadTimeout = t.Timeout && Config.TimeoutLeitura > 0 ? Config.TimeoutLeitura : -1,
                        WriteTimeout = t.Timeout && Config.TimeoutEscrita > 0 ? Config.TimeoutEscrita : -1
                    };

                    _serialPort.DtrEnable = t.Dtr;
                    _serialPort.RtsEnable = t.Dtr;

                    _serialPort.Open();
                    
                    string tmpInfo = t.Timeout ? "ComTimeouts" : "SemTimeouts";
                    _log.Info($"Porta serial {Config.PortaCom} aberta ({Config.BaudRate} bps, {Config.DataBits}{Config.Paridade.ToString()[0]}{(int)Config.StopBits}, DTR/RTS={t.Dtr}, {tmpInfo})", Config.Nome);
                    return; // Sucesso, sai do método
                }
                catch (IOException ex) when (ex.Message.Contains("não está funcionando") || ex.Message.Contains("functioning"))
                {
                    ultimoErro = ex;
                    _serialPort?.Dispose();
                    string tmpInfo = t.Timeout ? "Configurados" : "Infinito(Default)";
                    _log.Aviso($"Falha ao abrir {Config.PortaCom} a {Config.BaudRate}bps (DTR/RTS={t.Dtr}, Timeouts={tmpInfo})", Config.Nome);
                }
            }

            // Se falhou ambas as tentativas, lança a exceção original
            if (ultimoErro != null) throw ultimoErro;
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

                    // Inicia ponte bidirecional em background
                    _ = Task.Run(() => PonteBidirecionalAsync(client, ct), ct);
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

        private async Task PonteBidirecionalAsync(TcpClient client, CancellationToken ct)
        {
            var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
            var clienteInfo = $"{endpoint?.Address}:{endpoint?.Port}";

            try
            {
                using var networkStream = client.GetStream();
                var buffer = new byte[4096];

                // Task TCP -> Serial: lê do TCP e escreve na serial
                var tcpToSerial = Task.Run(async () =>
                {
                    try
                    {
                        while (!ct.IsCancellationRequested && client.Connected)
                        {
                            var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, ct);
                            if (bytesRead == 0) break; // Cliente desconectou

                            if (_serialPort?.IsOpen == true)
                            {
                                _serialPort.Write(buffer, 0, bytesRead);
                                _log.Info($"TCP->Serial: {bytesRead} bytes de {clienteInfo}", Config.Nome);
                            }
                        }
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        _log.Erro($"Erro TCP->Serial ({clienteInfo}): {ex.Message}", Config.Nome);
                    }
                }, ct);

                // Task Serial -> TCP: lê da serial e escreve no TCP
                var serialToTcp = Task.Run(async () =>
                {
                    var serialBuffer = new byte[4096];
                    try
                    {
                        while (!ct.IsCancellationRequested && client.Connected && _serialPort?.IsOpen == true)
                        {
                            try
                            {
                                var bytesRead = await _serialPort.ReadAsync(serialBuffer, 0, serialBuffer.Length, ct);
                                if (bytesRead > 0)
                                {
                                    await networkStream.WriteAsync(serialBuffer, 0, bytesRead, ct);
                                    await networkStream.FlushAsync(ct);
                                    _log.Info($"Serial->TCP: {bytesRead} bytes para {clienteInfo}", Config.Nome);
                                }
                            }
                            catch (TimeoutException)
                            {
                                // Timeout normal de leitura serial - continua
                            }
                        }
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        _log.Erro($"Erro Serial->TCP ({clienteInfo}): {ex.Message}", Config.Nome);
                    }
                }, ct);

                // Aguarda qualquer uma das tasks terminar (desconexão ou erro)
                await Task.WhenAny(tcpToSerial, serialToTcp);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.Erro($"Erro na ponte ({clienteInfo}): {ex.Message}", Config.Nome);
            }
            finally
            {
                lock (_lockClientes) _clientesAtivos.Remove(client);
                try { client.Close(); } catch { }
                _log.Info($"Conexão encerrada: {clienteInfo}", Config.Nome);

                // Tenta reconectar porta serial se caiu
                if (_serialPort?.IsOpen != true && !ct.IsCancellationRequested)
                {
                    await TentarReconectarSerialAsync(ct);
                }
            }
        }

        private async Task TentarReconectarSerialAsync(CancellationToken ct)
        {
            Status = ChannelStatus.Reconectando;
            var tentativa = 0;

            while (!ct.IsCancellationRequested)
            {
                tentativa++;
                // Backoff exponencial: 1s, 2s, 4s, 8s... máximo 30s
                var delay = Math.Min(1000 * (int)Math.Pow(2, tentativa - 1), 30000);

                _log.Aviso($"Tentando reconectar porta serial (tentativa {tentativa}, próxima em {delay / 1000}s)...", Config.Nome);

                try
                {
                    await Task.Delay(delay, ct);
                    AbrirPortaSerial();
                    Status = ChannelStatus.Ativo;
                    _log.Info("Porta serial reconectada com sucesso!", Config.Nome);
                    return;
                }
                catch (Exception ex)
                {
                    UltimoErro = ex.Message;
                    _log.Erro($"Reconexão falhou: {ex.Message}", Config.Nome);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
