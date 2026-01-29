using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class CreateFamilyModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public CreateFamilyModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    [BindProperty]
    [Required(ErrorMessage = "Family name is required.")]
    [Display(Name = "Family name")]
    [StringLength(100, MinimumLength = 1)]
    public string FamilyName { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        return Page();
    }

    public IActionResult OnPost()
    {
        FamilyName = (FamilyName ?? "").Trim();
        if (string.IsNullOrEmpty(FamilyName))
        {
            ModelState.AddModelError(nameof(FamilyName), "Family name is required.");
            return Page();
        }

        if (_storage.FamilyExists(FamilyName))
        {
            ModelState.AddModelError(nameof(FamilyName), "A family with this name already exists. Please choose a different name.");
            return Page();
        }

        var folderName = _storage.CreateFamily(FamilyName);
        if (folderName == null)
        {
            ModelState.AddModelError(nameof(FamilyName), "Could not create the family. The name may already exist.");
            return Page();
        }

        return RedirectToPage("/FamilyDetail", new { familyName = folderName });
    }
}
