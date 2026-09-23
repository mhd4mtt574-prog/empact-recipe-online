using System.Text.Json;
using EmpactRecipeOnline.Models;

namespace EmpactRecipeOnline.Services;

public sealed class ResalePricingService
{
    private readonly ResalePricingData _data;

    public ResalePricingService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "resale-pricing.json");
        var json = File.ReadAllText(path);
        _data = JsonSerializer.Deserialize<ResalePricingData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Resale pricing data could not be loaded.");
    }

    public string PricingSourceWorkbook => _data.SourcePricingWorkbook;

    public ResaleAccessIdentity? ResolveAccess(string idNumber)
    {
        var id = NormalizeId(idNumber);
        var senior = _data.SeniorManagers.FirstOrDefault(x => NormalizeId(x.IdNumber) == id);
        if (senior is not null)
            return new ResaleAccessIdentity { IdNumber = id, Name = senior.Name.Trim(), IsSeniorManager = true };

        var matches = _data.UnitManagers
            .Where(x => NormalizeId(x.IdNumber) == id && x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0) return null;

        return new ResaleAccessIdentity
        {
            IdNumber = id,
            Name = matches[0].Name.Trim(),
            UnitCodes = matches.Select(x => x.UnitCode.Trim()).Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public List<ResaleUnit> GetUnits(ResaleAccessIdentity access)
    {
        IEnumerable<ResaleUnit> units = access.IsSeniorManager
            ? _data.Units
            : _data.Units.Where(x => access.UnitCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase));
        return units.OrderBy(x => x.Name).ThenBy(x => x.Code).ToList();
    }

    public bool CanAccessUnit(ResaleAccessIdentity access, string unitCode) =>
        access.IsSeniorManager || access.UnitCodes.Contains(unitCode, StringComparer.OrdinalIgnoreCase);

    public ResaleUnit? GetUnit(string unitCode) =>
        _data.Units.FirstOrDefault(x => x.Code.Equals(unitCode, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ResaleUnit> GetAllUnits() => _data.Units;

    public IReadOnlyList<ResaleCategoryPrice> GetPricesForCategory(string category)
    {
        return category.Trim().Equals("B", StringComparison.OrdinalIgnoreCase)
            ? _data.CategoryBPrices
            : _data.CategoryAPrices;
    }

    private static string NormalizeId(string? id)
    {
        var digits = new string((id ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length is > 0 and < 13 ? digits.PadLeft(13, '0') : digits;
    }
}
