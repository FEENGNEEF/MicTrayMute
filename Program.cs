using System.Windows.Forms;

namespace MicTrayMute;

internal static class Program
{
    private const string MutexName = "Global\\MicTrayMute_9C24F601_8F5C_46C7_A9B1_5EAA7F53074F";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Mic Tray Mute is already running in the system tray.",
                "Mic Tray Mute",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
