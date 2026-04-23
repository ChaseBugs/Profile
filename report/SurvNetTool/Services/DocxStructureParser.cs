using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SurvNetTool.Models;

namespace SurvNetTool.Services;

/// <summary>
/// Parses a SurvNet / RKI questionnaire DOCX into a structured model.
///
/// Expected DOCX layout (matches INV_Fragebogen_Erw.docx and similar):
///   Bold or numbered paragraph  →  section heading
///       e.g.  "1)  Angaben zur betroffenen Person"
///   Table immediately following →  field rows
///       left cell  = German label   ("Name", "Geburtsdatum", …)
///       right cell = placeholder    («AddressPerson.Surname», …) or empty
/// </summary>
public static class DocxStructureParser
{
    public static DocxQuestionnaire Parse(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Cannot read document body.");

        var result = new DocxQuestionnaire { SourceFile = Path.GetFileName(docxPath) };
        DocxSection? current = null;
        int seq = 1;

        foreach (var child in body.ChildElements)
        {
            switch (child)
            {
                case Paragraph para:
                {
                    var text = ParaText(para).Trim();
                    if (string.IsNullOrWhiteSpace(text)) break;

                    if (IsSectionHeading(para, text))
                    {
                        current = new DocxSection { Heading = CleanHeading(text) };
                        result.Sections.Add(current);
                    }
                    break;
                }

                case Table table:
                {
                    // If no heading seen yet, create a default section
                    if (current == null)
                    {
                        current = new DocxSection { Heading = "Allgemein" };
                        result.Sections.Add(current);
                    }

                    foreach (var row in table.Descendants<TableRow>())
                    {
                        var cells = row.Descendants<TableCell>().ToList();
                        if (cells.Count < 2) continue;

                        var label       = CellText(cells[0]).Trim();
                        var placeholder = CellText(cells[1]).Trim();

                        if (string.IsNullOrWhiteSpace(label)) continue;
                        if (IsHeaderRow(label))               continue;

                        current.Fields.Add(new DocxField
                        {
                            QuestionId  = $"Q{seq:D2}",
                            Label       = CleanLabel(label),
                            Placeholder = placeholder
                        });
                        seq++;
                    }
                    break;
                }
            }
        }

        return result;
    }

    // ── Heading detection ────────────────────────────────────────────────

    private static bool IsSectionHeading(Paragraph para, string text)
    {
        // Pattern: "1)  …" or "2.  …"
        if (Regex.IsMatch(text, @"^\d+[\).]")) return true;

        // All runs bold (and text is not very long — avoid bold table cells)
        var runs = para.Descendants<Run>().ToList();
        if (runs.Count > 0 && runs.All(r => r.RunProperties?.Bold != null) && text.Length < 120)
            return true;

        // Paragraph style named Heading*
        var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static string CleanHeading(string s)
        => Regex.Replace(s, @"^\d+[\).]\s*", "").Trim();

    // ── Text extraction ──────────────────────────────────────────────────

    private static string ParaText(Paragraph para)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var run in para.Descendants<Run>())
            sb.Append(run.InnerText);
        return sb.ToString();
    }

    private static string CellText(TableCell cell)
    {
        // Join multiple paragraphs inside the cell with a space
        var parts = cell.Descendants<Paragraph>()
                        .Select(ParaText)
                        .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(" ", parts);
    }

    private static string CleanLabel(string s)
        => Regex.Replace(s, @"\s+", " ").Trim();

    // ── Header-row guard ─────────────────────────────────────────────────

    private static bool IsHeaderRow(string label)
    {
        if (label.Length < 2) return true;
        // All-uppercase short labels like "NR" or "FELD" are likely column headers
        if (label == label.ToUpper() && label.Length < 10) return true;
        return false;
    }
}
