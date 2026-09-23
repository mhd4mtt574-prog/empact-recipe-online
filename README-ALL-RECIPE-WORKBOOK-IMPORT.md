# Multi-workbook recipe import — 24 July 2026

The application seed now includes usable recipes from the supplied chicken, fish, mutton/lamb, pork, salad, starch, and vegetarian costing workbooks.

- 242 recipes imported in this update.
- 2198 separate ingredient lines retained.
- All worksheet recipe sections were scanned; blank templates were ignored.
- Workbook ingredient rows remain separate; supplier/product lines are not combined.
- APL codes, pack sizes, prices, factors, quantities, units, cooking notes, portions, categories, and recipe waste are retained.
- Missing item codes are replaced only when a similar active APL product is found and recorded in `App_Data/all-recipe-import-substitutions.json`.
- Existing beef recipes and administrator-maintained recipes are preserved.
- Only Administrators can add, edit, cost, link APL products, or change recipes. Unit users can view approved recipes, scale displayed portions, and print.
