using System.IO.Pipes;
using System.Text;

namespace OpenVSPE.UI.Services
{
    /// <summary>
    /// Cliente de Named Pipe para comunicação com o serviço OpenVSPE.
    /// Envia comandos e recebe respostas.
    /// </summary>
    public class PipeClient
    {
        private const string PIPE_NAME = "OpenVSPE_Control";
        private const int TIMEOUT_MS = 5000;

        /// <summary>
        /// Envia um comando para o serviço e retorna a resposta.
        /// </summary>
        public static async Task<string> EnviarComandoAsync(string comando)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                
                await pipe.ConnectAsync(TIMEOUT_MS);
                pipe.ReadMode = PipeTransmissionMode.Message;

                // Envia o comando
                var cmdBytes = Encoding.UTF8.GetBytes(comando);
                await pipe.WriteAsync(cmdBytes, 0, cmdBytes.Length);
                await pipe.FlushAsync();

                // Lê a resposta
                var buffer = new byte[65536];
                var sb = new StringBuilder();

                do
                {
                    var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                } while (!pipe.IsMessageComplete);

                return sb.ToString();
            }
            catch (TimeoutException)
            {
                return "ERRO: Serviço não está respondendo (timeout)";
            }
            catch (Exception ex)
            {
                return $"ERRO: {ex.Message}";
            }
        }

        /// <summary>
        /// Verifica se o serviço está acessível.
        /// </summary>
        public static async Task<bool> ServicoDisponivelAsync()
        {
            var resposta = await EnviarComandoAsync("ping");
            return resposta == "pong";
        }
    }
}
