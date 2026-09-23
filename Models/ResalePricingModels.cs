using System.ComponentModel.DataAnnotations;

namespace EmpactRecipeOnline.Models;

public sealed class ResalePricingData
{
    public string SourceAccessWorkbook { get; set; } = string.Empty;
    public string SourcePricingWorkbook { get; set; } = string.Empty;
    public string SourceWorkflowReference { get; set; } = string.Empty;
    public string AccessWorkbookSha256 { get; set; } = string.Empty;
    public string PricingWorkbookSha256 { get; set; } = string.Empty;
    public List<ResaleUnit> Units { get; set; } = new();
    public List<ResaleUnitManager> UnitManagers { get; set; } = new();
    public List<ResaleSeniorManager> SeniorManagers { get; set; } = new();
    public List<ResaleCategoryPrice> CategoryAPrices { get; set; } = new();
    public List<ResaleCategoryPrice> CategoryBPrices { get; set; } = new();
}

public sealed class ResaleUnit
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RegionalManager { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string GeneralManager { get; set; } = string.Empty;
    public string Grouping { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public sealed class ResaleUnitManager
{
    public string IdNumber { get; set; } = string.Empty;
    public string PersonnelNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
}

public sealed class ResaleSeniorManager
{
    public string IdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ResaleCategoryPrice
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Puom { get; set; } = string.Empty;
    public string SupplierItemCode { get; set; } = string.Empty;
    public decimal CurrentNettCaseCost { get; set; }
    public decimal CurrentNettEachCost { get; set; }
    public string MainCategory { get; set; } = string.Empty;
    public string BrandCategorySize { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string Uom { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
}

public sealed class ResaleAccessIdentity
{
    public string IdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSeniorManager { get; set; }
    public List<string> UnitCodes { get; set; } = new();
}

public sealed class ResaleAccessViewModel
{
    [Required, Display(Name = "South African ID number")]
    [RegularExpression(@"^\d{13}$", ErrorMessage = "Enter a valid 13-digit ID number.")]
    public string IdNumber { get; set; } = string.Empty;
}

public sealed class ResalePricingViewModel
{
    public string AccessName { get; set; } = string.Empty;
    public bool IsSeniorManager { get; set; }
    public List<ResaleUnit> Units { get; set; } = new();
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
    public List<ResaleCategoryPrice> Prices { get; set; } = new();
    public string SourcePricingWorkbook { get; set; } = string.Empty;
    public string SourceSheet { get; set; } = string.Empty;
}

// Retained for compatibility with the existing application data store. The rebuilt
// resale tool is read-only and does not use monthly price-entry records or locks.
public sealed class ResaleMonthlyPriceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UnitCode { get; set; } = string.Empty;
    public string StockItem { get; set; } = string.Empty;
    public DateTime PricingMonth { get; set; }
    public decimal CurrentCost { get; set; }
    public decimal OldSellingPrice { get; set; }
    public decimal NewSellingPrice { get; set; }
    public decimal GpPercent { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
    public string UpdatedByIdNumberMasked { get; set; } = string.Empty;
}

public sealed class ResaleMonthLockRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UnitCode { get; set; } = string.Empty;
    public DateTime PricingMonth { get; set; }
    public DateTime LockedAtUtc { get; set; } = DateTime.UtcNow;
    public string LockedBy { get; set; } = string.Empty;
    public string LockedByIdNumberMasked { get; set; } = string.Empty;
}
