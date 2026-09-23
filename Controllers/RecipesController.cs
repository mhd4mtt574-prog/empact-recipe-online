using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class RecipesController(JsonDataStore store, AllergenService allergenService, NutritionService nutritionService, IWebHostEnvironment env) : Controller
{
    public IActionResult Index(string? q, string? category, string? department)
    {
        var recipes = store.Data.Recipes.AsEnumerable();
        if (!User.IsInRole("Administrator")) recipes = recipes.Where(x => x.IsApproved);
        if (!string.IsNullOrWhiteSpace(q)) recipes = recipes.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category)) recipes = recipes.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(department)) recipes = recipes.Where(x => x.Department.Equals(department, StringComparison.OrdinalIgnoreCase));
        ViewBag.Query = q;
        ViewBag.Category = category;
        ViewBag.Department = department;
        ViewBag.Departments = RecipeDepartments.All;
        ViewBag.Categories = store.Data.Recipes.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
        return View(recipes.OrderBy(x => RecipeDepartments.SortOrder(x.Department)).ThenBy(x => x.Name).ToList());
    }

    public IActionResult Details(Guid id, int? portions)
    {
        var sourceRecipe = store.Data.Recipes.FirstOrDefault(x => x.Id == id);
        if (sourceRecipe is null) return NotFound();
        var userRegion = User.FindFirstValue("region") ?? User.FindFirstValue("unit") ?? "Cape Town";
        var recipe = store.GetRegionalRecipe(id, userRegion);
        var allergenScan = allergenService.DetectDetailed(recipe.Ingredients, store.Data.ApprovedProducts);
        recipe.AutoDetectedAllergens = allergenScan.Allergens;
        recipe.AutoDetectedMayContainAllergens = allergenScan.MayContainAllergens;
        recipe.AllergenDetectionSources = allergenScan.Sources;
        recipe.LastAllergenScanAtUtc = DateTime.UtcNow;
        recipe.Allergens = recipe.Allergens.Concat(allergenScan.Allergens).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        recipe.MayContainAllergens = recipe.MayContainAllergens.Concat(allergenScan.MayContainAllergens).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        recipe.SupplierVerificationRequired |= allergenScan.ReviewRequired;
        ViewBag.AutoAllergenFindings = allergenScan.Sources;
        ViewBag.RecipeRegion = JsonDataStore.DivisionDisplayName(JsonDataStore.GetDivisionForUnit(userRegion));
        ViewBag.DisplayPortions = Math.Clamp(portions ?? recipe.StandardPortions, 1, 100000);
        ViewBag.NutritionAnalysis = nutritionService.CalculateFromCache(recipe, 1);
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdText, out var parsedUserId) ? parsedUserId : Guid.Empty;
        ViewBag.DesiredMarginPercent = store.GetDesiredMargin(recipe.Id, userId, recipe.DesiredMarginPercent);
        ViewBag.MarginUnit = User.FindFirstValue("unit") ?? "Current unit";
        LogCurrentUserActivity("RecipeView", "Recipe", recipe.Id, recipe.Name, recipe.Department, portions ?? recipe.StandardPortions, "Recipe details viewed");
        return View(recipe);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateNutrition(Guid id, int portions, CancellationToken cancellationToken)
    {
        var recipe = store.Data.Recipes.FirstOrDefault(x => x.Id == id);
        if (recipe is null) return NotFound();
        try
        {
            var analysis = await nutritionService.GenerateAsync(recipe, cancellationToken);
            LogCurrentUserActivity("NutritionGenerate", "Recipe", recipe.Id, recipe.Name, recipe.Department, analysis.CoveragePercent, $"Automatic nutrition generated with {analysis.CoveragePercent:N0}% ingredient coverage");
            TempData["Success"] = analysis.IsComplete
                ? "Nutritional analysis generated from the recipe ingredients. Review all source matches before marking it verified."
                : $"Nutrition generated with {analysis.CoveragePercent:N0}% ingredient coverage. Review the unmapped/conversion-warning ingredients below.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Nutrition lookup could not be completed: " + ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id, portions = Math.Clamp(portions, 1, 100000) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult UpdateDesiredMargin(Guid id, int portions, decimal desiredMarginPercent)
    {
        var recipe = store.Data.Recipes.FirstOrDefault(x => x.Id == id);
        if (recipe is null) return NotFound();
        if (desiredMarginPercent < 0m || desiredMarginPercent > 99.99m)
        {
            TempData["Error"] = "Desired GP margin must be between 0% and 99.99%.";
            return RedirectToAction(nameof(Details), new { id, portions });
        }
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId)) return Forbid();
        store.UpsertDesiredMargin(recipe.Id, userId, desiredMarginPercent, User.Identity?.Name ?? "Unknown");
        LogCurrentUserActivity("MarginUpdate", "Recipe", recipe.Id, recipe.Name, recipe.Department, desiredMarginPercent, "Desired GP margin updated");
        TempData["Success"] = $"Desired GP margin saved at {desiredMarginPercent:N2}% for your unit.";
        return RedirectToAction(nameof(Details), new { id, portions = Math.Clamp(portions, 1, 100000) });
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Create()
    {
        SetLists();
        return View("Edit", new RecipeEditViewModel());
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Edit(Guid id)
    {
        var recipe = store.Data.Recipes.FirstOrDefault(x => x.Id == id);
        if (recipe is null) return NotFound();

        SetLists();
        var editScan = allergenService.DetectDetailed(recipe.Ingredients, store.Data.ApprovedProducts);
        ViewBag.AutoAllergenFindings = editScan.Sources;
        ViewBag.AutoDetectedAllergens = editScan.Allergens;
        ViewBag.AutoDetectedMayContain = editScan.MayContainAllergens;
        return View(new RecipeEditViewModel
        {
            Id = recipe.Id,
            Code = recipe.Code,
            Name = recipe.Name,
            Category = recipe.Category,
            Department = recipe.Department,
            Unit = recipe.Unit,
            BasePortions = recipe.EffectiveBasePortions,
            StandardPortions = recipe.StandardPortions,
            WastePercent = recipe.WastePercent,
            DesiredMarginPercent = recipe.DesiredMarginPercent,
            Method = recipe.Method,
            ServingInstructions = recipe.ServingInstructions,
            ExistingImageUrl = string.IsNullOrWhiteSpace(recipe.ImageUrl) ? RecipeMediaService.GetDefaultMediaForRecipe(recipe) : recipe.ImageUrl,
            IngredientsText = ToIngredientText(recipe.Ingredients),
            AllergensText = string.Join(", ", recipe.Allergens),
            MayContainText = string.Join(", ", recipe.MayContainAllergens),
            SupplierVerificationRequired = recipe.SupplierVerificationRequired,
            EnergyKjPerPortion = recipe.EnergyKjPerPortion,
            EnergyKcalPerPortion = recipe.EnergyKcalPerPortion,
            ProteinGramsPerPortion = recipe.ProteinGramsPerPortion,
            CarbohydrateGramsPerPortion = recipe.CarbohydrateGramsPerPortion,
            SugarGramsPerPortion = recipe.SugarGramsPerPortion,
            FatGramsPerPortion = recipe.FatGramsPerPortion,
            SaturatedFatGramsPerPortion = recipe.SaturatedFatGramsPerPortion,
            FibreGramsPerPortion = recipe.FibreGramsPerPortion,
            SodiumMgPerPortion = recipe.SodiumMgPerPortion,
            NutritionVerified = recipe.NutritionVerified,
            SpecialDietsText = string.Join(", ", recipe.SpecialDiets),
            DietitianApprovalRequired = recipe.DietitianApprovalRequired,
            IsApproved = recipe.IsApproved
        });
    }


    [Authorize(Roles = "Administrator")]
    public IActionResult Allocate(string? q, string? department)
    {
        var recipes = store.Data.Recipes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q)) recipes = recipes.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(department)) recipes = recipes.Where(x => x.Department.Equals(department, StringComparison.OrdinalIgnoreCase));
        ViewBag.Query = q;
        ViewBag.Department = department;
        ViewBag.Departments = RecipeDepartments.All;
        return View(recipes.OrderBy(x => RecipeDepartments.SortOrder(x.Department)).ThenBy(x => x.Name).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
    public IActionResult AllocateRecipe(Guid id, string department, string? q, string? filterDepartment)
    {
        var recipe = store.Data.Recipes.FirstOrDefault(x => x.Id == id);
        if (recipe is null) return NotFound();
        recipe.Department = RecipeDepartments.Normalize(department);
        recipe.Version = Math.Max(1, recipe.Version + 1);
        recipe.UpdatedAtUtc = DateTime.UtcNow;
        recipe.UpdatedBy = User.Identity?.Name ?? "Unknown";
        store.UpsertRecipe(recipe);
        TempData["Success"] = $"{recipe.Name} was allocated to {recipe.Department}.";
        return RedirectToAction(nameof(Allocate), new { q, department = filterDepartment });
    }


    [HttpGet, Authorize(Roles = "Administrator")]
    public IActionResult SearchApl(string? q, string? division, int limit = 60)
    {
        var normalizedDivision = NormalizeAplDivision(division);
        var term = (q ?? string.Empty).Trim();
        limit = Math.Clamp(limit, 1, 100);

        var products = store.Data.ApprovedProducts
            .Where(x => x.IsActive && x.Division.Equals(normalizedDivision, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(term))
        {
            products = products.Where(x =>
                x.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Specification.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Brand.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.VendorName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var results = products
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.ProductCode)
            .Take(limit)
            .AsEnumerable()
            .Select(x =>
            {
                var normalized = NormalizePackCost(x.UnitOfMeasure, x.UnitCost);
                return new
                {
                    id = x.Id,
                    division = x.Division,
                    productCode = x.ProductCode,
                    productName = x.ProductName,
                    description = x.Description,
                    specification = x.Specification,
                    brand = x.Brand,
                    supplier = x.VendorName,
                    packSize = x.UnitOfMeasure,
                    unitOfMeasure = x.UnitOfMeasure,
                    purchasePrice = x.UnitCost,
                    factor = ParseAplFactor(x.BaseUnits),
                    normalizedQuantity = normalized.Quantity,
                    normalizedUnit = normalized.Unit,
                    normalizedCost = normalized.Cost,
                    normalizedCostLabel = normalized.Label
                };
            })
            .ToList();

        return Json(new { division = normalizedDivision, count = results.Count, results });
    }

    [HttpGet, Authorize(Roles = "Administrator")]
    public IActionResult LookupAplCode(string? code, string? division)
    {
        var normalizedDivision = NormalizeAplDivision(division);
        var itemCode = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(itemCode))
            return Json(new { found = false, division = normalizedDivision });

        var product = store.Data.ApprovedProducts.FirstOrDefault(x =>
            x.IsActive
            && x.Division.Equals(normalizedDivision, StringComparison.OrdinalIgnoreCase)
            && x.ProductCode.Equals(itemCode, StringComparison.OrdinalIgnoreCase));

        if (product is null)
            return Json(new { found = false, division = normalizedDivision, productCode = itemCode });

        var normalized = NormalizePackCost(product.UnitOfMeasure, product.UnitCost);
        return Json(new
        {
            found = true,
            product = new
            {
                id = product.Id,
                division = product.Division,
                productCode = product.ProductCode,
                productName = product.ProductName,
                description = product.Description,
                specification = product.Specification,
                brand = product.Brand,
                supplier = product.VendorName,
                packSize = product.UnitOfMeasure,
                unitOfMeasure = product.UnitOfMeasure,
                purchasePrice = product.UnitCost,
                factor = ParseAplFactor(product.BaseUnits),
                normalizedQuantity = normalized.Quantity,
                normalizedUnit = normalized.Unit,
                normalizedCost = normalized.Cost,
                normalizedCostLabel = normalized.Label
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
    public IActionResult Save(RecipeEditViewModel model, string? actionType)
    {
        SetLists();
        NormalizeNumericInputs(model);

        var existing = model.Id.HasValue
            ? store.Data.Recipes.FirstOrDefault(x => x.Id == model.Id.Value)
            : null;

        if (model.Id.HasValue && existing is null)
        {
            ModelState.AddModelError(string.Empty, "The recipe could not be found. Please return to the recipe list and try again.");
            return View("Edit", model);
        }

        List<IngredientLine> ingredients = new();
        try
        {
            ingredients = ParseIngredients(model.IngredientsText);
            for (var i = 0; i < ingredients.Count; i++)
            {
                if (existing is not null && i < existing.Ingredients.Count)
                {
                    var oldLine = existing.Ingredients[i];
                    if (oldLine.ProductCode.Equals(ingredients[i].ProductCode, StringComparison.OrdinalIgnoreCase))
                    {
                        ingredients[i].ApprovedProductId = oldLine.ApprovedProductId;
                        ingredients[i].AplDivision = oldLine.AplDivision;
                    }
                }
                store.LinkIngredientToApprovedProduct(ingredients[i], model.Unit);
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(model.IngredientsText), ex.Message);
        }

        var detected = allergenService.DetectDetailed(ingredients, store.Data.ApprovedProducts);
        ViewBag.AutoAllergenFindings = detected.Sources;
        ViewBag.AutoDetectedAllergens = detected.Allergens;
        ViewBag.AutoDetectedMayContain = detected.MayContainAllergens;
        if (string.Equals(actionType, "detect", StringComparison.OrdinalIgnoreCase))
        {
            model.AllergensText = string.Join(", ", SplitCsv(model.AllergensText).Concat(detected.Allergens).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x));
            model.MayContainText = string.Join(", ", SplitCsv(model.MayContainText).Concat(detected.MayContainAllergens).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x));
            model.SupplierVerificationRequired |= detected.ReviewRequired;
            SetLists();
            return View("Edit", model);
        }

        if (!ModelState.IsValid)
            return View("Edit", model);

        // Update the tracked recipe object directly. This avoids stale copies and
        // guarantees that the same recipe shown in the list is the one being saved.
        var recipe = existing ?? new Recipe { Id = model.Id ?? Guid.NewGuid(), Version = 0 };

        recipe.Code = model.Code.Trim();
        recipe.Name = model.Name.Trim();
        recipe.Category = model.Category.Trim();
        recipe.Department = RecipeDepartments.Normalize(model.Department);
        recipe.Unit = model.Unit.Trim();
        recipe.BasePortions = 10;
        recipe.StandardPortions = 10;
        recipe.WastePercent = model.WastePercent;
        recipe.DesiredMarginPercent = model.DesiredMarginPercent;
        recipe.Method = model.Method?.Trim() ?? string.Empty;
        recipe.ServingInstructions = model.ServingInstructions?.Trim() ?? string.Empty;
        recipe.Ingredients = ingredients;
        recipe.Allergens = SplitCsv(model.AllergensText);
        recipe.MayContainAllergens = SplitCsv(model.MayContainText);
        recipe.AutoDetectedAllergens = detected.Allergens;
        recipe.AutoDetectedMayContainAllergens = detected.MayContainAllergens;
        recipe.AllergenDetectionSources = detected.Sources;
        recipe.LastAllergenScanAtUtc = DateTime.UtcNow;
        recipe.SupplierVerificationRequired = model.SupplierVerificationRequired || detected.ReviewRequired;
        recipe.EnergyKjPerPortion = model.EnergyKjPerPortion;
        recipe.EnergyKcalPerPortion = model.EnergyKcalPerPortion;
        recipe.ProteinGramsPerPortion = model.ProteinGramsPerPortion;
        recipe.CarbohydrateGramsPerPortion = model.CarbohydrateGramsPerPortion;
        recipe.SugarGramsPerPortion = model.SugarGramsPerPortion;
        recipe.FatGramsPerPortion = model.FatGramsPerPortion;
        recipe.SaturatedFatGramsPerPortion = model.SaturatedFatGramsPerPortion;
        recipe.FibreGramsPerPortion = model.FibreGramsPerPortion;
        recipe.SodiumMgPerPortion = model.SodiumMgPerPortion;
        recipe.NutritionVerified = model.NutritionVerified;
        recipe.SpecialDiets = SplitCsv(model.SpecialDietsText);
        recipe.DietitianApprovalRequired = model.DietitianApprovalRequired;
        recipe.IsApproved = model.IsApproved;
        if (recipe.IsApproved)
        {
            var checks = RecipeCheckService.Evaluate(recipe, store.Data.Recipes, store.Data.ApprovedProducts);
            if (checks.CriticalCount > 0)
            {
                recipe.IsApproved = false;
                ModelState.AddModelError(string.Empty, $"Approval blocked: {checks.CriticalCount} critical recipe check(s) must be corrected first.");
                SetLists();
                model.IsApproved = false;
                return View("Edit", model);
            }
        }
        recipe.Version = Math.Max(1, recipe.Version + 1);
        recipe.UpdatedAtUtc = DateTime.UtcNow;
        recipe.UpdatedBy = User.Identity?.Name ?? "Unknown";
        recipe.HasAdministratorCorrections = true;
        recipe.LastCorrectedAtUtc = recipe.UpdatedAtUtc;
        recipe.LastCorrectedBy = recipe.UpdatedBy;

        store.UpsertRecipe(recipe);

        // Confirm the persisted record can be read back before redirecting.
        var persisted = store.Data.Recipes.FirstOrDefault(x => x.Id == recipe.Id);
        if (persisted is null)
        {
            ModelState.AddModelError(string.Empty, "The recipe could not be saved. Please try again.");
            return View("Edit", model);
        }

        TempData["Success"] = $"{persisted.Name} was updated successfully (version {persisted.Version}).";
        return RedirectToAction(nameof(Index));
    }

    private void SetLists()
    {
        ViewBag.Categories = new[] { "Beef", "Chicken", "Fish", "Pork", "Lamb & Mutton", "Vegetarian", "Salads", "Soups", "Starches", "Vegetables", "Grains", "Desserts", "Pastas", "Other" };
        ViewBag.Departments = RecipeDepartments.All;
        ViewBag.Units = store.Data.Units;
    }


    private static string NormalizeAplDivision(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (v.Contains("GAUTENG") || v.Contains("JOHANNESBURG") || v == "JHB") return "JHB";
        if (v.Contains("KWA") || v.Contains("NATAL") || v.Contains("DURBAN") || v == "KZN") return "KZN";
        return "CT";
    }

    private static (decimal? Quantity, string Unit, decimal? Cost, string Label) NormalizePackCost(string? packSize, decimal purchasePrice)
    {
        if (purchasePrice <= 0 || string.IsNullOrWhiteSpace(packSize)) return (null, string.Empty, null, string.Empty);

        var value = packSize.Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("LITRES", "L").Replace("LITRE", "L").Replace("LTR", "L").Replace("LT", "L");
        var match = Regex.Match(value, @"^(?<count>\d+(?:[.,]\d+)?)X(?<size>\d+(?:[.,]\d+)?)(?<unit>KG|G|ML|L)?(?:$|[^A-Z].*)", RegexOptions.IgnoreCase);
        if (!match.Success) return (null, string.Empty, null, string.Empty);

        if (!decimal.TryParse(match.Groups["count"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var count)
            || !decimal.TryParse(match.Groups["size"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var size)
            || count <= 0 || size <= 0)
            return (null, string.Empty, null, string.Empty);

        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        decimal quantity;
        string standardUnit;
        switch (unit)
        {
            case "G": quantity = count * size / 1000m; standardUnit = "kg"; break;
            case "KG": quantity = count * size; standardUnit = "kg"; break;
            case "ML": quantity = count * size / 1000m; standardUnit = "L"; break;
            case "L": quantity = count * size; standardUnit = "L"; break;
            default: quantity = count * size; standardUnit = "each"; break;
        }

        if (quantity <= 0) return (null, string.Empty, null, string.Empty);
        var cost = purchasePrice / quantity;
        return (quantity, standardUnit, cost, $"R {cost:N2}/{standardUnit}");
    }

    private static decimal ParseAplFactor(string? raw)
    {
        if (decimal.TryParse((raw ?? string.Empty).Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var factor) && factor > 0)
            return factor;
        return 1m;
    }

    private void LogCurrentUserActivity(string action, string entityType, Guid entityId, string entityName, string department, decimal? value, string notes)
    {
        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        store.LogActivity(new ActivityLog
        {
            UserId = userId, UserName = User.Identity?.Name ?? "Unknown", UserRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            Unit = User.FindFirstValue("unit") ?? string.Empty, Region = User.FindFirstValue("region") ?? string.Empty,
            Action = action, EntityType = entityType, EntityId = entityId, EntityName = entityName, Department = department, NumericValue = value, Notes = notes
        });
    }

    private static List<string> SplitCsv(string? value) => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    private static string ToIngredientText(IEnumerable<IngredientLine> items) => string.Join(Environment.NewLine, items.Select(x =>
        string.Join(" | ", new[]
        {
            x.ProductCode, x.Name, x.CookingNotes, x.PackSize,
            x.UnitCost.ToString(CultureInfo.InvariantCulture),
            x.EffectiveFactor.ToString(CultureInfo.InvariantCulture),
            x.Quantity.ToString(CultureInfo.InvariantCulture), x.Unit,
            x.BatchIssueOverride?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            x.WasteCostOverride?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            x.BatchCostOverride?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            x.CostPerPortionOverride?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        })));

    private static List<IngredientLine> ParseIngredients(string text)
    {
        var result = new List<IngredientLine>();
        var lineNo = 0;
        foreach (var raw in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            lineNo++;
            var p = raw.Trim().Split('|').Select(x => x.Trim()).ToArray();

            // Editable clean costing order:
            // APL Code | Product | Cooking Notes | Pack Size | Purchase Price | Factor | Standard Quantity | Unit of Measure
            // | Batch Issue override | Waste Factor override | Batch Cost override | Cost per Portion override
            // Earlier eight-column recipes remain supported; blank override values keep formulas active.
            if (p.Length < 8 || p.Length > 12 ||
                !decimal.TryParse(p[4].Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ||
                !decimal.TryParse(p[5].Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var factor) ||
                !decimal.TryParse(p[6].Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var qty))
                throw new FormatException($"Ingredient line {lineNo} must contain the clean costing columns from APL Code through Unit of Measure, with optional editable Batch Issue, Waste Factor, Batch Cost and Cost per Portion values.");

            if (factor <= 0)
                throw new FormatException($"Ingredient line {lineNo}: Factor must be greater than zero.");

            decimal? OptionalDecimal(int index)
            {
                if (index >= p.Length || string.IsNullOrWhiteSpace(p[index])) return null;
                if (!decimal.TryParse(p[index].Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException($"Ingredient line {lineNo}: calculated override column {index + 1} is not a valid number.");
                return value;
            }

            result.Add(new IngredientLine
            {
                ProductCode = p[0],
                Name = p[1],
                CookingNotes = p[2],
                PackSize = p[3],
                UnitCost = price,
                Factor = factor,
                Quantity = qty,
                Unit = p[7],
                BatchIssueOverride = OptionalDecimal(8),
                WasteCostOverride = OptionalDecimal(9),
                BatchCostOverride = OptionalDecimal(10),
                CostPerPortionOverride = OptionalDecimal(11),
                WastePercent = 0m
            });
        }

        if (result.Count == 0) throw new FormatException("Add at least one ingredient.");
        return result;
    }

    private async Task<string> SaveImage(IFormFile file)
    {
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".mp4", ".webm" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) throw new InvalidOperationException("Unsupported image or video format.");
        if (file.Length > 15 * 1024 * 1024) throw new InvalidOperationException("Media file exceeds 15 MB.");
        var uploadsPath = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);
        var safeStem = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), @"[^a-zA-Z0-9_-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(safeStem)) safeStem = "recipe-media";
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{safeStem}{ext}";
        var path = Path.Combine(uploadsPath, name);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream);
        return $"/uploads/{name}";
    }

    private void NormalizeNumericInputs(RecipeEditViewModel model)
    {
        if (TryReadDecimal(nameof(model.DesiredMarginPercent), out var margin))
        {
            model.DesiredMarginPercent = margin;
            ModelState.Remove(nameof(model.DesiredMarginPercent));
        }

        if (TryReadInt(nameof(model.StandardPortions), out var portions))
        {
            model.StandardPortions = portions;
            ModelState.Remove(nameof(model.StandardPortions));
        }

        if (TryReadInt(nameof(model.BasePortions), out var basePortions))
        {
            model.BasePortions = basePortions;
            ModelState.Remove(nameof(model.BasePortions));
        }

        if (TryReadDecimal(nameof(model.WastePercent), out var waste))
        {
            model.WastePercent = waste;
            ModelState.Remove(nameof(model.WastePercent));
        }

        foreach (var field in new[]
        {
            nameof(model.EnergyKjPerPortion), nameof(model.EnergyKcalPerPortion),
            nameof(model.ProteinGramsPerPortion), nameof(model.CarbohydrateGramsPerPortion),
            nameof(model.SugarGramsPerPortion), nameof(model.FatGramsPerPortion),
            nameof(model.SaturatedFatGramsPerPortion), nameof(model.FibreGramsPerPortion),
            nameof(model.SodiumMgPerPortion)
        })
        {
            if (!TryReadDecimal(field, out var nutritionValue)) continue;
            typeof(RecipeEditViewModel).GetProperty(field)?.SetValue(model, nutritionValue);
            ModelState.Remove(field);
        }
    }

    private bool TryReadDecimal(string key, out decimal value)
    {
        value = default;
        var raw = Request.Form[key].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        raw = raw.Trim();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(raw.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private bool TryReadInt(string key, out int value)
    {
        value = default;
        var raw = Request.Form[key].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
    }

    private static Recipe CloneRecipe(Recipe recipe) => new()
    {
        Id = recipe.Id,
        Code = recipe.Code,
        Name = recipe.Name,
        Category = recipe.Category,
        Department = recipe.Department,
        Unit = recipe.Unit,
        BasePortions = recipe.BasePortions,
        StandardPortions = recipe.StandardPortions,
        WastePercent = recipe.WastePercent,
        DesiredMarginPercent = recipe.DesiredMarginPercent,
        Method = recipe.Method,
        ServingInstructions = recipe.ServingInstructions,
        ImageUrl = recipe.ImageUrl,
        Allergens = recipe.Allergens.ToList(),
        MayContainAllergens = recipe.MayContainAllergens.ToList(),
        SupplierVerificationRequired = recipe.SupplierVerificationRequired,
        AutoDetectedAllergens = recipe.AutoDetectedAllergens.ToList(),
        AutoDetectedMayContainAllergens = recipe.AutoDetectedMayContainAllergens.ToList(),
        AllergenDetectionSources = recipe.AllergenDetectionSources.Select(x => new AllergenDetectionSource { Allergen = x.Allergen, IngredientName = x.IngredientName, DetectionType = x.DetectionType, Detail = x.Detail, IsMayContain = x.IsMayContain, RequiresVerification = x.RequiresVerification }).ToList(),
        LastAllergenScanAtUtc = recipe.LastAllergenScanAtUtc,
        EnergyKjPerPortion = recipe.EnergyKjPerPortion,
        EnergyKcalPerPortion = recipe.EnergyKcalPerPortion,
        ProteinGramsPerPortion = recipe.ProteinGramsPerPortion,
        CarbohydrateGramsPerPortion = recipe.CarbohydrateGramsPerPortion,
        SugarGramsPerPortion = recipe.SugarGramsPerPortion,
        FatGramsPerPortion = recipe.FatGramsPerPortion,
        SaturatedFatGramsPerPortion = recipe.SaturatedFatGramsPerPortion,
        FibreGramsPerPortion = recipe.FibreGramsPerPortion,
        SodiumMgPerPortion = recipe.SodiumMgPerPortion,
        NutritionVerified = recipe.NutritionVerified,
        NutritionAutoGenerated = recipe.NutritionAutoGenerated,
        NutritionGeneratedAtUtc = recipe.NutritionGeneratedAtUtc,
        NutritionCoveragePercent = recipe.NutritionCoveragePercent,
        NutritionSourceNote = recipe.NutritionSourceNote,
        SpecialDiets = recipe.SpecialDiets.ToList(),
        DietitianApprovalRequired = recipe.DietitianApprovalRequired,
        IsApproved = recipe.IsApproved,
        Version = recipe.Version,
        UpdatedAtUtc = recipe.UpdatedAtUtc,
        UpdatedBy = recipe.UpdatedBy,
        Ingredients = recipe.Ingredients.Select(i => new IngredientLine
        {
            ApprovedProductId = i.ApprovedProductId,
            AplDivision = i.AplDivision,
            ProductCode = i.ProductCode,
            Name = i.Name,
            CookingNotes = i.CookingNotes,
            PackSize = i.PackSize,
            Quantity = i.Quantity,
            Unit = i.Unit,
            UnitCost = i.UnitCost,
            Factor = i.Factor,
            WastePercent = i.WastePercent
        }).ToList()
    };
}

internal static class StringExtensions
{
    public static string OrIfBlank(this string? current, string? fallback)
        => string.IsNullOrWhiteSpace(current) ? fallback ?? string.Empty : current;
}
