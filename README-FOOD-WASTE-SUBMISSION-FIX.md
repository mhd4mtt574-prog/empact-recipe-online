# Food Waste submission reliability fix

- Submit and Save Draft buttons now post to explicit FoodWaste endpoints.
- Unit resolution accepts unit code, unit name, display label, stored UnitCode, stored Unit, and username (useful for unit-code usernames such as S0490).
- Successful daily submissions show a persistent “Today’s Food Waste record is submitted” state with submitter/time.
- Existing Food Waste totals, draft flow, compliance, workstation category roll-up, and reporting remain intact.
