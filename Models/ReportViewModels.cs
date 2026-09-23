namespace EmpactRecipeOnline.Models;

public sealed class ReportsViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string UserQuery { get; set; } = string.Empty;
    public IReadOnlyList<string> Units { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Regions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Departments { get; set; } = Array.Empty<string>();
    public IReadOnlyList<UnitComplianceRow> UnitCompliance { get; set; } = Array.Empty<UnitComplianceRow>();
    public IReadOnlyList<UserActivityRow> UserActivity { get; set; } = Array.Empty<UserActivityRow>();
    public IReadOnlyList<AplComplianceRow> AplCompliance { get; set; } = Array.Empty<AplComplianceRow>();
    public IReadOnlyList<GpMarginReportRow> GpMargins { get; set; } = Array.Empty<GpMarginReportRow>();
    public IReadOnlyList<PromotionComplianceRow> PromotionCompliance { get; set; } = Array.Empty<PromotionComplianceRow>();
    public IReadOnlyList<DailyTieBackReportRow> DailyTieBacks { get; set; } = Array.Empty<DailyTieBackReportRow>();
    public DailyTieBackUpload? TodayUpload { get; set; }
    public bool IsAdministrator { get; set; }
    public string CurrentUnit { get; set; } = string.Empty;
    public int TodayMissingAfterDeadline { get; set; }
}

public sealed class UnitComplianceRow
{
    public string Unit { get; set; } = string.Empty; public string Region { get; set; } = string.Empty;
    public int ActiveUsers { get; set; } public int RecipeViews { get; set; } public int RecipesUsed { get; set; }
    public int MarginsSet { get; set; } public int PromotionActions { get; set; } public int ComplianceScore { get; set; }
    public string Status => ComplianceScore >= 80 ? "Green" : ComplianceScore >= 60 ? "Amber" : "Red";
}
public sealed class UserActivityRow
{
    public string User { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; public string Unit { get; set; } = string.Empty; public string Region { get; set; } = string.Empty;
    public DateTime? LastLoginUtc { get; set; } public DateTime? LastActivityUtc { get; set; } public int Logins { get; set; } public int RecipeViews { get; set; } public int MarginUpdates { get; set; } public int PromotionActions { get; set; }
}
public sealed class AplComplianceRow
{
    public string Recipe { get; set; } = string.Empty; public string Code { get; set; } = string.Empty; public string Department { get; set; } = string.Empty;
    public int IngredientLines { get; set; } public int LinkedLines { get; set; } public int MissingCodes { get; set; } public decimal CompliancePercent { get; set; }
}
public sealed class GpMarginReportRow
{
    public string Unit { get; set; } = string.Empty; public string Recipe { get; set; } = string.Empty; public string Department { get; set; } = string.Empty;
    public decimal CostPerPortion { get; set; } public decimal DesiredMarginPercent { get; set; } public decimal RequiredSellingPrice { get; set; } public DateTime UpdatedAtUtc { get; set; } public string UpdatedBy { get; set; } = string.Empty;
}
public sealed class PromotionComplianceRow
{
    public string Promotion { get; set; } = string.Empty; public string Unit { get; set; } = string.Empty; public string Region { get; set; } = string.Empty;
    public int Views { get; set; } public int Downloads { get; set; } public int Prints { get; set; } public DateTime? LastActionUtc { get; set; }
    public string Status => Downloads > 0 || Prints > 0 ? "Complete" : Views > 0 ? "Viewed" : "Not accessed";
}


public sealed class DailyTieBackReportRow
{
    public DateTime BusinessDate { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public Guid? UploadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime? UploadedAtUtc { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public decimal? PlannedGpPercent { get; set; }
    public decimal? PlannedGpRand { get; set; }
    public decimal? ActualGpPercent { get; set; }
    public decimal? ActualGpRand { get; set; }
    public bool IsUploaded => UploadId.HasValue;
    public bool IsLate => UploadedAtUtc.HasValue && UploadedAtUtc.Value.ToLocalTime() > BusinessDate.Date.AddHours(9);
    public string Status
    {
        get
        {
            if (IsUploaded) return IsLate ? "Uploaded late" : "Uploaded on time";
            var deadline = BusinessDate.Date.AddHours(9);
            return DateTime.Now > deadline ? "Overdue" : "Pending";
        }
    }
}
