namespace EmpactRecipeOnline.Models;

public sealed class MenuIngredientUsageRow
{
    public string IngredientName { get; set; } = string.Empty;
    public int RecipeCount { get; set; }
    public int IngredientLineCount { get; set; }
    public IReadOnlyList<string> RecipeNames { get; set; } = Array.Empty<string>();
}

public sealed class LowerComparableReplacementRow
{
    public string SourceProductName { get; set; } = string.Empty;
    public string SourceProductCode { get; set; } = string.Empty;
    public Guid SourceProductId { get; set; }
    public string ReplacementProductName { get; set; } = string.Empty;
    public string ReplacementProductCode { get; set; } = string.Empty;
    public Guid ReplacementProductId { get; set; }
    public string ComparableUnit { get; set; } = string.Empty;
    public decimal CurrentComparableCost { get; set; }
    public decimal LowerComparableCost { get; set; }
    public decimal SavingPerComparableUnit { get; set; }
    public int RecipeCount { get; set; }
    public int IngredientLineCount { get; set; }
}

public sealed class MenuAdjustmentViewModel
{
    public string Division { get; set; } = "CT";
    public string Query { get; set; } = string.Empty;
    public string SourceIngredient { get; set; } = string.Empty;
    public string ApprovedProductQuery { get; set; } = string.Empty;
    public string LowerProductQuery { get; set; } = string.Empty;
    public Guid? ReplacementProductId { get; set; }
    public IReadOnlyList<MenuIngredientUsageRow> Ingredients { get; set; } = Array.Empty<MenuIngredientUsageRow>();
    public IReadOnlyList<ApprovedProduct> ApprovedProducts { get; set; } = Array.Empty<ApprovedProduct>();
    public IReadOnlyList<MenuItemReplacementAudit> RecentReplacements { get; set; } = Array.Empty<MenuItemReplacementAudit>();
    public IReadOnlyList<LowerComparableReplacementRow> LowerComparableReplacements { get; set; } = Array.Empty<LowerComparableReplacementRow>();
}
