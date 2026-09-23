# Food Waste Programme Module

Added a standalone Food Waste section to the Food by Empact Group app.

## Included source documents
The following uploaded files are stored server-side in `App_Data/FoodWasteProgram` and can be opened by authenticated users from the Food Waste page:

- `PROC40-Food-Waste-Management.pdf`
- `F14a-Workstation-Waste-Record-Form.pdf`
- `F14b-Food-Waste-Tracker-Template.xlsb`
- `Food-Waste-Training-June-2026.pdf`

## Unit workflow
1. Unit user opens **Food Waste** from the top navigation.
2. The page is scoped to that user's unit where possible.
3. User completes the daily combined F14a/F14b submission:
   - Date, workstation and tare weight
   - Food waste categories: Production Waste, Over Production Waste, Unused/Expired, Plate Scrapings, Food Samples
   - Workstation record lines with type, description, weight and employee name
   - Waste diversion: Bokashi, Bio Bins, other composting
   - Meals produced and used-oil litres/supplier
   - Function flag and notes
4. Submission replaces any previous submission for the same unit/date so the latest record is the official one.
5. Activity is logged as `FoodWasteSubmitted`.

## Dashboard and compliance
- Food Waste is now a top-level section alongside Recipes and Resale Pricing.
- The main dashboard includes a Food Waste preview card.
- Administrator Compliance Dashboard now includes Food Waste as a module.
- Food Waste compliance is calculated as daily tracker submissions divided by expected weekdays for the selected period.
- Food Waste page includes wheel/donut visuals for:
  - Food waste by category
  - Waste diversion
  - Meals produced
- Monthly totals are shown for all key sections.

## Suggestions implemented as placeholders
The page includes suggested next enhancements for:
- Food Waste Champion register
- Corrective actions for missed submissions or high waste
- Training evidence tracking
- Audit document links for used-oil certificates and monthly support files
