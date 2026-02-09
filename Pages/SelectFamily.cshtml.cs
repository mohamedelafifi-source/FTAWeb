using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class SelectFamilyModel : PageModel
{
    private readonly IFamilyStorageService _storage;
    private readonly IFamilyPasswordService _passwords;

    public SelectFamilyModel(IFamilyStorageService storage, IFamilyPasswordService passwords)
    {
        _storage = storage;
        _passwords = passwords;
    }

    public IReadOnlyList<string> Families { get; set; } = Array.Empty<string>();

    /// <summary>True when we should show the password form (GET or failed POST). False after correct password: show family list.</summary>
    public bool ShowPasswordForm { get; set; }

    public string? PasswordError { get; set; }

    [BindProperty]
    public string? AccessPassword { get; set; }

    /// <summary>GET: always show the password form. No list until they POST with correct password.</summary>
    public IActionResult OnGet()
    {
        _passwords.EnsurePasswordFileExists();
        ShowPasswordForm = true;
        Families = Array.Empty<string>();
        return Page();
    }

    /// <summary>POST: validate password. If correct, show the list of family names that have this password. User then selects one to go to FamilyDetail.</summary>
    public IActionResult OnPost()
    {
        _passwords.EnsurePasswordFileExists();

        var password = (AccessPassword ?? "").Trim();
        if (string.IsNullOrEmpty(password))
        {
            ShowPasswordForm = true;
            PasswordError = "Please enter a password.";
            Families = Array.Empty<string>();
            return Page();
        }

        var byPassword = _passwords.GetFamiliesByPassword(password);
        if (byPassword.Count == 0)
        {
            ShowPasswordForm = true;
            PasswordError = "No families use this password. Try again.";
            Families = Array.Empty<string>();
            return Page();
        }

        var allFamilies = _storage.ListFamilies();
        Families = allFamilies.Where(f => byPassword.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
        ShowPasswordForm = false;
        PasswordError = null;
        return Page();
    }
}
