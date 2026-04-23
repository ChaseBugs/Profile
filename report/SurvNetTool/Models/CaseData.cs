namespace SurvNetTool.Models;

/// <summary>
/// Represents the full data model for a SurvNet 3.0 IfSG case.
/// Maps to the official Transport XML schema:
///   IfSG_VG-Nr. 8f SchemaSurvNet_3.0_FD2025.xsd
///   NS: http://www3.rki.de/ns/SurvNet/2025/01/Transport
/// </summary>
public class CaseData
{
    // ── Transport / site metadata ────────────────────────────────────────
    /// <summary>Sending authority code (StringSiteDataType, e.g. "1.01.").</summary>
    public string CodeSiteSender   { get; set; } = "1.01.";
    /// <summary>Receiving authority code (StringSiteDataType, e.g. "1." for RKI).</summary>
    public string CodeSiteReceiver { get; set; } = "1.";
    /// <summary>LandkreisCatalogueSimpleType integer (e.g. 11001001). 0 = unknown.</summary>
    public int ReportingCounty { get; set; } = 0;

    // ── Legacy sender fields (kept for form compatibility) ───────────────
    public string SenderName { get; set; } = string.Empty;
    public string SenderAGS  { get; set; } = string.Empty;

    // ── Case reference (INV/@Token) ──────────────────────────────────────
    public string CaseId      { get; set; } = string.Empty;
    public string ActionType  { get; set; } = "update";

    // ── Legacy disease fields (kept for form compatibility) ──────────────
    public string DiseaseCode { get; set; } = string.Empty;
    public string DiseaseName { get; set; } = string.Empty;

    // ── Person data → AddPers attributes ────────────────────────────────
    public string FirstName    { get; set; } = string.Empty;
    public string LastName     { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; } = default;
    public string Sex          { get; set; } = "U";
    public string Street       { get; set; } = string.Empty;
    public string PostalCode   { get; set; } = string.Empty;
    public string City         { get; set; } = string.Empty;

    // ── Legacy address fields (kept for form compatibility) ──────────────
    public string State   { get; set; } = string.Empty;
    public string Country { get; set; } = "DE";
    public string Email   { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    // ── Notification date (INV/@ReportingDate) ───────────────────────────
    public DateTime NotificationDate { get; set; } = DateTime.Today;

    // ── Legacy notification fields (kept for form compatibility) ─────────
    public string ReportingPhysician { get; set; } = string.Empty;
    public string ReportingFacility  { get; set; } = string.Empty;
    public string HealthOfficeId     { get; set; } = string.Empty;
    public string HealthOfficeName   { get; set; } = string.Empty;

    // ── Legacy clinical fields (kept for form compatibility) ─────────────
    public DateTime? OnsetDate             { get; set; }
    public DateTime? DiagnosisDate         { get; set; }
    public bool HospitalizationRequired    { get; set; }
    public bool Deceased                   { get; set; }
    public List<string> Symptoms           { get; set; } = new();
    public bool LabConfirmed               { get; set; }
    public string PathogenName             { get; set; } = string.Empty;
    public DateTime? SampleDate            { get; set; }
    public string SampleType               { get; set; } = string.Empty;
    public string TestMethod               { get; set; } = string.Empty;
    public string Laboratory               { get; set; } = string.Empty;

    // ── Questionnaire answers from grid (Q01–Q27) ────────────────────────
    /// <summary>
    /// All questionnaire answers collected from the grid.
    /// XmlGeneratorService maps these to INV/Group/Field elements.
    /// </summary>
    public List<QuestionnaireAnswer> QuestionnaireAnswers { get; set; } = new();
}

public class QuestionnaireAnswer
{
    public string QuestionId   { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string Answer       { get; set; } = string.Empty;
}
