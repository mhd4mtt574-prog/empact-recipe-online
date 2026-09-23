using System.Security.Claims;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class PromotionsController(JsonDataStore store) : Controller
{
    public IActionResult Index()
    {
        var items = User.IsInRole("Administrator")
            ? store.Data.Promotions.OrderByDescending(x => x.StartDate).ToList()
            : store.Data.Promotions.Where(x => x.IsActive || (x.IsPublished && !string.IsNullOrWhiteSpace(x.DocumentUrl))).OrderByDescending(x => x.StartDate).ToList();
        return View(items);
    }

    [Authorize(Roles="Administrator")]
    public IActionResult Create() => View("Edit", new Promotion());

    [Authorize(Roles="Administrator")]
    public IActionResult Edit(Guid id)
    {
        var item = store.Data.Promotions.FirstOrDefault(x => x.Id == id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles="Administrator")]
    public IActionResult Save(Promotion model)
    {
        if (model.EndDate.Date < model.StartDate.Date)
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after the start date.");
        if (!ModelState.IsValid) return View("Edit", model);
        var existing = store.Data.Promotions.FirstOrDefault(x => x.Id == model.Id);
        if (existing is null) store.Data.Promotions.Add(model);
        else { existing.Title=model.Title; existing.Description=model.Description; existing.StartDate=model.StartDate; existing.EndDate=model.EndDate; existing.Region=model.Region; existing.DocumentUrl=model.DocumentUrl; existing.DocumentFileName=model.DocumentFileName; existing.IsPublished=model.IsPublished; model=existing; }
        model.UpdatedAtUtc=DateTime.UtcNow; model.UpdatedBy=User.Identity?.Name ?? "Administrator";
        store.Save();
        TempData["Success"]="Promotion saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult ViewDocument(Guid id)
    {
        var item = store.Data.Promotions.FirstOrDefault(x => x.Id == id);
        if (item is null || string.IsNullOrWhiteSpace(item.DocumentUrl)) return NotFound();
        LogPromotion(item, "PromotionView");
        return Redirect(item.DocumentUrl);
    }

    [HttpGet]
    public IActionResult Download(Guid id)
    {
        var item = store.Data.Promotions.FirstOrDefault(x => x.Id == id);
        if (item is null || string.IsNullOrWhiteSpace(item.DocumentUrl)) return NotFound();
        LogPromotion(item, "PromotionDownload");
        var relative = item.DocumentUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative);
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        var fileName = string.IsNullOrWhiteSpace(item.DocumentFileName) ? Path.GetFileName(fullPath) : item.DocumentFileName;
        return PhysicalFile(fullPath, "application/pdf", fileName);
    }

    [HttpGet]
    public IActionResult Print(Guid id)
    {
        var item = store.Data.Promotions.FirstOrDefault(x => x.Id == id);
        if (item is null || string.IsNullOrWhiteSpace(item.DocumentUrl)) return NotFound();
        LogPromotion(item, "PromotionPrint");
        return View(item);
    }

    private void LogPromotion(Promotion item, string action)
    {
        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        store.LogActivity(new ActivityLog { UserId=userId, UserName=User.Identity?.Name ?? "Unknown", UserRole=User.FindFirstValue(ClaimTypes.Role) ?? string.Empty, Unit=User.FindFirstValue("unit") ?? string.Empty, Region=User.FindFirstValue("region") ?? string.Empty, Action=action, EntityType="Promotion", EntityId=item.Id, EntityName=item.Title });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles="Administrator")]
    public IActionResult Delete(Guid id)
    {
        var item=store.Data.Promotions.FirstOrDefault(x=>x.Id==id);
        if(item is not null){store.Data.Promotions.Remove(item);store.Save();}
        TempData["Success"]="Promotion removed.";
        return RedirectToAction(nameof(Index));
    }
}
