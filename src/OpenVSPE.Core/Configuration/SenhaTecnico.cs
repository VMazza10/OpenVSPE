using System.Security.Cryptography;

namespace OpenVSPE.Core.Configuration
{
    /// <summary>
    /// Gera e confere a senha de acesso às configurações.
    /// </summary>
    public static class SenhaTecnico
    {
        private const int TamanhoSal = 16;
        private const int Iteracoes = 100_000;
        private const int TamanhoHash = 32;

        public static string Gerar(string senha)
        {
            var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
            var hash = DerivarHash(senha, sal);
            return $"{Convert.ToBase64String(sal)}:{Convert.ToBase64String(hash)}";
        }

        public static bool Conferir(string senha, string? guardada)
        {
            if (string.IsNullOrEmpty(guardada))
                return false;

            var separador = guardada.IndexOf(':');
            if (separador < 0)
                return false;

            Span<byte> sal = stackalloc byte[TamanhoSal];
            Span<byte> hash = stackalloc byte[TamanhoHash];
            if (!Convert.TryFromBase64Chars(guardada.AsSpan(0, separador), sal, out int bytesSal)
                || bytesSal != sal.Length
                || !Convert.TryFromBase64Chars(guardada.AsSpan(separador + 1), hash, out int bytesHash)
                || bytesHash != hash.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(DerivarHash(senha, sal), hash);
        }

        public static bool TemSenha(string? guardada) => !string.IsNullOrEmpty(guardada);

        private static byte[] DerivarHash(string senha, ReadOnlySpan<byte> sal)
            => Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
    }
}
