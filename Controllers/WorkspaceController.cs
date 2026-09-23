using System.Security.Claims;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class WorkspaceController(JsonDataStore store, WorkspaceService workspace) : Controller
{
    public IActionResult Index(string? q)
    {
        var user = workspace.GetCurrentUser(User);
        var model = new WorkspaceDashboardViewModel
        {
            Tasks = workspace.GetTasks(User),
            Query = q ?? string.Empty,
            SearchResults = workspace.Search(User, q),
            IsAdministrator = User.IsInRole("Administrator")
        };
        if (user is not null)
        {
            model.Favourites = store.Data.UserFavourites.Where(x => x.UserId == user.Id).OrderByDescending(x => x.AddedAtUtc).ToList();
            model.RecentActivity = store.Data.ActivityLogs.Where(x => x.UserId == user.Id).OrderByDescending(x => x.OccurredAtUtc).Take(12).ToList();
        }
        if (model.IsAdministrator) model.AttentionItems = workspace.GetAdminAttention().Take(100).ToList();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ToggleFavourite(string entityType, Guid? entityId, string name, string url, string? returnUrl)
    {
        var user = workspace.GetCurrentUser(User);
        if (user is null) return Forbid();
        var added = store.ToggleFavourite(user.Id, entityType ?? string.Empty, entityId, name ?? string.Empty, url ?? string.Empty);
        TempData["Success"] = added ? "Added to favourites." : "Removed from favourites.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles="Administrator")]
    public IActionResult History(string? q, string? unit, string? action, DateTime? from, DateTime? to)
    {
        var start = (from ?? DateTime.Today.AddDays(-30)).Date.ToUniversalTime();
        var end = (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        var logs = store.Data.ActivityLogs.Where(x => x.OccurredAtUtc >= start && x.OccurredAtUtc <= end).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q)) logs = logs.Where(x => x.UserName.Contains(q,StringComparison.OrdinalIgnoreCase) || x.EntityName.Contains(q,StringComparison.OrdinalIgnoreCase) || x.Notes.Contains(q,StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(unit)) logs = logs.Where(x => x.Unit.Equals(unit,StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(action)) logs = logs.Where(x => x.Action.Equals(action,StringComparison.OrdinalIgnoreCase));
        ViewBag.Query=q??""; ViewBag.Unit=unit??""; ViewBag.Action=action??""; ViewBag.From=(from??DateTime.Today.AddDays(-30)).Date; ViewBag.To=(to??DateTime.Today).Date;
        ViewBag.Units=store.Data.ActivityLogs.Select(x=>x.Unit).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList();
        ViewBag.Actions=store.Data.ActivityLogs.Select(x=>x.Action).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList();
        return View(logs.OrderByDescending(x=>x.OccurredAtUtc).Take(1000).ToList());
    }
}
