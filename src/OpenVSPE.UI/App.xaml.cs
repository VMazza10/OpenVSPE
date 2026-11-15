using System.Threading;
using System.Windows;
using OpenVSPE.Core;

namespace OpenVSPE.UI;

/// <summary>
/// Controla a inicialização da tela do OpenVSPE.
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, Vigia.MutexTela, out bool createdNew);

        if (!createdNew)
        {
            if (!e.Args.Contains(Vigia.ArgumentoReaberturaTela))
                System.Windows.MessageBox.Show("O OpenVSPE já está em execução.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
            return;
        }

        // Inicia o motor na sessão do usuário para evitar bloqueios de driver USB na sessão 0.
        try
        {
            if (!Vigia.MotorAberto())
                Vigia.AbrirMotor(AppDomain.CurrentDomain.BaseDirectory);
        }
        catch { }

        base.OnStartup(e);
    }
}

