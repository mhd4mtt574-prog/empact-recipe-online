namespace EmpactRecipeOnline.Models;

public sealed class ComplianceDashboardViewModel
{
    public string Region { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<string> Regions { get; set; } = new();
    public List<ComplianceUnitOption> Units { get; set; } = new();
    public List<ComplianceUnitRow> UnitRows { get; set; } = new();
    public List<ComplianceModuleScore> ModuleScores { get; set; } = new();
    public List<ComplianceRegionScore> RegionScores { get; set; } = new();
    public List<ComplianceTrendPoint> Trend { get; set; } = new();
    public int OverallCompliance { get; set; }
    public int CompliantUnits { get; set; }
    public int AttentionUnits { get; set; }
    public int OutstandingUnits { get; set; }
    public DateTime? LastActivityUtc { get; set; }
    public string ScopeLabel { get; set; } = "All units";
}

public sealed class ComplianceUnitOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Label => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} · {Name}";
}

public sealed class ComplianceUnitRow
{
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ResalePricing { get; set; }
    public int RecipeCosting { get; set; }
    public int Allergens { get; set; }
    public int ReportsTieBack { get; set; }
    public int FoodWaste { get; set; }
    public int RecipeAdjustments { get; set; }
    public int Promotions { get; set; }
    public int Overall { get; set; }
    public DateTime? LastActivityUtc { get; set; }
    public string Status => Overall >= 90 ? "Compliant" : Overall >= 70 ? "Attention" : "Outstanding";
    public string HighestOutstandingModule { get; set; } = string.Empty;
}

public sealed class ComplianceModuleScore
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
}

public sealed class ComplianceRegionScore
{
    public string Region { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Units { get; set; }
}

public sealed class ComplianceTrendPoint
{
    public string Label { get; set; } = string.Empty;
    public int Score { get; set; }
}
