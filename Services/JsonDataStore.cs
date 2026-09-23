using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using EmpactRecipeOnline.Models;

namespace EmpactRecipeOnline.Services;

public sealed class JsonDataStore
{
    private readonly object _gate = new();
    private string _path;
    private readonly string _seedPath;
    private readonly string _aplSeedPath;
    private readonly string _unitSeedPath;
    private readonly string _webRootPath;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public AppData Data { get; private set; }

    public JsonDataStore(IWebHostEnvironment env)
    {
        var contentDataFolder = Path.Combine(env.ContentRootPath, "App_Data");
        _seedPath = Path.Combine(contentDataFolder, "recipe-seed.json");
        _aplSeedPath = Path.Combine(contentDataFolder, "apl-seed.json");
        _unitSeedPath = Path.Combine(contentDataFolder, "unit-seed.json");
        _path = ResolveWritableDataPath(contentDataFolder);
        _webRootPath = env.WebRootPath;
        Data = LoadOrSeed();
        // Merge new workbook seed recipes without replacing administrator-maintained recipes.
        MergeWorkbookRecipes();
        // Existing recipes are preserved during application upgrades and restarts.
        // Recipe removal must only occur through an explicit administrator action.
        foreach (var user in Data.Users) if (string.IsNullOrWhiteSpace(user.Username)) user.Username = user.Email;
        Data.Promotions ??= new List<Promotion>();
        Data.UnitRecipeMargins ??= new List<UnitRecipeMargin>();
        Data.ActivityLogs ??= new List<ActivityLog>();
        Data.DailyTieBackUploads ??= new List<DailyTieBackUpload>();
        Data.MenuItemReplacementAudits ??= new List<MenuItemReplacementAudit>();
        Data.ResaleMonthlyPrices ??= new List<ResaleMonthlyPriceRecord>();
        Data.ResaleMonthLocks ??= new List<ResaleMonthLockRecord>();
        Data.FoodWasteRecords ??= new List<FoodWasteRecord>();
        Data.NutritionProfiles ??= new List<NutritionProfile>();
        Data.UserFavourites ??= new List<UserFavourite>();
        Data.FoodWasteReductionPlans ??= new List<FoodWasteReductionPlan>();
        Data.UnitFoodWasteProfiles ??= new List<UnitFoodWasteProfile>();
        Data.FoodWasteSupportDocuments ??= new List<FoodWasteSupportDocument>();
        StandardizeRecipePortionsToTen();
        EnsureMandelaDayPromotion();
        MergeApprovedProductSeed();
        MergeUnitAccounts();
        EnsureAdministratorCapeTownApl();
        AutoLinkRecipeIngredients();
        EnsureApprovedProductList();
        BackfillRecipeDepartments();
        BackfillRecipeMedia();
    }


    private void StandardizeRecipePortionsToTen()
    {
        var changed = false;
        foreach (var recipe in Data.Recipes)
        {
            if (recipe.StandardPortions == 10 && recipe.BasePortions == 10) continue;
            var oldBase = recipe.BasePortions <= 0 ? Math.Max(1, recipe.StandardPortions) : recipe.BasePortions;
            var scale = 10m / Math.Max(1, oldBase);
            foreach (var line in recipe.Ingredients)
            {
                line.Quantity *= scale;
                if (line.BatchIssueOverride.HasValue) line.BatchIssueOverride *= scale;
                if (line.WasteCostOverride.HasValue) line.WasteCostOverride *= scale;
                if (line.BatchCostOverride.HasValue) line.BatchCostOverride *= scale;
                // CostPerPortionOverride is already a per-portion value and is intentionally not scaled.
            }
            recipe.StandardPortions = 10;
            recipe.BasePortions = 10;
            changed = true;
        }
        if (changed) WriteDataUnsafe();
    }

    public void Save()
    {
        lock (_gate)
        {
            WriteDataUnsafe();
        }
    }

    public decimal GetDesiredMargin(Guid recipeId, Guid userId, decimal recipeDefault)
    {
        lock (_gate)
        {
            var user = Data.Users.FirstOrDefault(x => x.Id == userId);
            if (user is null) return recipeDefault;
            var unitKey = GetMarginUnitKey(user);
            return Data.UnitRecipeMargins
                .FirstOrDefault(x => x.RecipeId == recipeId && x.UnitKey.Equals(unitKey, StringComparison.OrdinalIgnoreCase))
                ?.DesiredMarginPercent ?? recipeDefault;
        }
    }

