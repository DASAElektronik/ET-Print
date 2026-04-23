using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ETPrinter.Services;

/// <summary>Minimaler File-Logger. Niemals werfen — IO-Fehler gehen an Debug.WriteLine.</summary>
public static class Log
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ETPrinter");
    private static readonly string LogFile = Path.Combine(LogDir, "log.txt");
    private static readonly string OldFile = Path.Combine(LogDir, "log.old.txt");
    private static readonly object _lock = new();
    private const long RotateSizeBytes = 1 * 1024 * 1024;

    public static void Info(string msg, [CallerMemberName] string caller = "") =>
        Write("INFO", msg, null, caller);

    public static void Warn(string msg, [CallerMemberName] string caller = "") =>
        Write("WARN", msg, null, caller);

    public static void Error(string msg, Exception? ex = null, [CallerMemberName] string caller = "") =>
        Write("ERROR", msg, ex, caller);

    private static void Write(string level, string msg, Exception? ex, string caller)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append($" [{level}] {caller}: {msg}\n");
            if (ex is not null)
                sb.Append($"{ex}\n");

            lock (_lock)
            {
                Directory.CreateDirectory(LogDir);

                // Rotation: alte Datei umbenennen wenn > 1 MB
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > RotateSizeBytes)
                    File.Move(LogFile, OldFile, overwrite: true);

                File.AppendAllText(LogFile, sb.ToString());
            }
        }
        catch (Exception ioEx)
        {
            Debug.WriteLine($"[Log] Schreiben fehlgeschlagen: {ioEx.Message}");
        }
    }
}
