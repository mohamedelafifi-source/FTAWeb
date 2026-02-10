using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FTAWeb.Services;

/// <summary>
/// Stores family data in Azure Blob Storage so local and deployed app share the same data.
/// Blob layout: {familyName}/{file}.json (tree), {familyName}/attachments/{personName}/{file}, _meta/family_passwords.txt
/// </summary>
public class AzureBlobFamilyStorageService : IFamilyStorageService
{
    private readonly BlobContainerClient _container;
    private const string AttachmentsPrefix = "attachments/";
    private const string MetaPrefix = "_meta/";
    private const string PasswordBlobName = "_meta/family_passwords.txt";

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };

    private static readonly Dictionary<string, string> AttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".png", "image/png" },
        { ".gif", "image/gif" }, { ".webp", "image/webp" }, { ".pdf", "application/pdf" }
    };

    public AzureBlobFamilyStorageService(IConfiguration configuration)
    {
        var section = configuration.GetSection("FamilyStorage").GetSection("Azure");
        var connectionString = GetConnectionString(configuration, section);
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Set FamilyStorage:Azure:ConnectionString, or both AccountName and AccountKey, in appsettings.Secrets.json or appsettings.Development.json.");
        var containerName = configuration["FamilyStorage:Azure:ContainerName"]?.Trim()
            ?? section["ContainerName"]?.Trim() ?? "fta-families";
        if (string.IsNullOrEmpty(containerName)) containerName = "fta-families";
        try
        {
            _container = new BlobContainerClient(connectionString, containerName);
            _container.CreateIfNotExists();
        }
        catch (FormatException ex) when (ex.Message.Contains("account information", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid Azure Storage settings. Use either (A) ConnectionString: paste the full string from Azure Portal → Access keys → key1 → Connection string, or (B) AccountName + AccountKey as two separate values. Ensure there are no extra line breaks or quotes.", ex);
        }
    }

    /// <summary>Get connection string from ConnectionString, or build from AccountName + AccountKey (avoids copy-paste issues).</summary>
    private static string? GetConnectionString(IConfiguration configuration, IConfigurationSection section)
    {
        var raw = configuration["FamilyStorage:Azure:ConnectionString"]?.Trim()
            ?? section["ConnectionString"]?.Trim()
            ?? Environment.GetEnvironmentVariable("FamilyStorage__Azure__ConnectionString")?.Trim();
        if (!string.IsNullOrEmpty(raw))
        {
            return string.Join("", raw.Split('\n', '\r').Select(s => s.Trim())).Trim();
        }
        var accountName = section["AccountName"]?.Trim() ?? Environment.GetEnvironmentVariable("FamilyStorage__Azure__AccountName")?.Trim();
        var accountKey = section["AccountKey"]?.Trim() ?? Environment.GetEnvironmentVariable("FamilyStorage__Azure__AccountKey")?.Trim();
        if (string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
            return null;
        return $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
    }

    public string GetFamiliesBasePath() => "azure-blob"; // Sentinel; password file is stored in blob

    private static string SanitizeFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "_";
        var invalid = Regex.Replace(name.Trim(), @"[\s\\/:*?""<>|]", "_");
        return string.IsNullOrEmpty(invalid) ? "_" : invalid;
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
        return ListFamiliesAsync().GetAwaiter().GetResult();
    }

    private async Task<List<string>> ListFamiliesAsync()
    {
        var list = new List<string>();
        try
        {
            await foreach (var item in _container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, "/"))
            {
                if (item.IsPrefix && item.Prefix != null)
                {
                    var name = item.Prefix.TrimEnd('/');
                    if (!string.IsNullOrEmpty(name) && !name.StartsWith("_", StringComparison.Ordinal))
                        list.Add(name);
                }
            }
        }
        catch (RequestFailedException) { }
        return list.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool FamilyExists(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return false;
        var sanitized = SanitizeFolderName(familyName);
        return ListFamilies().Any(f => string.Equals(f, sanitized, StringComparison.OrdinalIgnoreCase));
    }

    public string? CreateFamily(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return null;
        if (FamilyExists(familyName)) return null;
        var folderName = SanitizeFolderName(familyName);
        // Create a placeholder blob so the "folder" exists (optional; listing by prefix will work without it)
        var placeholder = _container.GetBlobClient($"{folderName}/.keep");
        try
        {
            placeholder.UploadAsync(new BinaryData("")).GetAwaiter().GetResult();
        }
        catch (RequestFailedException)
        {
            return null;
        }
        return folderName;
    }

    public IReadOnlyList<string> GetFamilyFiles(string familyName)
    {
        return GetFamilyFilesAsync(familyName).GetAwaiter().GetResult();
    }

    private async Task<List<string>> GetFamilyFilesAsync(string familyName)
    {
        var prefix = SanitizeFolderName(familyName) + "/";
        var list = new List<string>();
        try
        {
            await foreach (var blob in _container.GetBlobsAsync(prefix: prefix))
            {
                var name = blob.Name;
                if (name != null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !name.Contains(AttachmentsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var file = name[prefix.Length..].Trim();
                    if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !file.StartsWith("."))
                        list.Add(file);
                }
            }
        }
        catch (RequestFailedException) { }
        return list.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task SaveFileAsync(string familyName, string fileName, Stream content, CancellationToken ct = default)
    {
        var folderName = SanitizeFolderName(familyName);
        var safeName = SanitizeFileName(fileName);
        var blob = _container.GetBlobClient($"{folderName}/{safeName}");
        await blob.UploadAsync(content, overwrite: true, ct);
    }

    public async Task<string?> GetFileContentAsync(string familyName, string fileName, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient($"{SanitizeFolderName(familyName)}/{fileName}");
        try
        {
            var r = await blob.DownloadContentAsync(ct);
            return r.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public bool RenameFile(string familyName, string oldFileName, string newFileName)
    {
        var folderName = SanitizeFolderName(familyName);
        var existing = GetFamilyFiles(familyName);
        var actualOld = existing.FirstOrDefault(f => string.Equals(f, oldFileName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(actualOld)) return false;
        var safeNew = SanitizeFileName(newFileName);
        if (string.Equals(actualOld, safeNew, StringComparison.OrdinalIgnoreCase)) return true;
        if (existing.Any(f => string.Equals(f, safeNew, StringComparison.OrdinalIgnoreCase))) return false;
        try
        {
            var src = _container.GetBlobClient($"{folderName}/{actualOld}");
            var dest = _container.GetBlobClient($"{folderName}/{safeNew}");
            dest.SyncCopyFromUriAsync(src.Uri).GetAwaiter().GetResult();
            src.DeleteIfExists();
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public bool DeleteFile(string familyName, string fileName)
    {
        var blob = _container.GetBlobClient($"{SanitizeFolderName(familyName)}/{fileName}");
        try
        {
            return blob.DeleteIfExists();
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public bool DeleteFamily(string familyName)
    {
        return DeleteFamilyAsync(familyName).GetAwaiter().GetResult();
    }

    private async Task<bool> DeleteFamilyAsync(string familyName)
    {
        var prefix = SanitizeFolderName(familyName) + "/";
        try
        {
            await foreach (var blob in _container.GetBlobsAsync(prefix: prefix))
                await _container.GetBlobClient(blob.Name).DeleteIfExistsAsync();
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private string AttachmentBlobPrefix(string familyName, string personName) =>
        $"{SanitizeFolderName(familyName)}/{AttachmentsPrefix}{SanitizeFolderName(personName)}/";

    public IReadOnlyList<string> ListAttachments(string familyName, string personName)
    {
        return ListAttachmentsAsync(familyName, personName).GetAwaiter().GetResult();
    }

    private async Task<List<string>> ListAttachmentsAsync(string familyName, string personName)
    {
        var prefix = AttachmentBlobPrefix(familyName, personName);
        var list = new List<string>();
        try
        {
            await foreach (var blob in _container.GetBlobsAsync(prefix: prefix))
            {
                if (blob.Name == null) continue;
                var fileName = blob.Name[prefix.Length..].Trim();
                if (!string.IsNullOrEmpty(fileName) && AllowedAttachmentExtensions.Contains(Path.GetExtension(fileName)))
                    list.Add(fileName);
            }
        }
        catch (RequestFailedException) { }
        return list.OrderBy(n => n).ToList();
    }

    public async Task<string?> SaveAttachmentAsync(string familyName, string personName, IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedAttachmentExtensions.Contains(ext)) return null;
        var prefix = AttachmentBlobPrefix(familyName, personName);
        var existing = ListAttachments(familyName, personName);
        var nextNum = 1;
        var personPrefix = SanitizeFolderName(personName);
        foreach (var f in existing)
        {
            var match = Regex.Match(f, @"^" + Regex.Escape(personPrefix) + @"-(\d+)\.");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n >= nextNum)
                nextNum = n + 1;
        }
        var fileName = $"{personPrefix}-{nextNum}{ext}";
        var blob = _container.GetBlobClient(prefix + fileName);
        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true, ct);
        return fileName;
    }

    public bool DeleteAttachment(string familyName, string personName, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext) || !AllowedAttachmentExtensions.Contains(ext)) return false;
        var blob = _container.GetBlobClient(AttachmentBlobPrefix(familyName, personName) + fileName);
        try
        {
            return blob.DeleteIfExists();
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public (Stream? stream, string? contentType) GetAttachment(string familyName, string personName, string fileName)
    {
        var blob = _container.GetBlobClient(AttachmentBlobPrefix(familyName, personName) + fileName);
        try
        {
            var r = blob.DownloadStreaming();
            var ext = Path.GetExtension(fileName);
            var contentType = AttachmentContentTypes.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";
            return (r.Value.Content, contentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return (null, null);
        }
    }

    public string? GetAttachmentPath(string familyName, string personName, string fileName) => null; // No local path in blob

    public async Task<string?> GetPasswordFileContentAsync(CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(PasswordBlobName);
        try
        {
            var r = await blob.DownloadContentAsync(ct);
            var content = r.Value?.Content;
            if (content == null)
                return null;
            try
            {
                var bytes = content.ToArray();
                if (bytes == null || bytes.Length == 0)
                    return null;
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SetPasswordFileContentAsync(string content, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(PasswordBlobName);
        await blob.UploadAsync(BinaryData.FromString(content ?? ""), overwrite: true, ct);
    }
}
