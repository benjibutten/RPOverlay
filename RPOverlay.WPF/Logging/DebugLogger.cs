using System;
using System.IO;

namespace RPOverlay.WPF.Logging;

internal static class DebugLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RPOverlay",
        "DebugOutput.log");

    static DebugLogger()
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir!);
        }
    }

    public static void Log(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var line = $"[{timestamp}] {message}";
            File.AppendAllText(LogPath, line + Environment.NewLine);
            System.Diagnostics.Debug.WriteLine(line);
        }
        catch
        {
            // Ignore logging errors
        }
    }

    public static void LogException(Exception ex)
    {
        Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
        Log($"StackTrace: {ex.StackTrace}");
    }
}
