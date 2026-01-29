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
}
