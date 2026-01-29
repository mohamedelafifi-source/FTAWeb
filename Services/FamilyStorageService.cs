using System.Text.RegularExpressions;

namespace FTAWeb.Services;

public class FamilyStorageService : IFamilyStorageService
{
    private readonly IWebHostEnvironment _env;
    private const string FamiliesFolder = "App_Data/Families";

    public FamilyStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public string GetFamiliesBasePath()
    {
        var path = Path.Combine(_env.ContentRootPath, FamiliesFolder);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    private static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "_";
        var invalid = Regex.Replace(name.Trim(), @"[\s\\/:*?""<>|]", "_");
        return string.IsNullOrEmpty(invalid) ? "_" : invalid;
    }

    private string GetFamilyPath(string familyName)
    {
        return Path.Combine(GetFamiliesBasePath(), SanitizeFolderName(familyName));
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "file.json";
        var invalid = Regex.Replace(name.Trim(), @"[\s\\/:*?""<>|]", "_");
        if (!invalid.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            invalid += ".json";
        return invalid;
    }

    public IReadOnlyList<string> ListFamilies()
    {
        var basePath = GetFamiliesBasePath();
        if (!Directory.Exists(basePath))
            return Array.Empty<string>();
        var dirs = Directory.GetDirectories(basePath);
        return dirs.Select(d => Path.GetFileName(d) ?? "").Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList();
    }

    public bool FamilyExists(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return false;
        var sanitized = SanitizeFolderName(familyName);
        var existing = ListFamilies();
        return existing.Any(f => string.Equals(f, sanitized, StringComparison.OrdinalIgnoreCase));
    }

    public string? CreateFamily(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return null;
        if (FamilyExists(familyName)) return null;
        var folderName = SanitizeFolderName(familyName);
        var path = Path.Combine(GetFamiliesBasePath(), folderName);
        Directory.CreateDirectory(path);
        return folderName;
    }

    public IReadOnlyList<string> GetFamilyFiles(string familyName)
    {
        var path = GetFamilyPath(familyName);
        if (!Directory.Exists(path)) return Array.Empty<string>();
        return Directory.GetFiles(path, "*.json").Select(Path.GetFileName).Where(n => n != null).Cast<string>().OrderBy(n => n).ToList();
    }

    public async Task SaveFileAsync(string familyName, string fileName, Stream content, CancellationToken ct = default)
    {
        var path = GetFamilyPath(familyName);
        if (!Directory.Exists(path)) return;
        var safeName = SanitizeFileName(fileName);
        var filePath = Path.Combine(path, safeName);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
    }

    public async Task<string?> GetFileContentAsync(string familyName, string fileName, CancellationToken ct = default)
    {
        var path = GetFamilyPath(familyName);
        var filePath = Path.Combine(path, fileName);
        if (!File.Exists(filePath)) return null;
        return await File.ReadAllTextAsync(filePath, ct);
    }

    public bool RenameFile(string familyName, string oldFileName, string newFileName)
    {
        var path = GetFamilyPath(familyName);
        var oldPath = Path.Combine(path, oldFileName);
        if (!File.Exists(oldPath)) return false;
        var safeNew = SanitizeFileName(newFileName);
        var newPath = Path.Combine(path, safeNew);
        if (File.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return false;
        File.Move(oldPath, newPath);
        return true;
    }

    public bool DeleteFile(string familyName, string fileName)
    {
        var path = GetFamilyPath(familyName);
        var filePath = Path.Combine(path, fileName);
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        return true;
    }

    public bool DeleteFamily(string familyName)
    {
        var path = GetFamilyPath(familyName);
        if (!Directory.Exists(path)) return false;
        Directory.Delete(path, true);
        return true;
    }
}
