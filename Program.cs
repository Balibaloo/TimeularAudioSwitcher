using System.Text.Json;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static async Task<int> Main(string[] args)
    {
        var baseFolder = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseFolder, "appsettings.json");
        if (!File.Exists(configPath))
        {
            MessageBox.Show($"Missing config at {configPath}. Copy the example appsettings.json there and edit.");
            return 1;
        }

        var json = await File.ReadAllTextAsync(configPath);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (cfg == null)
        {
            MessageBox.Show("Failed to parse config file.");
            return 2;
        }

        //Logger.Init(cfg.LogPath);
        Logger.Log($"Starting TimeularAudioSwitcher (PID {Environment.ProcessId})");

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var audio = new AudioSwitcher();
        var ble = new BluetoothService(cfg, audio);

        // Start BLE listener in background
        _ = Task.Run(() => ble.RunAsync());

        var form = new MainForm(cfg, audio, ble);
        if (cfg.StartMinimized)
        {
            form.WindowState = FormWindowState.Minimized;
            form.ShowInTaskbar = false;
        }

        Application.Run(form);
        return 0;
    }
}
