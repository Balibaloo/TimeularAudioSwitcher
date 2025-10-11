using System.Text;

static class Logger
{
    static object _lock = new();
    static string? _logPath;

    public static void Init(string logPath)
    {
        _logPath = logPath;
        try
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(_logPath, $"--- Log start {DateTime.Now:O} ---{Environment.NewLine}");
        }
        catch { /* swallow */ }
    }

    public static void Log(string line)
    {
        lock (_lock)
        {
            var text = $"[{DateTime.Now:O}] {line}{Environment.NewLine}";
            Console.WriteLine(line);
            if (!string.IsNullOrEmpty(_logPath))
            {
                try { File.AppendAllText(_logPath, text); } catch { }
            }
        }
    }
}
