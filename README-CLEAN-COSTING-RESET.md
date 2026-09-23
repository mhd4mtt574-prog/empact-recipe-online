# Clean costing baseline reset

This build intentionally removes all legacy recipes on first startup and starts with an empty recipe library.

New recipes use the attached `Clean costing sheet 2026` structure:

- APL Code
- Product
- Cooking Notes
- Pack Size
- Purchase Price
- Factor
- Standard Quantity
- Unit of Measure
- Batch Issue
- Waste Factor
- Batch Cost
- Cost per Portion

Formulas:

- Batch Issue = Standard Quantity / Standard Portion Basis × Required Portions
- Waste Factor = (Batch Issue × Purchase Price / Factor) × Waste Rate
- Batch Cost = (Batch Issue × Purchase Price / Factor) + Waste Factor
- Cost per Portion = Batch Cost / Required Portions

The one-time marker `App_Data/clean-costing-baseline-v1.marker` prevents later administrator-created recipes from being cleared again.
