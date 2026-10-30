using System.Runtime.InteropServices;

namespace OpenVSPE.Printing
{
    /// <summary>
    /// Envia dados RAW diretamente para uma impressora instalada no Windows
    /// usando a API winspool.drv (P/Invoke). Não processa os dados pelo driver.
    /// Compatível com ESC/POS, texto bruto e qualquer formato RAW.
    /// </summary>
    public static class RawPrinterHelper
    {
        #region P/Invoke - winspool.drv

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] DOCINFOA di);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [DllImport("winspool.drv", EntryPoint = "SetJobA", SetLastError = true)]
        private static extern bool SetJob(IntPtr hPrinter, int jobId, int level, IntPtr pJob, int command);

        private const int JOB_CONTROL_DELETE = 3;

        #endregion

        /// <summary>
        /// Cancela todos os jobs presos na fila da impressora antes de enviar um novo.
        /// Evita que o spooler trave após o primeiro job RAW.
        /// </summary>
        public static void PurgeQueue(string printerName)
        {
            try
            {
                if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero)) return;
                try
                {
                    // Usa PowerShell/WMI via Process para cancelar jobs presos
                    // (EnumJobs via P/Invoke é complexo — usamos Get-PrintJob via WMI)
                    var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe",
                        $"-NoProfile -Command \"Get-PrintJob -PrinterName '{printerName}' -ErrorAction SilentlyContinue | Remove-PrintJob -ErrorAction SilentlyContinue\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit(3000);
                }
                finally { ClosePrinter(hPrinter); }
            }
            catch { /* não bloqueia se falhar */ }
        }

        /// <summary>
        /// Envia dados brutos (RAW) para a impressora sem processamento pelo driver.
        /// </summary>
        /// <param name="printerName">Nome exato da impressora no Windows (ex: "EPSON TM-T20II Receipt")</param>
        /// <param name="data">Dados brutos a serem enviados</param>
        /// <param name="documentName">Nome do documento para o spooler</param>
        /// <returns>true se enviado com sucesso</returns>
        public static bool SendRawData(string printerName, byte[] data, string documentName = "OpenVSPE RAW Print")
        {
            IntPtr hPrinter = IntPtr.Zero;

            try
            {
                // 1. Abre a impressora
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    throw new InvalidOperationException($"Não foi possível abrir a impressora '{printerName}'. Verifique se está instalada. Erro: {Marshal.GetLastWin32Error()}");

                // 2. Inicia o documento
                var docInfo = new DOCINFOA
                {
                    pDocName = documentName,
                    pDataType = "RAW"
                };

                if (!StartDocPrinter(hPrinter, 1, docInfo))
                    throw new InvalidOperationException($"Falha em StartDocPrinter. Erro: {Marshal.GetLastWin32Error()}");

                try
                {
                    // 3. Inicia a página
                    if (!StartPagePrinter(hPrinter))
                        throw new InvalidOperationException($"Falha em StartPagePrinter. Erro: {Marshal.GetLastWin32Error()}");

                    try
                    {
                        // 4. Escreve os dados
                        var pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
                        try
                        {
                            Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
                            if (!WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out int written))
                                throw new InvalidOperationException($"Falha em WritePrinter. Erro: {Marshal.GetLastWin32Error()}");
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(pUnmanagedBytes);
                        }
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }

                return true;
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                    ClosePrinter(hPrinter);
            }
        }

        /// <summary>
        /// Verifica se uma impressora com o nome dado está instalada no Windows.
        /// </summary>
        public static bool PrinterExists(string printerName)
        {
            try
            {
                if (OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                {
                    ClosePrinter(hPrinter);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
