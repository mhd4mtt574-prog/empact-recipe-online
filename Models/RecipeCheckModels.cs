namespace EmpactRecipeOnline.Models;

public enum RecipeCheckSeverity { Passed, Warning, Critical }
public sealed class RecipeCheckIssue
{
    public string Code { get; set; } = "";
    public string Check { get; set; } = "";
    public string Message { get; set; } = "";
    public RecipeCheckSeverity Severity { get; set; }
}
public sealed class RecipeCheckResult
{
    public Guid RecipeId { get; set; }
    public string Recipe { get; set; } = "";
    public string Code { get; set; } = "";
    public string Department { get; set; } = "";
    public bool IsApproved { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<RecipeCheckIssue> Issues { get; set; } = new();
    public int CriticalCount => Issues.Count(x => x.Severity == RecipeCheckSeverity.Critical);
    public int WarningCount => Issues.Count(x => x.Severity == RecipeCheckSeverity.Warning);
    public string Status => CriticalCount > 0 ? "Action required" : WarningCount > 0 ? "Review recommended" : "Passed";
}
