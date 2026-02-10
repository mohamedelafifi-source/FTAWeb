namespace FTAWeb.Services;

/// <summary>
/// Reads/writes family passwords in a plain text file next to the Families folder (FolderName|Password per line).
/// Folder name = family directory name; password can differ (e.g. folder Hammad, password Hossam).
/// </summary>
public class FamilyPasswordService : IFamilyPasswordService
{
    private readonly IFamilyStorageService _storage;
    private readonly IConfiguration _configuration;
    private const string PasswordFileName = "family_passwords.txt";
    private const char Separator = '|';
    private const string OverridesSection = "FamilyPasswordOverrides";

    private static readonly string[] SeedLines = new[]
    {
        "Taymour|anna",
        "Afifi|neimat"
    };

    public FamilyPasswordService(IFamilyStorageService storage, IConfiguration configuration)
    {
        _storage = storage;
        _configuration = configuration;
    }

    /// <summary>Gets password for a folder: from FamilyPasswordOverrides config (folder -> password), or folder name as default.</summary>
    private string GetPasswordForFolder(string folderName)
    {
        var overrides = _configuration.GetSection(OverridesSection).Get<Dictionary<string, string>>();
        if (overrides == null || overrides.Count == 0) return folderName;
        var match = overrides.FirstOrDefault(kv => string.Equals(kv.Key, folderName, StringComparison.OrdinalIgnoreCase));
        if (match.Key != null && !string.IsNullOrWhiteSpace(match.Value))
            return match.Value.Trim();
        return folderName;
    }

    private List<(string FamilyName, string Password)> ReadAll()
    {
        var content = _storage.GetPasswordFileContentAsync().GetAwaiter().GetResult();
        var list = new List<(string, string)>();
        if (string.IsNullOrEmpty(content)) return list;
        foreach (var line in content.Split('\n', '\r'))
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
        var lines = entries.Select(e => $"{e.FamilyName}{Separator}{e.Password}");
        var content = string.Join(Environment.NewLine, lines);
        _storage.SetPasswordFileContentAsync(content).GetAwaiter().GetResult();
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
        var content = _storage.GetPasswordFileContentAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(content))
            _storage.SetPasswordFileContentAsync(string.Join(Environment.NewLine, SeedLines)).GetAwaiter().GetResult();
        SyncWithDirectories();
    }

    public void SyncWithDirectories()
    {
        var directories = _storage.ListFamilies();
        var dirSet = new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase);
        var existing = ReadAll();

        // Keep only entries whose family folder exists (match case-insensitive)
        var merged = existing.Where(e => dirSet.Contains(e.FamilyName)).ToList();
        var inFile = new HashSet<string>(merged.Select(e => e.FamilyName), StringComparer.OrdinalIgnoreCase);

        // Add an entry for each directory that has no password yet (use override from config if set, else folder name)
        foreach (var dir in directories)
        {
            if (inFile.Contains(dir)) continue;
            var password = GetPasswordForFolder(dir);
            merged.Add((dir, password));
            inFile.Add(dir);
        }

        WriteAll(merged);
    }
}
