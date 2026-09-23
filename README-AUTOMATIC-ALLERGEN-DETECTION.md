# Automatic allergen detection

The application now re-scans allergens whenever a recipe is saved and every time a regional recipe is displayed.

Detection combines:

- exact regional Approved Product List allergen declarations;
- ingredient-name keyword detection;
- "may contain" declarations from regional products; and
- supplier-label verification warnings for compound ingredients such as sauces, stocks, seasonings and premixes.

The recipe view shows which ingredient and data source triggered every warning. Administrators can retain or add confirmed declarations manually. A recipe is never declared allergen-free solely because the scan found no keyword match; the user is reminded to verify supplier labels.

Regional behaviour is preserved. Gauteng, Cape Town and KwaZulu-Natal recipes are scanned against the products selected for that region.
