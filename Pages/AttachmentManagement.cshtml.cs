using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FTAWeb.Services;

namespace FTAWeb.Pages;

public class AttachmentManagementModel : PageModel
{
    private readonly IFamilyStorageService _storage;

    public AttachmentManagementModel(IFamilyStorageService storage)
    {
        _storage = storage;
    }

    [FromRoute]
    public string FamilyName { get; set; } = string.Empty;

    [FromQuery]
    public string FileName { get; set; } = string.Empty;

    [FromQuery]
    public string PersonName { get; set; } = string.Empty;

    public IReadOnlyList<string> Attachments { get; set; } = Array.Empty<string>();

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
    }

    public string ViewAttachmentUrl(string fileName)
    {
        return Url.Page("/ViewAttachment", new { familyName = FamilyName, personName = PersonName, fileName }) ?? "";
    }

    public IActionResult OnGet(string familyName, string fileName, string personName)
    {
        FamilyName = familyName ?? "";
        FileName = fileName ?? "";
        PersonName = personName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(PersonName))
            return RedirectToPage("/Index");
        if (!_storage.FamilyExists(FamilyName))
            return RedirectToPage("/SelectFamily");
        Attachments = _storage.ListAttachments(FamilyName, PersonName);
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(string familyName, string fileName, string personName, IFormFile attachmentFile, CancellationToken ct)
    {
        FamilyName = familyName ?? "";
        FileName = fileName ?? "";
        PersonName = personName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(PersonName))
            return RedirectToPage("/Index");

        var saved = await _storage.SaveAttachmentAsync(FamilyName, PersonName, attachmentFile, ct);
        if (saved != null)
            TempData["Message"] = "Attachment added.";
        else
            TempData["Error"] = "Invalid file. Allowed: photos (jpg, png, gif, webp) and PDF.";
        return RedirectToPage("/AttachmentManagement", new { familyName = FamilyName, fileName = FileName, personName = PersonName });
    }

    public IActionResult OnPostDelete(string familyName, string fileName, string personName, string attachmentFileName)
    {
        FamilyName = familyName ?? "";
        FileName = fileName ?? "";
        PersonName = personName ?? "";
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(PersonName) || string.IsNullOrEmpty(attachmentFileName))
            return RedirectToPage("/AttachmentManagement", new { familyName = FamilyName, fileName = FileName, personName = PersonName });

        if (_storage.DeleteAttachment(FamilyName, PersonName, attachmentFileName))
            TempData["Message"] = "Attachment deleted.";
        else
            TempData["Error"] = "Could not delete attachment.";
        return RedirectToPage("/AttachmentManagement", new { familyName = FamilyName, fileName = FileName, personName = PersonName });
    }
}
