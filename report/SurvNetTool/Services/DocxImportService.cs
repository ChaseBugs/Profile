using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SurvNetTool.Models;

namespace SurvNetTool.Services;

/// <summary>
/// Parses a CVD questionnaire DOCX and extracts question/answer pairs.
///
/// Strategy (in priority order):
///   1. Tables  — each row with ≥2 non-empty cells is treated as (question, answer).
///      If 3 cells the first is a sequence number, cells[1]/[2] are question/answer.
///   2. Numbered paragraphs — lines matching "^\d+[.)]\s+" are questions;
///      the immediately following indented/blank line is the answer placeholder.
/// </summary>
public static class DocxImportService
{
    public static List<QuestionnaireAnswer> Import(string docxPath)
    {
        using var doc = WordprocessingDocument.Open(docxPath, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Cannot read document body.");

        // Try tables first — they carry the most structure
        var results = ExtractFromTables(body);
        if (results.Count > 0)
            return results;

        // Fallback: numbered paragraphs
        return ExtractFromParagraphs(body);
    }

    // ── Table extraction ─────────────────────────────────────────────────
    // Handles:
    //   2-column table: | Question text  | Answer / checkbox |
    //   3-column table: | No. | Question | Answer            |
    private static List<QuestionnaireAnswer> ExtractFromTables(Body body)
    {
        var results = new List<QuestionnaireAnswer>();
        int seq = 1;

        foreach (var table in body.Descendants<Table>())
        {
            foreach (var row in table.Descendants<TableRow>())
            {
                var cells = row.Descendants<TableCell>().ToList();
                if (cells.Count < 2) continue;

                string col0 = CellText(cells[0]).Trim();
                string col1 = CellText(cells[1]).Trim();
                string col2 = cells.Count > 2 ? CellText(cells[2]).Trim() : string.Empty;

                // Skip header rows (all-caps or very short marker cells)
                if (IsHeaderRow(col0, col1)) continue;

                string questionText;
                string answerText;

                if (cells.Count >= 3 && IsSequenceNumber(col0))
                {
                    // 3-col layout: Nr | Question | Answer
                    questionText = col1;
                    answerText   = col2;
                }
                else
                {
                    // 2-col layout: Question | Answer
                    questionText = col0;
                    answerText   = col1;
                }

                questionText = CleanText(questionText);
                answerText   = CleanCheckboxText(answerText);

                if (string.IsNullOrWhiteSpace(questionText)) continue;

                results.Add(new QuestionnaireAnswer
                {
                    QuestionId   = $"Q{seq:D2}",
                    QuestionText = questionText,
                    Answer       = answerText
                });
                seq++;
            }
        }

        return results;
    }

    // ── Paragraph extraction (fallback) ──────────────────────────────────
    // Detects:  "1. Frage text?"  or  "1) Frage text?"
    // The line(s) that follow (until the next numbered question) become the answer.
    private static List<QuestionnaireAnswer> ExtractFromParagraphs(Body body)
    {
        var results = new List<QuestionnaireAnswer>();
        var paras   = body.Descendants<Paragraph>().ToList();

        QuestionnaireAnswer? current = null;
        var answerLines = new List<string>();

        void Flush()
        {
            if (current == null) return;
            current.Answer = CleanCheckboxText(string.Join(" ", answerLines).Trim());
            results.Add(current);
            current     = null;
            answerLines = new List<string>();
        }

        int seq = 1;
        foreach (var para in paras)
        {
            string text = ParagraphText(para).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (TryParseNumberedQuestion(text, out string questionText))
            {
                Flush();
                current = new QuestionnaireAnswer
                {
                    QuestionId   = $"Q{seq:D2}",
                    QuestionText = CleanText(questionText)
                };
                seq++;
            }
            else if (current != null)
            {
                answerLines.Add(text);
            }
        }
        Flush();

        return results;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string CellText(TableCell cell)
    {
        var lines = cell.Descendants<Paragraph>()
                        .Select(ParagraphText)
                        .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(" | ", lines);
    }

    private static string ParagraphText(Paragraph para)
    {
        // Gather runs; convert checkbox SDT content controls to ☐/☑ symbols
        var sb = new System.Text.StringBuilder();
        foreach (var run in para.Descendants<Run>())
            sb.Append(run.InnerText);
        return sb.ToString();
    }

    // Remove redundant whitespace and non-printable chars
    private static string CleanText(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        // Remove leading sequence numbers left inside the question text (e.g. "1. ")
        s = System.Text.RegularExpressions.Regex.Replace(s, @"^\d+[.)]\s*", "");
        return s;
    }

    // Normalise checkbox characters (Word uses private-use Unicode for checkboxes)
    private static string CleanCheckboxText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace("\u2610", "☐").Replace("\u2611", "☑").Replace("\u2612", "☒");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private static bool IsSequenceNumber(string s)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(s.Trim(), @"^\d+[.)]?$");
    }

    private static bool IsHeaderRow(string col0, string col1)
    {
        // Skip rows where both cells are short uppercase tokens (table headers)
        var both = col0 + col1;
        if (both.Length < 3) return true;
        if (both == both.ToUpper() && both.Length < 40) return true;
        return false;
    }

    private static bool TryParseNumberedQuestion(string text, out string questionText)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"^\d+[.)]\s+(.+)");
        if (m.Success)
        {
            questionText = m.Groups[1].Value;
            return true;
        }
        questionText = string.Empty;
        return false;
    }
}
