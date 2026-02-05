using System.Text.RegularExpressions;

namespace FTAWeb.Services;

public class FamilyStorageService : IFamilyStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _familiesBasePath;
    private const string AttachmentsFolder = "attachments";

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };

    private static readonly Dictionary<string, string> AttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".gif", "image/gif" }, { ".webp", "image/webp" }, { ".pdf", "application/pdf" }
    };

    public FamilyStorageService(IWebHostEnvironment env, IConfiguration configuration)
    {
        _env = env;
        _familiesBasePath = ResolveFamiliesBasePath(env, configuration);
    }

    /// <summary>
    /// Resolves the writable path for family data. IMPORTANT: Always prefer the path that already
    /// has data (e.g. App_Data/Families with existing family folders) so we never "hide" existing
    /// data after a code change. New paths (e.g. HOME/data on Azure, or future BLOB) only when
    /// the current location has no family data. Override with FamilyStorage:BasePath if needed.
    /// </summary>
    private static string ResolveFamiliesBasePath(IWebHostEnvironment env, IConfiguration configuration)
    {
        var configuredPath = configuration["FamilyStorage:BasePath"]?.Trim();
        if (!string.IsNullOrEmpty(configuredPath))
            return Path.Combine(configuredPath, "Families");

        var legacyPath = Path.Combine(env.ContentRootPath, "App_Data", "Families");

        // Prefer existing data: if the old location has any family folders, keep using it
        if (Directory.Exists(legacyPath))
        {
            try
            {
                var hasFamilies = Directory.GetDirectories(legacyPath).Length > 0;
                if (hasFamilies)
                    return legacyPath;
            }
            catch { /* ignore */ }
        }

        // Azure App Service (and no existing data in legacy path): use persistent home directory
        var websiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(websiteName) && !string.IsNullOrEmpty(home))
        {
            var azurePath = Path.Combine(home, "data", "Families");
            return azurePath;
        }

        // Local / default: use ContentRootPath
        return legacyPath;
    }

    public string GetFamiliesBasePath()
    {
        if (!Directory.Exists(_familiesBasePath))
            Directory.CreateDirectory(_familiesBasePath);
        return _familiesBasePath;
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

    private string GetAttachmentsPath(string familyName, string personName)
    {
        var personFolder = SanitizeFolderName(personName);
        return Path.Combine(GetFamilyPath(familyName), AttachmentsFolder, personFolder);
    }

    public IReadOnlyList<string> ListAttachments(string familyName, string personName)
    {
        var path = GetAttachmentsPath(familyName, personName);
        if (!Directory.Exists(path)) return Array.Empty<string>();
        return Directory.GetFiles(path)
            .Select(Path.GetFileName)
            .Where(n => n != null && AllowedAttachmentExtensions.Contains(Path.GetExtension(n)))
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();
    }

    public async Task<string?> SaveAttachmentAsync(string familyName, string personName, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedAttachmentExtensions.Contains(ext)) return null;

        var path = GetAttachmentsPath(familyName, personName);
        Directory.CreateDirectory(path);
        var prefix = SanitizeFolderName(personName);
        var existing = ListAttachments(familyName, personName);
        var nextNum = 1;
        foreach (var f in existing)
        {
            var match = System.Text.RegularExpressions.Regex.Match(f, @"^" + Regex.Escape(prefix) + @"-(\d+)\.");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n >= nextNum)
                nextNum = n + 1;
        }
        var fileName = $"{prefix}-{nextNum}{ext}";
        var filePath = Path.Combine(path, fileName);
        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            await file.CopyToAsync(fs, ct);
        return fileName;
    }

    public bool DeleteAttachment(string familyName, string personName, string fileName)
    {
        var path = GetAttachmentsPath(familyName, personName);
        var filePath = Path.Combine(path, fileName);
        if (!File.Exists(filePath)) return false;
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedAttachmentExtensions.Contains(ext)) return false;
        File.Delete(filePath);
        return true;
    }

    public (Stream? stream, string? contentType) GetAttachment(string familyName, string personName, string fileName)
    {
        var filePath = GetAttachmentPath(familyName, personName, fileName);
        if (filePath == null) return (null, null);
        var ext = Path.GetExtension(fileName);
        var contentType = AttachmentContentTypes.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";
        return (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), contentType);
    }

    public string? GetAttachmentPath(string familyName, string personName, string fileName)
    {
        var path = GetAttachmentsPath(familyName, personName);
        var filePath = Path.Combine(path, fileName);
        if (!File.Exists(filePath)) return null;
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedAttachmentExtensions.Contains(ext)) return null;
        return filePath;
    }
}
