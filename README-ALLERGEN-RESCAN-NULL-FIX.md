# Allergen rescan null fix

## Issue
Rescanning allergens from the recipe edit screen could throw a `System.NullReferenceException` in `RecipesController.SplitCsv` when one of the editable CSV fields (Allergens, May Contain Allergens, or Special Diets) was posted as `null`.

## Fix
`SplitCsv` now accepts a nullable string and converts null to `string.Empty` before splitting:

```csharp
private static List<string> SplitCsv(string? value) =>
    (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x)
        .ToList();
```

This keeps empty fields valid during allergen detection/rescanning and preserves the existing distinct/sorted behaviour.

## Validation
Source-level validation completed. The build environment used to package this project does not have the .NET SDK installed, so run `dotnet restore` and `dotnet build`/`dotnet run` on the Windows development machine before deployment.
