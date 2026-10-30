using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Logging;

namespace OpenVSPE.Service
{
    /// <summary>
    /// Servidor de Named Pipe para comunicação entre a interface WPF e o serviço.
    /// Permite enviar comandos como 'status', 'reload', 'restart-channel' etc.
    /// </summary>
    public class PipeServer
    {
        private const string PIPE_NAME = "OpenVSPE_Control";
        private readonly ChannelManagerService _channelManager;
        private readonly LogManager _log;
        private readonly ConfigManager _configManager;

        public PipeServer(ChannelManagerService channelManager, LogManager log, ConfigManager configManager)
        {
            _channelManager = channelManager;
            _log = log;
            _configManager = configManager;
        }

        /// <summary>
        /// Inicia o servidor de Named Pipe em loop contínuo.
        /// </summary>
        public async Task StartAsync(CancellationToken ct)
        {
            _log.Info("PipeServer iniciado, aguardando comandos...");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var pipeServer = new NamedPipeServerStream(
                        PIPE_NAME,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    await pipeServer.WaitForConnectionAsync(ct);
                    
                    // Processa em background para manter o servidor livre para novas conexões
                    _ = Task.Run(async () => {
                        try 
                        {
                            using (pipeServer)
                            {
                                await ProcessarComandoAsync(pipeServer, ct);
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Erro($"Erro ao processar cliente: {ex.Message}");
                        }
                    }, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.Erro($"Erro no PipeServer: {ex.Message}", ex: ex);
                    await Task.Delay(1000, ct);
                }
            }
        }

        private async Task ProcessarComandoAsync(NamedPipeServerStream pipe, CancellationToken ct)
        {
            try
            {
                // Lê o comando
                var buffer = new byte[4096];
                var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, ct);
                var comando = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                _log.Info($"Comando recebido: {comando}");

                string resposta;

                // Processa o comando
                var partes = comando.Split(' ', 2);
                var acao = partes[0].ToLowerInvariant();
                var parametro = partes.Length > 1 ? partes[1] : null;

                switch (acao)
                {
                    case "status":
                        var status = _channelManager.ObterStatus();
                        resposta = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                        break;

                    case "reload":
                        _ = Task.Run(() => _channelManager.RecarregarAsync(ct), ct);
                        resposta = "OK: Recarregando em background...";
                        break;

                    case "stop":
                        _ = Task.Run(() => _channelManager.PararTudoAsync(), ct);
                        resposta = "OK: Parando em background...";
                        break;

                    case "start":
                        _ = Task.Run(() => _channelManager.RecarregarAsync(ct), ct);
                        resposta = "OK: Iniciando em background...";
                        break;

                    case "restart":
                        if (string.IsNullOrEmpty(parametro))
                        {
                            resposta = "ERRO: Informe o ID do canal (restart <id>)";
                        }
                        else
                        {
                            _ = Task.Run(() => _channelManager.ReiniciarCanalAsync(parametro, ct), ct);
                            resposta = $"OK: Reiniciando canal {parametro} em background...";
                        }
                        break;

                    case "config":
                        var config = _configManager.Carregar();
                        resposta = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                        break;

                    case "ping":
                        resposta = "pong";
                        break;

                    default:
                        resposta = $"ERRO: Comando desconhecido '{acao}'. Comandos: status, reload, restart <id>, config, ping";
                        break;
                }

                // Envia a resposta
                var respostaBytes = Encoding.UTF8.GetBytes(resposta);
                await pipe.WriteAsync(respostaBytes, 0, respostaBytes.Length, ct);
                await pipe.FlushAsync(ct);
            }
            catch (Exception ex)
            {
                _log.Erro($"Erro ao processar comando: {ex.Message}", ex: ex);
            }
        }
    }
}
