# Resale Pricing — Rebuilt category workflow

The resale tool has been rebuilt from scratch around the supplied reference files.

## Sources
- `App requested file (2).xlsx`: authorised IDs, unit allocations and each unit's Category A/B allocation.
- `Pricing A B.xlsx`: authoritative Category A and Category B current nett pricing and selling prices.
- `Recommended Resale Selling Prices FY26 - MASTER catagory lookup(1).csv`: workflow/layout reference.

## Unit workflow
1. The user opens **Value Added → Resale pricing**.
2. The user enters their 13-digit ID number.
3. The ID is matched to the active Unit Managers list.
4. The app identifies the user's allocated unit(s).
5. The unit's Category A or B allocation is read from the Units sheet.
6. The matching `Catagory A` or `Catagory B` price list opens automatically.
7. The list is read-only and shows the item, product/pack, current nett each cost and selling price.

No cost entry, selling-price entry, GP calculation, monthly confirmation or month-lock workflow is used in this rebuilt tool.

Senior managers remain authorised and can view a specific unit or switch directly between Category A and Category B.

Resale list access continues to be written to the application's activity log for audit purposes.
