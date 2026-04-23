using System.Text;
using System.Xml;
using System.Xml.Linq;
using SurvNetTool.Models;

namespace SurvNetTool.Services;

/// <summary>
/// Generates the two XML outputs required for a DOCX questionnaire import.
///
/// ① Template XML — shows which DOCX field maps to which SurvNet placeholder.
///    Every &lt;Field&gt; carries the German label and the «placeholder» token.
///    Purpose: reference document so staff can see the field↔XML mapping.
///
/// ② Final XML — SurvNet transport XML (via XmlGeneratorService) with actual
///    answer values filled in by the user.
/// </summary>
public static class TemplateXmlService
{
    // ── ① Template XML ────────────────────────────────────────────────────
    public static string GenerateTemplate(DocxQuestionnaire questionnaire)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("QuestionnaireTemplate",
                new XAttribute("source",      questionnaire.SourceFile),
                new XAttribute("exportDatum", DateTime.Today.ToString("yyyy-MM-dd")),
                new XComment(" This file shows the mapping: DOCX field label → SurvNet placeholder token. "),
                new XComment(" Use it as a reference when configuring SurvNet fragenId assignments. "),
                questionnaire.Sections.Select(BuildSectionElement)
            )
        );
        return FormatXml(doc);
    }

    private static XElement BuildSectionElement(DocxSection section)
        => new("Section",
            new XAttribute("name", section.Heading),
            section.Fields.Select(f => new XElement("Field",
                new XAttribute("seq",         f.QuestionId),
                new XAttribute("label",       f.Label),
                new XAttribute("placeholder", f.Placeholder)
            ))
        );

    // ── ② Final SurvNet XML ───────────────────────────────────────────────
    /// <summary>
    /// Builds a CaseData from the questionnaire answers and forwards it to
    /// XmlGeneratorService, producing the standard SurvNet transport XML.
    /// </summary>
    public static string GenerateFinal(
        DocxQuestionnaire questionnaire,
        CaseData          caseData)
    {
        // Overwrite questionnaire answers with what the user typed
        caseData.QuestionnaireAnswers = questionnaire.AllFields
            .Select(f => new QuestionnaireAnswer
            {
                QuestionId   = f.QuestionId,
                QuestionText = f.Label,
                Answer       = f.Answer
            })
            .ToList();

        return XmlGeneratorService.Generate(caseData);
    }

    // ── Formatting ────────────────────────────────────────────────────────
    private static string FormatXml(XDocument doc)
    {
        using var ms = new System.IO.MemoryStream();
        var settings = new XmlWriterSettings
        {
            Indent        = true,
            IndentChars   = "  ",
            NewLineChars  = "\r\n",
            Encoding      = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        using (var writer = XmlWriter.Create(ms, settings))
            doc.Save(writer);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
