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

        var (stream, contentType) = _storage.GetAttachment(familyName, personName, fileName);
        if (stream == null)
            return NotFound();

        return File(stream, contentType ?? "application/octet-stream", fileName);
    }
}
