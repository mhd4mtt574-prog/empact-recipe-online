using System.Globalization;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class ApprovedProductsController(JsonDataStore store) : Controller
{
    private static List<string> SplitCsv(string? value) => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private const int PageSize = 100;

    public IActionResult Index(string? q, string? status, string? division, int page = 1)
    {
        division = NormalizeDivision(division ?? DefaultDivision());
        page = Math.Max(1, page);

        var products = store.Data.ApprovedProducts
            .Where(x => x.Division.Equals(division, StringComparison.OrdinalIgnoreCase))
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            products = products.Where(x =>
                x.ProductCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.SupplierItemCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.VendorName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.ContractCode.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        if (status == "active") products = products.Where(x => x.IsActive);
        if (status == "inactive") products = products.Where(x => !x.IsActive);

        var ordered = products.OrderBy(x => x.ProductName).ThenBy(x => x.VendorName).ToList();
        var total = ordered.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, totalPages);

        ViewBag.Query = q;
        ViewBag.Status = status;
        ViewBag.Division = division;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalProducts = total;
        ViewBag.DivisionCounts = store.Data.ApprovedProducts
            .GroupBy(x => x.Division)
            .ToDictionary(g => g.Key, g => g.Count());
        ViewBag.RecipeCount = store.Data.Recipes.Count;
        ViewBag.LinkedLines = store.Data.Recipes.Sum(r => r.Ingredients.Count(i => !string.IsNullOrWhiteSpace(i.ProductCode)));
        ViewBag.IsAdministrator = User.IsInRole("Administrator");

        return View(ordered.Skip((page - 1) * PageSize).Take(PageSize).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(Guid id, string productName, string unitOfMeasure, string unitCost, bool isActive,
        string division, string? q, string? status, int page = 1, string? allergens = null, string? mayContain = null, bool allergenVerificationRequired = false)
    {
        var product = store.Data.ApprovedProducts.FirstOrDefault(x => x.Id == id);
        if (product is null) return NotFound();
        if (!TryParseMoney(unitCost, out var parsedCost) || parsedCost < 0)
        {
            TempData["Error"] = "Enter a valid unit cost.";
            return RedirectToAction(nameof(Index), new { division, q, status, page });
        }
        if (string.IsNullOrWhiteSpace(productName))
        {
            TempData["Error"] = "Product name is required.";
            return RedirectToAction(nameof(Index), new { division, q, status, page });
        }

        product.ProductName = productName;
        product.UnitOfMeasure = unitOfMeasure ?? string.Empty;
        product.UnitCost = parsedCost;
        product.IsActive = isActive;
        product.Allergens = SplitCsv(allergens);
        product.MayContainAllergens = SplitCsv(mayContain);
        product.AllergenVerificationRequired = allergenVerificationRequired;
        var saved = store.UpsertApprovedProduct(product, User.Identity?.Name ?? "Administrator");
        var recipes = store.Data.Recipes.Count(r => r.Ingredients.Any(i =>
            i.ProductCode.Equals(saved.ProductCode, StringComparison.OrdinalIgnoreCase)));
        TempData["Success"] = $"{saved.ProductCode} ({saved.Division}) was updated. Costs were recalculated in {recipes} linked recipe(s).";
        return RedirectToAction(nameof(Index), new { division = saved.Division, q = q ?? saved.ProductCode, status, page });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string division, string productCode, string productName, string unitOfMeasure, string unitCost, string? allergens = null, string? mayContain = null, bool allergenVerificationRequired = false)
    {
        division = NormalizeDivision(division);
        if (!TryParseMoney(unitCost, out var parsedCost) || parsedCost < 0 || string.IsNullOrWhiteSpace(productCode) || string.IsNullOrWhiteSpace(productName))
        {
            TempData["Error"] = "Complete the division, product code, name and a valid unit cost.";
            return RedirectToAction(nameof(Index), new { division });
        }

        store.UpsertApprovedProduct(new ApprovedProduct
        {
            Division = division,
            ProductCode = productCode,
            ProductName = productName,
            UnitOfMeasure = unitOfMeasure ?? string.Empty,
            UnitCost = parsedCost,
            IsActive = true,
            Allergens = SplitCsv(allergens),
            MayContainAllergens = SplitCsv(mayContain),
            AllergenVerificationRequired = allergenVerificationRequired
        }, User.Identity?.Name ?? "Administrator");
        TempData["Success"] = $"{productCode.Trim().ToUpperInvariant()} was added to the {division} Approved Product List.";
        return RedirectToAction(nameof(Index), new { division, q = productCode });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ApplyAll(string division)
    {
        var count = store.ApplyAllApprovedProductCosts(User.Identity?.Name ?? "Administrator");
        TempData["Success"] = $"Approved Product List costs were applied to {count} recipe(s).";
        return RedirectToAction(nameof(Index), new { division = NormalizeDivision(division) });
    }

    private string DefaultDivision()
    {
        if (User.IsInRole("Administrator")) return "CT";
        var user = store.Data.Users.FirstOrDefault(x => x.Email.Equals(User.Identity?.Name, StringComparison.OrdinalIgnoreCase));
        var unit = user?.Unit ?? string.Empty;
        if (unit.Contains("Gauteng", StringComparison.OrdinalIgnoreCase) || unit.Contains("Johannesburg", StringComparison.OrdinalIgnoreCase)) return "JHB";
        if (unit.Contains("Durban", StringComparison.OrdinalIgnoreCase) || unit.Contains("KwaZulu", StringComparison.OrdinalIgnoreCase)) return "KZN";
        return "CT";
    }

    private static string NormalizeDivision(string? division)
    {
        var value = (division ?? "CT").Trim().ToUpperInvariant();
        return value switch { "JHB" => "JHB", "KZN" => "KZN", _ => "CT" };
    }

    private static bool TryParseMoney(string? raw, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        raw = raw.Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(raw.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
