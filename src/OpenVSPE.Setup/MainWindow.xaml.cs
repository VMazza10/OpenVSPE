using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Windows;
using Microsoft.Win32;

namespace OpenVSPE.Setup
{
    public partial class MainWindow : Window
    {
        private string _targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenVSPE");

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnInstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnInstall.IsEnabled = false;
                btnCancel.IsEnabled = false;
                pbProgress.Visibility = Visibility.Visible;
                lblSubStatus.Visibility = Visibility.Visible;

                await Task.Run(() => PerformInstallation());

                lblStatus.Text = "Instalação concluída com sucesso!";
                lblSubStatus.Text = "O atalho foi criado na sua área de trabalho.";
                btnInstall.Content = "Concluir";
                btnInstall.IsEnabled = true;
                btnInstall.Click -= btnInstall_Click;
                btnInstall.Click += (s, ev) => Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro durante a instalação: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                btnInstall.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private void PerformInstallation()
        {
            UpdateUI("Verificando pré-requisitos (.NET 8)...", 5);
            CheckAndInstallDotNet8();

            UpdateUI("Parando processos anteriores...", 10);
            StopExistingProcesses();

            UpdateUI("Limpando diretórios de destino...", 20);
            if (Directory.Exists(_targetDir))
            {
                // Limpa UI e Service para garantir que não fiquem EXEs antigos (ex: OpenVSPE.UI.exe vs OpenVSPE_V1.1.7.exe)
                if (Directory.Exists(Path.Combine(_targetDir, "ui"))) 
                    Directory.Delete(Path.Combine(_targetDir, "ui"), true);
                if (Directory.Exists(Path.Combine(_targetDir, "service"))) 
                    Directory.Delete(Path.Combine(_targetDir, "service"), true);
            }

            Directory.CreateDirectory(_targetDir);
            Directory.CreateDirectory(Path.Combine(_targetDir, "ui"));
            Directory.CreateDirectory(Path.Combine(_targetDir, "service"));
            Directory.CreateDirectory(Path.Combine(_targetDir, "config"));
            Directory.CreateDirectory(Path.Combine(_targetDir, "logs"));

            UpdateUI("Copiando arquivos do programa...", 40);
            string sourceDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? "";
            
            // Lógica inteligente: verifica se está dentro da pasta DEPLOY ou se ela está ao lado
            string uiSource = Path.Combine(sourceDir, "ui");
            string serviceSource = Path.Combine(sourceDir, "service");

            if (!Directory.Exists(uiSource) || !Directory.Exists(serviceSource))
            {
                // Se não achou na pasta atual, tenta a subpasta DEPLOY (estilo anterior)
                string deployDir = Path.Combine(sourceDir, "DEPLOY");
                uiSource = Path.Combine(deployDir, "ui");
                serviceSource = Path.Combine(deployDir, "service");
            }

            if (!Directory.Exists(uiSource) || !Directory.Exists(serviceSource))
            {
                 throw new DirectoryNotFoundException("Arquivos de instalação (pastas 'ui' e 'service') não encontrados junto ao instalador.");
            }

            CopyFiles(uiSource, Path.Combine(_targetDir, "ui"));
            CopyFiles(serviceSource, Path.Combine(_targetDir, "service"));

            UpdateUI("Registrando serviço no Windows...", 70);
            InstallService();

            UpdateUI("Configurando Firewall...", 85);
            ConfigureFirewall();

            UpdateUI("Criando atalhos...", 95);
            CreateShortcut();

            UpdateUI("Abrindo OpenVSPE...", 98);
            LaunchApp();

            UpdateUI("Concluído!", 100);
        }

        private void LaunchApp()
        {
            try
            {
                // Tenta encontrar o executável com versão primeiro (OpenVSPE_V1.1.7.exe etc)
                string uiDir = Path.Combine(_targetDir, "ui");
                var files = Directory.GetFiles(uiDir, "OpenVSPE*.exe").ToList();
                
                // Priorização: qualquer coisa que contenha "_" ou "v" ou "V" e terminando em .exe
                string uiExe = files.Where(f => f.Contains("_v", StringComparison.OrdinalIgnoreCase) || f.Contains("_V", StringComparison.OrdinalIgnoreCase))
                                    .OrderByDescending(f => f)
                                    .FirstOrDefault();

                if (string.IsNullOrEmpty(uiExe))
                    uiExe = files.OrderByDescending(f => f).FirstOrDefault();

                if (string.IsNullOrEmpty(uiExe)) 
                    uiExe = Path.Combine(uiDir, "OpenVSPE.UI.exe");

                if (File.Exists(uiExe))
                {
                    Process.Start(new ProcessStartInfo(uiExe) { UseShellExecute = true });
                }
            }
            catch { }
        }

