using System.ComponentModel.DataAnnotations;

namespace EmpactRecipeOnline.Models;

public enum UserRole { Administrator, HeadOffice, UnitManager, UnitUser }

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    [Required] public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.UnitUser;
    public string Unit { get; set; } = "Gauteng";
    public string Region { get; set; } = "Cape Town";
    public string UnitCode { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = true;
}

public sealed class IngredientLine
{
    public Guid? ApprovedProductId { get; set; }
    // Region-specific APL selections. Keys are CT, JHB and KZN.
    public Dictionary<string, Guid> RegionalApprovedProductIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string AplDivision { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // These fields mirror columns D and E in the clean costing template.
    public string CookingNotes { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    // Quantity is the standard issue quantity from column H of the costing sheet.
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    // UnitCost is the APL purchase price from column F.
    public decimal UnitCost { get; set; }
    // Factor is the pack/conversion factor from column G. A zero or missing value is treated as 1.
    public decimal Factor { get; set; } = 1m;
    // Retained for compatibility with earlier saved recipes. The costing sheet uses recipe-level waste.
    public decimal WastePercent { get; set; }

    // Optional administrator-entered overrides for calculated clean-sheet columns J-M.
    // Null means the standard clean costing formula remains active.
    public decimal? BatchIssueOverride { get; set; }
    public decimal? WasteCostOverride { get; set; }
    public decimal? BatchCostOverride { get; set; }
    public decimal? CostPerPortionOverride { get; set; }

    public decimal EffectiveFactor => Factor <= 0 ? 1m : Factor;
    public decimal GetFormulaBatchIssue(int basePortions, int requiredPortions)
    {
        var safeBase = basePortions <= 0 ? Math.Max(1, requiredPortions) : basePortions;
        return Quantity / safeBase * Math.Max(1, requiredPortions);
    }
    public decimal GetBatchIssue(int basePortions, int requiredPortions)
        => BatchIssueOverride ?? GetFormulaBatchIssue(basePortions, requiredPortions);
    public decimal GetBaseCost(int basePortions, int requiredPortions)
        => GetBatchIssue(basePortions, requiredPortions) * UnitCost / EffectiveFactor;
    public decimal GetFormulaWasteCost(int basePortions, int requiredPortions, decimal recipeWastePercent)
        => GetBaseCost(basePortions, requiredPortions) * Math.Max(0m, recipeWastePercent) / 100m;
    public decimal GetWasteCost(int basePortions, int requiredPortions, decimal recipeWastePercent)
        => WasteCostOverride ?? GetFormulaWasteCost(basePortions, requiredPortions, recipeWastePercent);
    public decimal GetFormulaLineCost(int basePortions, int requiredPortions, decimal recipeWastePercent)
        => GetBaseCost(basePortions, requiredPortions) + GetWasteCost(basePortions, requiredPortions, recipeWastePercent);
    public decimal GetLineCost(int basePortions, int requiredPortions, decimal recipeWastePercent)
        => BatchCostOverride ?? GetFormulaLineCost(basePortions, requiredPortions, recipeWastePercent);
    public decimal GetCostPerPortion(int basePortions, int requiredPortions, decimal recipeWastePercent)
        => CostPerPortionOverride ?? GetLineCost(basePortions, requiredPortions, recipeWastePercent) / Math.Max(1, requiredPortions);
    public bool IsAplLinked => ApprovedProductId.HasValue && !string.IsNullOrWhiteSpace(ProductCode);
}


public sealed class NutritionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IngredientKey { get; set; } = string.Empty;
    public string IngredientDisplayName { get; set; } = string.Empty;
    public long? FoodDataCentralId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceReferenceCode { get; set; } = string.Empty;
    public string SourceFoodName { get; set; } = string.Empty;
    public string SourceDataType { get; set; } = string.Empty;
    public decimal EnergyKcalPer100g { get; set; }
    public decimal EnergyKjPer100g { get; set; }
    public decimal ProteinGramsPer100g { get; set; }
    public decimal CarbohydrateGramsPer100g { get; set; }
    public decimal SugarGramsPer100g { get; set; }
    public decimal FatGramsPer100g { get; set; }
    public decimal SaturatedFatGramsPer100g { get; set; }
    public decimal FibreGramsPer100g { get; set; }
    public decimal SodiumMgPer100g { get; set; }
    // Optional conversion controls for volume/count based recipe units.
    public decimal? GramsPerMillilitre { get; set; }
    public decimal? GramsPerEach { get; set; }
    public decimal MatchConfidencePercent { get; set; }
    public bool Verified { get; set; }
    public bool RequiresReview { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IngredientNutritionContribution
{
    public string IngredientName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public decimal RecipeQuantity { get; set; }
    public string RecipeUnit { get; set; } = string.Empty;
    public decimal? EstimatedGrams { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceReferenceCode { get; set; } = string.Empty;
    public string SourceFoodName { get; set; } = string.Empty;
    public long? FoodDataCentralId { get; set; }
    public decimal EnergyKcal { get; set; }
    public decimal EnergyKj { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbohydrateGrams { get; set; }
    public decimal SugarGrams { get; set; }
    public decimal FatGrams { get; set; }
    public decimal SaturatedFatGrams { get; set; }
    public decimal FibreGrams { get; set; }
    public decimal SodiumMg { get; set; }
    public bool IncludedInTotal { get; set; }
    public bool RequiresReview { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class RecipeNutritionAnalysisResult
{
    public int Portions { get; set; }
    public List<IngredientNutritionContribution> Ingredients { get; set; } = new();
    public decimal EnergyKcalTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.EnergyKcal);
    public decimal EnergyKjTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.EnergyKj);
    public decimal ProteinGramsTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.ProteinGrams);
    public decimal CarbohydrateGramsTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.CarbohydrateGrams);
    public decimal SugarGramsTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.SugarGrams);
    public decimal FatGramsTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.FatGrams);
    public decimal SaturatedFatGramsTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.SaturatedFatGrams);
    public decimal FibreGramsTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.FibreGrams);
    public decimal SodiumMgTotal => Ingredients.Where(x => x.IncludedInTotal).Sum(x => x.SodiumMg);
    public decimal CoveragePercent => Ingredients.Count == 0 ? 0 : Ingredients.Count(x => x.IncludedInTotal) * 100m / Ingredients.Count;
    public bool IsComplete => Ingredients.Count > 0 && Ingredients.All(x => x.IncludedInTotal);
    public bool ReviewRequired => Ingredients.Any(x => x.RequiresReview || !x.IncludedInTotal);
}

