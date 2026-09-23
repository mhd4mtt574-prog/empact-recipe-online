using System.Security.Claims;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class ResalePricingController(ResalePricingService pricing, JsonDataStore store, IDataProtectionProvider dataProtection) : Controller
{
    private const string CookieName = "EmpactResaleAccess";
    private readonly IDataProtector _protector = dataProtection.CreateProtector("EmpactRecipeOnline.ResalePricingAccess.v2");

    [HttpGet]
    public IActionResult Access()
    {
        if (GetAccess() is not null) return RedirectToAction(nameof(Index));
        return View(new ResaleAccessViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Access(ResaleAccessViewModel model)
    {
        model.IdNumber = new string((model.IdNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (!ModelState.IsValid) return View(model);

        var access = pricing.ResolveAccess(model.IdNumber);
        if (access is null)
        {
            ModelState.AddModelError(nameof(model.IdNumber), "This ID is not authorised for the resale pricing tool.");
            return View(model);
        }

        Response.Cookies.Append(CookieName, _protector.Protect(access.IdNumber), new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            MaxAge = TimeSpan.FromHours(8)
        });
        Log(access, "ResaleAccessGranted", string.Empty, "Resale pricing ID access verified.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Index(string? unitCode, string? category, string? search)
    {
        var access = GetAccess();
        if (access is null) return RedirectToAction(nameof(Access));

        var units = pricing.GetUnits(access);
        ResaleUnit? selectedUnit = null;
        string selectedCategory;

        if (access.IsSeniorManager)
        {
            if (!string.IsNullOrWhiteSpace(unitCode))
                selectedUnit = units.FirstOrDefault(x => x.Code.Equals(unitCode, StringComparison.OrdinalIgnoreCase));
            selectedCategory = selectedUnit?.Category?.Trim().ToUpperInvariant()
                ?? (category?.Trim().ToUpperInvariant() is "B" ? "B" : "A");
        }
        else
        {
            selectedUnit = !string.IsNullOrWhiteSpace(unitCode)
                ? units.FirstOrDefault(x => x.Code.Equals(unitCode, StringComparison.OrdinalIgnoreCase))
                : units.FirstOrDefault();
            if (selectedUnit is null) return Forbid();
            selectedCategory = selectedUnit.Category.Trim().ToUpperInvariant();
        }

        var rows = pricing.GetPricesForCategory(selectedCategory).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            rows = rows.Where(x => x.ItemCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.ItemDescription.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.Brand.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.BrandCategorySize.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.MainCategory.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var vm = new ResalePricingViewModel
        {
            AccessName = access.Name,
            IsSeniorManager = access.IsSeniorManager,
            Units = units,
            UnitCode = selectedUnit?.Code ?? string.Empty,
            UnitName = selectedUnit?.Name ?? (access.IsSeniorManager ? "Management / Sales" : string.Empty),
            Category = selectedCategory,
            Search = search?.Trim() ?? string.Empty,
            Prices = rows.OrderBy(x => x.ItemDescription).ThenBy(x => x.ItemCode).ToList(),
            SourcePricingWorkbook = pricing.PricingSourceWorkbook,
            SourceSheet = $"Catagory {selectedCategory}"
        };

        Log(access, "ResalePriceListViewed", selectedUnit?.Code ?? $"CATEGORY-{selectedCategory}",
            $"Viewed Category {selectedCategory} resale price list from {pricing.PricingSourceWorkbook}; {vm.Prices.Count} displayed item(s).");
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Lock()
    {
        Response.Cookies.Delete(CookieName);
        return RedirectToAction(nameof(Access));
    }

    private ResaleAccessIdentity? GetAccess()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrWhiteSpace(token)) return null;
        try { return pricing.ResolveAccess(_protector.Unprotect(token)); }
        catch { Response.Cookies.Delete(CookieName); return null; }
    }

    private void Log(ResaleAccessIdentity access, string action, string unitCode, string notes)
    {
        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        store.LogActivity(new ActivityLog
        {
            UserId = userId,
            UserName = access.Name,
            UserRole = access.IsSeniorManager ? "Resale Senior Manager" : "Resale Unit Manager",
            Unit = unitCode,
            Region = User.FindFirstValue("region") ?? string.Empty,
            Action = action,
            EntityType = "ResalePricing",
            EntityName = access.Name,
            Notes = $"Verified ID {MaskId(access.IdNumber)}; signed-in app user {User.Identity?.Name ?? "Unknown"}. {notes}"
        });
    }

    private static string MaskId(string id)
    {
        var digits = new string((id ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"*********{digits[^4..]}" : "*************";
    }
}
