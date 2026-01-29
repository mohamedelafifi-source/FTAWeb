using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class TreeViewModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public TreeViewModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    public string JsonContent { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string familyName, string fileName, CancellationToken ct)
    {
        FamilyName = familyName ?? "";
        FileName = fileName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(FileName))
            return RedirectToPage("/Index");
        if (!_storage.FamilyExists(FamilyName))
            return RedirectToPage("/SelectFamily");
        var content = await _storage.GetFileContentAsync(FamilyName, FileName, ct);
        if (string.IsNullOrEmpty(content))
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        JsonContent = content;
        return Page();
    }
}
