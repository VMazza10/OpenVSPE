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

        /// <summary>
        /// Quando verdadeiro, o vigia reabre o OpenVSPE sozinho se ele for fechado. Padrão: ligado.
        /// </summary>
        public bool ReabrirSozinho { get; set; } = true;

        /// <summary>
        /// Senha do técnico, usada na tela de Configurações. Nulo quando não há senha definida.
        /// </summary>
        public string? SenhaTecnico { get; set; }
    }
}
