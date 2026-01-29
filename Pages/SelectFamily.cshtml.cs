using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class SelectFamilyModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public SelectFamilyModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    public IReadOnlyList<string> Families { get; set; } = Array.Empty<string>();

    public void OnGet()
    {
        Families = _storage.ListFamilies();
    }
}
