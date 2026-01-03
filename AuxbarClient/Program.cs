namespace AuxbarClient;

static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        // Single instance check using a named mutex
        const string mutexName = "AuxbarClient_SingleInstance_Mutex";

        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            MessageBox.Show(
                "Auxbar is already running.\n\nCheck your system tray for the Auxbar icon.",
                "Auxbar Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}
