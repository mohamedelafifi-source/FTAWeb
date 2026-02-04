using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class ViewAttachmentModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public ViewAttachmentModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    public IActionResult OnGet(string familyName, string personName, string fileName)
    {
        if (string.IsNullOrEmpty(familyName) || string.IsNullOrEmpty(personName) || string.IsNullOrEmpty(fileName))
            return NotFound();
        if (!_storage.FamilyExists(familyName))
            return NotFound();

        var path = _storage.GetAttachmentPath(familyName, personName, fileName);
        if (path == null)
            return NotFound();

        var ext = Path.GetExtension(fileName);
        var contentType = ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
        return PhysicalFile(path, contentType, fileName);
    }
}
