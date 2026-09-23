# Costing formula alignment

The recipe costing engine now follows `Clean costing sheet 2026.xlsm`:

1. `Batch issue = Standard quantity / Base portions * Required portions`
2. `Base cost = Batch issue * Purchase price / Factor`
3. `Waste factor = Base cost * Recipe waste percentage`
4. `Batch cost = Base cost + Waste factor`
5. `Cost per portion = Batch cost / Required portions`
6. Recipe totals are the sum of all ingredient batch costs and cost-per-portion values.

The ingredient editor format is:

`Product Code | Product | Standard Quantity | Unit | Purchase Price | Factor`

Existing recipes with no BasePortions value use their current StandardPortions as the base, preserving the original quantities. Missing or zero factors are treated as 1.
