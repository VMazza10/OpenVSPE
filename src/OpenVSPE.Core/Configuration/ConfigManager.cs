using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVSPE.Core.Models;

namespace OpenVSPE.Core.Configuration
{
    /// <summary>
    /// Gerencia leitura e gravação das configurações de canais em arquivo JSON.
    /// Localização padrão: %ProgramData%\OpenVSPE\channels.json
    /// </summary>
    public class ConfigManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _configPath;
        private readonly object _lock = new();

        /// <summary>
        /// Cria um ConfigManager com caminho padrão ou customizado.
        /// </summary>
        /// <param name="configDirectory">Diretório de configuração. Padrão: %ProgramData%\OpenVSPE</param>
        public ConfigManager(string? configDirectory = null)
        {
            var dir = configDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenVSPE");
            
            Directory.CreateDirectory(dir);
            _configPath = Path.Combine(dir, "channels.json");
        }

        /// <summary>
        /// Caminho completo do arquivo de configuração.
        /// </summary>
        public string ConfigPath => _configPath;

        /// <summary>
        /// Carrega a configuração do arquivo. Retorna configuração vazia se o arquivo não existir.
        /// </summary>
        public AppConfig Carregar()
        {
            lock (_lock)
            {
                if (!File.Exists(_configPath))
                    return new AppConfig();

                try
                {
                    var json = File.ReadAllText(_configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    // Se o arquivo estiver corrompido, retorna config vazia e loga
                    Console.Error.WriteLine($"[ConfigManager] Erro ao ler configuração: {ex.Message}");
                    return new AppConfig();
                }
            }
        }

        /// <summary>
        /// Salva a configuração no arquivo JSON.
        /// </summary>
        public void Salvar(AppConfig config)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(config, _jsonOptions);
                
                // Escreve em arquivo temporário primeiro para evitar corrupção
                var tempPath = _configPath + ".tmp";
                File.WriteAllText(tempPath, json);
                
                // Substitui o arquivo original
                File.Move(tempPath, _configPath, overwrite: true);
            }
        }

        /// <summary>
        /// Valida se há conflito de portas TCP entre os canais.
        /// Retorna lista de erros encontrados.
        /// </summary>
        public static List<string> ValidarConflitos(AppConfig config)
        {
            var erros = new List<string>();
            var portasUsadas = new Dictionary<string, string>(); // "ip:porta" -> nome do canal
            var comsUsadas = new Dictionary<string, string>();   // "COM3" -> nome do canal

            foreach (var canal in config.Canais.Where(c => c.Habilitado))
            {
                // Verifica conflito de porta TCP
                var chavePorta = $"{canal.IpEscuta}:{canal.PortaTcp}";
                if (portasUsadas.TryGetValue(chavePorta, out var canalExistente))
                {
                    erros.Add($"Conflito de porta TCP {chavePorta}: '{canal.Nome}' e '{canalExistente}'");
                }
                else
                {
                    portasUsadas[chavePorta] = canal.Nome;
                }

                // Verifica conflito de porta COM (apenas canais seriais)
                if (canal.Tipo == ChannelType.Serial && !string.IsNullOrEmpty(canal.PortaCom))
                {
                    var comUpper = canal.PortaCom.ToUpperInvariant();
                    if (comsUsadas.TryGetValue(comUpper, out var canalComExistente))
                    {
                        erros.Add($"Conflito de porta COM {comUpper}: '{canal.Nome}' e '{canalComExistente}'");
                    }
                    else
                    {
                        comsUsadas[comUpper] = canal.Nome;
                    }
                }
            }

            return erros;
        }
    }
}
