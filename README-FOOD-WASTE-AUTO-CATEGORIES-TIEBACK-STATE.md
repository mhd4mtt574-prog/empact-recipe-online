# Food Waste automatic category roll-up & tie-back upload state

## Food Waste
- Workstation Waste Record lines are now the source of truth for daily food-waste category totals.
- PW -> Production Waste
- OPW -> Over Production Waste
- UE -> Unused / Expired
- PS -> Plate Scrapings
- FS -> Food Samples
- The category summary updates live in the browser and is recalculated server-side on both draft save and final submission.
- Category total fields are read-only to avoid double capture and inconsistent totals.

## Daily tie-back
- Once today's tie-back has been uploaded, the upload prompt is replaced with a clear "sheet is loaded" confirmation.
- The loaded file can be downloaded.
- A deliberate "Replace today's sheet" control remains available if the wrong file was uploaded.
- The existing upload record continues to drive daily tie-back compliance.
