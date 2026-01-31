using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class FamilyDetailModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public FamilyDetailModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    [FromRoute]
    public string FamilyName { get; set; } = string.Empty;

    public IReadOnlyList<string> Files { get; set; } = Array.Empty<string>();

    public IActionResult OnGet(string familyName)
    {
        FamilyName = familyName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || !_storage.FamilyExists(FamilyName))
            return RedirectToPage("/SelectFamily");
        Files = _storage.GetFamilyFiles(FamilyName);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(string familyName, IFormFile jsonFile, CancellationToken ct)
    {
        FamilyName = familyName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || !_storage.FamilyExists(FamilyName))
            return RedirectToPage("/SelectFamily");
        if (jsonFile == null || jsonFile.Length == 0)
        {
            TempData["Error"] = "Please select a valid Tree file.";
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        }
        var fileName = Path.GetFileName(jsonFile.FileName);
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName = fileName ?? "file.json";
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName += ".json";
        await _storage.SaveFileAsync(FamilyName, fileName, jsonFile.OpenReadStream(), ct);
        TempData["Message"] = "File uploaded successfully.";
        return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
    }

    public IActionResult OnPostDelete(string familyName, string fileName)
    {
        FamilyName = familyName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(fileName))
            return RedirectToPage("/SelectFamily");
        if (_storage.DeleteFile(FamilyName, fileName))
            TempData["Message"] = "File deleted.";
        else
            TempData["Error"] = "Could not delete file.";
        return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
    }
}
