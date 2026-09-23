# Recipe ingredient to APL linking

This version links individual recipe ingredient lines to a specific regional Approved Product List record.

- Recipes use CT, JHB or KZN according to their assigned unit.
- Open a recipe and select **Link APL items**.
- Select an ingredient, search the regional APL, and select **Link product**.
- The APL product ID, product code, division, product name, unit and current cost are stored on the ingredient.
- Future APL cost updates use the exact linked product ID and automatically recalculate the recipe.
- Existing lines are automatically linked only when there is one unambiguous exact code or exact product-name match.
