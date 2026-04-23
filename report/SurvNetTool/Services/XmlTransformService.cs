using System.Globalization;
using System.Xml.Linq;
using SurvNetTool.Models;

namespace SurvNetTool.Services;

/// <summary>
/// Transforms a source questionnaire XML (client's existing format)
/// into a SurvNet-compatible transport XML (RKI standard).
///
/// SOURCE format (client):           TARGET format (SurvNet/RKI):
///   InfectionReport                   SurvNetTransfer
///     Patient / Vorname         →        Person / Vorname
///     Erkrankung / ICD10        →        Erkrankung / ICD10Code
///     Labor / Erreger           →        Erreger / Erregername
///     Fragebogen / Frage nr="1" →        Fragebogen / Antwort fragenId="Q01"
///     DD.MM.YYYY dates          →        yyyy-MM-dd dates
///     männlich / Ja / Nein      →        M / true / false
/// </summary>
public static class XmlTransformService
{
    public static string TransformFromSource(string sourceXml)
    {
        var src  = XDocument.Parse(sourceXml);
        var data = MapSourceToModel(src);
        return XmlGeneratorService.Generate(data);
    }

    private static CaseData MapSourceToModel(XDocument src)
    {
        var root   = src.Root ?? throw new InvalidOperationException("XML root element not found.");
        var meta   = root.Element("ReportMeta")   ?? throw new InvalidOperationException("Missing <ReportMeta> element.");
        var pat    = root.Element("Patient")       ?? throw new InvalidOperationException("Missing <Patient> element.");
        var erk    = root.Element("Erkrankung")    ?? throw new InvalidOperationException("Missing <Erkrankung> element.");
        var labor  = root.Element("Labor")         ?? throw new InvalidOperationException("Missing <Labor> element.");
        var arzt   = root.Element("MeldendeArzt")  ?? throw new InvalidOperationException("Missing <MeldendeArzt> element.");
        var fragen = root.Element("Fragebogen");

        var data = new CaseData
        {
            // Metadaten
            SenderName     = meta.Element("HealthOffice")?.Value ?? string.Empty,
            SenderAGS      = meta.Element("HealthOffice")?.Attribute("code")?.Value ?? string.Empty,

            // MeldungsId — GUID des bestehenden SurvNet-Falles
            CaseId         = meta.Element("ExistingSurvNetCaseId")?.Value ?? string.Empty,
            ActionType     = "aktualisieren",

            // Erkrankung
            DiseaseCode    = erk.Element("ICD10")?.Value ?? string.Empty,
            DiseaseName    = erk.Element("Bezeichnung")?.Value ?? string.Empty,

            // Person
            FirstName      = pat.Element("Vorname")?.Value ?? string.Empty,
            LastName       = pat.Element("Nachname")?.Value ?? string.Empty,
            DateOfBirth    = ParseGermanDate(pat.Element("Geburtsdatum")?.Value),
            Sex            = MapGeschlecht(pat.Element("Geschlecht")?.Value),
            Street         = pat.Element("Adresse")?.Element("Strasse")?.Value ?? string.Empty,
            PostalCode     = pat.Element("Adresse")?.Element("PLZ")?.Value ?? string.Empty,
            City           = pat.Element("Adresse")?.Element("Ort")?.Value ?? string.Empty,
            State          = pat.Element("Adresse")?.Element("Bundesland")?.Value ?? string.Empty,
            Country        = "DE",
            Phone          = NormalizePhone(pat.Element("Telefon")?.Value),
            Email          = pat.Element("Email")?.Value ?? string.Empty,

            // Meldender
            NotificationDate   = ParseGermanDate(arzt.Element("Meldedatum")?.Value),
            ReportingPhysician = arzt.Element("Name")?.Value ?? string.Empty,
            ReportingFacility  = arzt.Element("Einrichtung")?.Value ?? string.Empty,
            HealthOfficeId     = meta.Element("HealthOffice")?.Attribute("code")?.Value ?? string.Empty,
            HealthOfficeName   = meta.Element("HealthOffice")?.Value ?? string.Empty,

            // Erkrankung (klinisch)
            OnsetDate               = ParseGermanDateNullable(erk.Element("Erkrankungsbeginn")?.Value),
            DiagnosisDate           = ParseGermanDateNullable(erk.Element("Diagnosedatum")?.Value),
            HospitalizationRequired = MapJaNein(erk.Element("Hospitalisierung")?.Value),
            Deceased                = MapJaNein(erk.Element("Verstorben")?.Value),
            Symptoms                = ParseSymptome(erk.Element("Symptome")?.Value),

            // Erreger
            LabConfirmed  = MapJaNein(labor.Element("Bestaetigt")?.Value),
            PathogenName  = labor.Element("Erreger")?.Value ?? string.Empty,
            SampleDate    = ParseGermanDateNullable(labor.Element("Probendatum")?.Value),
            SampleType    = labor.Element("Probenmaterial")?.Value ?? string.Empty,
            TestMethod    = labor.Element("Methode")?.Value ?? string.Empty,
            Laboratory    = labor.Element("Laborname")?.Value ?? string.Empty
        };

        // Fragebogen: <Frage nr="1"> → <Antwort fragenId="Q01">
        if (fragen != null)
        {
            foreach (var frage in fragen.Elements("Frage"))
            {
                var nr = frage.Attribute("nr")?.Value ?? "0";
                data.QuestionnaireAnswers.Add(new QuestionnaireAnswer
                {
                    QuestionId   = $"Q{int.Parse(nr):D2}",
                    QuestionText = frage.Element("Text")?.Value ?? string.Empty,
                    Answer       = frage.Element("Antwort")?.Value ?? string.Empty
                });
            }
        }

        return data;
    }

    // ── Mapping helpers ───────────────────────────────────────────────────

    private static DateTime ParseGermanDate(string? raw)
        => ParseGermanDateNullable(raw) ?? DateTime.Today;

    private static DateTime? ParseGermanDateNullable(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParseExact(raw.Trim(), "dd.MM.yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    // männlich → M,  weiblich → W,  divers → D,  * → U
    private static string MapGeschlecht(string? raw) => raw?.ToLower().Trim() switch
    {
        "männlich" or "maennlich" or "male" or "m" => "M",
        "weiblich" or "female" or "f" or "w"       => "W",
        "divers" or "diverse" or "d"               => "D",
        _                                          => "U"
    };

    // Ja/Nein → true/false
    private static bool MapJaNein(string? raw) =>
        raw?.ToLower().Trim() is "ja" or "yes" or "true" or "1";

    // "Diarrhoe, Erbrechen, Fieber" → List<string>
    private static List<string> ParseSymptome(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    // "030 12345678" → "+49 30 12345678"
    private static string NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        if (s.StartsWith("0") && !s.StartsWith("+"))
            s = "+49 " + s[1..];
        return s;
    }
}
