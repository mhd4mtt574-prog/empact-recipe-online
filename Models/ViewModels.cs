using System.ComponentModel.DataAnnotations;

namespace EmpactRecipeOnline.Models;

public sealed class LoginViewModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public sealed class RegisterViewModel
{
    [Required] public string DisplayName { get; set; } = string.Empty;
    [Required] public string Username { get; set; } = string.Empty;
    [Required, MinLength(8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
    [Required] public string Unit { get; set; } = string.Empty;
}

public sealed class RecipeEditViewModel
{
    public Guid? Id { get; set; }
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Category { get; set; } = "Other";
    [Required] public string Department { get; set; } = "Vegetables";
    [Required] public string Unit { get; set; } = "Head Office";
    [Range(0, 100000)] public int BasePortions { get; set; }
    [Range(1, 100000)] public int StandardPortions { get; set; } = 10;
    [Range(0, 100)] public decimal WastePercent { get; set; } = 5;
    [Range(0, 99.99)] public decimal DesiredMarginPercent { get; set; } = 65;
    public string Method { get; set; } = string.Empty;
    public string ServingInstructions { get; set; } = string.Empty;
    public string ExistingImageUrl { get; set; } = string.Empty;
    public IFormFile? ImageFile { get; set; }
    public string IngredientsText { get; set; } = string.Empty;
    public string AllergensText { get; set; } = string.Empty;
    public string MayContainText { get; set; } = string.Empty;
    public bool SupplierVerificationRequired { get; set; }
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
    public string SpecialDietsText { get; set; } = string.Empty;
    public bool DietitianApprovalRequired { get; set; }
    public bool IsApproved { get; set; }
}


public sealed class RecipeAplLinkViewModel
{
    public Recipe Recipe { get; set; } = new();
    public int IngredientIndex { get; set; }
    public string Search { get; set; } = string.Empty;
    public string Division { get; set; } = "CT";
    public List<ApprovedProduct> Matches { get; set; } = new();
}

public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8), DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
}
