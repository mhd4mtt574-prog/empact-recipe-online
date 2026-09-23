# Automatic Nutritional Analysis

The recipe module now supports ingredient-based nutritional analysis.

## Workflow

1. Open a recipe as an Administrator.
2. Choose **Generate / refresh nutrition** in the Nutritional analysis section.
3. The application searches USDA FoodData Central for any ingredient reference that is not already cached.
4. Matches are saved in the application's persistent `NutritionProfiles` data and reused by other recipes with the same APL code/name.
5. The recipe totals energy, protein, carbohydrate, total sugar, total fat, saturated fat, fibre and sodium and stores the result per finished portion.
6. The recipe screen displays the nutritional contribution of every ingredient, including the USDA reference match and whether review is required.

## Reference source

The lookup uses the USDA FoodData Central API. `appsettings.json` contains a `Nutrition` section. The included `DEMO_KEY` is suitable only for low-volume testing. Replace `FoodDataCentralApiKey` with a private data.gov API key for normal use.

## Important verification controls

- All automatically generated values remain **Estimated** until explicitly verified.
- Ingredient search matches below the confidence threshold are flagged for review.
- kg/g ingredient quantities can be converted directly.
- litre/ml ingredients use 1 g/ml unless a verified density is stored; these are flagged for review.
- EACH/BUNCH/PUNNET/LOAF/SLICE-style units require a grams-per-unit conversion. The system may infer a value from a simple pack-size such as `500G`, but this is still flagged for review.
- Unmapped or unconvertible ingredients are excluded rather than silently treated as zero, and the recipe shows ingredient coverage %.
- Supplier-specific product nutrition, preparation/cooking losses and finished yield can differ from generic reference data. Formal nutrition labels and therapeutic-diet decisions require appropriate verification.
