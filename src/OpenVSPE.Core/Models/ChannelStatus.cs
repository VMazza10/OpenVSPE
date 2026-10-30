namespace OpenVSPE.Core.Models
{
    /// <summary>
    /// Status operacional de um canal.
    /// </summary>
    public enum ChannelStatus
    {
        Parado = 0,
        Iniciando = 1,
        Ativo = 2,
        Erro = 3,
        Reconectando = 4
    }
}
