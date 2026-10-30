namespace OpenVSPE.Core.Models
{
    /// <summary>
    /// Configuração raiz do aplicativo, contendo todos os canais.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Lista de canais configurados.
        /// </summary>
        public List<ChannelConfig> Canais { get; set; } = new();
    }
}
