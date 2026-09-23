using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Claims;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class OperationsController(JsonDataStore store, ResalePricingService resalePricing) : Controller
{
    [Authorize(Roles = "Administrator")]
    public IActionResult Ingredients()
    {
        var ingredients = store.Data.Recipes.SelectMany(r => r.Ingredients)
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.Key, Recipes = g.Count(), AverageCost = g.Average(x => x.UnitCost) })
            .OrderBy(x => x.Name).Take(100).ToList();
        ViewBag.Items = ingredients;
        return View();
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult MenuAdjustments(string? q, string? sourceIngredient, string? aplQ, string? lowerQ, string? division)
    {
        division = NormalizeDivision(division);
        var usages = store.Data.Recipes
            .SelectMany(recipe => recipe.Ingredients.Select(line => new { Recipe = recipe, Line = line }))
            .Where(x => !string.IsNullOrWhiteSpace(x.Line.Name))
            .GroupBy(x => x.Line.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new MenuIngredientUsageRow
            {
                IngredientName = group.Key,
                RecipeCount = group.Select(x => x.Recipe.Id).Distinct().Count(),
                IngredientLineCount = group.Count(),
                RecipeNames = group.Select(x => x.Recipe.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList()
            })
            .AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
            usages = usages.Where(x => x.IngredientName.Contains(q, StringComparison.OrdinalIgnoreCase));

        return View(new MenuAdjustmentViewModel
        {
            Division = division,
            Query = q ?? string.Empty,
            SourceIngredient = sourceIngredient ?? string.Empty,
            ApprovedProductQuery = aplQ ?? string.Empty,
            LowerProductQuery = lowerQ ?? string.Empty,
            Ingredients = usages.OrderByDescending(x => x.RecipeCount).ThenBy(x => x.IngredientName).ToList(),
            ApprovedProducts = store.Data.ApprovedProducts.Where(x => x.IsActive && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(aplQ)
                    || x.ProductName.Contains(aplQ, StringComparison.OrdinalIgnoreCase)
                    || x.ProductCode.Contains(aplQ, StringComparison.OrdinalIgnoreCase)
                    || x.Division.Contains(aplQ, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.ProductName).ThenBy(x => x.ProductCode).Take(500).ToList(),
            RecentReplacements = store.Data.MenuItemReplacementAudits.OrderByDescending(x => x.ReplacedAtUtc).Take(25).ToList(),
            LowerComparableReplacements = BuildLowerComparableReplacements(lowerQ, division)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
    public IActionResult ReplaceMenuItem(string sourceIngredient, Guid replacementProductId, bool confirmReplacement, string division)
    {
        division = NormalizeDivision(division);
        sourceIngredient = (sourceIngredient ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sourceIngredient))
        {
            TempData["Error"] = "Select the existing recipe item that must be replaced.";
            return RedirectToAction(nameof(MenuAdjustments));
        }
        if (!confirmReplacement)
        {
            TempData["Error"] = "Confirm the bulk replacement before continuing.";
            return RedirectToAction(nameof(MenuAdjustments), new { sourceIngredient });
        }

        var product = store.Data.ApprovedProducts.FirstOrDefault(x => x.Id == replacementProductId && x.IsActive && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            TempData["Error"] = "Select an active approved product as the replacement.";
            return RedirectToAction(nameof(MenuAdjustments), new { sourceIngredient });
        }

        var affectedRecipes = store.Data.Recipes
            .Where(r => r.Ingredients.Any(i => i.Name.Trim().Equals(sourceIngredient, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (affectedRecipes.Count == 0)
        {
            TempData["Error"] = $"No recipe ingredient named '{sourceIngredient}' was found.";
            return RedirectToAction(nameof(MenuAdjustments));
        }

        var lineCount = 0;
        var userName = User.Identity?.Name ?? "Administrator";
        foreach (var recipe in affectedRecipes)
        {
            foreach (var line in recipe.Ingredients.Where(i => i.Name.Trim().Equals(sourceIngredient, StringComparison.OrdinalIgnoreCase)))
            {
                store.SetRegionalIngredientProduct(line, division, product);
                if (line.Factor <= 0) line.Factor = 1m;
                lineCount++;
            }
            recipe.HasAdministratorCorrections = true;
            recipe.LastCorrectedAtUtc = DateTime.UtcNow;
            recipe.LastCorrectedBy = userName;
            recipe.UpdatedAtUtc = DateTime.UtcNow;
            recipe.UpdatedBy = userName;
            recipe.Version = Math.Max(1, recipe.Version + 1);
        }

        store.Data.MenuItemReplacementAudits.Add(new MenuItemReplacementAudit
        {
            SourceIngredientName = sourceIngredient,
            ReplacementProductId = product.Id,
            ReplacementProductCode = product.ProductCode,
            ReplacementProductName = product.ProductName,
            RecipesUpdated = affectedRecipes.Count,
            IngredientLinesUpdated = lineCount,
            ReplacedBy = userName
        });
        store.Save();
        store.LogActivity(new ActivityLog
        {
            UserName = userName, UserRole = "Administrator", Action = "MenuItemReplacement", EntityType = "ApprovedProduct",
            EntityId = product.Id, EntityName = product.ProductName, NumericValue = lineCount,
            Notes = $"Replaced '{sourceIngredient}' in {affectedRecipes.Count} recipes with {product.ProductCode} - {product.ProductName}."
        });

        TempData["Success"] = $"'{sourceIngredient}' was replaced with '{product.ProductName}' in {affectedRecipes.Count} recipes ({lineCount} ingredient lines).";
        return RedirectToAction(nameof(MenuAdjustments));
    }


    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
    public IActionResult MoveToLowerComparableProducts(Guid[] selectedSourceProductIds, bool confirmLowerComparable, string division)
    {
        division = NormalizeDivision(division);
        if (!confirmLowerComparable)
        {
            TempData["Error"] = "Confirm the selected lower-price replacements before continuing.";
            return RedirectToAction(nameof(MenuAdjustments));
        }

        var selected = (selectedSourceProductIds ?? Array.Empty<Guid>()).Distinct().ToHashSet();
        if (selected.Count == 0)
        {
            TempData["Error"] = "Select at least one product replacement to apply.";
            return RedirectToAction(nameof(MenuAdjustments));
        }

        var opportunities = BuildLowerComparableReplacements(null, division)
            .Where(x => selected.Contains(x.SourceProductId)).ToList();
        if (opportunities.Count == 0)
        {
            TempData["Error"] = "The selected products no longer have a valid lower-price match for the exact same ingredient.";
            return RedirectToAction(nameof(MenuAdjustments));
        }

        var userName = User.Identity?.Name ?? "Administrator";
        var totalRecipes = new HashSet<Guid>();
        var totalLines = 0;
        decimal estimatedSaving = 0m;

        foreach (var opportunity in opportunities)
        {
            var replacement = store.Data.ApprovedProducts.FirstOrDefault(x => x.Id == opportunity.ReplacementProductId && x.IsActive && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase));
            if (replacement is null) continue;

            var sourceIdentity = NormalizeIngredientIdentity(opportunity.SourceProductName);
            var affectedRecipes = store.Data.Recipes
                .Where(r => r.Ingredients.Any(i => (i.RegionalApprovedProductIds != null && i.RegionalApprovedProductIds.TryGetValue(division, out var mapped) && mapped == opportunity.SourceProductId)
                    || (!(i.RegionalApprovedProductIds?.ContainsKey(division) ?? false) && NormalizeIngredientIdentity(i.Name).Equals(sourceIdentity, StringComparison.Ordinal))))
                .ToList();
            var lineCount = 0;

            foreach (var recipe in affectedRecipes)
            {
                var recipeChanged = false;
                foreach (var line in recipe.Ingredients.Where(i => (i.RegionalApprovedProductIds != null && i.RegionalApprovedProductIds.TryGetValue(division, out var mapped) && mapped == opportunity.SourceProductId)
                    || (!(i.RegionalApprovedProductIds?.ContainsKey(division) ?? false) && NormalizeIngredientIdentity(i.Name).Equals(sourceIdentity, StringComparison.Ordinal))))
                {
                    store.SetRegionalIngredientProduct(line, division, replacement);
                    if (line.Factor <= 0) line.Factor = 1m;
                    lineCount++;
                    recipeChanged = true;
                }
                if (recipeChanged)
                {
                    totalRecipes.Add(recipe.Id);
                    recipe.HasAdministratorCorrections = true;
                    recipe.LastCorrectedAtUtc = DateTime.UtcNow;
                    recipe.LastCorrectedBy = userName;
                    recipe.UpdatedAtUtc = DateTime.UtcNow;
                    recipe.UpdatedBy = userName;
                    recipe.Version = Math.Max(1, recipe.Version + 1);
                }
            }

            if (lineCount == 0) continue;
            totalLines += lineCount;
            estimatedSaving += opportunity.SavingPerComparableUnit * lineCount;
            store.Data.MenuItemReplacementAudits.Add(new MenuItemReplacementAudit
            {
                SourceIngredientName = $"{opportunity.SourceProductName} ({opportunity.SourceProductCode})",
                ReplacementProductId = replacement.Id,
                ReplacementProductCode = replacement.ProductCode,
                ReplacementProductName = replacement.ProductName,
                RecipesUpdated = affectedRecipes.Count,
                IngredientLinesUpdated = lineCount,
                ReplacedBy = userName
            });
        }

        store.Save();
        store.LogActivity(new ActivityLog
        {
            UserName = userName, UserRole = "Administrator", Action = "SelectedLowerPriceReplacement", EntityType = "ApprovedProduct",
            EntityName = "Selected exact-ingredient lower-price replacements", NumericValue = totalLines,
            Notes = $"Moved {totalLines} ingredient lines in {totalRecipes.Count} recipes using {opportunities.Count} administrator-selected exact ingredient replacements. Indicative normalized saving: R {estimatedSaving:N2}."
        });

        TempData["Success"] = $"{totalLines} ingredient lines across {totalRecipes.Count} recipes were moved using {opportunities.Count} selected lower-price replacements.";
        return RedirectToAction(nameof(MenuAdjustments));
    }

    private List<LowerComparableReplacementRow> BuildLowerComparableReplacements(string? search = null, string division = "CT")
    {
        division = NormalizeDivision(division);
        var active = store.Data.ApprovedProducts.Where(x => x.IsActive && x.UnitCost > 0 && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase)).ToList();
        var usedProductIds = new HashSet<Guid>();
        foreach (var line in store.Data.Recipes.SelectMany(r => r.Ingredients))
        {
            if (line.RegionalApprovedProductIds != null && line.RegionalApprovedProductIds.TryGetValue(division, out var regionalId))
                usedProductIds.Add(regionalId);
            else
            {
                var identity = NormalizeIngredientIdentity(line.Name);
                var regional = active.Where(x => NormalizeIngredientIdentity(x.ProductName).Equals(identity, StringComparison.Ordinal)).OrderBy(x => x.ProductCode).FirstOrDefault();
                if (regional is not null) usedProductIds.Add(regional.Id);
            }
        }
        var rows = new List<LowerComparableReplacementRow>();

        foreach (var source in active.Where(x => usedProductIds.Contains(x.Id)))
        {
            var sourceCost = NormalizePackCost(source.UnitOfMeasure, source.UnitCost);
            if (!sourceCost.Cost.HasValue || string.IsNullOrWhiteSpace(sourceCost.Unit)) continue;

            var sourceIdentity = NormalizeIngredientIdentity(source.ProductName);
            if (string.IsNullOrWhiteSpace(sourceIdentity)) continue;

            var replacement = active
                .Where(x => x.Id != source.Id
                    && x.Division.Equals(source.Division, StringComparison.OrdinalIgnoreCase)
                    && NormalizeIngredientIdentity(x.ProductName).Equals(sourceIdentity, StringComparison.Ordinal))
                .Select(x => new { Product = x, Normalized = NormalizePackCost(x.UnitOfMeasure, x.UnitCost) })
                .Where(x => x.Normalized.Cost.HasValue
                    && x.Normalized.Unit.Equals(sourceCost.Unit, StringComparison.OrdinalIgnoreCase)
                    && x.Normalized.Cost.Value < sourceCost.Cost.Value)
                .OrderBy(x => x.Normalized.Cost)
                .FirstOrDefault();
            if (replacement is null) continue;

            if (!string.IsNullOrWhiteSpace(search)
                && !source.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !source.ProductCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !replacement.Product.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !replacement.Product.ProductCode.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var lines = store.Data.Recipes.SelectMany(r => r.Ingredients.Select(i => new { Recipe = r, Line = i }))
                .Where(x => (x.Line.RegionalApprovedProductIds != null && x.Line.RegionalApprovedProductIds.TryGetValue(division, out var mapped) && mapped == source.Id)
                    || (!(x.Line.RegionalApprovedProductIds?.ContainsKey(division) ?? false) && NormalizeIngredientIdentity(x.Line.Name).Equals(sourceIdentity, StringComparison.Ordinal)))
                .ToList();
            if (lines.Count == 0) continue;

            rows.Add(new LowerComparableReplacementRow
            {
                SourceProductId = source.Id,
                SourceProductCode = source.ProductCode,
                SourceProductName = source.ProductName,
                ReplacementProductId = replacement.Product.Id,
                ReplacementProductCode = replacement.Product.ProductCode,
                ReplacementProductName = replacement.Product.ProductName,
                ComparableUnit = sourceCost.Unit,
                CurrentComparableCost = sourceCost.Cost.Value,
                LowerComparableCost = replacement.Normalized.Cost!.Value,
                SavingPerComparableUnit = sourceCost.Cost.Value - replacement.Normalized.Cost.Value,
                RecipeCount = lines.Select(x => x.Recipe.Id).Distinct().Count(),
                IngredientLineCount = lines.Count
            });
        }
        return rows.OrderByDescending(x => x.SavingPerComparableUnit * x.IngredientLineCount).ThenBy(x => x.SourceProductName).ToList();
    }

    private static string NormalizeDivision(string? division)
    {
        var value = (division ?? "CT").Trim().ToUpperInvariant();
        return value switch { "JHB" or "GAUTENG" => "JHB", "KZN" or "KWAZULU-NATAL" => "KZN", _ => "CT" };
    }

    private static string NormalizeIngredientIdentity(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return string.Empty;
        return Regex.Replace(productName.ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
    }

    private static (decimal? Quantity, string Unit, decimal? Cost) NormalizePackCost(string? packSize, decimal purchasePrice)
    {
        if (purchasePrice <= 0 || string.IsNullOrWhiteSpace(packSize)) return (null, string.Empty, null);
        var value = packSize.Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("LITRES", "L").Replace("LITRE", "L").Replace("LTR", "L").Replace("LT", "L");
        var match = Regex.Match(value, @"^(?<count>\d+(?:[.,]\d+)?)X(?<size>\d+(?:[.,]\d+)?)(?<unit>KG|G|ML|L)?(?:$|[^A-Z].*)", RegexOptions.IgnoreCase);
        if (!match.Success) return (null, string.Empty, null);
        if (!decimal.TryParse(match.Groups["count"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var count)
            || !decimal.TryParse(match.Groups["size"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var size)
            || count <= 0 || size <= 0) return (null, string.Empty, null);
        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        decimal quantity; string standardUnit;
        switch (unit)
        {
            case "G": quantity = count * size / 1000m; standardUnit = "kg"; break;
            case "KG": quantity = count * size; standardUnit = "kg"; break;
            case "ML": quantity = count * size / 1000m; standardUnit = "L"; break;
            case "L": quantity = count * size; standardUnit = "L"; break;
            default: quantity = count * size; standardUnit = "each"; break;
        }
        return quantity > 0 ? (quantity, standardUnit, purchasePrice / quantity) : (null, string.Empty, null);
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Allergens()
    {
        ViewBag.Items = store.Data.Recipes.SelectMany(r => r.Allergens)
            .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.Key, Recipes = g.Count() })
            .OrderByDescending(x => x.Recipes).ToList();
        ViewBag.ReviewCount = store.Data.Recipes.Count(r => r.SupplierVerificationRequired);
        return View();
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Checks(string? status, string? department, string? q)
    {
        var results = store.Data.Recipes.Select(r => RecipeCheckService.Evaluate(r, store.Data.Recipes, store.Data.ApprovedProducts)).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status)) results = results.Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(department)) results = results.Where(x => x.Department.Equals(department, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q)) results = results.Where(x => x.Recipe.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(q, StringComparison.OrdinalIgnoreCase));
        ViewBag.Status = status; ViewBag.Department = department; ViewBag.Query = q; ViewBag.Departments = RecipeDepartments.All;
        return View(results.OrderByDescending(x => x.CriticalCount).ThenByDescending(x => x.WarningCount).ThenBy(x => x.Recipe).ToList());
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Units()
    {
        ViewBag.Items = store.Data.Units.Select(u => new
        {
            Name = u,
            Users = store.Data.Users.Count(x => x.Unit.Equals(u, StringComparison.OrdinalIgnoreCase)),
            Recipes = store.Data.Recipes.Count(x => x.Unit.Equals(u, StringComparison.OrdinalIgnoreCase))
        }).ToList();
        return View();
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Users(string? q, string? status, string? role)
    {
        var users = store.Data.Users.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = q.Trim();
            users = users.Where(x =>
                x.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.Unit) && x.Unit.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.UnitCode) && x.UnitCode.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            users = status.Equals("active", StringComparison.OrdinalIgnoreCase)
                ? users.Where(x => x.IsActive && x.IsApproved)
                : users.Where(x => !x.IsActive || !x.IsApproved);
        }
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
            users = users.Where(x => x.Role == parsedRole);

        ViewBag.Items = users.OrderBy(x => x.DisplayName).ThenBy(x => x.Username).ToList();
        ViewBag.Query = q ?? string.Empty;
        ViewBag.Status = status ?? string.Empty;
        ViewBag.Role = role ?? string.Empty;
        ViewBag.Roles = Enum.GetNames<UserRole>();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
    public IActionResult ResetUserPassword(Guid id)
    {
        const string temporaryPassword = "Empact@2026!";
        var user = store.Data.Users.FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            TempData["Error"] = "The selected user could not be found.";
            return RedirectToAction(nameof(Users));
        }

        var password = PasswordService.CreateHash(temporaryPassword);
        user.PasswordHash = password.Hash;
        user.Salt = password.Salt;
        user.MustChangePassword = true;
        user.IsApproved = true;
        user.IsActive = true;
        store.Save();

        var adminName = User.Identity?.Name ?? "Administrator";
        store.LogActivity(new ActivityLog
        {
            UserName = adminName,
            UserRole = "Administrator",
            Unit = user.Unit,
            Region = user.Region,
            Action = "PasswordReset",
            EntityType = "User",
            EntityId = user.Id,
            EntityName = user.Username,
            Notes = $"Password reset for {user.Username}; password change required at next login."
        });

        TempData["Success"] = $"Password reset for {user.Username}. Temporary password: {temporaryPassword}. The user must change it at next login.";
        return RedirectToAction(nameof(Users), new { q = user.Username });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
    public IActionResult ToggleUserActive(Guid id)
    {
        var user = store.Data.Users.FirstOrDefault(x => x.Id == id);
        if (user is null)
        {
            TempData["Error"] = "The selected user could not be found.";
            return RedirectToAction(nameof(Users));
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserId, out var adminId) && adminId == user.Id && user.IsActive)
        {
            TempData["Error"] = "You cannot deactivate the administrator account you are currently using.";
            return RedirectToAction(nameof(Users), new { q = user.Username });
        }

        user.IsActive = !user.IsActive;
        if (user.IsActive) user.IsApproved = true;
        store.Save();

        var adminName = User.Identity?.Name ?? "Administrator";
        store.LogActivity(new ActivityLog
        {
            UserName = adminName,
            UserRole = "Administrator",
            Unit = user.Unit,
            Region = user.Region,
            Action = user.IsActive ? "UserActivated" : "UserDeactivated",
            EntityType = "User",
            EntityId = user.Id,
            EntityName = user.Username,
            Notes = user.IsActive ? "User account activated." : "User account deactivated."
        });

        TempData["Success"] = $"{user.Username} has been {(user.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Users), new { q = user.Username });
    }

    public IActionResult Reports(DateTime? from, DateTime? to, string? unit, string? region, string? department, string? userQuery)
    {
        var isAdmin = User.IsInRole("Administrator");
        var currentUnit = User.FindFirstValue("unit") ?? string.Empty;
        if (!isAdmin) unit = currentUnit;
        var model = BuildReports(from, to, unit, region, department, userQuery);
        model.IsAdministrator = isAdmin;
        model.CurrentUnit = currentUnit;
        model.TodayUpload = store.Data.DailyTieBackUploads
            .Where(x => x.BusinessDate.Date == DateTime.Today && TieBackUnitMatcher.Matches(x.Unit, currentUnit, User.FindFirstValue("unitCode")))
            .OrderByDescending(x => x.UploadedAtUtc).FirstOrDefault();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UploadTieBack(DateTime businessDate, IFormFile tieBackFile)
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = Guid.TryParse(idText, out var userId) ? store.Data.Users.FirstOrDefault(x => x.Id == userId) : null;
        if (user is null) return Forbid();
        if (tieBackFile is null || tieBackFile.Length == 0) { TempData["Error"] = "Please select a daily tie-back sheet."; return RedirectToAction(nameof(Reports), new { unit=user.Unit }); }
        if (tieBackFile.Length > 25_000_000) { TempData["Error"] = "The file is larger than 25 MB."; return RedirectToAction(nameof(Reports), new { unit=user.Unit }); }
        var allowed = new[] { ".pdf", ".xlsx", ".xls", ".xlsm", ".csv" };
        var extension = Path.GetExtension(tieBackFile.FileName).ToLowerInvariant();
        if (!allowed.Contains(extension)) { TempData["Error"] = "Upload a PDF, Excel or CSV tie-back sheet."; return RedirectToAction(nameof(Reports), new { unit=user.Unit }); }
        var date = businessDate == default ? DateTime.Today : businessDate.Date;
        if (date > DateTime.Today) { TempData["Error"] = "The tie-back date cannot be in the future."; return RedirectToAction(nameof(Reports), new { unit=user.Unit }); }
        var safeUnit = string.Concat(user.Unit.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var stored = $"{date:yyyyMMdd}-{safeUnit}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(store.GetTieBackUploadFolder(), stored);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await tieBackFile.CopyToAsync(stream);
        var gp = TieBackGpExtractor.Extract(path, date);
        var upload = new DailyTieBackUpload
        {
            BusinessDate=date, Unit=user.Unit, Region=user.Region, OriginalFileName=Path.GetFileName(tieBackFile.FileName), StoredFileName=stored, ContentType=tieBackFile.ContentType ?? "application/octet-stream", FileSizeBytes=tieBackFile.Length, UploadedAtUtc=DateTime.UtcNow, UploadedByUserId=user.Id, UploadedBy=user.DisplayName,
            PlannedGpPercent=gp.PlannedGpPercent, PlannedGpRand=gp.PlannedGpRand, ActualGpPercent=gp.ActualGpPercent, ActualGpRand=gp.ActualGpRand, GpMatchedBusinessDate=gp.MatchedBusinessDate
        };
        store.AddDailyTieBack(upload);
        store.LogActivity(new ActivityLog { UserId=user.Id, UserName=user.DisplayName, UserRole=user.Role.ToString(), Unit=user.Unit, Region=user.Region, Action="TieBackUpload", EntityType="DailyTieBack", EntityId=upload.Id, EntityName=upload.OriginalFileName, Notes=date.ToString("yyyy-MM-dd") });
        TempData["Success"] = $"Daily tie-back sheet uploaded for {date:dd MMM yyyy}.";
        return RedirectToAction(nameof(Reports), new { unit=user.Unit, from=date.ToString("yyyy-MM-dd"), to=date.ToString("yyyy-MM-dd") });
    }

    [HttpGet]
    public IActionResult DownloadTieBack(Guid id)
    {
        var upload = store.Data.DailyTieBackUploads.FirstOrDefault(x => x.Id == id);
        if (upload is null) return NotFound();
        var currentUnit = User.FindFirstValue("unit") ?? string.Empty;
        if (!User.IsInRole("Administrator") && !TieBackUnitMatcher.Matches(upload.Unit, currentUnit, User.FindFirstValue("unitCode"))) return Forbid();
        var path = Path.Combine(store.GetTieBackUploadFolder(), upload.StoredFileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType, upload.OriginalFileName);
    }

    [HttpGet, Authorize(Roles = "Administrator")]
    public IActionResult ExportReport(string report, DateTime? from, DateTime? to, string? unit, string? region, string? department, string? userQuery)
    {
        var model = BuildReports(from, to, unit, region, department, userQuery);
        var csv = new StringBuilder();
        switch ((report ?? string.Empty).ToLowerInvariant())
        {
            case "activity":
                csv.AppendLine("User,Role,Unit,Region,Last Login,Last Activity,Logins,Recipe Views,Margin Updates,Promotion Actions");
                foreach (var x in model.UserActivity) csv.AppendLine(Csv(x.User,x.Role,x.Unit,x.Region,Date(x.LastLoginUtc),Date(x.LastActivityUtc),x.Logins,x.RecipeViews,x.MarginUpdates,x.PromotionActions));
                break;
            case "apl":
                csv.AppendLine("Recipe,Code,Department,Ingredient Lines,APL Linked,Missing Codes,Compliance %");
                foreach (var x in model.AplCompliance) csv.AppendLine(Csv(x.Recipe,x.Code,x.Department,x.IngredientLines,x.LinkedLines,x.MissingCodes,x.CompliancePercent));
                break;
            case "gp":
                csv.AppendLine("Unit,Recipe,Department,Cost per Portion,Desired GP %,Required Selling Price,Updated,Updated By");
                foreach (var x in model.GpMargins) csv.AppendLine(Csv(x.Unit,x.Recipe,x.Department,x.CostPerPortion,x.DesiredMarginPercent,x.RequiredSellingPrice,x.UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),x.UpdatedBy));
                break;
            case "promotions":
                csv.AppendLine("Promotion,Unit,Region,Views,Downloads,Prints,Last Action,Status");
                foreach (var x in model.PromotionCompliance) csv.AppendLine(Csv(x.Promotion,x.Unit,x.Region,x.Views,x.Downloads,x.Prints,Date(x.LastActionUtc),x.Status));
                break;
            case "tieback":
                csv.AppendLine("Date,Unit,Region,Status,Uploaded At,Uploaded By,Planned GP %,Planned GP R,Actual GP %,Actual GP R,File");
                foreach (var x in model.DailyTieBacks) csv.AppendLine(Csv(x.BusinessDate.ToString("yyyy-MM-dd"),x.Unit,x.Region,x.Status,Date(x.UploadedAtUtc),x.UploadedBy,x.PlannedGpPercent,x.PlannedGpRand,x.ActualGpPercent,x.ActualGpRand,x.FileName));
                break;
            default:
                csv.AppendLine("Unit,Region,Active Users,Recipe Views,Recipes Used,Margins Set,Promotion Actions,Compliance Score,Status");
                foreach (var x in model.UnitCompliance) csv.AppendLine(Csv(x.Unit,x.Region,x.ActiveUsers,x.RecipeViews,x.RecipesUsed,x.MarginsSet,x.PromotionActions,x.ComplianceScore,x.Status));
                break;
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"{report}-report-{DateTime.Today:yyyyMMdd}.csv");
    }

    private ReportsViewModel BuildReports(DateTime? from, DateTime? to, string? unit, string? region, string? department, string? userQuery)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
        var logs = store.Data.ActivityLogs.Where(x => x.OccurredAtUtc >= fromDate.ToUniversalTime() && x.OccurredAtUtc <= toDate.ToUniversalTime()).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(unit)) logs = logs.Where(x => x.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(region)) logs = logs.Where(x => x.Region.Equals(region, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(department)) logs = logs.Where(x => x.Department.Equals(department, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(userQuery)) logs = logs.Where(x => x.UserName.Contains(userQuery, StringComparison.OrdinalIgnoreCase));
        var activity = logs.ToList();

        var users = store.Data.Users.Where(x => x.IsActive).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(unit)) users = users.Where(x => x.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(region)) users = users.Where(x => x.Region.Equals(region, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(userQuery)) users = users.Where(x => x.DisplayName.Contains(userQuery, StringComparison.OrdinalIgnoreCase) || x.Username.Contains(userQuery, StringComparison.OrdinalIgnoreCase));

        var unitRows = users.GroupBy(x => new { x.Unit, x.Region }).Select(g =>
        {
            var ul = activity.Where(a => a.Unit.Equals(g.Key.Unit, StringComparison.OrdinalIgnoreCase)).ToList();
            var recipeViews = ul.Count(a => a.Action == "RecipeView"); var recipesUsed = ul.Where(a => a.Action == "RecipeView").Select(a => a.EntityId).Distinct().Count();
            var margins = ul.Count(a => a.Action == "MarginUpdate"); var promos = ul.Count(a => a.Action.StartsWith("Promotion", StringComparison.Ordinal));
            var active = ul.Select(a => a.UserId).Where(x => x.HasValue).Distinct().Count();
            var score = Math.Min(100, (active > 0 ? 20 : 0) + (recipeViews > 0 ? 25 : 0) + (recipesUsed >= 3 ? 20 : recipesUsed * 6) + (margins > 0 ? 20 : 0) + (promos > 0 ? 15 : 0));
            return new UnitComplianceRow { Unit=g.Key.Unit, Region=g.Key.Region, ActiveUsers=active, RecipeViews=recipeViews, RecipesUsed=recipesUsed, MarginsSet=margins, PromotionActions=promos, ComplianceScore=score };
        }).OrderBy(x => x.ComplianceScore).ThenBy(x => x.Unit).ToList();

        var userRows = users.Select(u => { var l=activity.Where(a => a.UserId==u.Id).ToList(); return new UserActivityRow { User=u.DisplayName, Role=u.Role.ToString(), Unit=u.Unit, Region=u.Region, LastLoginUtc=l.Where(a=>a.Action=="Login").MaxBy(a=>a.OccurredAtUtc)?.OccurredAtUtc, LastActivityUtc=l.MaxBy(a=>a.OccurredAtUtc)?.OccurredAtUtc, Logins=l.Count(a=>a.Action=="Login"), RecipeViews=l.Count(a=>a.Action=="RecipeView"), MarginUpdates=l.Count(a=>a.Action=="MarginUpdate"), PromotionActions=l.Count(a=>a.Action.StartsWith("Promotion",StringComparison.Ordinal)) }; }).OrderByDescending(x=>x.LastActivityUtc).ToList();

        var recipes = store.Data.Recipes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(department)) recipes=recipes.Where(x=>x.Department.Equals(department,StringComparison.OrdinalIgnoreCase));
        var apl = recipes.Select(r => { var total=r.Ingredients.Count; var linked=r.Ingredients.Count(i=>i.IsAplLinked); return new AplComplianceRow { Recipe=r.Name, Code=r.Code, Department=r.Department, IngredientLines=total, LinkedLines=linked, MissingCodes=r.Ingredients.Count(i=>string.IsNullOrWhiteSpace(i.ProductCode)), CompliancePercent=total==0?0:Math.Round(linked*100m/total,1) }; }).OrderBy(x=>x.CompliancePercent).ThenBy(x=>x.Recipe).ToList();

        var marginRows = (from m in store.Data.UnitRecipeMargins join r in store.Data.Recipes on m.RecipeId equals r.Id where m.UpdatedAtUtc >= fromDate.ToUniversalTime() && m.UpdatedAtUtc <= toDate.ToUniversalTime() select new GpMarginReportRow { Unit=m.UnitKey, Recipe=r.Name, Department=r.Department, CostPerPortion=r.CostPerPortion, DesiredMarginPercent=m.DesiredMarginPercent, RequiredSellingPrice=m.DesiredMarginPercent>=100?0:r.CostPerPortion/(1-m.DesiredMarginPercent/100m), UpdatedAtUtc=m.UpdatedAtUtc, UpdatedBy=m.UpdatedBy }).ToList();
        if (!string.IsNullOrWhiteSpace(unit)) marginRows=marginRows.Where(x=>x.Unit.Equals(unit,StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(department)) marginRows=marginRows.Where(x=>x.Department.Equals(department,StringComparison.OrdinalIgnoreCase)).ToList();

        var promotionUnits = users.GroupBy(x => new { x.Unit, x.Region }).Select(x => x.Key).ToList();
        var promotionRows = (from promotion in store.Data.Promotions.Where(x => x.IsPublished)
                             from u in promotionUnits
                             let actions = activity.Where(a => a.EntityType == "Promotion" && a.EntityId == promotion.Id && a.Unit.Equals(u.Unit, StringComparison.OrdinalIgnoreCase)).ToList()
                             select new PromotionComplianceRow
                             {
                                 Promotion = promotion.Title, Unit = u.Unit, Region = u.Region,
                                 Views = actions.Count(x => x.Action == "PromotionView"),
                                 Downloads = actions.Count(x => x.Action == "PromotionDownload"),
                                 Prints = actions.Count(x => x.Action == "PromotionPrint"),
                                 LastActionUtc = actions.Count == 0 ? null : actions.Max(x => x.OccurredAtUtc)
                             }).OrderBy(x => x.Status).ThenBy(x => x.Unit).ToList();

        var reportingUnits = users.Where(x => x.Role is UserRole.UnitManager or UserRole.UnitUser).GroupBy(x => new { x.Unit, x.Region, x.UnitCode }).Select(g => g.Key).ToList();
        var tieBackRows = new List<DailyTieBackReportRow>();
        for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
        {
            foreach (var u in reportingUnits)
            {
                var upload = store.Data.DailyTieBackUploads
                    .Where(x => x.BusinessDate.Date == day && TieBackUnitMatcher.Matches(x.Unit, u.Unit, u.UnitCode))
                    .OrderByDescending(x => x.UploadedAtUtc).FirstOrDefault();
                tieBackRows.Add(new DailyTieBackReportRow
                {
                    BusinessDate=day, Unit=u.Unit, Region=u.Region, UploadId=upload?.Id, FileName=upload?.OriginalFileName ?? string.Empty, UploadedAtUtc=upload?.UploadedAtUtc, UploadedBy=upload?.UploadedBy ?? string.Empty,
                    PlannedGpPercent=upload?.PlannedGpPercent, PlannedGpRand=upload?.PlannedGpRand, ActualGpPercent=upload?.ActualGpPercent, ActualGpRand=upload?.ActualGpRand
                });
            }
        }
        var missingToday = DateTime.Now.TimeOfDay >= new TimeSpan(9,0,0) ? tieBackRows.Count(x => x.BusinessDate.Date == DateTime.Today && !x.IsUploaded) : 0;

        return new ReportsViewModel { From=fromDate, To=toDate.Date, Unit=unit??"", Region=region??"", Department=department??"", UserQuery=userQuery??"", Units=store.Data.Users.Select(x=>x.Unit).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList(), Regions=store.Data.Users.Select(x=>x.Region).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x).ToList(), Departments=RecipeDepartments.All, UnitCompliance=unitRows, UserActivity=userRows, AplCompliance=apl, GpMargins=marginRows.OrderByDescending(x=>x.UpdatedAtUtc).ToList(), PromotionCompliance=promotionRows, DailyTieBacks=tieBackRows.OrderByDescending(x=>x.BusinessDate).ThenBy(x=>x.Unit).ToList(), TodayMissingAfterDeadline=missingToday };
    }

    private static string Csv(params object?[] values) => string.Join(",", values.Select(v => $"\"{(v?.ToString() ?? string.Empty).Replace("\"", "\"\"")}\""));
    private static string Date(DateTime? value) => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;


    [Authorize(Roles = "Administrator")]
    public IActionResult ComplianceDashboard(string? region, string? unitCode, DateTime? from, DateTime? to)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        var allUnits = resalePricing.GetAllUnits()
            .Where(x => !x.Status.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
            .Select(x => new ComplianceUnitOption
            {
                Code = x.Code.Trim(), Name = x.Name.Trim(), Region = x.Region.Trim(),
                Category = x.Category.Trim().ToUpperInvariant()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Select(x => x.First())
            .OrderBy(x => x.Region).ThenBy(x => x.Name).ToList();

        var regions = allUnits.Select(x => x.Region).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var selectedRegion = regions.FirstOrDefault(x => x.Equals(region ?? string.Empty, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var unitOptions = allUnits.Where(x => string.IsNullOrWhiteSpace(selectedRegion) || x.Region.Equals(selectedRegion, StringComparison.OrdinalIgnoreCase)).ToList();
        var selectedUnit = unitOptions.FirstOrDefault(x => x.Code.Equals(unitCode ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(unitCode) && selectedUnit is null) unitCode = null;

        var scoped = selectedUnit is not null ? new List<ComplianceUnitOption> { selectedUnit } : unitOptions;
        var rows = BuildComplianceRows(scoped, fromDate, toDate);
        var moduleScores = BuildModuleScores(rows);
        var overall = rows.Count == 0 ? 0 : (int)Math.Round(rows.Average(x => x.Overall));

        var regionScores = allUnits.GroupBy(x => x.Region, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var rr = BuildComplianceRows(g.ToList(), fromDate, toDate);
                return new ComplianceRegionScore { Region = string.IsNullOrWhiteSpace(g.Key) ? "Unallocated" : g.Key, Units = rr.Count, Score = rr.Count == 0 ? 0 : (int)Math.Round(rr.Average(x => x.Overall)) };
            }).OrderByDescending(x => x.Score).ThenBy(x => x.Region).ToList();

        var trend = new List<ComplianceTrendPoint>();
        var month = new DateTime(toDate.Year, toDate.Month, 1).AddMonths(-5);
        for (var i = 0; i < 6; i++)
        {
            var start = month.AddMonths(i);
            var end = start.AddMonths(1).AddDays(-1);
            if (end > DateTime.Today) end = DateTime.Today;
            var tr = BuildComplianceRows(scoped, start, end);
            trend.Add(new ComplianceTrendPoint { Label = start.ToString("MMM yyyy"), Score = tr.Count == 0 ? 0 : (int)Math.Round(tr.Average(x => x.Overall)) });
        }

        var relevantUnitCodes = scoped.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relevantNames = scoped.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rangeStartUtc = fromDate.ToUniversalTime();
        var rangeEndUtc = toDate.AddDays(1).AddTicks(-1).ToUniversalTime();
        var lastActivity = store.Data.ActivityLogs
            .Where(x => x.OccurredAtUtc >= rangeStartUtc && x.OccurredAtUtc <= rangeEndUtc &&
                (relevantUnitCodes.Contains(x.Unit) || relevantNames.Contains(x.Unit)))
            .OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault()?.OccurredAtUtc;

        var scopeLabel = selectedUnit is not null ? $"{selectedUnit.Code} · {selectedUnit.Name}"
            : !string.IsNullOrWhiteSpace(selectedRegion) ? $"{selectedRegion} region" : "All units";

        var model = new ComplianceDashboardViewModel
        {
            Region = selectedRegion, UnitCode = selectedUnit?.Code ?? string.Empty, From = fromDate, To = toDate,
            Regions = regions, Units = unitOptions, UnitRows = rows.OrderBy(x => x.Overall).ThenBy(x => x.UnitName).ToList(),
            ModuleScores = moduleScores, RegionScores = regionScores, Trend = trend, OverallCompliance = overall,
            CompliantUnits = rows.Count(x => x.Overall >= 90), AttentionUnits = rows.Count(x => x.Overall >= 70 && x.Overall < 90),
            OutstandingUnits = rows.Count(x => x.Overall < 70), LastActivityUtc = lastActivity, ScopeLabel = scopeLabel
        };
        return View(model);
    }

    private List<ComplianceUnitRow> BuildComplianceRows(IReadOnlyCollection<ComplianceUnitOption> units, DateTime fromDate, DateTime toDate)
    {
        var startUtc = fromDate.ToUniversalTime();
        var endUtc = toDate.AddDays(1).AddTicks(-1).ToUniversalTime();
        var logs = store.Data.ActivityLogs.Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc <= endUtc).ToList();
        var rows = new List<ComplianceUnitRow>();
        foreach (var unit in units)
        {
            var unitLogs = logs.Where(x => x.Unit.Equals(unit.Code, StringComparison.OrdinalIgnoreCase) || x.Unit.Equals(unit.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var appUsers = store.Data.Users.Where(x => x.UnitCode.Equals(unit.Code, StringComparison.OrdinalIgnoreCase) || x.Unit.Equals(unit.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (appUsers.Count > 0)
            {
                var ids = appUsers.Select(x => x.Id).ToHashSet();
                unitLogs.AddRange(logs.Where(x => x.UserId.HasValue && ids.Contains(x.UserId.Value) && !unitLogs.Any(u => u.Id == x.Id)));
            }

            var resale = unitLogs.Any(x => x.Action == "ResalePriceListViewed") ? 100 : 0;
            var hasRecipeView = unitLogs.Any(x => x.Action == "RecipeView");
            var hasMargin = unitLogs.Any(x => x.Action == "MarginUpdate");
            var recipeCosting = hasRecipeView && hasMargin ? 100 : hasRecipeView || hasMargin ? 50 : 0;

            var workDays = Enumerable.Range(0, (toDate.Date - fromDate.Date).Days + 1).Select(i => fromDate.Date.AddDays(i))
                .Where(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).ToList();
            var uploadedDays = store.Data.DailyTieBackUploads.Where(x => x.BusinessDate.Date >= fromDate.Date && x.BusinessDate.Date <= toDate.Date &&
                TieBackUnitMatcher.Matches(x.Unit, unit.Code, unit.Name))
                .Select(x => x.BusinessDate.Date).Distinct().Count();
            var tieBack = workDays.Count == 0 ? 0 : Math.Min(100, (int)Math.Round(uploadedDays * 100m / workDays.Count));

            var foodWasteSubmittedDays = store.Data.FoodWasteRecords.Where(x => !x.IsDraft && x.BusinessDate.Date >= fromDate.Date && x.BusinessDate.Date <= toDate.Date &&
                (x.UnitCode.Equals(unit.Code, StringComparison.OrdinalIgnoreCase) || x.UnitName.Equals(unit.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.BusinessDate.Date).Distinct().Count();
            var dailyWasteScore = workDays.Count == 0 ? 0 : Math.Min(100, (int)Math.Round(foodWasteSubmittedDays * 100m / workDays.Count));
            var wasteProfile = store.Data.UnitFoodWasteProfiles.FirstOrDefault(x => x.UnitCode.Equals(unit.Code, StringComparison.OrdinalIgnoreCase));
            var profileScore = wasteProfile is not null && !string.IsNullOrWhiteSpace(wasteProfile.ChampionName) && wasteProfile.TrainingCompletedDate.HasValue ? 100 : wasteProfile is not null && !string.IsNullOrWhiteSpace(wasteProfile.ChampionName) ? 50 : 0;
            var currentMonthPlan = store.Data.FoodWasteReductionPlans.Where(x => x.UnitCode.Equals(unit.Code, StringComparison.OrdinalIgnoreCase) && x.ReviewMonth.Year == toDate.Year && x.ReviewMonth.Month == toDate.Month).OrderByDescending(x=>x.UpdatedAtUtc).FirstOrDefault();
            var planScore = currentMonthPlan is null ? 0 : currentMonthPlan.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase) ? 100 : Math.Max(50, currentMonthPlan.ProgressPercent);
            var foodWaste = (int)Math.Round(dailyWasteScore * 0.70m + profileScore * 0.15m + planScore * 0.15m);

            var activePromotions = store.Data.Promotions.Where(x => x.IsPublished && x.EndDate.Date >= fromDate.Date && x.StartDate.Date <= toDate.Date).ToList();
            var promotions = activePromotions.Count == 0 ? 100 : (int)Math.Round(activePromotions.Count(p => unitLogs.Any(x => x.EntityType == "Promotion" && x.EntityId == p.Id)) * 100m / activePromotions.Count);

            var scores = new Dictionary<string,int>
            {
                ["Resale pricing"] = resale, ["Recipe costing"] = recipeCosting,
                ["Reports & tie-back"] = tieBack, ["Food waste"] = foodWaste, ["Promotions"] = promotions
            };
            var overall = (int)Math.Round(scores.Values.Average());
            rows.Add(new ComplianceUnitRow
            {
                UnitCode = unit.Code, UnitName = unit.Name, Region = unit.Region, Category = unit.Category,
                ResalePricing = resale, RecipeCosting = recipeCosting, ReportsTieBack = tieBack, FoodWaste = foodWaste,
                Promotions = promotions, Overall = overall,
                LastActivityUtc = unitLogs.OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault()?.OccurredAtUtc,
                HighestOutstandingModule = scores.OrderBy(x => x.Value).ThenBy(x => x.Key).First().Key
            });
        }
        return rows;
    }

    private static List<ComplianceModuleScore> BuildModuleScores(IReadOnlyCollection<ComplianceUnitRow> rows)
    {
        int Avg(Func<ComplianceUnitRow,int> pick) => rows.Count == 0 ? 0 : (int)Math.Round(rows.Average(x => pick(x)));
        return new List<ComplianceModuleScore>
        {
            new() { Name="Resale pricing", Score=Avg(x=>x.ResalePricing) },
            new() { Name="Recipe costing", Score=Avg(x=>x.RecipeCosting) },
            new() { Name="Reports & tie-back", Score=Avg(x=>x.ReportsTieBack) },
            new() { Name="Food waste", Score=Avg(x=>x.FoodWaste) },
            new() { Name="Promotions", Score=Avg(x=>x.Promotions) }
        };
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Settings() => View();
}
