using System.Security.Claims;
using EmpactRecipeOnline.Models;

namespace EmpactRecipeOnline.Services;

public sealed class WorkspaceService(JsonDataStore store, ResalePricingService resalePricing)
{
    public AppUser? GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? store.Data.Users.FirstOrDefault(x => x.Id == userId) : null;
    }

    public List<WorkspaceTask> GetTasks(ClaimsPrincipal principal)
    {
        var user = GetCurrentUser(principal);
        if (user is null) return new();
        var tasks = new List<WorkspaceTask>();
        var isAdmin = principal.IsInRole("Administrator");
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var resolvedUnit = (!string.IsNullOrWhiteSpace(user.UnitCode) ? resalePricing.GetUnit(user.UnitCode) : null) ?? resalePricing.GetAllUnits().FirstOrDefault(x => x.Name.Equals(user.Unit ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        var unitCode = resolvedUnit?.Code ?? user.UnitCode ?? string.Empty;
        var unitName = resolvedUnit?.Name ?? user.Unit ?? string.Empty;
        bool UnitMatch(string? code, string? name) => (!string.IsNullOrWhiteSpace(unitCode) && string.Equals(code, unitCode, StringComparison.OrdinalIgnoreCase)) || string.Equals(name, unitName, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && today.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
        {
            var wasteSubmitted = store.Data.FoodWasteRecords.Any(x => !x.IsDraft && x.BusinessDate.Date == today && UnitMatch(x.UnitCode, x.UnitName));
            if (!wasteSubmitted) tasks.Add(new WorkspaceTask { Key="food-waste-today", Title="Submit today’s Food Waste record", Detail="The daily Food Waste Tracker has not been submitted for your unit.", Module="Food Waste", Severity="Due", Url="/FoodWaste", DueDate=today });

            var tieBackSubmitted = store.Data.DailyTieBackUploads.Any(x => x.BusinessDate.Date == today && TieBackUnitMatcher.Matches(x.Unit, unitCode, unitName, user.Unit));
            if (!tieBackSubmitted) tasks.Add(new WorkspaceTask { Key="tieback-today", Title="Upload today’s tie-back", Detail="Daily reports & tie-back evidence is still outstanding.", Module="Reports", Severity=DateTime.Now.TimeOfDay >= new TimeSpan(9,0,0) ? "Overdue" : "Due", Url="/Operations/Reports#tieback", DueDate=today, IsOverdue=DateTime.Now.TimeOfDay >= new TimeSpan(9,0,0) });
        }

        if (!isAdmin)
        {
            var ids = new HashSet<Guid> { user.Id };
            var resaleViewed = store.Data.ActivityLogs.Any(x => x.OccurredAtUtc >= monthStart.ToUniversalTime() && ids.Contains(x.UserId ?? Guid.Empty) && x.Action == "ResalePriceListViewed");
            if (!resaleViewed) tasks.Add(new WorkspaceTask { Key="resale-month", Title="Review this month’s resale prices", Detail="Open the Category A/B resale price list allocated to your unit.", Module="Resale Pricing", Severity="Reminder", Url="/ResalePricing" });

            var activePromos = store.Data.Promotions.Where(x => x.IsPublished && x.StartDate.Date <= today && x.EndDate.Date >= today).ToList();
            var unseen = activePromos.Count(p => !store.Data.ActivityLogs.Any(x => x.UserId == user.Id && x.EntityType == "Promotion" && x.EntityId == p.Id));
            if (unseen > 0) tasks.Add(new WorkspaceTask { Key="promotions", Title=$"Review {unseen} active promotion{(unseen==1?"":"s")}", Detail="There are published promotions your account has not opened yet.", Module="Promotions", Severity="Reminder", Url="/Promotions" });

            var profile = store.Data.UnitFoodWasteProfiles.FirstOrDefault(x => x.UnitCode.Equals(unitCode, StringComparison.OrdinalIgnoreCase));
            if (profile is null || string.IsNullOrWhiteSpace(profile.ChampionName)) tasks.Add(new WorkspaceTask { Key="waste-champion", Title="Appoint a Food Waste Champion", Detail="Record the champion and backup for your unit.", Module="Food Waste", Severity="Attention", Url="/FoodWaste#champion" });
            else if (!profile.TrainingCompletedDate.HasValue || (profile.RefresherDueDate.HasValue && profile.RefresherDueDate.Value.Date <= today.AddDays(30))) tasks.Add(new WorkspaceTask { Key="waste-training", Title="Food Waste training needs attention", Detail="Capture training completion or an upcoming refresher date for the Food Waste Champion.", Module="Food Waste", Severity="Attention", Url="/FoodWaste#champion" });

            var plan = store.Data.FoodWasteReductionPlans.Where(x => x.UnitCode.Equals(unitCode, StringComparison.OrdinalIgnoreCase) && x.ReviewMonth.Year == today.Year && x.ReviewMonth.Month == today.Month).OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
            if (plan is null) tasks.Add(new WorkspaceTask { Key="waste-plan", Title="Create this month’s Food Waste Reduction Plan", Detail="Record the issue, root cause, action owner and due date for the monthly review.", Module="Food Waste", Severity="Attention", Url="/FoodWaste#reduction-plan" });
            else if (!plan.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase) && plan.DueDate.Date < today) tasks.Add(new WorkspaceTask { Key="waste-plan-overdue", Title="Food Waste action is overdue", Detail=$"{plan.Action} · owner: {plan.Owner}", Module="Food Waste", Severity="Overdue", Url="/FoodWaste#reduction-plan", DueDate=plan.DueDate, IsOverdue=true });
        }

        if (isAdmin)
        {
            var incompleteNutrition = store.Data.Recipes.Count(x => x.NutritionAutoGenerated && x.NutritionCoveragePercent < 95m);
            if (incompleteNutrition > 0) tasks.Add(new WorkspaceTask { Key="nutrition-quality", Title=$"{incompleteNutrition} recipes have incomplete nutrition coverage", Detail="Review unmatched ingredients and unit conversions before relying on the estimate.", Module="Recipes", Severity="Attention", Url="/Operations/Checks" });
            var supplierChecks = store.Data.Recipes.Count(x => x.SupplierVerificationRequired);
            if (supplierChecks > 0) tasks.Add(new WorkspaceTask { Key="allergen-supplier", Title=$"{supplierChecks} recipes need supplier verification", Detail="Supplier label evidence is still required for these recipe allergen checks.", Module="Recipes", Severity="Attention", Url="/Operations/Allergens" });
            var attention = GetAdminAttention().Count;
            if (attention > 0) tasks.Add(new WorkspaceTask { Key="unit-attention", Title=$"{attention} unit actions need attention", Detail="Open the management work queue to see exactly what is outstanding.", Module="Compliance", Severity="Attention", Url="/Workspace#attention" });
        }
        return tasks.OrderByDescending(x => x.IsOverdue).ThenBy(x => SeverityRank(x.Severity)).ThenBy(x => x.Title).ToList();
    }

    public List<WorkspaceSearchResult> Search(ClaimsPrincipal principal, string? query)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2) return new();
        var results = new List<WorkspaceSearchResult>();
        results.AddRange(store.Data.Recipes.Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Department.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(20)
            .Select(x => new WorkspaceSearchResult { Type="Recipe", Title=x.Name, Subtitle=$"{x.Code} · {x.Department}", Url=$"/Recipes/Details/{x.Id}", EntityId=x.Id }));

        var user = GetCurrentUser(principal);
        var category = "A";
        if (user is not null && !principal.IsInRole("Administrator"))
        {
            var unit = resalePricing.GetUnit(user.UnitCode) ?? resalePricing.GetAllUnits().FirstOrDefault(x => x.Name.Equals(user.Unit, StringComparison.OrdinalIgnoreCase));
            if (unit?.Category?.Trim().ToUpperInvariant() == "B") category="B";
        }
        results.AddRange(resalePricing.GetPricesForCategory(category).Where(x => x.ItemDescription.Contains(q, StringComparison.OrdinalIgnoreCase) || x.ItemCode.Contains(q, StringComparison.OrdinalIgnoreCase) || x.BrandCategorySize.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(20)
            .Select(x => new WorkspaceSearchResult { Type="Resale price", Title=x.ItemDescription, Subtitle=$"{x.ItemCode} · Category {category} · R {x.SellingPrice:N2}", Url=$"/ResalePricing?search={Uri.EscapeDataString(x.ItemCode)}" }));

        results.AddRange(store.Data.Promotions.Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(10)
            .Select(x => new WorkspaceSearchResult { Type="Promotion", Title=x.Title, Subtitle=$"{x.StartDate:dd MMM} – {x.EndDate:dd MMM yyyy}", Url="/Promotions", EntityId=x.Id }));

        var modules = new[] {
            new WorkspaceSearchResult{Type="Module",Title="Food Waste Tracker",Subtitle="Daily waste records, reduction plans and compliance",Url="/FoodWaste"},
            new WorkspaceSearchResult{Type="Module",Title="Reports & tie-back",Subtitle="Daily tie-back evidence and operational reporting",Url="/Operations/Reports"},
            new WorkspaceSearchResult{Type="Module",Title="Compliance Dashboard",Subtitle="Organisation, region and unit compliance",Url="/Operations/ComplianceDashboard"},
            new WorkspaceSearchResult{Type="Module",Title="Resale Pricing",Subtitle="Category A/B resale price book",Url="/ResalePricing"}
        };
        results.AddRange(modules.Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)));
        return results.Take(60).ToList();
    }

    public List<AdminAttentionItem> GetAdminAttention()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var items = new List<AdminAttentionItem>();
        var units = resalePricing.GetAllUnits().Where(x => !x.Status.Equals("CLOSED", StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();
        foreach (var unit in units)
        {
            var users = store.Data.Users.Where(x => x.UnitCode.Equals(unit.Code, StringComparison.OrdinalIgnoreCase) || x.Unit.Equals(unit.Name, StringComparison.OrdinalIgnoreCase)).Select(x=>x.Id).ToHashSet();
            if (today.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                if (!store.Data.FoodWasteRecords.Any(x => !x.IsDraft && x.BusinessDate.Date == today && (x.UnitCode.Equals(unit.Code,StringComparison.OrdinalIgnoreCase) || x.UnitName.Equals(unit.Name,StringComparison.OrdinalIgnoreCase))))
                    items.Add(new AdminAttentionItem { UnitCode=unit.Code, UnitName=unit.Name, Region=unit.Region, Module="Food Waste", Issue="Today’s Food Waste record has not been submitted.", Severity="Attention", Url=$"/FoodWaste?region={Uri.EscapeDataString(unit.Region)}&unitCode={Uri.EscapeDataString(unit.Code)}" });
                if (DateTime.Now.TimeOfDay >= new TimeSpan(9,0,0) && !store.Data.DailyTieBackUploads.Any(x => x.BusinessDate.Date==today && TieBackUnitMatcher.Matches(x.Unit, unit.Code, unit.Name)))
                    items.Add(new AdminAttentionItem { UnitCode=unit.Code, UnitName=unit.Name, Region=unit.Region, Module="Reports", Issue="Today’s tie-back is outstanding after 09:00.", Severity="Outstanding", Url="/Operations/Reports" });
            }
            if (!store.Data.ActivityLogs.Any(x => x.OccurredAtUtc >= monthStart.ToUniversalTime() && x.Action=="ResalePriceListViewed" && x.UserId.HasValue && users.Contains(x.UserId.Value)))
                items.Add(new AdminAttentionItem { UnitCode=unit.Code, UnitName=unit.Name, Region=unit.Region, Module="Resale Pricing", Issue="Resale price list has not been accessed this month.", Severity="Attention", Url="/Operations/ComplianceDashboard" });
            var profile=store.Data.UnitFoodWasteProfiles.FirstOrDefault(x=>x.UnitCode.Equals(unit.Code,StringComparison.OrdinalIgnoreCase));
            if(profile is null || string.IsNullOrWhiteSpace(profile.ChampionName)) items.Add(new AdminAttentionItem { UnitCode=unit.Code, UnitName=unit.Name, Region=unit.Region, Module="Food Waste", Issue="Food Waste Champion has not been recorded.", Severity="Attention", Url=$"/FoodWaste?region={Uri.EscapeDataString(unit.Region)}&unitCode={Uri.EscapeDataString(unit.Code)}#champion" });
        }
        return items.OrderBy(x => x.Region).ThenBy(x => x.UnitName).ThenBy(x => x.Module).ToList();
    }

    private static int SeverityRank(string value) => value.Equals("Overdue",StringComparison.OrdinalIgnoreCase) ? 0 : value.Equals("Attention",StringComparison.OrdinalIgnoreCase) ? 1 : 2;
}
