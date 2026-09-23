using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace EmpactRecipeOnline.Services;

public sealed class TieBackGpResult
{
    public decimal? PlannedGpPercent { get; init; }
    public decimal? PlannedGpRand { get; init; }
    public decimal? ActualGpPercent { get; init; }
    public decimal? ActualGpRand { get; init; }
    public DateTime? MatchedBusinessDate { get; init; }
    public bool HasValues => PlannedGpPercent.HasValue || PlannedGpRand.HasValue || ActualGpPercent.HasValue || ActualGpRand.HasValue;
}

/// <summary>
/// Reads the Empact daily tie-back workbook and extracts the TOTAL planned / actual GP values.
/// The supplied template repeats daily blocks. Each block contains PLANNED GROSS PROFIT
/// and ACTUAL GROSS PROFIT headings, five meal rows, then a total row.
/// </summary>
public static class TieBackGpExtractor
{
    public static TieBackGpResult Extract(string path, DateTime businessDate)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not ".xlsx" and not ".xlsm") return new TieBackGpResult();

        try
        {
            using var archive = ZipFile.OpenRead(path);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetEntry = FindFirstWorksheet(archive);
            if (sheetEntry is null) return new TieBackGpResult();

            var cells = ReadCells(sheetEntry, sharedStrings);
            if (cells.Count == 0) return new TieBackGpResult();

            var titleRows = cells
                .Where(x => StringValue(x.Value).Contains("PLANNED GROSS PROFIT", StringComparison.OrdinalIgnoreCase))
                .Select(x => RowNumber(x.Key))
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var candidates = new List<BlockCandidate>();
            foreach (var titleRow in titleRows)
            {
                var totalRow = titleRow + 7; // title, sub-heading, five meal rows, total
                var plannedSales = DecimalValue(cells, $"M{totalRow}");
                var plannedCost = DecimalValue(cells, $"N{totalRow}");
                var plannedRand = DecimalValue(cells, $"O{totalRow}") ?? Difference(plannedSales, plannedCost);
                var plannedPct = DecimalValue(cells, $"P{totalRow}") ?? Ratio(plannedRand, plannedSales);

                var actualSales = DecimalValue(cells, $"Q{totalRow}");
                var actualCost = DecimalValue(cells, $"R{totalRow}");
                var actualRand = DecimalValue(cells, $"S{totalRow}") ?? Difference(actualSales, actualCost);
                var actualPct = DecimalValue(cells, $"T{totalRow}") ?? Ratio(actualRand, actualSales);

                var blockDate = FindBlockDate(cells, titleRow + 2, titleRow + 6);
                candidates.Add(new BlockCandidate(
                    titleRow,
                    blockDate,
                    ToPercentPoints(plannedPct), plannedRand,
                    ToPercentPoints(actualPct), actualRand));
            }

            if (candidates.Count == 0) return new TieBackGpResult();

            var exact = candidates.FirstOrDefault(x => x.BlockDate?.Date == businessDate.Date);
            var selected = exact
                ?? candidates.Where(x => x.BlockDate.HasValue)
                    .OrderBy(x => Math.Abs((x.BlockDate!.Value.Date - businessDate.Date).TotalDays))
                    .FirstOrDefault()
                ?? candidates.LastOrDefault(x => x.HasFinancialData)
                ?? candidates[0];

            return new TieBackGpResult
            {
                PlannedGpPercent = selected.PlannedGpPercent,
                PlannedGpRand = selected.PlannedGpRand,
                ActualGpPercent = selected.ActualGpPercent,
                ActualGpRand = selected.ActualGpRand,
                MatchedBusinessDate = selected.BlockDate
            };
        }
        catch
        {
            // The evidence upload must never fail just because the workbook cannot be read.
            return new TieBackGpResult();
        }
    }

    private sealed record BlockCandidate(
        int TitleRow,
        DateTime? BlockDate,
        decimal? PlannedGpPercent,
        decimal? PlannedGpRand,
        decimal? ActualGpPercent,
        decimal? ActualGpRand)
    {
        public bool HasFinancialData =>
            (PlannedGpRand.HasValue && PlannedGpRand.Value != 0) ||
            (ActualGpRand.HasValue && ActualGpRand.Value != 0) ||
            (PlannedGpPercent.HasValue && PlannedGpPercent.Value != 0) ||
            (ActualGpPercent.HasValue && ActualGpPercent.Value != 0);
    }

    private static ZipArchiveEntry? FindFirstWorksheet(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relsEntry is null)
            return archive.Entries.FirstOrDefault(x => x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

        using var workbookStream = workbookEntry.Open();
        using var relsStream = relsEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var rels = XDocument.Load(relsStream);
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        var firstSheet = workbook.Descendants(main + "sheet").FirstOrDefault();
        var relId = firstSheet?.Attribute(rel + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relId)) return null;
        var target = rels.Descendants(pkgRel + "Relationship").FirstOrDefault(x => (string?)x.Attribute("Id") == relId)?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target)) return null;
        var fullName = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
        fullName = NormalizeZipPath(fullName);
        return archive.GetEntry(fullName);
    }

    private static string NormalizeZipPath(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(part);
        }
        return string.Join('/', parts);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return new List<string>();
        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc.Descendants(ns + "si").Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToList();
    }

    private static Dictionary<string, object?> ReadCells(ZipArchiveEntry sheetEntry, IReadOnlyList<string> sharedStrings)
    {
        using var stream = sheetEntry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in doc.Descendants(ns + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            if (string.IsNullOrWhiteSpace(reference)) continue;
            var type = cell.Attribute("t")?.Value;
            var valueText = cell.Element(ns + "v")?.Value;
            object? value = null;

            if (type == "s" && int.TryParse(valueText, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                value = sharedStrings[sharedIndex];
            else if (type == "inlineStr")
                value = string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
            else if (type is "str" or "e")
                value = valueText;
            else if (decimal.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                value = number;
            else if (!string.IsNullOrWhiteSpace(valueText))
                value = valueText;

            result[reference] = value;
        }
        return result;
    }

    private static DateTime? FindBlockDate(IReadOnlyDictionary<string, object?> cells, int startRow, int endRow)
    {
        for (var row = startRow; row <= endRow; row++)
        {
            foreach (var column in new[] { "A", "B" })
            {
                if (!cells.TryGetValue($"{column}{row}", out var value) || value is null) continue;
                if (value is decimal serial && serial > 20000 && serial < 80000)
                {
                    try { return DateTime.FromOADate((double)serial).Date; } catch { }
                }
                var text = StringValue(value).Trim();
                if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-ZA"), DateTimeStyles.AllowWhiteSpaces, out var parsed) ||
                    DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
                    return parsed.Date;
            }
        }
        return null;
    }

    private static decimal? DecimalValue(IReadOnlyDictionary<string, object?> cells, string reference)
    {
        if (!cells.TryGetValue(reference, out var value) || value is null) return null;
        if (value is decimal d) return d;
        var text = StringValue(value).Replace("R", "", StringComparison.OrdinalIgnoreCase).Replace("%", "").Trim();
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("en-ZA"), out parsed)
            ? parsed : null;
    }

    private static decimal? Difference(decimal? left, decimal? right) => left.HasValue && right.HasValue ? left.Value - right.Value : null;
    private static decimal? Ratio(decimal? numerator, decimal? denominator) => numerator.HasValue && denominator.HasValue && denominator.Value != 0 ? numerator.Value / denominator.Value : null;
    private static decimal? ToPercentPoints(decimal? value) => value.HasValue ? (Math.Abs(value.Value) <= 1.5m ? value.Value * 100m : value.Value) : null;
    private static string StringValue(object? value) => value?.ToString() ?? string.Empty;
    private static int RowNumber(string reference)
    {
        var digits = new string(reference.SkipWhile(c => !char.IsDigit(c)).ToArray());
        return int.TryParse(digits, out var row) ? row : 0;
    }
}
