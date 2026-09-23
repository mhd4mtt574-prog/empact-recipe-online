using System.ComponentModel.DataAnnotations;

namespace EmpactRecipeOnline.Models;

public sealed class FoodWasteRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public DateTime BusinessDate { get; set; } = DateTime.Today;
    [Required] public string UnitCode { get; set; } = string.Empty;
    [Required] public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string UnitManager { get; set; } = string.Empty;
    public string RegionalManager { get; set; } = string.Empty;
    public string Workstation { get; set; } = string.Empty;
    [Range(0, 100000)] public decimal TareWeightKg { get; set; }
    [Range(0, 100000)] public decimal ProductionWasteKg { get; set; }
    [Range(0, 100000)] public decimal OverProductionWasteKg { get; set; }
    [Range(0, 100000)] public decimal UnusedExpiredKg { get; set; }
    [Range(0, 100000)] public decimal PlateScrapingsKg { get; set; }
    [Range(0, 100000)] public decimal FoodSamplesKg { get; set; }
    [Range(0, 100000)] public decimal BokashiWasteKg { get; set; }
    [Range(0, 100000)] public decimal BioBinsKg { get; set; }
    [Range(0, 100000)] public decimal OtherCompostingKg { get; set; }
    public string OtherCompostingMethod { get; set; } = string.Empty;
    [Range(0, 10000000)] public int MainDishMeals { get; set; }
    [Range(0, 10000000)] public int UrbanFlavourMeals { get; set; }
    [Range(0, 10000000)] public int ChefsSignatureMeals { get; set; }
    [Range(0, 10000000)] public int VeggieMeals { get; set; }
    [Range(0, 10000000)] public int OtherMeals { get; set; }
    [Range(0, 10000000)] public int FunctionMeals { get; set; }
    [Range(0, 10000000)] public int FunctionPlatters { get; set; }
    public bool HadFunction { get; set; }
    [Range(0, 100000)] public decimal UsedOilLitres { get; set; }
    public string UsedOilSupplier { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<FoodWasteLine> Lines { get; set; } = new();
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDraft { get; set; }
    public DateTime? DraftSavedAtUtc { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public decimal TotalFoodWasteKg => ProductionWasteKg + OverProductionWasteKg + UnusedExpiredKg + PlateScrapingsKg + FoodSamplesKg;
    public decimal TotalDiversionKg => BokashiWasteKg + BioBinsKg + OtherCompostingKg;
    public int TotalMeals => MainDishMeals + UrbanFlavourMeals + ChefsSignatureMeals + VeggieMeals + OtherMeals + FunctionMeals + FunctionPlatters;
}

public sealed class FoodWasteLine
{
    public string TypeOfWaste { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Range(0, 100000)] public decimal WeightKg { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

public sealed class FoodWasteDashboardViewModel
{
    public string Region { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public DateTime Month { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public List<string> Regions { get; set; } = new();
    public List<FoodWasteUnitOption> Units { get; set; } = new();
    public List<FoodWasteRecord> Records { get; set; } = new();
    public FoodWasteRecord NewRecord { get; set; } = new();
    public FoodWasteTotals Totals { get; set; } = new();
    public List<FoodWasteCategoryTotal> CategoryTotals { get; set; } = new();
    public List<FoodWasteCategoryTotal> DiversionTotals { get; set; } = new();
    public List<FoodWasteCategoryTotal> MealTotals { get; set; } = new();
    public int ExpectedWorkDays { get; set; }
    public int SubmittedDays { get; set; }
    public int CompliancePercent { get; set; }
    public DateTime? LastSubmissionUtc { get; set; }
    public bool CanSelectUnit { get; set; }
    public string ScopeLabel { get; set; } = string.Empty;
    public UnitFoodWasteProfile? WasteProfile { get; set; }
    public List<FoodWasteReductionPlan> ReductionPlans { get; set; } = new();
    public FoodWasteRecord? CurrentDraft { get; set; }
    public List<FoodWasteSupportDocument> SupportDocuments { get; set; } = new();
}

public sealed class FoodWasteUnitOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Label => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} · {Name}";
}

public sealed class FoodWasteTotals
{
    public decimal ProductionWasteKg { get; set; }
    public decimal OverProductionWasteKg { get; set; }
    public decimal UnusedExpiredKg { get; set; }
    public decimal PlateScrapingsKg { get; set; }
    public decimal FoodSamplesKg { get; set; }
    public decimal BokashiWasteKg { get; set; }
    public decimal BioBinsKg { get; set; }
    public decimal OtherCompostingKg { get; set; }
    public decimal UsedOilLitres { get; set; }
    public int MainDishMeals { get; set; }
    public int UrbanFlavourMeals { get; set; }
    public int ChefsSignatureMeals { get; set; }
    public int VeggieMeals { get; set; }
    public int OtherMeals { get; set; }
    public int FunctionMeals { get; set; }
    public int FunctionPlatters { get; set; }
    public decimal TotalFoodWasteKg => ProductionWasteKg + OverProductionWasteKg + UnusedExpiredKg + PlateScrapingsKg + FoodSamplesKg;
    public decimal TotalDiversionKg => BokashiWasteKg + BioBinsKg + OtherCompostingKg;
    public int TotalMeals => MainDishMeals + UrbanFlavourMeals + ChefsSignatureMeals + VeggieMeals + OtherMeals + FunctionMeals + FunctionPlatters;
    public decimal WasteKgPerMeal => TotalMeals <= 0 ? 0 : TotalFoodWasteKg / TotalMeals;
}

public sealed class FoodWasteCategoryTotal
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = "kg";
    public int Percent { get; set; }
}
