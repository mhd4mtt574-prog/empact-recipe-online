using System.ComponentModel.DataAnnotations;

namespace EmpactRecipeOnline.Models;

public sealed class UserFavourite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FoodWasteReductionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime ReviewMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [Required] public string Issue { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    [Required] public string Action { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);
    public string Status { get; set; } = "Open";
    [Range(0,100)] public int ProgressPercent { get; set; }
    public string EvidenceNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class UnitFoodWasteProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    [Required] public string ChampionName { get; set; } = string.Empty;
    public string ChampionEmail { get; set; } = string.Empty;
    public string BackupName { get; set; } = string.Empty;
    public DateTime? TrainingCompletedDate { get; set; }
    public DateTime? RefresherDueDate { get; set; }
    public string TrainingEvidenceNotes { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class WorkspaceTask
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Url { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
}

public sealed class WorkspaceSearchResult
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
}

public sealed class WorkspaceDashboardViewModel
{
    public List<WorkspaceTask> Tasks { get; set; } = new();
    public List<UserFavourite> Favourites { get; set; } = new();
    public List<ActivityLog> RecentActivity { get; set; } = new();
    public List<WorkspaceSearchResult> SearchResults { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public bool IsAdministrator { get; set; }
    public List<AdminAttentionItem> AttentionItems { get; set; } = new();
}

public sealed class AdminAttentionItem
{
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Severity { get; set; } = "Attention";
    public string Url { get; set; } = string.Empty;
}

public sealed class FoodWasteSupportDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime DocumentMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public string DocumentType { get; set; } = "Other";
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid UploadedByUserId { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
