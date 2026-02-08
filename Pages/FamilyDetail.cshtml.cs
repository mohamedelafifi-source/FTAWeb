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
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
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

    public async Task<IActionResult> OnPostImportAsync(string familyName, IFormFile textFile, string treeFileName, CancellationToken ct)
    {
        FamilyName = familyName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || !_storage.FamilyExists(FamilyName))
            return RedirectToPage("/SelectFamily");
        if (textFile == null || textFile.Length == 0)
        {
            TempData["Error"] = "Please select a text file.";
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        }
        treeFileName = (treeFileName ?? "").Trim();
        if (string.IsNullOrEmpty(treeFileName))
        {
            TempData["Error"] = "Please enter a tree file name.";
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        }
        if (!treeFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            treeFileName += ".json";

        string text;
        using (var reader = new StreamReader(textFile.OpenReadStream()))
            text = await reader.ReadToEndAsync(ct);

        var (json, error) = ImportTreeService.ParseAndGenerateJson(text);
        if (error != null)
        {
            TempData["Error"] = "Import failed: " + error;
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        }
        if (string.IsNullOrEmpty(json))
        {
            TempData["Error"] = "Import produced no data.";
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await using var stream = new MemoryStream(bytes);
        await _storage.SaveFileAsync(FamilyName, treeFileName, stream, ct);
        TempData["Message"] = "Tree imported successfully as " + treeFileName + ".";
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
