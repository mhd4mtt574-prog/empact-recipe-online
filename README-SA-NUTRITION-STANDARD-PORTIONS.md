# Standard portions and South African nutrition

- All master recipes are now standardised to **10 portions** at seed level and at application start-up.
- Nutrition analysis is always calculated for **1 finished portion**, irrespective of the temporary costing/print portion scaler.
- The ingredient nutrition breakdown therefore shows the **estimated ingredient weight for 1 portion**, not the 10-portion master batch.
- Nutrition matching is **SAFOODS-first** when a licensed South African nutrition export is supplied as `App_Data/safoods-nutrition.csv`.
- A header-only template is included as `App_Data/safoods-nutrition-template.csv`.
- USDA FoodData Central can remain as an optional fallback for ingredients not available in the local SAFOODS file. Those rows are labelled with their source.
- No SAMRC/SAFOODS composition records are bundled in this project. Use of SAFOODS data in another product requires the appropriate SAMRC permission/licence.
