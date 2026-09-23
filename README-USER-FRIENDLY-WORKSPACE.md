# User-friendly workspace update

This build adds the requested usability layer to Empact Recipe Online while preserving the existing recipe, resale pricing, food waste, reports, compliance, allergen and nutrition functions.

## Added

- **My Tasks** workspace with contextual due, reminder, attention and overdue tasks.
- **Dashboard task panel** and quick actions for Food Waste, tie-back, resale pricing and recipes.
- **Global search** from the top navigation across recipes, resale prices, promotions and major modules.
- **Favourites** for frequently used recipes and shortcuts.
- **Recently viewed/activity** list for the signed-in user.
- **Food Waste save-as-draft** workflow. Drafts are restored and do not count toward compliance until submitted.
- **Submission confirmation** with user and time, plus Food Waste data-quality warnings.
- **Food Waste Champion and training register** per unit.
- **Monthly Food Waste Reduction Plan** with issue, root cause, action, owner, due date, progress, status and evidence notes.
- **Food Waste audit evidence uploads** for used-oil certificates, training evidence, monthly waste reports, reduction-plan evidence and other support files.
- **Administrator attention queue** describing the exact unit/module issue requiring action.
- **Audit & change history** with filters for user/item text, unit, action and date range.
- **Print / Save PDF** controls added to key operational views; existing CSV/export functions remain.
- **Mobile/tablet UX improvements** with larger touch targets and responsive workspace, attention and evidence views.
- **Food Waste compliance refinement**: 70% daily tracker completion, 15% champion/training readiness, 15% current-month reduction plan.

## Existing data quality protections retained

- Nutrition coverage/review flags remain visible and administrator tasks surface incomplete nutrition coverage.
- Supplier allergen verification requirements are surfaced in administrator tasks.
- Food Waste submissions warn when meal/activity figures and waste totals appear internally inconsistent.

## Persistence

New records are stored in the existing JSON data store:

- UserFavourites
- UnitFoodWasteProfiles
- FoodWasteReductionPlans
- FoodWasteSupportDocuments

Uploaded Food Waste evidence is stored under the app's writable data folder in `FoodWasteUploads` so application upgrades do not need to overwrite evidence files.

## Important production note

This environment does not contain the .NET SDK, so the source has been structurally validated but not compiled here. Run `dotnet restore` and `dotnet run` on the Windows test machine before publishing.
