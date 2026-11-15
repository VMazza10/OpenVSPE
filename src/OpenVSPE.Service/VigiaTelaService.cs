using System.Diagnostics;
using OpenVSPE.Core;
using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Logging;

namespace OpenVSPE.Service
{
    internal sealed class VigiaTelaService : BackgroundService
    {
        private readonly LogManager _log;
        private readonly ConfigManager _configManager;

        public VigiaTelaService(LogManager log, ConfigManager configManager)
        {
            _log = log;
            _configManager = configManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                    using var motor = Process.GetCurrentProcess();
                    if (motor.SessionId == 0)
                        return;

                    if (_configManager.Carregar().ReabrirSozinho
                        && !Vigia.TelaAberta()
                        && Vigia.AbrirTela(AppContext.BaseDirectory))
                    {
                        _log.Aviso("Tela reaberta pelo vigia");
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Erro("Erro ao verificar a tela do OpenVSPE", ex: ex);
                }
            }
        }
    }
}
