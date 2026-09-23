# Windows data permission fix

The application now selects a writable location for `app-data.json`.

1. It first checks the optional `EMPACT_RECIPE_DATA_FOLDER` environment variable.
2. It then checks the application's `App_Data` folder.
3. If that folder is protected, it falls back to:
   `%LOCALAPPDATA%\EmpactRecipeOnline\App_Data\app-data.json`

When falling back, the application attempts to copy any existing `App_Data\app-data.json` into the writable location so saved recipes and settings are retained.

Atomic saves also remove stale/read-only `.tmp` files and clear the read-only flag on the target data file before replacement.

The packaged seed files (`recipe-seed.json`, `apl-seed.json`, and `unit-seed.json`) remain in the application's `App_Data` directory and are not moved.
