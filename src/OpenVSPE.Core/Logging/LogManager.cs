using System.Diagnostics;

namespace OpenVSPE.Core.Logging
{
    /// <summary>
    /// Gerencia logs do sistema em arquivo rotativo e Windows Event Viewer.
    /// </summary>
    public class LogManager : IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _sourceName;
        private StreamWriter? _fileWriter;
        private readonly object _lock = new();
        private string _currentLogDate = string.Empty;
        private bool _eventLogDisponivel;

        /// <summary>
        /// Evento disparado quando uma nova entrada de log é registrada.
        /// Usado pela interface WPF para exibir logs em tempo real.
        /// </summary>
        public event Action<string>? OnLog;

        /// <summary>
        /// Cria um LogManager.
        /// </summary>
        /// <param name="logDirectory">Pasta de logs. Padrão: %ProgramData%\OpenVSPE\logs</param>
        /// <param name="sourceName">Nome da fonte no Event Viewer. Padrão: OpenVSPE</param>
        public LogManager(string? logDirectory = null, string sourceName = "OpenVSPE")
        {
            _sourceName = sourceName;
            _logDirectory = logDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenVSPE", "logs");

            Directory.CreateDirectory(_logDirectory);

            // Tenta registrar fonte no Event Viewer (requer admin na primeira vez)
            try
            {
                if (!EventLog.SourceExists(_sourceName))
                {
                    EventLog.CreateEventSource(_sourceName, "Application");
                }
                _eventLogDisponivel = true;
            }
            catch
            {
                // Sem privilégios admin - Event Viewer não disponível, mas logs em arquivo funcionam
                _eventLogDisponivel = false;
            }

            // Limpa logs ao iniciar (mantém apenas o dia atual se já existir, ou limpa tudo)
            LimparLogsAntigos(0); 
        }

        public void LimparLogsAntigos(int diasManter)
        {
            try
            {
                if (!Directory.Exists(_logDirectory)) return;

                var limite = DateTime.Now.AddDays(-diasManter);
                var arquivos = Directory.GetFiles(_logDirectory, "*.log");

                foreach (var arquivo in arquivos)
                {
                    try
                    {
                        var info = new FileInfo(arquivo);
                        if (info.LastWriteTime < limite)
                        {
                            info.Delete();
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Registra uma mensagem informativa.
        /// </summary>
        public void Info(string mensagem, string? canal = null)
        {
            Registrar("INFO", mensagem, canal);
        }

        /// <summary>
        /// Registra um aviso.
        /// </summary>
        public void Aviso(string mensagem, string? canal = null)
        {
            Registrar("AVISO", mensagem, canal);
        }

        /// <summary>
        /// Registra um erro.
        /// </summary>
        public void Erro(string mensagem, string? canal = null, Exception? ex = null)
        {
            var msg = ex != null ? $"{mensagem} | Exceção: {ex.Message}" : mensagem;
            Registrar("ERRO", msg, canal);

            // Erros vão para o Event Viewer também
            if (_eventLogDisponivel)
            {
                try
                {
                    EventLog.WriteEntry(_sourceName, msg, EventLogEntryType.Error);
                }
                catch { /* Ignora falhas silenciosamente */ }
            }
        }

        private void Registrar(string nivel, string mensagem, string? canal)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var canalTag = canal != null ? $"[{canal}] " : "";
            var linha = $"[{timestamp}] [{nivel}] {canalTag}{mensagem}";

            lock (_lock)
            {
                EnsureFileWriter();
                _fileWriter?.WriteLine(linha);
                _fileWriter?.Flush();
            }

            // Dispara evento para a UI
            OnLog?.Invoke(linha);

            // Debug output
            System.Diagnostics.Debug.WriteLine(linha);
        }

        private void EnsureFileWriter()
        {
            var hoje = DateTime.Now.ToString("yyyy-MM-dd");
            if (hoje != _currentLogDate)
            {
                _fileWriter?.Dispose();
                _fileWriter = null;
                var filePath = Path.Combine(_logDirectory, $"openvspe_{hoje}.log");
                try
                {
                    var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _fileWriter = new StreamWriter(fs) { AutoFlush = true };
                    _currentLogDate = hoje;
                }
                catch (Exception)
                {
                    // Se não conseguir abrir o arquivo, continua sem log em arquivo
                    _currentLogDate = hoje;
                }
            }
        }

        /// <summary>
        /// Retorna o caminho da pasta de logs.
        /// </summary>
        public string LogDirectory => _logDirectory;

        public void Dispose()
        {
            lock (_lock)
            {
                _fileWriter?.Dispose();
                _fileWriter = null;
            }
        }
    }
}
