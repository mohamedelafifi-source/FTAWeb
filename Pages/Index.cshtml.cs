using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FTAWeb.Pages;

public class IndexModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost(IFormFile jsonFile)
    {
        if (jsonFile == null || jsonFile.Length == 0)
        {
            ModelState.AddModelError("jsonFile", "Please select a valid JSON file.");
            return Page();
        }

        // Store the file content in session or temp data
        using var reader = new StreamReader(jsonFile.OpenReadStream());
        var jsonContent = reader.ReadToEnd();
        
        TempData["JsonContent"] = jsonContent;
        TempData["FileName"] = jsonFile.FileName;

        return RedirectToPage("/TreeView");
    }
}
