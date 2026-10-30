using System.Threading;
using System.Windows;

namespace OpenVSPE.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "OpenVSPE_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show("O OpenVSPE já está em execução.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
            return;
        }

        // Tenta iniciar o motor (Service) no contexto do usuário (Session 1) para evitar bloqueios de driver USB no LocalSystem (Session 0)
        try
        {
            var processName = "OpenVSPE.Service";
            if (System.Diagnostics.Process.GetProcessesByName(processName).Length == 0)
            {
                var exePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "service", "OpenVSPE.Service.exe");
                if (System.IO.File.Exists(exePath))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(exePath)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
        }
        catch { }

        base.OnStartup(e);
    }
}

