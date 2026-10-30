namespace OpenVSPE.Core.Models
{
    /// <summary>
    /// Informações de status de um canal para transporte via IPC.
    /// </summary>
    public class ChannelStatusInfo
    {
        public ChannelStatus Status { get; set; }
        public string? Erro { get; set; }
        public int Conexoes { get; set; }
    }
}
