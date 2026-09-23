using EmpactRecipeOnline.Services;
using EmpactRecipeOnline.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class HomeController(JsonDataStore store, ResalePricingService resalePricing, WorkspaceService workspace) : Controller
{
    public IActionResult Index()
    {
        ViewBag.RecipeCount = store.Data.Recipes.Count;
        ViewBag.ApprovedCount = store.Data.Recipes.Count(x => x.IsApproved);
        ViewBag.AllergenReviewCount = store.Data.Recipes.Count(x => x.SupplierVerificationRequired);
        ViewBag.Recent = store.Data.Recipes.OrderByDescending(x => x.UpdatedAtUtc).Take(6).ToList();
        ViewBag.MyTasks = workspace.GetTasks(User).Take(6).ToList();
        var currentWorkspaceUser = workspace.GetCurrentUser(User);
        ViewBag.Favourites = currentWorkspaceUser is null ? new List<UserFavourite>() : store.Data.UserFavourites.Where(x => x.UserId == currentWorkspaceUser.Id).OrderByDescending(x=>x.AddedAtUtc).Take(4).ToList();

        // Dashboard resale preview must come from the resale pricing workbook, never from recipes.
        var signedInUnitCode = User.FindFirst("unit")?.Value?.Trim() ?? string.Empty;
        var resaleUnit = !string.IsNullOrWhiteSpace(signedInUnitCode) ? resalePricing.GetUnit(signedInUnitCode) : null;
        var resaleCategory = resaleUnit?.Category?.Trim().ToUpperInvariant() is "B" ? "B" : "A";
        ViewBag.ResalePreview = resalePricing.GetPricesForCategory(resaleCategory)
            .OrderBy(x => x.ItemDescription)
            .ThenBy(x => x.ItemCode)
            .Take(4)
            .ToList();
        ViewBag.ResaleCategory = resaleCategory;
        ViewBag.ResaleUnitName = resaleUnit?.Name ?? (User.IsInRole("Administrator") ? "Management preview" : signedInUnitCode);

        var nowMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = Guid.TryParse(currentUserId, out var uid) ? store.Data.Users.FirstOrDefault(x => x.Id == uid) : null;
        var foodWasteRecords = store.Data.FoodWasteRecords.Where(x => !x.IsDraft && x.BusinessDate.Year == nowMonth.Year && x.BusinessDate.Month == nowMonth.Month).ToList();
        if (!User.IsInRole("Administrator") && currentUser is not null)
        {
            foodWasteRecords = foodWasteRecords.Where(x => x.UnitCode.Equals(currentUser.UnitCode, StringComparison.OrdinalIgnoreCase) || x.UnitName.Equals(currentUser.Unit, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        ViewBag.FoodWasteTotalKg = foodWasteRecords.Sum(x => x.TotalFoodWasteKg);
        ViewBag.FoodWasteDiversionKg = foodWasteRecords.Sum(x => x.TotalDiversionKg);
        ViewBag.FoodWasteMeals = foodWasteRecords.Sum(x => x.TotalMeals);
        ViewBag.FoodWasteLastSubmission = foodWasteRecords.OrderByDescending(x => x.SubmittedAtUtc).FirstOrDefault()?.SubmittedAtUtc;
        var foodWasteDays = foodWasteRecords.Select(x => new { x.UnitCode, Day = x.BusinessDate.Date }).Distinct().Count();
        ViewBag.FoodWasteSubmissions = foodWasteDays;

        if (User.IsInRole("Administrator") && DateTime.Now.TimeOfDay >= new TimeSpan(9,0,0))
        {
            var units = store.Data.Users.Where(x=>x.IsActive && (x.Role == EmpactRecipeOnline.Models.UserRole.UnitManager || x.Role == EmpactRecipeOnline.Models.UserRole.UnitUser) && !string.IsNullOrWhiteSpace(x.Unit)).Select(x=>x.Unit).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ViewBag.MissingTieBacks = units.Count(u => !store.Data.DailyTieBackUploads.Any(x => x.BusinessDate.Date == DateTime.Today && TieBackUnitMatcher.Matches(x.Unit, u)));
        }
        return View();
    }
    [AllowAnonymous] public IActionResult Error() => View();
}
