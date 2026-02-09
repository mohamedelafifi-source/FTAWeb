namespace FTAWeb.Services;

/// <summary>
/// Reads/writes family passwords in a plain text file next to the Families folder (FamilyName|Password per line).
/// </summary>
public class FamilyPasswordService : IFamilyPasswordService
{
    private readonly IFamilyStorageService _storage;
    private const string PasswordFileName = "family_passwords.txt";
    private const char Separator = '|';

    private static readonly string[] SeedLines = new[]
    {
        "Taymour|anna",
        "Afifi|neimat"
    };

    public FamilyPasswordService(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    private string GetPasswordFilePath()
    {
        var familiesPath = _storage.GetFamiliesBasePath();
        var parent = Path.GetDirectoryName(familiesPath) ?? familiesPath;
        return Path.Combine(parent, PasswordFileName);
    }

    private List<(string FamilyName, string Password)> ReadAll()
    {
        var path = GetPasswordFilePath();
        var list = new List<(string, string)>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            var idx = t.IndexOf(Separator);
            if (idx <= 0) continue;
            var name = t[..idx].Trim();
            var pass = t[(idx + 1)..].Trim();
            if (string.IsNullOrEmpty(name)) continue;
            list.Add((name, pass));
        }
        return list;
    }

    private void WriteAll(List<(string FamilyName, string Password)> entries)
    {
        var path = GetPasswordFilePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var lines = entries.Select(e => $"{e.FamilyName}{Separator}{e.Password}");
        File.WriteAllLines(path, lines);
    }

    public IReadOnlyList<string> GetFamiliesByPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return Array.Empty<string>();
        var p = password.Trim();
        var all = ReadAll();
        return all.Where(e => string.Equals(e.Password, p, StringComparison.OrdinalIgnoreCase)).Select(e => e.FamilyName).ToList();
    }

    public void SetPassword(string familyFolderName, string password)
    {
        if (string.IsNullOrWhiteSpace(familyFolderName)) return;
        var name = familyFolderName.Trim();
        var pass = (password ?? "").Trim();
        var all = ReadAll();
        var idx = all.FindIndex(e => string.Equals(e.FamilyName, name, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            all[idx] = (all[idx].FamilyName, pass);
        else
            all.Add((name, pass));
        WriteAll(all);
    }

    public void RemovePassword(string familyFolderName)
    {
        if (string.IsNullOrWhiteSpace(familyFolderName)) return;
        var name = familyFolderName.Trim();
        var all = ReadAll();
        all.RemoveAll(e => string.Equals(e.FamilyName, name, StringComparison.OrdinalIgnoreCase));
        WriteAll(all);
    }

    public void EnsurePasswordFileExists()
    {
        var path = GetPasswordFilePath();
        if (File.Exists(path)) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllLines(path, SeedLines);
    }
}
