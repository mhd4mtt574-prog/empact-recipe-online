# Beef workbook import — 24 July 2026

The application seed now includes all usable recipes from the workbook tabs **BEEF**, **BEEF 2**, and **BEEF 3**.

- 51 approved recipes imported.
- Workbook ingredient rows remain separate; supplier/product lines are not combined.
- APL codes, pack sizes, prices, factors, quantities, units, cooking notes, portions, categories, and recipe waste are retained.
- Missing item codes are replaced with the closest active APL product and recorded in `App_Data/beef-import-substitutions.json`.
- Seed recipes are merged by stable recipe code. Existing administrator-maintained recipes are preserved and are not overwritten.
- Only Administrators can add, edit, cost, link APL products, or change recipes. Unit users can view approved recipes, scale displayed portions, and print.
