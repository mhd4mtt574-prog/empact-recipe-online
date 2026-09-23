using EmpactRecipeOnline.Models;

namespace EmpactRecipeOnline.Services;

public sealed class AllergenService
{
    private static readonly Dictionary<string, string[]> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gluten (wheat)"] = ["wheat", "flour", "bread", "breadcrumbs", "pasta", "noodle", "couscous", "bulgur", "barley", "rye", "oats", "pita", "wrap", "tortilla", "soy sauce"],
        ["Egg"] = ["egg", "mayonnaise", "mayo"],
        ["Milk"] = ["milk", "cream", "butter", "cheese", "feta", "yoghurt", "yogurt", "whey", "casein"],
        ["Peanuts"] = ["peanut", "groundnut"],
        ["Tree nuts"] = ["almond", "cashew", "walnut", "pecan", "hazelnut", "pistachio", "macadamia", "brazil nut", "pine nut"],
        ["Soy"] = ["soy", "soya", "tofu", "edamame", "tempeh"],
        ["Fish"] = ["fish", "hake", "salmon", "tuna", "anchovy", "sardine", "cod", "trout"],
        ["Crustaceans"] = ["prawn", "shrimp", "crab", "lobster", "crayfish"],
        ["Molluscs"] = ["mussel", "oyster", "squid", "calamari", "octopus", "clam", "scallop"],
        ["Sesame"] = ["sesame", "tahini"],
        ["Mustard"] = ["mustard"],
        ["Celery"] = ["celery", "celeriac"],
        ["Lupin"] = ["lupin"],
        ["Sulphites"] = ["wine", "vinegar", "sulphite", "sulfite", "dried fruit"]
    };

    private static readonly string[] VerificationTerms = ["stock", "gravy", "seasoning", "spice", "sauce", "marinade", "dressing", "processed", "sausage", "bacon", "ham", "premix", "powder"];

    public AllergenDetectionResult DetectDetailed(IEnumerable<IngredientLine> ingredients, IEnumerable<ApprovedProduct>? approvedProducts = null)
    {
        var productById = (approvedProducts ?? Enumerable.Empty<ApprovedProduct>()).ToDictionary(x => x.Id);
        var findings = new List<AllergenDetectionSource>();

        foreach (var ingredient in ingredients)
        {
            var ingredientName = (ingredient.Name ?? string.Empty).Trim();
            var searchable = $"{ingredientName} {ingredient.CookingNotes}";

            foreach (var rule in Rules)
            {
                var matchedKeyword = rule.Value.FirstOrDefault(keyword => ContainsWholeOrPhrase(searchable, keyword));
                if (matchedKeyword is null) continue;
                findings.Add(new AllergenDetectionSource
                {
                    Allergen = rule.Key,
                    IngredientName = ingredientName,
                    DetectionType = "Ingredient name",
                    Detail = $"Matched '{matchedKeyword}'"
                });
            }

            ApprovedProduct? product = null;
            if (ingredient.ApprovedProductId.HasValue)
                productById.TryGetValue(ingredient.ApprovedProductId.Value, out product);

            if (product is not null)
            {
                foreach (var allergen in product.Allergens.Where(x => !string.IsNullOrWhiteSpace(x)))
                    findings.Add(new AllergenDetectionSource { Allergen = allergen.Trim(), IngredientName = ingredientName, DetectionType = "Regional APL product", Detail = $"{product.ProductName} ({product.ProductCode})" });
                foreach (var allergen in product.MayContainAllergens.Where(x => !string.IsNullOrWhiteSpace(x)))
                    findings.Add(new AllergenDetectionSource { Allergen = allergen.Trim(), IngredientName = ingredientName, DetectionType = "Regional APL product", Detail = $"{product.ProductName} ({product.ProductCode})", IsMayContain = true });
                if (product.AllergenVerificationRequired)
                    findings.Add(new AllergenDetectionSource { Allergen = "Supplier label verification", IngredientName = ingredientName, DetectionType = "Regional APL product", Detail = $"{product.ProductName} requires label verification", RequiresVerification = true });
            }

            var verificationMatch = VerificationTerms.FirstOrDefault(term => ContainsWholeOrPhrase(searchable, term));
            if (verificationMatch is not null)
                findings.Add(new AllergenDetectionSource { Allergen = "Supplier label verification", IngredientName = ingredientName, DetectionType = "Compound ingredient", Detail = $"Matched '{verificationMatch}'", RequiresVerification = true });
        }

        findings = findings
            .GroupBy(x => $"{x.Allergen}|{x.IngredientName}|{x.DetectionType}|{x.IsMayContain}|{x.RequiresVerification}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Allergen).ThenBy(x => x.IngredientName)
            .ToList();

        return new AllergenDetectionResult
        {
            Allergens = findings.Where(x => !x.IsMayContain && !x.RequiresVerification).Select(x => x.Allergen).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            MayContainAllergens = findings.Where(x => x.IsMayContain).Select(x => x.Allergen).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            ReviewRequired = findings.Any(x => x.RequiresVerification),
            Sources = findings
        };
    }

    public (List<string> Allergens, bool ReviewRequired) Detect(IEnumerable<IngredientLine> ingredients)
    {
        var result = DetectDetailed(ingredients);
        return (result.Allergens, result.ReviewRequired);
    }

    private static bool ContainsWholeOrPhrase(string value, string keyword)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(keyword)) return false;
        var normalizedValue = $" {Normalize(value)} ";
        var normalizedKeyword = Normalize(keyword);
        return normalizedValue.Contains($" {normalizedKeyword} ", StringComparison.OrdinalIgnoreCase)
            || normalizedValue.Contains($" {normalizedKeyword}s ", StringComparison.OrdinalIgnoreCase)
            || normalizedValue.Contains($" {normalizedKeyword}ed ", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
