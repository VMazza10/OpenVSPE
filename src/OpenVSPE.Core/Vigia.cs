using System.ComponentModel;
using System.Diagnostics;

namespace OpenVSPE.Core
{
    /// <summary>
    /// Verifica e religa a tela e o motor do OpenVSPE.
    /// </summary>
    public static class Vigia
    {
        public const string MutexTela = "OpenVSPE_SingleInstance_Mutex";
        public const string MutexMotor = "OpenVSPE_Motor_Mutex";
        public const string ProcessoMotor = "OpenVSPE.Service";
        public const string ArgumentoReaberturaTela = "--reaberta-pelo-vigia";

        public static string? AcharExeTela(string pastaUi)
        {
            try
            {
                return Directory.EnumerateFiles(pastaUi, "OpenVSPE*.exe")
                    .OrderByDescending(arquivo => Path.GetFileName(arquivo).Contains("_v", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(arquivo => arquivo)
                    .FirstOrDefault();
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        public static bool TelaAberta()
        {
            bool existe = Mutex.TryOpenExisting(MutexTela, out var mutex);
            mutex?.Dispose();
            return existe;
        }

        public static bool MotorAberto()
        {
            var motores = Process.GetProcessesByName(ProcessoMotor);
            foreach (var motor in motores)
                motor.Dispose();

            return motores.Length > 0;
        }

        public static bool AbrirMotor(string pastaUi)
        {
            var exe = Path.Combine(pastaUi, "..", "service", ProcessoMotor + ".exe");
            if (!File.Exists(exe))
                return false;

            return Iniciar(new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        public static bool AbrirTela(string pastaMotor)
        {
            var pastaUi = Path.Combine(pastaMotor, "..", "ui");
            string? exe;
            try
            {
                exe = AcharExeTela(pastaUi);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            if (exe is null)
                return false;

            return Iniciar(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = pastaUi,
                ArgumentList = { ArgumentoReaberturaTela }
            });
        }

        private static bool Iniciar(ProcessStartInfo info)
        {
            try
            {
                using var processo = Process.Start(info);
                return processo is not null;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                return false;
            }
        }
    }
}
