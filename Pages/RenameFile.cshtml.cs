using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class RenameFileModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public RenameFileModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    [FromRoute]
    [BindProperty(SupportsGet = true)]
    public string FamilyName { get; set; } = string.Empty;

    [FromQuery]
    [BindProperty(SupportsGet = true)]
    public string FileName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "New file name is required.")]
    [Display(Name = "New file name")]
    public string NewFileName { get; set; } = string.Empty;

    public IActionResult OnGet(string familyName, string fileName)
    {
        FamilyName = familyName ?? "";
        FileName = fileName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(FileName))
            return RedirectToPage("/SelectFamily");
        if (!_storage.FamilyExists(FamilyName))
            return RedirectToPage("/SelectFamily");
        var files = _storage.GetFamilyFiles(FamilyName);
        if (!files.Contains(FileName))
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        NewFileName = FileName;
        return Page();
    }

    public IActionResult OnPost()
    {
        NewFileName = (NewFileName ?? "").Trim();
        if (string.IsNullOrEmpty(NewFileName))
        {
            ModelState.AddModelError(nameof(NewFileName), "New file name is required.");
            return Page();
        }
        if (!NewFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            NewFileName += ".json";
        if (!_storage.RenameFile(FamilyName, FileName, NewFileName))
        {
            ModelState.AddModelError(nameof(NewFileName), "Could not rename. The new name may already exist or the file may be in use.");
            return Page();
        }
        TempData["Message"] = "File renamed successfully.";
        return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
    }
}
