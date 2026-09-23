using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class RecipeAplLinksController(JsonDataStore store) : Controller
{
    public IActionResult Index(Guid recipeId, int ingredientIndex = 0, string? q = null)
    {
        var recipe = store.Data.Recipes.FirstOrDefault(x => x.Id == recipeId);
        if (recipe is null) return NotFound();
        if (recipe.Ingredients.Count == 0) return RedirectToAction("Edit", "Recipes", new { id = recipeId });

        ingredientIndex = Math.Clamp(ingredientIndex, 0, recipe.Ingredients.Count - 1);
        var ingredient = recipe.Ingredients[ingredientIndex];
        q ??= string.IsNullOrWhiteSpace(ingredient.ProductCode) || ingredient.ProductCode.StartsWith("APL-", StringComparison.OrdinalIgnoreCase)
            ? ingredient.Name
            : ingredient.ProductCode;
        var division = JsonDataStore.GetDivisionForUnit(recipe.Unit);

        return View(new RecipeAplLinkViewModel
        {
            Recipe = recipe,
            IngredientIndex = ingredientIndex,
            Search = q ?? string.Empty,
            Division = division,
            Matches = store.SearchApprovedProducts(division, q, 75).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Link(Guid recipeId, int ingredientIndex, Guid productId)
    {
        if (!store.LinkRecipeIngredient(recipeId, ingredientIndex, productId, User.Identity?.Name ?? "Unknown"))
        {
            TempData["Error"] = "The ingredient could not be linked. Confirm that the APL product belongs to the same regional division as the recipe.";
            return RedirectToAction(nameof(Index), new { recipeId, ingredientIndex });
        }
        TempData["Success"] = "The ingredient was linked to the Approved Product List and the recipe cost was recalculated.";
        return RedirectToAction(nameof(Index), new { recipeId, ingredientIndex });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Unlink(Guid recipeId, int ingredientIndex)
    {
        store.UnlinkRecipeIngredient(recipeId, ingredientIndex, User.Identity?.Name ?? "Unknown");
        TempData["Success"] = "The ingredient was unlinked from the Approved Product List.";
        return RedirectToAction(nameof(Index), new { recipeId, ingredientIndex });
    }
}
