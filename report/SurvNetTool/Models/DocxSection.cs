namespace SurvNetTool.Models;

/// <summary>
/// Represents the full parsed structure of a questionnaire DOCX:
/// one or more sections, each containing labelled fields with optional placeholders.
/// </summary>
public class DocxQuestionnaire
{
    public string SourceFile { get; set; } = string.Empty;
    public List<DocxSection> Sections { get; set; } = new();

    /// <summary>All fields across all sections, in document order.</summary>
    public IEnumerable<DocxField> AllFields => Sections.SelectMany(s => s.Fields);
}

public class DocxSection
{
    public string Heading { get; set; } = string.Empty;
    public List<DocxField> Fields { get; set; } = new();
}

public class DocxField
{
    /// <summary>Running Q-number across all sections, e.g. "Q01".</summary>
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>Left-column text: the German field label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Right-column placeholder token from the DOCX template,
    /// e.g. «AddressPerson.Surname».  Empty when the field has no pre-filled token.
    /// </summary>
    public string Placeholder { get; set; } = string.Empty;

    /// <summary>Answer typed by the user in the application.</summary>
    public string Answer { get; set; } = string.Empty;
}
