# Recipe corrections preservation update

- Live application data now uses the stable `%LOCALAPPDATA%\EmpactRecipeOnline\App_Data\app-data.json` location by default.
- When upgrading from an older project-folder data file, the newer data file is migrated to the stable location.
- Packaged recipe seed data only adds recipes that do not already exist; it does not overwrite administrator-maintained recipes.
- Administrator recipe saves are marked with correction metadata and remain intact through restarts and future application package upgrades.
- In Recipe Checks, the recipe name links directly to the recipe edit screen.
- Method, allergen and presentation/serving-instruction checks have been removed from corrective actions and approval checks.
