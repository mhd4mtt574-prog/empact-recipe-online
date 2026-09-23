# Administrator Compliance Dashboard

A new administrator-only Compliance Dashboard has been added under Administration.

## Drill-down workflow
- Organisation view: Region = All, Unit = All.
- Region view: select a region; the Unit dropdown is rebuilt to contain only units in that region.
- Unit view: select a unit to refresh the whole dashboard for that unit only.
- Date range filters apply to activity-based compliance measures.

## Visuals
- KPI cards: overall compliance, compliant units, attention units, outstanding units, last activity.
- Compliance by module progress bars.
- Compliance status donut.
- Unit/module heatmap.
- Top units requiring attention table.
- Six-month compliance trend.
- Compliance by region bars.

## Current compliance rules
The dashboard intentionally uses saved application data rather than fabricated scores:
- Resale pricing: 100% once the unit viewed its resale price list in the selected period.
- Recipe costing: 50% for recipe-view or margin activity; 100% when both are present.
- Allergens: percentage of the unit's recipes not requiring supplier verification.
- Reports & tie-back: percentage of weekdays in the selected period with a tie-back upload.
- Recipe adjustments: percentage of the unit's recipes currently approved.
- Promotions: percentage of published promotions overlapping the period with recorded unit activity; 100% if none apply.

These rules are centralised in `OperationsController.BuildComplianceRows` and can be revised as Empact finalises formal compliance definitions.
