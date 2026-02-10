namespace FTAWeb.Services;

public class StorageModeService : IStorageModeService
{
    public StorageModeService(IConfiguration configuration)
    {
        var section = configuration.GetSection("FamilyStorage").GetSection("Azure");
        var conn = configuration["FamilyStorage:Azure:ConnectionString"]?.Trim()
            ?? section["ConnectionString"]?.Trim()
            ?? Environment.GetEnvironmentVariable("FamilyStorage__Azure__ConnectionString")?.Trim();
        var hasAccount = !string.IsNullOrWhiteSpace(section["AccountName"]?.Trim()) && !string.IsNullOrWhiteSpace(section["AccountKey"]?.Trim());
        IsAzureBlob = !string.IsNullOrEmpty(conn) || hasAccount;
        StorageModeName = IsAzureBlob ? "Azure Blob" : "Local files";
    }

    public string StorageModeName { get; }
    public bool IsAzureBlob { get; }
}
