namespace OpenVSPE.Core.Models
{
    /// <summary>
    /// Tipo de canal disponível no sistema.
    /// </summary>
    public enum ChannelType
    {
        /// <summary>
        /// Canal serial COM -> TCP (balanças, impressoras com COM virtual).
        /// </summary>
        Serial = 0,

        /// <summary>
        /// Canal de impressora USB direta -> TCP (impressão RAW via Winspool).
        /// </summary>
        Printer = 1
    }
}
