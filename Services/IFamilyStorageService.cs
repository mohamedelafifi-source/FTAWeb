using Microsoft.AspNetCore.Http;

namespace FTAWeb.Services;

public interface IFamilyStorageService
{
    string GetFamiliesBasePath();
    IReadOnlyList<string> ListFamilies();
    bool FamilyExists(string familyName);
    /// <summary>Creates a family. Returns the folder name used, or null if already exists.</summary>
    string? CreateFamily(string familyName);
    IReadOnlyList<string> GetFamilyFiles(string familyName);
    Task SaveFileAsync(string familyName, string fileName, Stream content, CancellationToken ct = default);
    Task<string?> GetFileContentAsync(string familyName, string fileName, CancellationToken ct = default);
    bool RenameFile(string familyName, string oldFileName, string newFileName);
    bool DeleteFile(string familyName, string fileName);
    bool DeleteFamily(string familyName);

    // Attachments (per family, per person) - same folder structure, can be swapped to BLOB later
    IReadOnlyList<string> ListAttachments(string familyName, string personName);
    Task<string?> SaveAttachmentAsync(string familyName, string personName, IFormFile file, CancellationToken ct = default);
    bool DeleteAttachment(string familyName, string personName, string fileName);
    (Stream? stream, string? contentType) GetAttachment(string familyName, string personName, string fileName);
    string? GetAttachmentPath(string familyName, string personName, string fileName);
}
