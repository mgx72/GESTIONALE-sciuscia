namespace Sciuscia.Foundations;

public sealed class AppPaths
{
    public AppPaths(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            throw new ArgumentException("La cartella dati è obbligatoria.", nameof(dataRoot));
        }

        DataRoot = Path.GetFullPath(dataRoot);
        DatabasePath = Path.Combine(DataRoot, "Dati", "sciuscia-lab.db");
        PhotosRoot = Path.Combine(DataRoot, "Fotografie");
        BackupRoot = Path.Combine(DataRoot, "Backup");
        LogsRoot = Path.Combine(DataRoot, "Log");
    }

    public string DataRoot { get; }
    public string DatabasePath { get; }
    public string PhotosRoot { get; }
    public string BackupRoot { get; }
    public string LogsRoot { get; }

    public string ErrorLogPath => Path.Combine(LogsRoot, "errori.jsonl");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(PhotosRoot);
        Directory.CreateDirectory(BackupRoot);
        Directory.CreateDirectory(LogsRoot);
    }

    public static string DefaultDataRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "Sciuscia", "GestionaleLAB");
    }
}