public sealed class AllergenDetectionSource
{
    public string Allergen { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string DetectionType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsMayContain { get; set; }
    public bool RequiresVerification { get; set; }
}

public sealed class AllergenDetectionResult
{
    public List<string> Allergens { get; set; } = new();
    public List<string> MayContainAllergens { get; set; } = new();
    public bool ReviewRequired { get; set; }
    public List<AllergenDetectionSource> Sources { get; set; } = new();
}

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string Department { get; set; } = "Vegetables";
    public string Unit { get; set; } = "Head Office";
    // BasePortions corresponds to the standard quantity basis in H7 of the costing sheet.
    // Existing recipes with no saved base use StandardPortions, preserving their current quantities.
    [Range(0, 100000)] public int BasePortions { get; set; }
    [Range(1, 100000)] public int StandardPortions { get; set; } = 10;
    // WastePercent corresponds to the recipe waste percentage in J1 of the costing sheet.
    [Range(0, 100)] public decimal WastePercent { get; set; } = 5m;
    [Range(0, 99.99)] public decimal DesiredMarginPercent { get; set; } = 65m;
    public string Method { get; set; } = string.Empty;
    public string ServingInstructions { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Allergens { get; set; } = new();
    public List<string> MayContainAllergens { get; set; } = new();
    public bool SupplierVerificationRequired { get; set; }
    public List<string> AutoDetectedAllergens { get; set; } = new();
    public List<string> AutoDetectedMayContainAllergens { get; set; } = new();
    public List<AllergenDetectionSource> AllergenDetectionSources { get; set; } = new();
    public DateTime? LastAllergenScanAtUtc { get; set; }

    // Administrator-maintained estimated nutrition values per finished portion.
    // These remain estimates until verified against supplier specifications or by a dietitian.
    [Range(0, 100000)] public decimal EnergyKjPerPortion { get; set; }
    [Range(0, 100000)] public decimal EnergyKcalPerPortion { get; set; }
    [Range(0, 10000)] public decimal ProteinGramsPerPortion { get; set; }
    [Range(0, 10000)] public decimal CarbohydrateGramsPerPortion { get; set; }
    [Range(0, 10000)] public decimal SugarGramsPerPortion { get; set; }
    [Range(0, 10000)] public decimal FatGramsPerPortion { get; set; }
    [Range(0, 10000)] public decimal SaturatedFatGramsPerPortion { get; set; }
    [Range(0, 10000)] public decimal FibreGramsPerPortion { get; set; }
    [Range(0, 1000000)] public decimal SodiumMgPerPortion { get; set; }
    public bool NutritionVerified { get; set; }
    public bool NutritionAutoGenerated { get; set; }
    public DateTime? NutritionGeneratedAtUtc { get; set; }
    public decimal NutritionCoveragePercent { get; set; }
    public string NutritionSourceNote { get; set; } = string.Empty;
    public List<string> SpecialDiets { get; set; } = new();
    public bool DietitianApprovalRequired { get; set; }

    public bool IsApproved { get; set; }
    public int Version { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
    // Marks administrator-maintained content so upgrade seed merges never replace it.
    public bool HasAdministratorCorrections { get; set; }
    public DateTime? LastCorrectedAtUtc { get; set; }
    public string LastCorrectedBy { get; set; } = string.Empty;
    public List<IngredientLine> Ingredients { get; set; } = new();
    public int EffectiveBasePortions => BasePortions <= 0 ? Math.Max(1, StandardPortions) : BasePortions;
    // Spreadsheet formulas:
    // Batch issue = Standard quantity / Base portions * Required portions
    // Waste factor = (Batch issue * Purchase price / Factor) * Waste %
    // Batch cost = (Batch issue * Purchase price / Factor) + Waste factor
    public decimal TotalCost => Ingredients.Sum(i => i.GetLineCost(EffectiveBasePortions, StandardPortions, WastePercent));
    public decimal CostPerPortion => StandardPortions <= 0 ? 0 : TotalCost / StandardPortions;
    public decimal RecommendedSellingPrice => DesiredMarginPercent >= 100 ? 0 : CostPerPortion / (1 - DesiredMarginPercent / 100m);
}

public sealed class ApprovedProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Division { get; set; } = "CT";
    [Required] public string ProductCode { get; set; } = string.Empty;
    [Required] public string ProductName { get; set; } = string.Empty;
    public string SupplierItemCode { get; set; } = string.Empty;
    public string ContractCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string BaseUnits { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    [Range(0, 100000000)] public decimal UnitCost { get; set; }
    public bool IsActive { get; set; } = true;
    // Supplier-declared allergens for this regional approved product.
    public List<string> Allergens { get; set; } = new();
    public List<string> MayContainAllergens { get; set; } = new();
    public bool AllergenVerificationRequired { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}


public sealed class UnitSeedRecord
{
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class Promotion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);
    public string Region { get; set; } = "All regions";
    public string DocumentUrl { get; set; } = string.Empty;
    public string DocumentFileName { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
    public bool IsActive => IsPublished && StartDate.Date <= DateTime.Today && EndDate.Date >= DateTime.Today;
}


public static class RecipeDepartments
{
    public static readonly string[] All = { "Beef", "Fish", "Chicken", "Lamb/Mutton", "Pork", "Salad", "Starch", "Grains", "Desserts", "Vegetables", "Pastas" };

    public static string Normalize(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        var match = All.FirstOrDefault(x => x.Equals(v, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;
        if (v.Contains("lamb", StringComparison.OrdinalIgnoreCase) || v.Contains("mutton", StringComparison.OrdinalIgnoreCase)) return "Lamb/Mutton";
        if (v.Contains("salad", StringComparison.OrdinalIgnoreCase)) return "Salad";
        if (v.Contains("starch", StringComparison.OrdinalIgnoreCase)) return "Starch";
        if (v.Contains("pasta", StringComparison.OrdinalIgnoreCase)) return "Pastas";
        if (v.Contains("grain", StringComparison.OrdinalIgnoreCase) || v.Contains("rice", StringComparison.OrdinalIgnoreCase)) return "Grains";
        if (v.Contains("dessert", StringComparison.OrdinalIgnoreCase) || v.Contains("pudding", StringComparison.OrdinalIgnoreCase) || v.Contains("cake", StringComparison.OrdinalIgnoreCase)) return "Desserts";
        if (v.Contains("veget", StringComparison.OrdinalIgnoreCase)) return "Vegetables";
        return "Vegetables";
    }

    public static int SortOrder(string? value)
    {
        var normalized = Normalize(value);
        var index = Array.IndexOf(All, normalized);
        return index < 0 ? All.Length : index;
    }

    public static string Infer(string? category, string? name)
    {
        var text = $"{category} {name}".ToLowerInvariant();
        if (text.Contains("beef")) return "Beef";
        if (text.Contains("fish") || text.Contains("hake") || text.Contains("tuna")) return "Fish";
        if (text.Contains("chicken")) return "Chicken";
        if (text.Contains("lamb") || text.Contains("mutton")) return "Lamb/Mutton";
        if (text.Contains("pork") || text.Contains("bacon")) return "Pork";
        if (text.Contains("salad")) return "Salad";
        if (text.Contains("pasta") || text.Contains("macaroni") || text.Contains("spaghetti") || text.Contains("noodle")) return "Pastas";
        if (text.Contains("grain") || text.Contains("rice") || text.Contains("couscous") || text.Contains("barley")) return "Grains";
        if (text.Contains("dessert") || text.Contains("pudding") || text.Contains("cake") || text.Contains("custard")) return "Desserts";
        if (text.Contains("starch") || text.Contains("potato") || text.Contains("pap") || text.Contains("maize")) return "Starch";
        return "Vegetables";
    }
}


public sealed class UnitRecipeMargin
{
    public Guid RecipeId { get; set; }
    public string UnitKey { get; set; } = string.Empty;
    [Range(0, 99.99)] public decimal DesiredMarginPercent { get; set; } = 65m;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class DailyTieBackUpload
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime BusinessDate { get; set; } = DateTime.Today;
    public string Unit { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid UploadedByUserId { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public decimal? PlannedGpPercent { get; set; }
    public decimal? PlannedGpRand { get; set; }
    public decimal? ActualGpPercent { get; set; }
    public decimal? ActualGpRand { get; set; }
    public DateTime? GpMatchedBusinessDate { get; set; }
}

public sealed class MenuItemReplacementAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ReplacedAtUtc { get; set; } = DateTime.UtcNow;
    public string SourceIngredientName { get; set; } = string.Empty;
    public Guid ReplacementProductId { get; set; }
    public string ReplacementProductCode { get; set; } = string.Empty;
    public string ReplacementProductName { get; set; } = string.Empty;
    public int RecipesUpdated { get; set; }
    public int IngredientLinesUpdated { get; set; }
    public string ReplacedBy { get; set; } = string.Empty;
}

public sealed class AppData
{
    public List<AppUser> Users { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
    public List<ApprovedProduct> ApprovedProducts { get; set; } = new();
    public List<string> Units { get; set; } = new();
    public List<Promotion> Promotions { get; set; } = new();
    public List<UnitRecipeMargin> UnitRecipeMargins { get; set; } = new();
    public List<ActivityLog> ActivityLogs { get; set; } = new();
    public List<DailyTieBackUpload> DailyTieBackUploads { get; set; } = new();
    public List<MenuItemReplacementAudit> MenuItemReplacementAudits { get; set; } = new();
    public List<ResaleMonthlyPriceRecord> ResaleMonthlyPrices { get; set; } = new();
    public List<ResaleMonthLockRecord> ResaleMonthLocks { get; set; } = new();
    public List<FoodWasteRecord> FoodWasteRecords { get; set; } = new();
    public List<NutritionProfile> NutritionProfiles { get; set; } = new();
    public List<UserFavourite> UserFavourites { get; set; } = new();
    public List<FoodWasteReductionPlan> FoodWasteReductionPlans { get; set; } = new();
    public List<UnitFoodWasteProfile> UnitFoodWasteProfiles { get; set; } = new();
    public List<FoodWasteSupportDocument> FoodWasteSupportDocuments { get; set; } = new();
}
