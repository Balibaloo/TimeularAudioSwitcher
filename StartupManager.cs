using Microsoft.Win32;

static class StartupManager
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "TimeularAudioSwitcher";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var value = key?.GetValue(AppName) as string;
        return !string.IsNullOrEmpty(value);
    }

    public static void Set(bool enabled)
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key!.SetValue(AppName, '"' + exePath + '"');
        else
            key!.DeleteValue(AppName, false);
    }
}