        private void UpdateUI(string status, double progress)
        {
            Dispatcher.Invoke(() =>
            {
                lblSubStatus.Text = status;
                pbProgress.Value = progress;
            });
        }

        private void StopExistingProcesses()
        {
            // Mata a UI antiga e a nova (V1.1.7) e o serviço
            int currentPid = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                if (p.Id != currentPid && p.ProcessName.StartsWith("OpenVSPE", StringComparison.OrdinalIgnoreCase))
                {
                    try { p.Kill(); } catch { }
                }
            }

            // Para o serviço se existir
            try
            {
                using var sc = new ServiceController("OpenVSPE");
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    // sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
            }
            catch { }
        }

        private void CopyFiles(string source, string target)
        {
            if (!Directory.Exists(source)) return;
            foreach (string file in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(source, file);
                string destPath = Path.Combine(target, relPath);
                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir != null) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, true);
            }
        }

        private void InstallService()
        {
            // Remove o serviço antigo (Session 0) pois agora o motor roda no User Session (Session 1) para evitar bug do CH340
            ExecuteCommand("sc.exe", "stop OpenVSPE");
            Thread.Sleep(1000);
            ExecuteCommand("sc.exe", "delete OpenVSPE");
            Thread.Sleep(1000);
        }

        private void ConfigureFirewall()
        {
            ExecuteCommand("netsh", "advfirewall firewall delete rule name=\"OpenVSPE TCP\"");
            ExecuteCommand("netsh", "advfirewall firewall add rule name=\"OpenVSPE TCP\" dir=in action=allow protocol=TCP localport=9100-9200");
        }

        private void CreateShortcut()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            
            string uiDir = Path.Combine(_targetDir, "ui");
            var files = Directory.GetFiles(uiDir, "OpenVSPE*.exe").ToList();

            string targetPath = files.Where(f => f.Contains("_v", StringComparison.OrdinalIgnoreCase) || f.Contains("_V", StringComparison.OrdinalIgnoreCase))
                                     .OrderByDescending(f => f)
                                     .FirstOrDefault();

            if (string.IsNullOrEmpty(targetPath))
                targetPath = files.OrderByDescending(f => f).FirstOrDefault();

            if (string.IsNullOrEmpty(targetPath))
                targetPath = Path.Combine(uiDir, "OpenVSPE.UI.exe");

            string iconPath = Path.Combine(_targetDir, @"ui\app.ico");
            string workDir = Path.GetDirectoryName(targetPath) ?? _targetDir;

            // 1. Atalho no Desktop
            string desktopShortcut = Path.Combine(desktop, "OpenVSPE.lnk");
            string psDesktop = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{desktopShortcut}'); $s.TargetPath='{targetPath}'; $s.WorkingDirectory='{workDir}'; $s.IconLocation='{iconPath},0'; $s.Save()";
            ExecuteCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psDesktop}\"");

            // 2. Atalho na Inicialização (Startup do Windows)
            string startupShortcut = Path.Combine(startup, "OpenVSPE.lnk");
            string psStartup = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{startupShortcut}'); $s.TargetPath='{targetPath}'; $s.WorkingDirectory='{workDir}'; $s.IconLocation='{iconPath},0'; $s.Save()";
            ExecuteCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psStartup}\"");
        }

        private void ExecuteCommand(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(30000);
        }

        private void CheckAndInstallDotNet8()
        {
            if (IsDotNet8Installed()) return;

            UpdateUI("Baixando .NET 8 Desktop Runtime...", 5);
            string url = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe";
            string tempFile = Path.Combine(Path.GetTempPath(), "dotnet8_runtime_setup.exe");

            try
            {
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadFile(url, tempFile);
                }

                UpdateUI("Instalando .NET 8 (Aguarde)...", 7);
                var psi = new ProcessStartInfo(tempFile, "/quiet /norestart")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                var process = Process.Start(psi);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                throw new Exception("Falha ao baixar/instalar o .NET 8 Runtime. Por favor, instale manualmente em aka.ms/dotnet8", ex);
            }
            finally
            {
                if (File.Exists(tempFile)) try { File.Delete(tempFile); } catch { }
            }
        }

        private bool IsDotNet8Installed()
        {
            try
            {
                var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                string output = process?.StandardOutput.ReadToEnd() ?? "";
                process?.WaitForExit();
                return output.Contains("Microsoft.WindowsDesktop.App 8.0");
            }
            catch
            {
                // Se der erro, assume que não tem e tenta instalar
                return false;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
