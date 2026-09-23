using EmpactRecipeOnline.Models;

namespace EmpactRecipeOnline.Services;

public static class RecipeCheckService
{
    public static RecipeCheckResult Evaluate(Recipe r, IReadOnlyCollection<Recipe> recipes, IReadOnlyCollection<ApprovedProduct> products)
    {
        var result = new RecipeCheckResult { RecipeId=r.Id, Recipe=r.Name, Code=r.Code, Department=r.Department, IsApproved=r.IsApproved, UpdatedAtUtc=r.UpdatedAtUtc };
        void Add(string code, string check, string message, RecipeCheckSeverity severity) => result.Issues.Add(new(){Code=code,Check=check,Message=message,Severity=severity});

        if (string.IsNullOrWhiteSpace(r.Name)) Add("RC001","Recipe identity","Recipe name is missing.",RecipeCheckSeverity.Critical);
        if (string.IsNullOrWhiteSpace(r.Code)) Add("RC002","Recipe identity","Recipe code is missing.",RecipeCheckSeverity.Critical);
        if (!RecipeDepartments.All.Contains(r.Department)) Add("RC003","Department","A valid costing department is required.",RecipeCheckSeverity.Critical);
        if (r.StandardPortions <= 0) Add("RC004","Portions","Standard portions must be greater than zero.",RecipeCheckSeverity.Critical);
        if (r.Ingredients.Count == 0) Add("RC006","Ingredients","The recipe has no ingredient lines.",RecipeCheckSeverity.Critical);

        foreach (var (i,index) in r.Ingredients.Select((x,i)=>(x,i+1)))
        {
            var line=$"Ingredient line {index} ({i.Name})";
            if (string.IsNullOrWhiteSpace(i.Name)) Add("RC010","Ingredient completeness",$"Ingredient line {index} has no name.",RecipeCheckSeverity.Critical);
            if (i.Quantity <= 0) Add("RC011","Ingredient quantity",$"{line} has a zero or negative quantity.",RecipeCheckSeverity.Critical);
            if (string.IsNullOrWhiteSpace(i.Unit)) Add("RC012","Ingredient unit",$"{line} has no unit of measure.",RecipeCheckSeverity.Critical);
            if (i.UnitCost <= 0) Add("RC013","Ingredient price",$"{line} has a zero or negative purchase price.",RecipeCheckSeverity.Critical);
            if (string.IsNullOrWhiteSpace(i.ProductCode)) Add("RC014","APL code",$"{line} has no APL product code.",RecipeCheckSeverity.Warning);
            if (!i.IsAplLinked) Add("RC015","APL linking",$"{line} is not linked to an approved product.",RecipeCheckSeverity.Warning);
            if (i.Quantity > 10000) Add("RC016","Quantity reasonableness",$"{line} has an unusually high quantity ({i.Quantity:N2} {i.Unit}).",RecipeCheckSeverity.Warning);
            if (i.UnitCost > 100000) Add("RC017","Cost reasonableness",$"{line} has an unusually high purchase price (R{i.UnitCost:N2}).",RecipeCheckSeverity.Warning);
            if (i.ApprovedProductId is Guid pid)
            {
                var p=products.FirstOrDefault(x=>x.Id==pid);
                if (p is null) Add("RC018","Approved product",$"{line} links to a product that no longer exists.",RecipeCheckSeverity.Critical);
                else if (!p.IsActive) Add("RC019","Approved product",$"{line} uses a discontinued or inactive product.",RecipeCheckSeverity.Critical);
                else
                {
                    var comparable=products.Where(x=>x.IsActive && !string.IsNullOrWhiteSpace(x.ProductName) && x.ProductName.Contains(i.Name,StringComparison.OrdinalIgnoreCase) && x.UnitCost>0).OrderBy(x=>x.UnitCost).FirstOrDefault();
                    if (comparable is not null && comparable.UnitCost < p.UnitCost * 0.95m) Add("RC020","Best value",$"A lower-cost comparable approved product may be available for {i.Name}: {comparable.ProductName} at R{comparable.UnitCost:N2}.",RecipeCheckSeverity.Warning);
                }
            }
        }

        foreach (var duplicate in r.Ingredients.Where(x=>!string.IsNullOrWhiteSpace(x.Name)).GroupBy(x=>x.Name.Trim(),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))
            Add("RC021","Duplicate ingredients",$"{duplicate.Key} appears {duplicate.Count()} times.",RecipeCheckSeverity.Warning);

        if (r.CostPerPortion <= 0) Add("RC030","Cost per portion","Cost per portion is zero or invalid.",RecipeCheckSeverity.Critical);
        if (r.CostPerPortion > 500) Add("RC031","Cost per portion",$"Cost per portion is unusually high at R{r.CostPerPortion:N2}.",RecipeCheckSeverity.Warning);
        if (r.DesiredMarginPercent < 0 || r.DesiredMarginPercent >= 100) Add("RC032","GP margin","Desired GP margin must be between 0% and 99.99%.",RecipeCheckSeverity.Critical);
        if (r.DesiredMarginPercent == 0) Add("RC033","GP margin","Desired GP margin has not been meaningfully set.",RecipeCheckSeverity.Warning);
        if (r.RecommendedSellingPrice > 0 && r.RecommendedSellingPrice < r.CostPerPortion) Add("RC034","Selling price","Required selling price is below cost per portion.",RecipeCheckSeverity.Critical);
        if (r.UpdatedAtUtc < DateTime.UtcNow.AddMonths(-12)) Add("RC042","Review date","Recipe has not been reviewed in the last 12 months.",RecipeCheckSeverity.Warning);
        var similar=recipes.FirstOrDefault(x=>x.Id!=r.Id && (x.Code.Equals(r.Code,StringComparison.OrdinalIgnoreCase) || x.Name.Equals(r.Name,StringComparison.OrdinalIgnoreCase)));
        if (similar is not null) Add("RC043","Duplicate recipe",$"Possible duplicate recipe: {similar.Code} – {similar.Name}.",RecipeCheckSeverity.Warning);
        if (r.BasePortions > 0 && r.StandardPortions > r.BasePortions*5) Add("RC044","Portion variance","Required portions are more than five times the base recipe portions.",RecipeCheckSeverity.Warning);
        if (result.Issues.Count==0) Add("PASS","All checks","All configured recipe checks passed.",RecipeCheckSeverity.Passed);
        return result;
    }
}
