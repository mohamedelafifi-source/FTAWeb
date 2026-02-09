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

    [FromRoute]
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
        if (!files.Any(f => string.Equals(f, FileName, StringComparison.OrdinalIgnoreCase)))
            return RedirectToPage("/FamilyDetail", new { familyName = FamilyName });
        // Pre-fill with name without extension so user edits "MyTree" and we add .json on save
        NewFileName = System.IO.Path.GetFileNameWithoutExtension(FileName) ?? FileName;
        return Page();
    }

    public IActionResult OnPost()
    {
        // Read from request (route has both on POST when form posts to same URL)
        var familyName = (FamilyName ?? Request.RouteValues["familyName"]?.ToString() ?? "").Trim();
        var originalFileName = (FileName ?? Request.RouteValues["fileName"]?.ToString() ?? Request.Form["FileName"].ToString()).Trim();
        var newNameRaw = (NewFileName ?? Request.Form["NewFileName"].ToString()).Trim();

        if (string.IsNullOrEmpty(familyName))
        {
            ModelState.AddModelError(string.Empty, "Family is missing. Please go back and try again.");
            NewFileName = newNameRaw;
            return Page();
        }
        if (string.IsNullOrEmpty(originalFileName))
        {
            ModelState.AddModelError(string.Empty, "Original file name is missing. Please go back and try again.");
            NewFileName = newNameRaw;
            return Page();
        }
        if (string.IsNullOrEmpty(newNameRaw))
        {
            ModelState.AddModelError(nameof(NewFileName), "New file name is required.");
            return Page();
        }
        var newNameWithExt = newNameRaw.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? newNameRaw : newNameRaw + ".json";
        var currentNameNoExt = System.IO.Path.GetFileNameWithoutExtension(originalFileName);
        if (string.Equals(newNameRaw, currentNameNoExt, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(NewFileName), "The new name is the same as the current name. Please enter a different name.");
            NewFileName = newNameRaw;
            return Page();
        }
        if (!_storage.RenameFile(familyName, originalFileName, newNameWithExt))
        {
            ModelState.AddModelError(nameof(NewFileName), "Could not rename. The new name may already exist or the file may be in use.");
            NewFileName = newNameRaw;
            return Page();
        }
        TempData["Message"] = "File renamed successfully.";
        return RedirectToPage("/FamilyDetail", new { familyName, refreshed = DateTime.UtcNow.Ticks });
    }
}
