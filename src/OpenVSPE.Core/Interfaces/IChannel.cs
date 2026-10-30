using OpenVSPE.Core.Models;

namespace OpenVSPE.Core.Interfaces
{
    /// <summary>
    /// Interface comum para qualquer canal (Serial ou Printer).
    /// </summary>
    public interface IChannel : IAsyncDisposable
    {
        /// <summary>
        /// Configuração do canal.
        /// </summary>
        ChannelConfig Config { get; }

        /// <summary>
        /// Status atual do canal.
        /// </summary>
        ChannelStatus Status { get; }

        /// <summary>
        /// Última mensagem de erro, se houver.
        /// </summary>
        string? UltimoErro { get; }

        /// <summary>
        /// Número de conexões TCP ativas no momento.
        /// </summary>
        int ConexoesAtivas { get; }

        /// <summary>
        /// Inicia o canal (abre porta serial/impressora e inicia listener TCP).
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Para o canal (fecha conexões, porta serial/impressora e listener TCP).
        /// </summary>
        Task StopAsync();
    }
}
