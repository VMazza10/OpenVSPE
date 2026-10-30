using OpenVSPE.Core.Configuration;
using OpenVSPE.Core.Interfaces;
using OpenVSPE.Core.Logging;
using OpenVSPE.Core.Models;
using OpenVSPE.Printing;
using OpenVSPE.Serial;

namespace OpenVSPE.Service
{
    /// <summary>
    /// Serviço principal que gerencia todos os canais (Serial e Printer).
    /// Carrega configurações, instancia e monitora cada canal.
    /// Falhas em um canal não afetam os demais.
    /// </summary>
    public class ChannelManagerService : BackgroundService
    {
        private readonly LogManager _log;
        private readonly ConfigManager _configManager;
        private readonly Dictionary<string, IChannel> _canais = new();
        private readonly object _lockCanais = new();
        private PipeServer? _pipeServer;

        public ChannelManagerService(LogManager log, ConfigManager configManager)
        {
            _log = log;
            _configManager = configManager;
        }

        /// <summary>
        /// Retorna lista de todos os canais configurados e seus status reais.
        /// </summary>
        public Dictionary<string, ChannelStatusInfo> ObterStatus()
        {
            var config = _configManager.Carregar();
            var resultado = new Dictionary<string, ChannelStatusInfo>();

            foreach (var c in config.Canais)
            {
                if (!c.Habilitado)
                {
                    resultado[c.Id] = new ChannelStatusInfo { Status = ChannelStatus.Parado, Erro = "Desabilitado" };
                    continue;
                }

                lock (_lockCanais)
                {
                    if (_canais.TryGetValue(c.Id, out var canal))
                    {
                        resultado[c.Id] = new ChannelStatusInfo 
                        { 
                            Status = canal.Status, 
                            Erro = canal.UltimoErro, 
                            Conexoes = canal.ConexoesAtivas 
                        };
                    }
                    else
                    {
                        resultado[c.Id] = new ChannelStatusInfo { Status = ChannelStatus.Erro, Erro = "Falha ao iniciar" };
                    }
                }
            }
            return resultado;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Info("=== OpenVSPE Service iniciando ===");

            // Inicia o servidor de Named Pipe para comunicação com a UI
            _pipeServer = new PipeServer(this, _log, _configManager);
            _ = Task.Run(() => _pipeServer.StartAsync(stoppingToken), stoppingToken);

            // Carrega e inicia os canais
            await CarregarEIniciarCanaisAsync(stoppingToken);

            // Loop de monitoramento de saúde
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, stoppingToken); // Verifica a cada 10 segundos
                    await MonitorarSaudeAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.Erro($"Erro no loop de monitoramento: {ex.Message}", ex: ex);
                }
            }

            // Para todos os canais
            await PararTodosCanaisAsync();
            _log.Info("=== OpenVSPE Service finalizado ===");
        }

        /// <summary>
        /// Carrega configurações e inicia todos os canais habilitados.
        /// </summary>
        public async Task CarregarEIniciarCanaisAsync(CancellationToken ct)
        {
            var config = _configManager.Carregar();

            // Valida conflitos
            var erros = ConfigManager.ValidarConflitos(config);
            foreach (var erro in erros)
            {
                _log.Erro(erro);
            }

            foreach (var canalConfig in config.Canais.Where(c => c.Habilitado))
            {
                await IniciarCanalAsync(canalConfig, ct);
            }

            _log.Info($"Canais carregados: {_canais.Count} ativo(s)");
        }

        private async Task IniciarCanalAsync(ChannelConfig config, CancellationToken ct)
        {
            // Tenta iniciar algumas vezes se falhar (transiente no startup do Windows)
            int tentativas = 0;
            const int maxTentativas = 3;

            while (tentativas < maxTentativas)
            {
                tentativas++;
                try
                {
                    IChannel? canal;
                    lock (_lockCanais)
                    {
                        if (!_canais.TryGetValue(config.Id, out canal))
                        {
                            canal = config.Tipo switch
                            {
                                ChannelType.Serial => new NativeWin32SerialChannel(config, _log),
                                ChannelType.Printer => new PrinterChannel(config, _log),
                                _ => throw new NotSupportedException($"Tipo de canal não suportado: {config.Tipo}")
                            };
                            _canais[config.Id] = canal;
                        }
                    }

                    await canal.StartAsync(ct);
                    return; // Sucesso
                }
                catch (Exception ex)
                {
                    if (tentativas >= maxTentativas)
                    {
                        _log.Erro($"Falha persistente ao iniciar canal '{config.Nome}' após {maxTentativas} tentativas: {ex.Message}", config.Nome, ex);
                        // Status ficará como Erro no objeto canal, o que é detectado pela UI/Monitor
                    }
                    else
                    {
                        _log.Aviso($"Tentativa {tentativas} falhou para '{config.Nome}': {ex.Message}. Retentando em 5s...", config.Nome);
                        await Task.Delay(5000, ct);
                    }
                }
            }
        }

        /// <summary>
        /// Reinicia um canal específico pelo ID.
        /// </summary>
        public async Task ReiniciarCanalAsync(string channelId, CancellationToken ct)
        {
            IChannel? canalExistente;
            lock (_lockCanais)
            {
                _canais.TryGetValue(channelId, out canalExistente);
            }

            if (canalExistente != null)
            {
                _log.Info($"Reiniciando canal '{canalExistente.Config.Nome}'...");
                await canalExistente.StopAsync();
                lock (_lockCanais) _canais.Remove(channelId);
            }

            var config = _configManager.Carregar();
            var canalConfig = config.Canais.FirstOrDefault(c => c.Id == channelId);
            if (canalConfig != null)
            {
                await IniciarCanalAsync(canalConfig, ct);
            }
        }

        /// <summary>
        /// Recarrega todas as configurações e reinicia todos os canais.
        /// </summary>
        public async Task RecarregarAsync(CancellationToken ct)
        {
            _log.Info("Recarregando configurações...");
            await PararTudoAsync();
            await CarregarEIniciarCanaisAsync(ct);
        }

        public async Task PararTudoAsync()
        {
            _log.Info("Parando todos os canais...");
            await PararTodosCanaisAsync();
        }

        private async Task PararTodosCanaisAsync()
        {
            List<IChannel> canaisParaParar;
            lock (_lockCanais)
            {
                canaisParaParar = _canais.Values.ToList();
                _canais.Clear();
            }

            foreach (var canal in canaisParaParar)
            {
                try
                {
                    await canal.StopAsync();
                }
                catch (Exception ex)
                {
                    _log.Erro($"Erro ao parar canal '{canal.Config.Nome}': {ex.Message}", canal.Config.Nome, ex);
                }
            }
        }

        private async Task MonitorarSaudeAsync(CancellationToken ct)
        {
            var config = _configManager.Carregar();
            var canaisHabilitados = config.Canais.Where(c => c.Habilitado).ToList();

            foreach (var canalConfig in canaisHabilitados)
            {
                IChannel? canalAtivo;
                lock (_lockCanais)
                {
                    _canais.TryGetValue(canalConfig.Id, out canalAtivo);
                }

                // Se o canal deveria estar rodando mas não está, ou está em erro
                if (canalAtivo == null || canalAtivo.Status == ChannelStatus.Erro)
                {
                    _log.Aviso($"Canal '{canalConfig.Nome}' em estado de erro ou inativo. Tentando (re)iniciar...", canalConfig.Nome);
                    
                    if (canalAtivo != null)
                    {
                        await canalAtivo.StopAsync();
                        lock (_lockCanais) _canais.Remove(canalConfig.Id);
                    }

                    await IniciarCanalAsync(canalConfig, ct);
                }
            }
        }
    }
}
