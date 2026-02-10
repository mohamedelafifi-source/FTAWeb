namespace FTAWeb.Services;

/// <summary>
/// Exposes which storage backend is in use (for display only).
/// </summary>
public interface IStorageModeService
{
    /// <summary>Display name of the current storage, e.g. "Azure Blob" or "Local files".</summary>
    string StorageModeName { get; }
    /// <summary>True when using Azure Blob Storage.</summary>
    bool IsAzureBlob { get; }
}
