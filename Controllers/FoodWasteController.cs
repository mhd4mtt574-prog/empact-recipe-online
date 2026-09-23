using System.Security.Claims;
using EmpactRecipeOnline.Models;
using EmpactRecipeOnline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmpactRecipeOnline.Controllers;

[Authorize]
public sealed class FoodWasteController(JsonDataStore store, ResalePricingService resalePricing) : Controller
{
    public IActionResult Index(string? region, string? unitCode, DateTime? month)
    {
        var m = month.HasValue ? new DateTime(month.Value.Year, month.Value.Month, 1) : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var user = CurrentUser();
        var canSelect = User.IsInRole("Administrator") || User.IsInRole("HeadOffice");
        var allUnits = GetUnitOptions();
        var regions = allUnits.Select(x => x.Region).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var selectedRegion = regions.FirstOrDefault(x => x.Equals(region ?? string.Empty, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var unitOptions = allUnits.Where(x => string.IsNullOrWhiteSpace(selectedRegion) || x.Region.Equals(selectedRegion, StringComparison.OrdinalIgnoreCase)).ToList();
        FoodWasteUnitOption? selectedUnit = null;
        if (canSelect)
        {
            selectedUnit = unitOptions.FirstOrDefault(x => x.Code.Equals(unitCode ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                ?? unitOptions.FirstOrDefault(x => x.Name.Equals(unitCode ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            selectedUnit = allUnits.FirstOrDefault(x => (!string.IsNullOrWhiteSpace(user?.UnitCode) && x.Code.Equals(user.UnitCode, StringComparison.OrdinalIgnoreCase))
                || x.Name.Equals(user?.Unit ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            unitOptions = selectedUnit is null ? new List<FoodWasteUnitOption>() : new List<FoodWasteUnitOption> { selectedUnit };
            selectedRegion = selectedUnit?.Region ?? user?.Region ?? string.Empty;
        }

        var scopedUnits = selectedUnit is not null ? new List<FoodWasteUnitOption> { selectedUnit } : unitOptions;
        var records = store.Data.FoodWasteRecords
            .Where(x => !x.IsDraft)
            .Where(x => x.BusinessDate.Year == m.Year && x.BusinessDate.Month == m.Month)
            .Where(x => scopedUnits.Count == 0 || scopedUnits.Any(u => UnitMatches(x, u)))
            .OrderByDescending(x => x.BusinessDate).ThenBy(x => x.UnitName).ToList();
        var totals = BuildTotals(records);
        var expectedDays = ExpectedWorkDays(m, DateTime.Today < m.AddMonths(1) ? DateTime.Today : m.AddMonths(1).AddDays(-1));
        var submittedDays = records.Select(x => new { x.UnitCode, Day = x.BusinessDate.Date }).Distinct().Count();
        var expected = Math.Max(1, expectedDays * Math.Max(1, scopedUnits.Count));
        var compliance = Math.Min(100, (int)Math.Round(submittedDays * 100m / expected));
        var todayRecord = selectedUnit is null ? null : records.FirstOrDefault(x => x.BusinessDate.Date == DateTime.Today && UnitMatches(x, selectedUnit));
        ViewBag.TodaySubmitted = todayRecord is not null;
        ViewBag.TodaySubmittedBy = todayRecord?.SubmittedBy ?? string.Empty;
        ViewBag.TodaySubmittedAt = todayRecord?.SubmittedAtUtc;
        var model = new FoodWasteDashboardViewModel
        {
            Region = selectedRegion,
            UnitCode = selectedUnit?.Code ?? string.Empty,
            Month = m,
            Regions = regions,
            Units = unitOptions,
            Records = records,
            Totals = totals,
            CategoryTotals = BuildCategoryTotals(totals),
            DiversionTotals = BuildDiversionTotals(totals),
            MealTotals = BuildMealTotals(totals),
            ExpectedWorkDays = expectedDays,
            SubmittedDays = submittedDays,
            CompliancePercent = compliance,
            LastSubmissionUtc = records.OrderByDescending(x => x.SubmittedAtUtc).FirstOrDefault()?.SubmittedAtUtc,
            CanSelectUnit = canSelect,
            ScopeLabel = selectedUnit is not null ? selectedUnit.Label : string.IsNullOrWhiteSpace(selectedRegion) ? "All units" : selectedRegion + " region",
            NewRecord = BuildNewRecord(selectedUnit, user, m),
            WasteProfile = selectedUnit is null ? null : store.Data.UnitFoodWasteProfiles.FirstOrDefault(x => x.UnitCode.Equals(selectedUnit.Code, StringComparison.OrdinalIgnoreCase)),
            ReductionPlans = selectedUnit is null ? new List<FoodWasteReductionPlan>() : store.Data.FoodWasteReductionPlans.Where(x => x.UnitCode.Equals(selectedUnit.Code, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.ReviewMonth).ThenByDescending(x=>x.UpdatedAtUtc).Take(24).ToList(),
            CurrentDraft = selectedUnit is null ? null : store.Data.FoodWasteRecords.Where(x => x.IsDraft && x.UnitCode.Equals(selectedUnit.Code, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.DraftSavedAtUtc).FirstOrDefault(),
            SupportDocuments = selectedUnit is null ? new List<FoodWasteSupportDocument>() : store.Data.FoodWasteSupportDocuments.Where(x => x.UnitCode.Equals(selectedUnit.Code, StringComparison.OrdinalIgnoreCase) && x.DocumentMonth.Year == m.Year && x.DocumentMonth.Month == m.Month).OrderByDescending(x=>x.UploadedAtUtc).ToList()
        };
        if (model.CurrentDraft is not null) model.NewRecord = model.CurrentDraft;
        return View(model);
    }


    public IActionResult Document(string id)
    {
        var safeId = (id ?? string.Empty).Trim().ToLowerInvariant();
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["proc40"] = "PROC40-Food-Waste-Management.pdf",
            ["f14a"] = "F14a-Workstation-Waste-Record-Form.pdf",
            ["f14b"] = "F14b-Food-Waste-Tracker-Template.xlsb",
            ["training"] = "Food-Waste-Training-June-2026.pdf"
        };
        if (!files.TryGetValue(safeId, out var fileName)) return NotFound();
        var path = Path.Combine(AppContext.BaseDirectory, "App_Data", "FoodWasteProgram", fileName);
        if (!System.IO.File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "FoodWasteProgram", fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var contentType = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/vnd.ms-excel.sheet.binary.macroEnabled.12";
        return PhysicalFile(path, contentType, fileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Submit(FoodWasteRecord record, string? region, string? selectedUnitCode, DateTime? month)
    {
        var user = CurrentUser();
        var canSelect = User.IsInRole("Administrator") || User.IsInRole("HeadOffice");
        var allUnits = GetUnitOptions();
        var selected = ResolveSelectedUnit(allUnits, user, canSelect, selectedUnitCode, record.UnitCode, record.UnitName);

        if (selected is null)
        {
            TempData["Error"] = "Select a valid unit before submitting food waste.";
            return RedirectToAction(nameof(Index), new { region, unitCode = selectedUnitCode, month });
        }

        record.UnitCode = selected.Code;
        record.UnitName = selected.Name;
        record.Region = selected.Region;
        record.UnitManager = string.IsNullOrWhiteSpace(record.UnitManager) ? (user?.DisplayName ?? User.Identity?.Name ?? string.Empty) : record.UnitManager;
        record.BusinessDate = record.BusinessDate == default ? DateTime.Today : record.BusinessDate.Date;
        record.IsDraft = false;
        record.DraftSavedAtUtc = null;
        record.SubmittedAtUtc = DateTime.UtcNow;
        record.SubmittedByUserId = user?.Id ?? Guid.Empty;
        record.SubmittedBy = User.Identity?.Name ?? user?.DisplayName ?? string.Empty;
        record.Lines = NormalizeLines(record.Lines);
        ApplyWorkstationCategoryTotals(record);

        // Replace the previous daily submission for the same unit and date so the latest submission is the official record.
        store.Data.FoodWasteRecords.RemoveAll(x => x.BusinessDate.Date == record.BusinessDate.Date &&
            (x.UnitCode.Equals(record.UnitCode, StringComparison.OrdinalIgnoreCase) || x.UnitName.Equals(record.UnitName, StringComparison.OrdinalIgnoreCase)));
        store.Data.FoodWasteRecords.Add(record);
        store.Save();
        store.LogActivity(new ActivityLog
        {
            UserId = user?.Id,
            UserName = User.Identity?.Name ?? string.Empty,
            UserRole = user?.Role.ToString() ?? string.Empty,
            Unit = record.UnitCode,
            Region = record.Region,
            Action = "FoodWasteSubmitted",
            EntityType = "FoodWaste",
            EntityId = record.Id,
            EntityName = record.BusinessDate.ToString("yyyy-MM-dd"),
            NumericValue = record.TotalFoodWasteKg,
            Notes = $"Submitted food waste record for {record.UnitCode} · {record.UnitName}: {record.TotalFoodWasteKg:N2} kg food waste, {record.TotalDiversionKg:N2} kg diverted."
        });
        TempData["Success"] = $"Food waste record submitted for {record.BusinessDate:dd MMM yyyy} by {record.SubmittedBy} at {DateTime.Now:HH:mm}.";
        if (record.TotalMeals > 0 && record.TotalFoodWasteKg == 0) TempData["Warning"] = "Data quality check: meals were recorded but total food waste is 0 kg. Please confirm that the waste weights are correct.";
        else if (record.Lines.Sum(x=>x.WeightKg) > 0 && record.TotalFoodWasteKg == 0) TempData["Warning"] = "Data quality check: workstation lines contain weights but the category totals are 0 kg. Review the summary fields.";
        return RedirectToAction(nameof(Index), new { region = selected.Region, unitCode = selected.Code, month = new DateTime(record.BusinessDate.Year, record.BusinessDate.Month, 1) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SaveDraft(FoodWasteRecord record, string? region, string? selectedUnitCode, DateTime? month)
    {
        var user = CurrentUser();
        var canSelect = User.IsInRole("Administrator") || User.IsInRole("HeadOffice");
        var allUnits = GetUnitOptions();
        var selected = ResolveSelectedUnit(allUnits, user, canSelect, selectedUnitCode, record.UnitCode, record.UnitName);
        if (selected is null) { TempData["Error"]="Select a valid unit before saving a draft."; return RedirectToAction(nameof(Index), new { region, unitCode=selectedUnitCode, month }); }
        record.UnitCode=selected.Code; record.UnitName=selected.Name; record.Region=selected.Region;
        record.BusinessDate = record.BusinessDate == default ? DateTime.Today : record.BusinessDate.Date;
        record.UnitManager = string.IsNullOrWhiteSpace(record.UnitManager) ? (user?.DisplayName ?? User.Identity?.Name ?? string.Empty) : record.UnitManager;
        record.SubmittedByUserId = user?.Id ?? Guid.Empty; record.SubmittedBy = User.Identity?.Name ?? user?.DisplayName ?? string.Empty;
        record.IsDraft=true; record.DraftSavedAtUtc=DateTime.UtcNow; record.Lines=NormalizeLines(record.Lines); ApplyWorkstationCategoryTotals(record);
        store.Data.FoodWasteRecords.RemoveAll(x => x.IsDraft && x.UnitCode.Equals(record.UnitCode,StringComparison.OrdinalIgnoreCase));
        store.Data.FoodWasteRecords.Add(record); store.Save();
        TempData["Success"]=$"Draft saved at {DateTime.Now:HH:mm}. You can continue it later.";
        return RedirectToAction(nameof(Index), new { region=selected.Region, unitCode=selected.Code, month=new DateTime(record.BusinessDate.Year,record.BusinessDate.Month,1), anchor="daily-entry" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SaveWasteProfile(UnitFoodWasteProfile profile, string? returnRegion, string? returnUnitCode, DateTime? month)
    {
        var user=CurrentUser(); if(user is null) return Forbid();
        var selected=GetUnitOptions().FirstOrDefault(x=>x.Code.Equals(returnUnitCode ?? profile.UnitCode,StringComparison.OrdinalIgnoreCase));
        if(selected is null) { TempData["Error"]="Select a valid unit."; return RedirectToAction(nameof(Index)); }
        if(!User.IsInRole("Administrator") && !User.IsInRole("HeadOffice") && !selected.Code.Equals(user.UnitCode,StringComparison.OrdinalIgnoreCase)) return Forbid();
        profile.UnitCode=selected.Code; profile.UnitName=selected.Name; profile.Region=selected.Region; profile.UpdatedAtUtc=DateTime.UtcNow; profile.UpdatedBy=user.DisplayName;
        store.UpsertFoodWasteProfile(profile);
        store.LogActivity(new ActivityLog{UserId=user.Id,UserName=user.DisplayName,UserRole=user.Role.ToString(),Unit=selected.Code,Region=selected.Region,Action="FoodWasteProfileUpdated",EntityType="FoodWasteProfile",EntityId=profile.Id,EntityName=selected.Name,Notes=$"Champion: {profile.ChampionName}; backup: {profile.BackupName}"});
        TempData["Success"]="Food Waste Champion and training details saved.";
        return RedirectToAction(nameof(Index),new{region=returnRegion,unitCode=selected.Code,month,anchor="champion"});
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SaveReductionPlan(FoodWasteReductionPlan plan, string? returnRegion, string? returnUnitCode, DateTime? month)
    {
        var user=CurrentUser(); if(user is null) return Forbid();
        var selected=GetUnitOptions().FirstOrDefault(x=>x.Code.Equals(returnUnitCode ?? plan.UnitCode,StringComparison.OrdinalIgnoreCase));
        if(selected is null) { TempData["Error"]="Select a valid unit."; return RedirectToAction(nameof(Index)); }
        if(!User.IsInRole("Administrator") && !User.IsInRole("HeadOffice") && !selected.Code.Equals(user.UnitCode,StringComparison.OrdinalIgnoreCase)) return Forbid();
        plan.UnitCode=selected.Code; plan.UnitName=selected.Name; plan.Region=selected.Region; plan.ReviewMonth=new DateTime((plan.ReviewMonth==default?DateTime.Today:plan.ReviewMonth).Year,(plan.ReviewMonth==default?DateTime.Today:plan.ReviewMonth).Month,1);
        if(plan.DueDate==default) plan.DueDate=DateTime.Today.AddDays(30); plan.UpdatedAtUtc=DateTime.UtcNow; plan.UpdatedBy=user.DisplayName;
        if(plan.CreatedAtUtc==default) plan.CreatedAtUtc=DateTime.UtcNow; if(string.IsNullOrWhiteSpace(plan.CreatedBy)) plan.CreatedBy=user.DisplayName;
        if(string.IsNullOrWhiteSpace(plan.Issue) || string.IsNullOrWhiteSpace(plan.Action)){TempData["Error"]="Enter the waste issue and the action to be taken.";return RedirectToAction(nameof(Index),new{region=returnRegion,unitCode=selected.Code,month,anchor="reduction-plan"});}
        store.UpsertFoodWasteReductionPlan(plan);
        store.LogActivity(new ActivityLog{UserId=user.Id,UserName=user.DisplayName,UserRole=user.Role.ToString(),Unit=selected.Code,Region=selected.Region,Action="FoodWasteReductionPlanUpdated",EntityType="FoodWasteReductionPlan",EntityId=plan.Id,EntityName=plan.Issue,NumericValue=plan.ProgressPercent,Notes=$"{plan.Action} · {plan.Status}"});
        TempData["Success"]="Food Waste Reduction Plan saved.";
        return RedirectToAction(nameof(Index),new{region=returnRegion,unitCode=selected.Code,month,anchor="reduction-plan"});
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UploadSupportDocument(string documentType, DateTime? documentMonth, string? returnRegion, string? returnUnitCode, IFormFile supportFile)
    {
        var user=CurrentUser(); if(user is null) return Forbid();
        var selected=GetUnitOptions().FirstOrDefault(x=>x.Code.Equals(returnUnitCode ?? user.UnitCode,StringComparison.OrdinalIgnoreCase));
        if(selected is null) { TempData["Error"]="Select a valid unit before uploading evidence."; return RedirectToAction(nameof(Index)); }
        if(!User.IsInRole("Administrator") && !User.IsInRole("HeadOffice") && !selected.Code.Equals(user.UnitCode,StringComparison.OrdinalIgnoreCase)) return Forbid();
        if(supportFile is null || supportFile.Length==0){TempData["Error"]="Choose a support document to upload.";return RedirectToAction(nameof(Index),new{region=returnRegion,unitCode=selected.Code,month=documentMonth});}
        var ext=Path.GetExtension(supportFile.FileName).ToLowerInvariant();
        var allowed=new[]{".pdf",".xlsx",".xls",".xlsm",".csv",".jpg",".jpeg",".png"};
        if(!allowed.Contains(ext)){TempData["Error"]="Upload PDF, Excel, CSV, JPG or PNG evidence.";return RedirectToAction(nameof(Index),new{region=returnRegion,unitCode=selected.Code,month=documentMonth});}
        var dm=documentMonth.HasValue?new DateTime(documentMonth.Value.Year,documentMonth.Value.Month,1):new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);
        var safeUnit=string.Concat(selected.Code.Select(c=>char.IsLetterOrDigit(c)?c:'-')); var stored=$"{dm:yyyyMM}-{safeUnit}-{Guid.NewGuid():N}{ext}";
        var path=Path.Combine(store.GetFoodWasteUploadFolder(),stored); await using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) await supportFile.CopyToAsync(stream);
        var doc=new FoodWasteSupportDocument{UnitCode=selected.Code,UnitName=selected.Name,Region=selected.Region,DocumentMonth=dm,DocumentType=string.IsNullOrWhiteSpace(documentType)?"Other":documentType,OriginalFileName=Path.GetFileName(supportFile.FileName),StoredFileName=stored,ContentType=supportFile.ContentType??"application/octet-stream",FileSizeBytes=supportFile.Length,UploadedAtUtc=DateTime.UtcNow,UploadedByUserId=user.Id,UploadedBy=user.DisplayName};
        store.Data.FoodWasteSupportDocuments.Add(doc); store.Save();
        store.LogActivity(new ActivityLog{UserId=user.Id,UserName=user.DisplayName,UserRole=user.Role.ToString(),Unit=selected.Code,Region=selected.Region,Action="FoodWasteEvidenceUploaded",EntityType="FoodWasteDocument",EntityId=doc.Id,EntityName=doc.OriginalFileName,Notes=doc.DocumentType});
        TempData["Success"]=$"{doc.DocumentType} uploaded for {dm:MMMM yyyy}.";
        return RedirectToAction(nameof(Index),new{region=returnRegion,unitCode=selected.Code,month=dm.ToString("yyyy-MM-01")});
    }

    [HttpGet]
    public IActionResult DownloadSupportDocument(Guid id)
    {
        var doc=store.Data.FoodWasteSupportDocuments.FirstOrDefault(x=>x.Id==id); if(doc is null) return NotFound();
        var user=CurrentUser(); if(user is null) return Forbid();
        if(!User.IsInRole("Administrator") && !User.IsInRole("HeadOffice") && !doc.UnitCode.Equals(user.UnitCode,StringComparison.OrdinalIgnoreCase)) return Forbid();
        var path=Path.Combine(store.GetFoodWasteUploadFolder(),doc.StoredFileName); if(!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path,string.IsNullOrWhiteSpace(doc.ContentType)?"application/octet-stream":doc.ContentType,doc.OriginalFileName);
    }

    [Authorize(Roles = "Administrator,HeadOffice")]
    public IActionResult ExportCsv(string? region, string? unitCode, DateTime? month)
    {
        var m = month.HasValue ? new DateTime(month.Value.Year, month.Value.Month, 1) : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var units = GetUnitOptions().Where(x => (string.IsNullOrWhiteSpace(region) || x.Region.Equals(region, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(unitCode) || x.Code.Equals(unitCode, StringComparison.OrdinalIgnoreCase))).ToList();
        var rows = store.Data.FoodWasteRecords.Where(x => !x.IsDraft && x.BusinessDate.Year == m.Year && x.BusinessDate.Month == m.Month)
            .Where(x => units.Count == 0 || units.Any(u => UnitMatches(x, u))).OrderBy(x => x.BusinessDate).ThenBy(x => x.UnitCode).ToList();
        var csv = new List<string> { Csv("Date","Unit Code","Unit Name","Region","Production Waste Kg","Over Production Waste Kg","Unused/Expired Kg","Plate Scrapings Kg","Food Samples Kg","Bokashi Kg","Bio Bins Kg","Other Composting Kg","Meals","Used Oil Litres","Submitted By","Submitted At") };
        csv.AddRange(rows.Select(x => Csv(x.BusinessDate.ToString("yyyy-MM-dd"), x.UnitCode, x.UnitName, x.Region, x.ProductionWasteKg, x.OverProductionWasteKg, x.UnusedExpiredKg, x.PlateScrapingsKg, x.FoodSamplesKg, x.BokashiWasteKg, x.BioBinsKg, x.OtherCompostingKg, x.TotalMeals, x.UsedOilLitres, x.SubmittedBy, x.SubmittedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))));
        return File(System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, csv)), "text/csv", $"food-waste-{m:yyyy-MM}.csv");
    }

    private AppUser? CurrentUser()
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idText, out var id) ? store.Data.Users.FirstOrDefault(x => x.Id == id) : null;
    }

    private List<FoodWasteUnitOption> GetUnitOptions()
    {
        return resalePricing.GetAllUnits()
            .Where(x => !x.Status.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
            .Select(x => new FoodWasteUnitOption { Code = x.Code.Trim(), Name = x.Name.Trim(), Region = x.Region.Trim() })
            .Where(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Code) ? x.Name : x.Code, StringComparer.OrdinalIgnoreCase).Select(x => x.First())
            .OrderBy(x => x.Region).ThenBy(x => x.Name).ToList();
    }

    private static FoodWasteUnitOption? ResolveSelectedUnit(List<FoodWasteUnitOption> allUnits, AppUser? user, bool canSelect, string? selectedUnitCode, string? recordUnitCode, string? recordUnitName)
    {
        var requested = new[] { selectedUnitCode, recordUnitCode, recordUnitName }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();

        if (canSelect)
            return allUnits.FirstOrDefault(x => requested.Any(v => x.Code.Equals(v, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(v, StringComparison.OrdinalIgnoreCase) || x.Label.Equals(v, StringComparison.OrdinalIgnoreCase)));

        var userKeys = new[] { user?.UnitCode, user?.Unit, user?.Username }
            .Concat(requested)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return allUnits.FirstOrDefault(x => userKeys.Any(v => x.Code.Equals(v, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(v, StringComparison.OrdinalIgnoreCase) || x.Label.Equals(v, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool UnitMatches(FoodWasteRecord record, FoodWasteUnitOption unit) =>
        record.UnitCode.Equals(unit.Code, StringComparison.OrdinalIgnoreCase) || record.UnitName.Equals(unit.Name, StringComparison.OrdinalIgnoreCase);

    private static FoodWasteRecord BuildNewRecord(FoodWasteUnitOption? selectedUnit, AppUser? user, DateTime month)
    {
        var today = DateTime.Today.Year == month.Year && DateTime.Today.Month == month.Month ? DateTime.Today : month;
        return new FoodWasteRecord
        {
            BusinessDate = today,
            UnitCode = selectedUnit?.Code ?? user?.UnitCode ?? string.Empty,
            UnitName = selectedUnit?.Name ?? user?.Unit ?? string.Empty,
            Region = selectedUnit?.Region ?? user?.Region ?? string.Empty,
            UnitManager = user?.DisplayName ?? string.Empty,
            Lines = Enumerable.Range(0, 5).Select(_ => new FoodWasteLine()).ToList()
        };
    }

    private static void ApplyWorkstationCategoryTotals(FoodWasteRecord record)
    {
        var lines = record.Lines ?? new List<FoodWasteLine>();
        static decimal Sum(IEnumerable<FoodWasteLine> source, params string[] codes) => source
            .Where(x => codes.Any(code => string.Equals((x.TypeOfWaste ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase)))
            .Sum(x => x.WeightKg);

        // F14a workstation entries are the source of truth. Category totals are derived automatically
        // so users never have to capture the same waste twice.
        record.ProductionWasteKg = Sum(lines, "PW", "Production Waste");
        record.OverProductionWasteKg = Sum(lines, "OPW", "Over Production Waste");
        record.UnusedExpiredKg = Sum(lines, "UE", "Unused/Expired", "Unused / Expired");
        record.PlateScrapingsKg = Sum(lines, "PS", "Plate Scrapings");
        record.FoodSamplesKg = Sum(lines, "FS", "Food Samples", "Food Retention Samples");
    }

    private static List<FoodWasteLine> NormalizeLines(List<FoodWasteLine>? lines)
    {
        return (lines ?? new List<FoodWasteLine>())
            .Where(x => !string.IsNullOrWhiteSpace(x.TypeOfWaste) || !string.IsNullOrWhiteSpace(x.Description) || x.WeightKg > 0 || !string.IsNullOrWhiteSpace(x.EmployeeName))
            .Select(x => new FoodWasteLine { TypeOfWaste = x.TypeOfWaste?.Trim() ?? string.Empty, Description = x.Description?.Trim() ?? string.Empty, WeightKg = Math.Max(0, x.WeightKg), EmployeeName = x.EmployeeName?.Trim() ?? string.Empty })
            .ToList();
    }

    private static FoodWasteTotals BuildTotals(IEnumerable<FoodWasteRecord> records)
    {
        var r = records.ToList();
        return new FoodWasteTotals
        {
            ProductionWasteKg = r.Sum(x => x.ProductionWasteKg), OverProductionWasteKg = r.Sum(x => x.OverProductionWasteKg), UnusedExpiredKg = r.Sum(x => x.UnusedExpiredKg),
            PlateScrapingsKg = r.Sum(x => x.PlateScrapingsKg), FoodSamplesKg = r.Sum(x => x.FoodSamplesKg), BokashiWasteKg = r.Sum(x => x.BokashiWasteKg),
            BioBinsKg = r.Sum(x => x.BioBinsKg), OtherCompostingKg = r.Sum(x => x.OtherCompostingKg), UsedOilLitres = r.Sum(x => x.UsedOilLitres),
            MainDishMeals = r.Sum(x => x.MainDishMeals), UrbanFlavourMeals = r.Sum(x => x.UrbanFlavourMeals), ChefsSignatureMeals = r.Sum(x => x.ChefsSignatureMeals),
            VeggieMeals = r.Sum(x => x.VeggieMeals), OtherMeals = r.Sum(x => x.OtherMeals), FunctionMeals = r.Sum(x => x.FunctionMeals), FunctionPlatters = r.Sum(x => x.FunctionPlatters)
        };
    }

    private static List<FoodWasteCategoryTotal> BuildCategoryTotals(FoodWasteTotals t)
    {
        var items = new List<FoodWasteCategoryTotal>
        {
            new() { Name="Production Waste", Value=t.ProductionWasteKg }, new() { Name="Over Production Waste", Value=t.OverProductionWasteKg },
            new() { Name="Unused / Expired", Value=t.UnusedExpiredKg }, new() { Name="Plate Scrapings", Value=t.PlateScrapingsKg }, new() { Name="Food Samples", Value=t.FoodSamplesKg }
        };
        return WithPercents(items, t.TotalFoodWasteKg);
    }

    private static List<FoodWasteCategoryTotal> BuildDiversionTotals(FoodWasteTotals t)
    {
        var items = new List<FoodWasteCategoryTotal>
        {
            new() { Name="Bokashi Waste", Value=t.BokashiWasteKg }, new() { Name="Bio Bins", Value=t.BioBinsKg }, new() { Name="Other Composting", Value=t.OtherCompostingKg },
            new() { Name="Used Oil", Value=t.UsedOilLitres, Unit="L" }
        };
        return WithPercents(items, Math.Max(t.TotalDiversionKg, t.UsedOilLitres));
    }

    private static List<FoodWasteCategoryTotal> BuildMealTotals(FoodWasteTotals t)
    {
        var items = new List<FoodWasteCategoryTotal>
        {
            new() { Name="Main Dish", Value=t.MainDishMeals, Unit="meals" }, new() { Name="Urban Flavour", Value=t.UrbanFlavourMeals, Unit="meals" },
            new() { Name="Chef's Signature", Value=t.ChefsSignatureMeals, Unit="meals" }, new() { Name="Veggie Meal", Value=t.VeggieMeals, Unit="meals" },
            new() { Name="Other Meals", Value=t.OtherMeals, Unit="meals" }, new() { Name="Functions", Value=t.FunctionMeals + t.FunctionPlatters, Unit="meals" }
        };
        return WithPercents(items, t.TotalMeals);
    }

    private static List<FoodWasteCategoryTotal> WithPercents(List<FoodWasteCategoryTotal> items, decimal total)
    {
        foreach (var item in items) item.Percent = total <= 0 ? 0 : (int)Math.Round(item.Value * 100m / total);
        return items;
    }

    private static int ExpectedWorkDays(DateTime monthStart, DateTime monthEnd)
    {
        if (monthEnd < monthStart) return 0;
        return Enumerable.Range(0, (monthEnd.Date - monthStart.Date).Days + 1)
            .Select(i => monthStart.Date.AddDays(i))
            .Count(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday);
    }

    private static string Csv(params object?[] values) => string.Join(",", values.Select(v => $"\"{(v?.ToString() ?? string.Empty).Replace("\"", "\"\"")}\""));
}