    public void UpsertDesiredMargin(Guid recipeId, Guid userId, decimal desiredMarginPercent, string updatedBy)
    {
        lock (_gate)
        {
            var user = Data.Users.FirstOrDefault(x => x.Id == userId)
                ?? throw new InvalidOperationException("The signed-in user could not be found.");
            var unitKey = GetMarginUnitKey(user);
            var existing = Data.UnitRecipeMargins.FirstOrDefault(x =>
                x.RecipeId == recipeId && x.UnitKey.Equals(unitKey, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Data.UnitRecipeMargins.Add(new UnitRecipeMargin
                {
                    RecipeId = recipeId,
                    UnitKey = unitKey,
                    DesiredMarginPercent = desiredMarginPercent,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = updatedBy
                });
            }
            else
            {
                existing.DesiredMarginPercent = desiredMarginPercent;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.UpdatedBy = updatedBy;
            }
            WriteDataUnsafe();
        }
    }

    private static string GetMarginUnitKey(AppUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.UnitCode)) return user.UnitCode.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(user.Unit)) return user.Unit.Trim().ToUpperInvariant();
        return user.Username.Trim().ToUpperInvariant();
    }

    public bool ToggleFavourite(Guid userId, string entityType, Guid? entityId, string name, string url)
    {
        lock (_gate)
        {
            Data.UserFavourites ??= new List<UserFavourite>();
            var existing = Data.UserFavourites.FirstOrDefault(x => x.UserId == userId &&
                x.EntityType.Equals(entityType ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                x.EntityId == entityId && x.Url.Equals(url ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                Data.UserFavourites.Remove(existing);
                WriteDataUnsafe();
                return false;
            }
            Data.UserFavourites.Add(new UserFavourite { UserId=userId, EntityType=entityType ?? string.Empty, EntityId=entityId, Name=name ?? string.Empty, Url=url ?? string.Empty });
            WriteDataUnsafe();
            return true;
        }
    }

    public void UpsertFoodWasteProfile(UnitFoodWasteProfile profile)
    {
        lock (_gate)
        {
            Data.UnitFoodWasteProfiles ??= new List<UnitFoodWasteProfile>();
            var existing = Data.UnitFoodWasteProfiles.FirstOrDefault(x => x.UnitCode.Equals(profile.UnitCode, StringComparison.OrdinalIgnoreCase));
            if (existing is null) Data.UnitFoodWasteProfiles.Add(profile);
            else
            {
                existing.UnitName=profile.UnitName; existing.Region=profile.Region; existing.ChampionName=profile.ChampionName;
                existing.ChampionEmail=profile.ChampionEmail; existing.BackupName=profile.BackupName; existing.TrainingCompletedDate=profile.TrainingCompletedDate;
                existing.RefresherDueDate=profile.RefresherDueDate; existing.TrainingEvidenceNotes=profile.TrainingEvidenceNotes; existing.UpdatedAtUtc=profile.UpdatedAtUtc; existing.UpdatedBy=profile.UpdatedBy;
            }
            WriteDataUnsafe();
        }
    }

    public void UpsertFoodWasteReductionPlan(FoodWasteReductionPlan plan)
    {
        lock (_gate)
        {
            Data.FoodWasteReductionPlans ??= new List<FoodWasteReductionPlan>();
            var existing = Data.FoodWasteReductionPlans.FirstOrDefault(x => x.Id == plan.Id);
            if (existing is null) Data.FoodWasteReductionPlans.Add(plan);
            else
            {
                existing.Issue=plan.Issue; existing.RootCause=plan.RootCause; existing.Action=plan.Action; existing.Owner=plan.Owner;
                existing.DueDate=plan.DueDate; existing.Status=plan.Status; existing.ProgressPercent=plan.ProgressPercent; existing.EvidenceNotes=plan.EvidenceNotes;
                existing.ReviewMonth=plan.ReviewMonth; existing.UpdatedAtUtc=plan.UpdatedAtUtc; existing.UpdatedBy=plan.UpdatedBy;
            }
            WriteDataUnsafe();
        }
    }

    public string GetFoodWasteUploadFolder()
    {
        var baseFolder = Path.GetDirectoryName(_path)!;
        var folder = Path.Combine(baseFolder, "FoodWasteUploads");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string GetTieBackUploadFolder()
    {
        var baseFolder = Path.GetDirectoryName(_path)!;
        var folder = Path.Combine(baseFolder, "TieBackUploads");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public void AddDailyTieBack(DailyTieBackUpload upload)
    {
        lock (_gate)
        {
            Data.DailyTieBackUploads ??= new List<DailyTieBackUpload>();
            var existing = Data.DailyTieBackUploads.FirstOrDefault(x =>
                x.BusinessDate.Date == upload.BusinessDate.Date &&
                TieBackUnitMatcher.Matches(x.Unit, upload.Unit));
            if (existing is not null) Data.DailyTieBackUploads.Remove(existing);
            Data.DailyTieBackUploads.Add(upload);
            WriteDataUnsafe();
        }
    }

    public void UpsertResaleMonthlyPrice(ResaleMonthlyPriceRecord record)
    {
        lock (_gate)
        {
            Data.ResaleMonthlyPrices ??= new List<ResaleMonthlyPriceRecord>();
            var month = new DateTime(record.PricingMonth.Year, record.PricingMonth.Month, 1);
            var existing = Data.ResaleMonthlyPrices.FirstOrDefault(x =>
                x.UnitCode.Equals(record.UnitCode, StringComparison.OrdinalIgnoreCase) &&
                x.StockItem.Equals(record.StockItem, StringComparison.OrdinalIgnoreCase) &&
                x.PricingMonth.Year == month.Year && x.PricingMonth.Month == month.Month);
            record.PricingMonth = month;
            if (existing is null) Data.ResaleMonthlyPrices.Add(record);
            else
            {
                existing.CurrentCost = record.CurrentCost;
                existing.OldSellingPrice = record.OldSellingPrice;
                existing.NewSellingPrice = record.NewSellingPrice;
                existing.GpPercent = record.GpPercent;
                existing.UpdatedAtUtc = record.UpdatedAtUtc;
                existing.UpdatedBy = record.UpdatedBy;
                existing.UpdatedByIdNumberMasked = record.UpdatedByIdNumberMasked;
            }
            WriteDataUnsafe();
        }
    }

    public ResaleMonthLockRecord? GetResaleMonthLock(string unitCode, DateTime month)
    {
        lock (_gate)
        {
            Data.ResaleMonthLocks ??= new List<ResaleMonthLockRecord>();
            return Data.ResaleMonthLocks.FirstOrDefault(x =>
                x.UnitCode.Equals(unitCode, StringComparison.OrdinalIgnoreCase) &&
                x.PricingMonth.Year == month.Year && x.PricingMonth.Month == month.Month);
        }
    }

    public void LockResaleMonth(ResaleMonthLockRecord record)
    {
        lock (_gate)
        {
            Data.ResaleMonthLocks ??= new List<ResaleMonthLockRecord>();
            var month = new DateTime(record.PricingMonth.Year, record.PricingMonth.Month, 1);
            var existing = Data.ResaleMonthLocks.FirstOrDefault(x =>
                x.UnitCode.Equals(record.UnitCode, StringComparison.OrdinalIgnoreCase) &&
                x.PricingMonth.Year == month.Year && x.PricingMonth.Month == month.Month);
            if (existing is not null) return;
            record.PricingMonth = month;
            Data.ResaleMonthLocks.Add(record);
            WriteDataUnsafe();
        }
    }

    public ResaleMonthlyPriceRecord? GetPreviousResalePrice(string unitCode, string stockItem, DateTime month)
    {
        lock (_gate)
        {
            return Data.ResaleMonthlyPrices
                .Where(x => x.UnitCode.Equals(unitCode, StringComparison.OrdinalIgnoreCase)
                    && x.StockItem.Equals(stockItem, StringComparison.OrdinalIgnoreCase)
                    && x.PricingMonth < new DateTime(month.Year, month.Month, 1))
                .OrderByDescending(x => x.PricingMonth)
                .FirstOrDefault();
        }
    }

    public void LogActivity(ActivityLog activity)
    {
        lock (_gate)
        {
            Data.ActivityLogs ??= new List<ActivityLog>();
            activity.OccurredAtUtc = activity.OccurredAtUtc == default ? DateTime.UtcNow : activity.OccurredAtUtc;
            Data.ActivityLogs.Add(activity);
            // Keep a practical rolling audit history in the JSON store.
            var cutoff = DateTime.UtcNow.AddYears(-2);
            Data.ActivityLogs.RemoveAll(x => x.OccurredAtUtc < cutoff);
            WriteDataUnsafe();
        }
    }


    public Recipe GetRegionalRecipe(Guid recipeId, string? regionOrUnit)
    {
        lock (_gate)
        {
            var source = Data.Recipes.FirstOrDefault(x => x.Id == recipeId);
            if (source is null) throw new KeyNotFoundException("Recipe not found.");
            var clone = JsonSerializer.Deserialize<Recipe>(JsonSerializer.Serialize(source, _options), _options)!;
            var division = GetDivisionForUnit(regionOrUnit);
            var regionalAllergens = new HashSet<string>(clone.Allergens, StringComparer.OrdinalIgnoreCase);
            var regionalMayContain = new HashSet<string>(clone.MayContainAllergens, StringComparer.OrdinalIgnoreCase);
            var requiresVerification = clone.SupplierVerificationRequired;
            foreach (var ingredient in clone.Ingredients)
            {
                ApplyRegionalProductToIngredient(ingredient, division);
                if (ingredient.ApprovedProductId.HasValue)
                {
                    var product = Data.ApprovedProducts.FirstOrDefault(x => x.Id == ingredient.ApprovedProductId.Value);
                    if (product is not null)
                    {
                        foreach (var allergen in product.Allergens) regionalAllergens.Add(allergen);
                        foreach (var allergen in product.MayContainAllergens) regionalMayContain.Add(allergen);
                        requiresVerification |= product.AllergenVerificationRequired;
                    }
                }
            }
            clone.Allergens = regionalAllergens.OrderBy(x => x).ToList();
            clone.MayContainAllergens = regionalMayContain.OrderBy(x => x).ToList();
            clone.SupplierVerificationRequired = requiresVerification;
            clone.Unit = DivisionDisplayName(division);
            return clone;
        }
    }

    public string GetCurrentUserDivision(string? region, string? unit)
        => GetDivisionForUnit(!string.IsNullOrWhiteSpace(region) ? region : unit);

    public void SetRegionalIngredientProduct(IngredientLine ingredient, string division, ApprovedProduct product)
    {
        division = NormalizeDivision(division);
        ingredient.RegionalApprovedProductIds ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        ingredient.RegionalApprovedProductIds[division] = product.Id;
        if (ingredient.AplDivision.Equals(division, StringComparison.OrdinalIgnoreCase) || !ingredient.ApprovedProductId.HasValue)
            ApplyApprovedProductToIngredient(ingredient, product);
    }

    private void ApplyRegionalProductToIngredient(IngredientLine ingredient, string division)
    {
        division = NormalizeDivision(division);
        ingredient.RegionalApprovedProductIds ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        ApprovedProduct? product = null;
        if (ingredient.RegionalApprovedProductIds.TryGetValue(division, out var mappedId))
            product = Data.ApprovedProducts.FirstOrDefault(x => x.Id == mappedId && x.IsActive && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase));

        var sourceProduct = ingredient.ApprovedProductId.HasValue
            ? Data.ApprovedProducts.FirstOrDefault(x => x.Id == ingredient.ApprovedProductId.Value)
            : null;
        var identity = NormalizeProductName(sourceProduct?.ProductName ?? ingredient.Name);
        if (product is null && !string.IsNullOrWhiteSpace(ingredient.ProductCode))
            product = Data.ApprovedProducts.FirstOrDefault(x => x.IsActive && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase)
                && x.ProductCode.Equals(ingredient.ProductCode, StringComparison.OrdinalIgnoreCase));
        if (product is null && !string.IsNullOrWhiteSpace(identity))
            product = Data.ApprovedProducts
                .Where(x => x.IsActive && x.Division.Equals(division, StringComparison.OrdinalIgnoreCase)
                    && NormalizeProductName(x.ProductName).Equals(identity, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.ProductCode)
                .FirstOrDefault();

        if (product is not null)
        {
            ingredient.RegionalApprovedProductIds[division] = product.Id;
            ApplyApprovedProductToIngredient(ingredient, product);
        }
        else
        {
            ingredient.ApprovedProductId = null;
            ingredient.AplDivision = division;
            ingredient.ProductCode = string.Empty;
            ingredient.UnitCost = 0m;
            ingredient.PackSize = string.Empty;
        }
    }

    public static string DivisionDisplayName(string division)
        => NormalizeDivision(division) switch { "JHB" => "Gauteng", "KZN" => "KwaZulu-Natal", _ => "Cape Town" };

    public Recipe UpsertRecipe(Recipe recipe)
    {
        lock (_gate)
        {
            var index = Data.Recipes.FindIndex(x => x.Id == recipe.Id);
            if (index >= 0)
                Data.Recipes[index] = recipe;
            else
                Data.Recipes.Add(recipe);

            WriteDataUnsafe();
            return recipe;
        }
    }


    public ApprovedProduct UpsertApprovedProduct(ApprovedProduct product, string updatedBy)
    {
        lock (_gate)
        {
            product.Division = NormalizeDivision(product.Division);
            product.ProductCode = product.ProductCode.Trim().ToUpperInvariant();
            product.ProductName = product.ProductName.Trim();
            product.SupplierItemCode = product.SupplierItemCode?.Trim() ?? string.Empty;
            product.ContractCode = product.ContractCode?.Trim() ?? string.Empty;
            product.Description = product.Description?.Trim() ?? string.Empty;
            product.Specification = product.Specification?.Trim() ?? string.Empty;
            product.Brand = product.Brand?.Trim() ?? string.Empty;
            product.UnitOfMeasure = product.UnitOfMeasure?.Trim() ?? string.Empty;
            product.BaseUnits = product.BaseUnits?.Trim() ?? string.Empty;
            product.VendorCode = product.VendorCode?.Trim() ?? string.Empty;
            product.VendorName = product.VendorName?.Trim() ?? string.Empty;
            product.Allergens = product.Allergens.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            product.MayContainAllergens = product.MayContainAllergens.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            product.UpdatedAtUtc = DateTime.UtcNow;
            product.UpdatedBy = updatedBy;

            var existing = Data.ApprovedProducts.FirstOrDefault(x => x.Id == product.Id);

            if (existing is null)
            {
                Data.ApprovedProducts.Add(product);
                existing = product;
            }
            else
            {
                existing.Division = product.Division;
                existing.ProductCode = product.ProductCode;
                existing.ProductName = product.ProductName;
                existing.SupplierItemCode = product.SupplierItemCode;
                existing.ContractCode = product.ContractCode;
                existing.Description = product.Description;
                existing.Specification = product.Specification;
                existing.Brand = product.Brand;
                existing.UnitOfMeasure = product.UnitOfMeasure;
                existing.BaseUnits = product.BaseUnits;
                existing.VendorCode = product.VendorCode;
                existing.VendorName = product.VendorName;
                existing.Allergens = product.Allergens.ToList();
                existing.MayContainAllergens = product.MayContainAllergens.ToList();
                existing.AllergenVerificationRequired = product.AllergenVerificationRequired;
                existing.UnitCost = product.UnitCost;
                existing.IsActive = product.IsActive;
                existing.UpdatedAtUtc = product.UpdatedAtUtc;
                existing.UpdatedBy = product.UpdatedBy;
            }

            ApplyProductCostToRecipes(existing);
            PersistUnsafe();
            return existing;
        }
    }

    public int ApplyAllApprovedProductCosts(string updatedBy)
    {
        lock (_gate)
        {
            var affectedRecipes = new HashSet<Guid>();
            foreach (var product in Data.ApprovedProducts.Where(x => x.IsActive))
            {
                foreach (var recipe in Data.Recipes)
                {
                    var changed = false;
                    if (!RecipeMatchesDivision(recipe, product.Division)) continue;
                    foreach (var ingredient in recipe.Ingredients.Where(i =>
                        (i.ApprovedProductId == product.Id || (!i.ApprovedProductId.HasValue && i.ProductCode.Equals(product.ProductCode, StringComparison.OrdinalIgnoreCase)))))
                    {
                        if (ingredient.UnitCost == product.UnitCost) continue;
                        ingredient.UnitCost = product.UnitCost;
                        changed = true;
                    }
                    if (changed) affectedRecipes.Add(recipe.Id);
                }
            }

            foreach (var recipe in Data.Recipes.Where(r => affectedRecipes.Contains(r.Id)))
            {
                recipe.Version = Math.Max(1, recipe.Version + 1);
                recipe.UpdatedAtUtc = DateTime.UtcNow;
                recipe.UpdatedBy = updatedBy;
            }

            PersistUnsafe();
            return affectedRecipes.Count;
        }
    }

    public void LinkIngredientToApprovedProduct(IngredientLine ingredient, string recipeUnit)
    {
        lock (_gate)
        {
            var division = GetDivisionForUnit(recipeUnit);
            ApprovedProduct? product = null;

            if (ingredient.ApprovedProductId.HasValue)
                product = Data.ApprovedProducts.FirstOrDefault(x => x.Id == ingredient.ApprovedProductId.Value && x.IsActive);

            if (product is null && !string.IsNullOrWhiteSpace(ingredient.ProductCode))
                product = Data.ApprovedProducts.FirstOrDefault(x => x.IsActive && x.Division == division &&
                    x.ProductCode.Equals(ingredient.ProductCode, StringComparison.OrdinalIgnoreCase));

            if (product is not null) ApplyApprovedProductToIngredient(ingredient, product);
        }
    }

    public bool LinkRecipeIngredient(Guid recipeId, int ingredientIndex, Guid productId, string updatedBy)
    {
        lock (_gate)
        {
            var recipe = Data.Recipes.FirstOrDefault(x => x.Id == recipeId);
            var product = Data.ApprovedProducts.FirstOrDefault(x => x.Id == productId && x.IsActive);
            if (recipe is null || product is null || ingredientIndex < 0 || ingredientIndex >= recipe.Ingredients.Count) return false;
            if (!RecipeMatchesDivision(recipe, product.Division)) return false;

            ApplyApprovedProductToIngredient(recipe.Ingredients[ingredientIndex], product);
            recipe.Version = Math.Max(1, recipe.Version + 1);
            recipe.UpdatedAtUtc = DateTime.UtcNow;
            recipe.UpdatedBy = updatedBy;
            PersistUnsafe();
            return true;
        }
    }

    public bool UnlinkRecipeIngredient(Guid recipeId, int ingredientIndex, string updatedBy)
    {
        lock (_gate)
        {
            var recipe = Data.Recipes.FirstOrDefault(x => x.Id == recipeId);
            if (recipe is null || ingredientIndex < 0 || ingredientIndex >= recipe.Ingredients.Count) return false;
            var ingredient = recipe.Ingredients[ingredientIndex];
            ingredient.ApprovedProductId = null;
            ingredient.AplDivision = string.Empty;
            recipe.Version = Math.Max(1, recipe.Version + 1);
            recipe.UpdatedAtUtc = DateTime.UtcNow;
            recipe.UpdatedBy = updatedBy;
            PersistUnsafe();
            return true;
        }
    }

    public IReadOnlyList<ApprovedProduct> SearchApprovedProducts(string division, string? query, int take = 50)
    {
        lock (_gate)
        {
            division = NormalizeDivision(division);
            var products = Data.ApprovedProducts.Where(x => x.IsActive && x.Division == division);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                products = products.Where(x => x.ProductCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.SupplierItemCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.VendorName.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            return products.OrderBy(x => x.ProductName).ThenBy(x => x.VendorName).Take(Math.Clamp(take, 1, 200)).ToList();
        }
    }

    private void AutoLinkRecipeIngredients()
    {
        var active = Data.ApprovedProducts.Where(x => x.IsActive).ToList();
        var uniqueByCode = active
            .GroupBy(x => $"{x.Division}|{x.ProductCode}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var uniqueByName = active
            .Select(x => new { Product = x, Key = $"{x.Division}|{NormalizeProductName(x.ProductName)}" })
            .Where(x => !x.Key.EndsWith("|", StringComparison.Ordinal))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Product, StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var recipe in Data.Recipes)
        {
            var division = GetDivisionForUnit(recipe.Unit);
            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.ApprovedProductId.HasValue && active.Any(x => x.Id == ingredient.ApprovedProductId.Value)) continue;

                ApprovedProduct? match = null;
                if (!string.IsNullOrWhiteSpace(ingredient.ProductCode) && !ingredient.ProductCode.StartsWith("APL-", StringComparison.OrdinalIgnoreCase))
                    uniqueByCode.TryGetValue($"{division}|{ingredient.ProductCode}", out match);

                if (match is null)
                    uniqueByName.TryGetValue($"{division}|{NormalizeProductName(ingredient.Name)}", out match);

                if (match is null) continue;
                ApplyApprovedProductToIngredient(ingredient, match);
                changed = true;
            }
        }
        if (changed) Save();
    }

    private static void ApplyApprovedProductToIngredient(IngredientLine ingredient, ApprovedProduct product)
    {
        ingredient.ApprovedProductId = product.Id;
        ingredient.AplDivision = product.Division;
        ingredient.ProductCode = product.ProductCode;
        ingredient.Name = product.ProductName;
        if (!string.IsNullOrWhiteSpace(product.BaseUnits)) ingredient.PackSize = product.BaseUnits;
        if (!string.IsNullOrWhiteSpace(product.UnitOfMeasure)) ingredient.Unit = product.UnitOfMeasure;
        ingredient.UnitCost = product.UnitCost;
    }

    private static string NormalizeProductName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    public static string GetDivisionForUnit(string? unit)
    {
        var value = unit ?? string.Empty;
        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (compact.Contains("GAUTENG") || compact.Contains("JOHANNESBURG") || compact.Contains("JHB") || compact.Contains("FREESTATE") || compact.Contains("MPUMALANGA") || compact.Contains("LIMPOPO") || compact.Contains("NORTHWEST")) return "JHB";
        if (compact.Contains("DURBAN") || compact.Contains("KWAZULUNATAL") || compact.Contains("KZN")) return "KZN";
        return "CT";
    }

    private void EnsureApprovedProductList()
    {
        Data.ApprovedProducts ??= new List<ApprovedProduct>();
        var changed = false;

        foreach (var ingredient in Data.Recipes.SelectMany(r => r.Ingredients))
        {
            if (string.IsNullOrWhiteSpace(ingredient.ProductCode))
            {
                ingredient.ProductCode = BuildProductCode(ingredient.Name);
                changed = true;
            }

            var product = Data.ApprovedProducts.FirstOrDefault(x => x.Division == "CT" &&
                x.ProductCode.Equals(ingredient.ProductCode, StringComparison.OrdinalIgnoreCase));
            if (product is null)
            {
                Data.ApprovedProducts.Add(new ApprovedProduct
                {
                    Division = "CT",
                    ProductCode = ingredient.ProductCode,
                    ProductName = ingredient.Name,
                    UnitOfMeasure = ingredient.Unit,
                    UnitCost = ingredient.UnitCost,
                    IsActive = true,
                    UpdatedBy = "System migration"
                });
                changed = true;
            }
        }

        if (changed) Save();
    }

    private void ApplyProductCostToRecipes(ApprovedProduct product)
    {
        foreach (var recipe in Data.Recipes)
        {
            if (!RecipeMatchesDivision(recipe, product.Division)) continue;
            var changed = false;
            foreach (var ingredient in recipe.Ingredients.Where(i =>
                (i.ApprovedProductId == product.Id || (!i.ApprovedProductId.HasValue && i.ProductCode.Equals(product.ProductCode, StringComparison.OrdinalIgnoreCase)))))
            {
                ingredient.Name = product.ProductName;
                ingredient.Unit = product.UnitOfMeasure;
                if (ingredient.UnitCost != product.UnitCost)
                {
                    ingredient.UnitCost = product.UnitCost;
                    changed = true;
                }
            }

            if (changed)
            {
                recipe.Version = Math.Max(1, recipe.Version + 1);
                recipe.UpdatedAtUtc = DateTime.UtcNow;
                recipe.UpdatedBy = product.UpdatedBy;
            }
        }
    }

    private void MergeApprovedProductSeed()
    {
        if (!File.Exists(_aplSeedPath)) return;
        var imported = JsonSerializer.Deserialize<List<ApprovedProduct>>(File.ReadAllText(_aplSeedPath), _options) ?? new();
        var byId = Data.ApprovedProducts.ToDictionary(x => x.Id);
        var changed = false;
        foreach (var product in imported)
        {
            product.Division = NormalizeDivision(product.Division);
            if (!byId.TryGetValue(product.Id, out var existing))
            {
                Data.ApprovedProducts.Add(product);
                byId[product.Id] = product;
                changed = true;
                continue;
            }

            // A newer APL extract is authoritative. Refresh existing product IDs so
            // recipe costing and resale pricing immediately use the new approved cost.
            if (product.UpdatedAtUtc <= existing.UpdatedAtUtc) continue;
            existing.Division = product.Division;
            existing.ProductCode = product.ProductCode;
            existing.ProductName = product.ProductName;
            existing.SupplierItemCode = product.SupplierItemCode;
            existing.ContractCode = product.ContractCode;
            existing.Description = product.Description;
            existing.Specification = product.Specification;
            existing.Brand = product.Brand;
            existing.UnitOfMeasure = product.UnitOfMeasure;
            existing.BaseUnits = product.BaseUnits;
            existing.VendorCode = product.VendorCode;
            existing.VendorName = product.VendorName;
            existing.UnitCost = product.UnitCost;
            existing.IsActive = product.IsActive;
            existing.Allergens = product.Allergens?.ToList() ?? new List<string>();
            existing.MayContainAllergens = product.MayContainAllergens?.ToList() ?? new List<string>();
            existing.AllergenVerificationRequired = product.AllergenVerificationRequired;
            existing.UpdatedAtUtc = product.UpdatedAtUtc;
            existing.UpdatedBy = product.UpdatedBy;
            ApplyProductCostToRecipes(existing);
            changed = true;
        }
        if (changed) Save();
    }

    private void MergeUnitAccounts()
    {
        if (!File.Exists(_unitSeedPath)) return;
        var units = JsonSerializer.Deserialize<List<UnitSeedRecord>>(File.ReadAllText(_unitSeedPath), _options) ?? new();
        var changed = false;
        foreach (var unit in units)
        {
            var displayUnit = $"{unit.UnitCode} - {unit.UnitName}";
            if (!Data.Units.Contains(displayUnit, StringComparer.OrdinalIgnoreCase))
            {
                Data.Units.Add(displayUnit);
                changed = true;
            }
            var existing = Data.Users.FirstOrDefault(x => x.Username.Equals(unit.UnitCode, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                var password = PasswordService.CreateHash("Empact@2026!");
                Data.Users.Add(new AppUser
                {
                    Username = unit.UnitCode, Email = unit.Email, DisplayName = unit.UnitName,
                    UnitCode = unit.UnitCode, Unit = displayUnit, Region = unit.Region,
                    PasswordHash = password.Hash, Salt = password.Salt, Role = UserRole.UnitUser,
                    IsApproved = true, IsActive = true, MustChangePassword = true
                });
                changed = true;
            }
        }
        if (changed) Save();
    }

    private void EnsureAdministratorCapeTownApl()
    {
        var changed = false;
        foreach (var admin in Data.Users.Where(x => x.Role == UserRole.Administrator))
        {
            if (admin.Unit == "Head Office") continue;
            admin.Unit = "Head Office";
            changed = true;
        }
        if (!Data.Units.Contains("Cape Town"))
        {
            Data.Units.Add("Cape Town");
            changed = true;
        }
        if (changed) Save();
    }

    private static string NormalizeDivision(string? division)
    {
        var value = (division ?? "CT").Trim().ToUpperInvariant();
        return value switch
        {
            "CTN" or "CAPE TOWN" => "CT",
            "JOHANNESBURG" or "GAUTENG" => "JHB",
            "KWAZULU NATAL" or "KWAZULU-NATAL" or "DURBAN" => "KZN",
            _ when value is "CT" or "JHB" or "KZN" => value,
            _ => "CT"
        };
    }

    private static bool RecipeMatchesDivision(Recipe recipe, string division)
    {
        return GetDivisionForUnit(recipe.Unit) == NormalizeDivision(division);
    }

    private static string BuildProductCode(string name)
    {
        var normalized = (name ?? "PRODUCT").Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"APL-{Convert.ToHexString(hash)[..8]}";
    }

    private void PersistUnsafe() => WriteDataUnsafe();

    private void WriteDataUnsafe()
    {
        var json = JsonSerializer.Serialize(Data, _options);

        try
        {
            WriteJsonDirectly(_path, json);
        }
        catch (UnauthorizedAccessException)
        {
            _path = ResolveLocalDataPathAndMigrate(_path);
            WriteJsonDirectly(_path, json);
        }
        catch (IOException)
        {
            _path = ResolveLocalDataPathAndMigrate(_path);
            WriteJsonDirectly(_path, json);
        }
    }

    private static void WriteJsonDirectly(string path, string json)
    {
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        RemoveReadOnlyAttribute(path);

        // Write directly to avoid Windows environments that permit creating a
        // temporary file but deny File.Move/File.Replace over the destination.
        using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.WriteThrough);
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(json);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static string ResolveLocalDataPathAndMigrate(string currentPath)
    {
        var localFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EmpactRecipeOnline",
            "App_Data");
        Directory.CreateDirectory(localFolder);
        var localPath = Path.Combine(localFolder, "app-data.json");

        if (!string.Equals(currentPath, localPath, StringComparison.OrdinalIgnoreCase)
            && !File.Exists(localPath)
            && File.Exists(currentPath))
        {
            try { File.Copy(currentPath, localPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return localPath;
    }

    private AppData LoadOrSeed()
    {
        if (File.Exists(_path))
            return JsonSerializer.Deserialize<AppData>(File.ReadAllText(_path), _options) ?? Seed();
        var data = Seed();
        Data = data;
        WriteDataUnsafe();
        return data;
    }

    private static string ResolveWritableDataPath(string contentDataFolder)
    {
        var configuredFolder = Environment.GetEnvironmentVariable("EMPACT_RECIPE_DATA_FOLDER");
        if (!string.IsNullOrWhiteSpace(configuredFolder))
        {
            var configuredPath = Path.Combine(configuredFolder, "app-data.json");
            if (CanWriteToFolder(configuredFolder)) return configuredPath;
        }

        // Always use one stable per-user data location. This prevents administrator
        // recipe corrections from being lost when a new application ZIP is extracted
        // to a different project folder during an upgrade.
        var localFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EmpactRecipeOnline",
            "App_Data");
        Directory.CreateDirectory(localFolder);
        var localPath = Path.Combine(localFolder, "app-data.json");
        var projectPath = Path.Combine(contentDataFolder, "app-data.json");

        // Migrate a project-folder data file only when it is newer than the stable
        // copy (or when no stable copy exists). The packaged recipe seed is never
        // allowed to overwrite administrator-maintained recipes.
        if (File.Exists(projectPath))
        {
            try
            {
                var shouldMigrate = !File.Exists(localPath)
                    || File.GetLastWriteTimeUtc(projectPath) > File.GetLastWriteTimeUtc(localPath);
                if (shouldMigrate)
                {
                    Directory.CreateDirectory(localFolder);
                    File.Copy(projectPath, localPath, true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return localPath;
    }

    private static bool CanWriteToFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void RemoveReadOnlyAttribute(string path)
    {
        if (!File.Exists(path)) return;
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }


    private void EnsureMandelaDayPromotion()
    {
        var id = Guid.Parse("6d3f2d08-7970-4a28-9daa-18bff2782026");
        var existing = Data.Promotions.FirstOrDefault(x => x.Id == id)
            ?? Data.Promotions.FirstOrDefault(x => x.Title.Equals("Serving Ubuntu – Nelson Mandela Day", StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            Data.Promotions.Add(new Promotion
            {
                Id = id,
                Title = "Serving Ubuntu – Nelson Mandela Day",
                Description = "Serving more than meals. Download or print the A4 Mandela Day Ubuntu artwork for use in your unit on 18 July 2026.",
                StartDate = new DateTime(2026, 7, 18),
                EndDate = new DateTime(2026, 7, 18),
                Region = "All regions",
                DocumentUrl = "/promotions/mandela-day-ubuntu-a4.pdf",
                DocumentFileName = "Mandela Day Ubuntu A4.pdf",
                IsPublished = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedBy = "System import"
            });
            Save();
            return;
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(existing.DocumentUrl)) { existing.DocumentUrl = "/promotions/mandela-day-ubuntu-a4.pdf"; changed = true; }
        if (string.IsNullOrWhiteSpace(existing.DocumentFileName)) { existing.DocumentFileName = "Mandela Day Ubuntu A4.pdf"; changed = true; }
        if (changed) Save();
    }

    private void MergeWorkbookRecipes()
    {
        if (!File.Exists(_seedPath)) return;
        var imported = JsonSerializer.Deserialize<List<Recipe>>(File.ReadAllText(_seedPath), _options) ?? new();
        var existingIds = Data.Recipes.Select(r => r.Id).ToHashSet();
        var existingCodes = Data.Recipes.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var recipe in imported)
        {
            if (existingIds.Contains(recipe.Id) || existingCodes.Contains(recipe.Code)) continue;
            Data.Recipes.Add(recipe);
            added++;
        }
        if (added > 0) Save();
    }

    private void BackfillRecipeDepartments()
    {
        var changed = false;
        foreach (var recipe in Data.Recipes)
        {
            if (!string.IsNullOrWhiteSpace(recipe.Department) && RecipeDepartments.All.Contains(recipe.Department, StringComparer.OrdinalIgnoreCase)) continue;
            recipe.Department = RecipeDepartments.Infer(recipe.Category, recipe.Name);
            changed = true;
        }
        if (changed) Save();
    }

    private void BackfillRecipeMedia()
    {
        var changed = false;
        foreach (var recipe in Data.Recipes)
        {
            if (ShouldReplaceRecipeMedia(recipe.ImageUrl))
            {
                var mapped = RecipeMediaService.GetDefaultMediaForRecipe(recipe);
                if (!string.Equals(recipe.ImageUrl, mapped, StringComparison.OrdinalIgnoreCase))
                {
                    recipe.ImageUrl = mapped;
                    changed = true;
                }
            }
        }

        if (changed) Save();
    }

    private bool ShouldReplaceRecipeMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var normalized = url.Split('?', '#')[0].Replace('\\', '/');

        if (RecipeMediaService.IsManagedDefaultMedia(normalized)) return true;

        if (normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var relative = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_webRootPath, relative);
            return !File.Exists(fullPath);
        }

        return normalized.StartsWith("/images/", StringComparison.OrdinalIgnoreCase);
    }

    private static AppData Seed()
    {
        var admin = PasswordService.CreateHash("admin123");
        var manager = PasswordService.CreateHash("manager123");
        return new AppData
        {
            Units = ["Head Office", "Gauteng", "Cape Town", "KwaZulu-Natal"],
            Users =
            [
                new AppUser { Username="admin", Email="admin@empact.local", DisplayName="System Administrator", PasswordHash=admin.Hash, Salt=admin.Salt, Role=UserRole.Administrator, Unit="Head Office", Region="Cape Town" },
                new AppUser { Username="manager", Email="manager@empact.local", DisplayName="Unit Manager", PasswordHash=manager.Hash, Salt=manager.Salt, Role=UserRole.UnitUser, Unit="Gauteng", Region="Gauteng" }
            ],
            Recipes = []
        };
    }
}
