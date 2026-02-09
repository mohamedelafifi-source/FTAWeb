namespace FTAWeb.Services;

/// <summary>
/// Stores and looks up family passwords in a simple text file (FamilyName|Password per line, no encryption).
/// </summary>
public interface IFamilyPasswordService
{
    /// <summary>Returns family folder names that have the given password (case-insensitive).</summary>
    IReadOnlyList<string> GetFamiliesByPassword(string password);

    /// <summary>Adds or updates the password for a family (by folder name).</summary>
    void SetPassword(string familyFolderName, string password);

    /// <summary>Removes the password entry for a family (e.g. when family is deleted).</summary>
    void RemovePassword(string familyFolderName);

    /// <summary>Ensures the password file exists; seeds with default families if empty.</summary>
    void EnsurePasswordFileExists();

    /// <summary>Syncs the password file with actual family directories: remove entries for missing dirs, add entry for each directory without one (password = folder name).</summary>
    void SyncWithDirectories();
}
