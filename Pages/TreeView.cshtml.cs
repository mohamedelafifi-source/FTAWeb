using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FTAWeb.Pages;

public class TreeViewModel : PageModel
{
    public string JsonContent { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        // Retrieve JSON content from TempData
        var jsonContent = TempData["JsonContent"]?.ToString();
        var fileName = TempData["FileName"]?.ToString();

        if (string.IsNullOrEmpty(jsonContent))
        {
            // No file selected, redirect back to index
            return RedirectToPage("/Index");
        }

        JsonContent = jsonContent;
        FileName = fileName ?? "Unknown";

        return Page();
    }
}
