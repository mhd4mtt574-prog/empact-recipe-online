using EmpactRecipeOnline.Models;

namespace EmpactRecipeOnline.Services;

public static class RecipeMediaService
{
    private static readonly string[] ManagedDefaultMediaPrefixes =
    [
        "/images/recipes/",
        "/images/login-background",
        "/images/login-static-food",
        "/images/login-clear-food",
        "/images/login-single-food",
        "/images/hero-food",
        "/images/default-food",
        "/images/food-background"
    ];

    public static bool IsVideo(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
         url.EndsWith(".webm", StringComparison.OrdinalIgnoreCase));

    public static bool IsManagedDefaultMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var normalized = url.Split('?', '#')[0].Replace('\\', '/').ToLowerInvariant();
        return ManagedDefaultMediaPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetDefaultMediaForRecipe(Recipe recipe)
        => GetDefaultMediaFor(recipe.Name, recipe.Category);

    public static string GetDefaultMediaFor(string? name, string? category)
    {
        var text = $"{name} {category}".ToLowerInvariant();

        if (ContainsAny(text, "salad", "slaw", "coleslaw", "greek")) return "/images/recipes/salads.png";
        if (ContainsAny(text, "soup", "broth", "bisque", "chowder")) return "/images/recipes/soups.png";
        if (ContainsAny(text, "rice", "pilaf", "couscous", "quinoa", "grain", "barley")) return "/images/recipes/grains.png";
        if (ContainsAny(text, "pasta", "spaghetti", "lasagne", "lasagna", "noodle", "macaroni", "penne", "linguine", "fettuccine", "potato", "chips", "wedges", "mash")) return "/images/recipes/starches.png";
        if (ContainsAny(text, "vegetable", "broccoli", "cauliflower", "spinach", "butternut", "carrot", "bean", "peas", "zucchini", "baby marrow")) return "/images/recipes/vegetables.png";
        if (ContainsAny(text, "fish", "salmon", "hake", "tuna", "snoek", "cod", "kingklip", "prawn", "shrimp", "mussel", "calamari")) return "/images/recipes/fish.png";
        if (ContainsAny(text, "chicken")) return "/images/recipes/chicken.png";
        if (ContainsAny(text, "pork", "bacon", "ham")) return "/images/recipes/pork.png";
        if (ContainsAny(text, "lamb", "mutton")) return "/images/recipes/lamb-mutton.png";
        if (ContainsAny(text, "beef", "steak", "mince", "meatball", "burger", "oxtail")) return "/images/recipes/beef.png";
        if (ContainsAny(text, "vegetarian", "vegan", "falafel", "lentil", "chickpea", "feta")) return "/images/recipes/vegetarian.png";

        return (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "beef" => "/images/recipes/beef.png",
            "chicken" => "/images/recipes/chicken.png",
            "fish" => "/images/recipes/fish.png",
            "pork" => "/images/recipes/pork.png",
            "lamb & mutton" => "/images/recipes/lamb-mutton.png",
            "lamb and mutton" => "/images/recipes/lamb-mutton.png",
            "vegetarian" => "/images/recipes/vegetarian.png",
            "salads" => "/images/recipes/salads.png",
            "soups" => "/images/recipes/soups.png",
            "starches" => "/images/recipes/starches.png",
            "vegetables" => "/images/recipes/vegetables.png",
            "grains" => "/images/recipes/grains.png",
            _ => "/images/recipes/other.png"
        };
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(text.Contains);
}
