using System.Text;
using System.Xml;
using System.Xml.Linq;
using SurvNetTool.Models;

namespace SurvNetTool.Services;

/// <summary>
/// Generates a SurvNet 3.0 Transport XML file conforming to:
///   IfSG_VG-Nr. 8f SchemaSurvNet_3.0_FD2025.xsd
///   Namespace: http://www3.rki.de/ns/SurvNet/2025/01/Transport
///
/// Structure:
///   Transport (GuidTransport, CodeSiteSender, CodeSiteReceiver, TransportNumber, CreatedAt)
///     INV  (GuidRecord, VersionNo=1, Schema=11, Token=CaseId, ...)
///       Group "AngabenZurPerson"    ← Q01, Q07
///       Group "Symptome"            ← Q08–Q18
///       Group "Krankheitsverlauf"   ← Q19–Q23
///       Group "Infektionsweg"       ← Q24–Q27
///       Addressee
///       Track
///     AddPers (GuidRecord, VersionNo=1, Schema=11, Surname, Forename, DateOfBirth, ...)
///       Addressee
///       Track
/// </summary>
public static class XmlGeneratorService
{
    private const string NS = "http://www3.rki.de/ns/SurvNet/2025/01/Transport";

    // Q-id to (Group name, Field key) mapping for INV questionnaire groups.
    // Group/Field names use official SurvNet 3.0 XmlNames from
    //   IfSG_VG-Nr. 8c SurvNet3MetaPublikation-3.47.0.xlsx (Sheet "Field").
    // Fields without an official XmlName are placed in the "Fragebogen" group.
    private static readonly Dictionary<string, (string Group, string FieldKey)> QMap =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Section 1 (Q01, Q07) ──────────────────────────────────────────
        { "Q01", ("AngabenZurPerson",      "Aktenzeichen")                      },
        { "Q07", ("AngabenZurPerson",      "Beruf_Arbeitgeber")                 },

        // ── Section 2a: clinical availability (Q08) ───────────────────────
        // IdField 1109 · XmlName ClinicalInfoAvailable
        { "Q08", ("ClinicalInfoAvailable", "ClinicalInfoAvailable")             },

        // ── Section 2b: symptoms — official Symptom codes (Q09–Q18) ───────
        // All children of FormINV (IdField 137100) per MetaPublikation.
        { "Q09", ("FormINV",               "Symptom0225")  },  // Schnupfen
        { "Q10", ("FormINV",               "Symptom0130")  },  // Husten
        { "Q11", ("FormINV",               "Symptom0103")  },  // Halsschmerzen
        { "Q12", ("FormINV",               "Symptom0064")  },  // Dyspnoe/Atemnot
        { "Q13", ("FormINV",               "Symptom0145")  },  // Konjunktivitis
        { "Q14", ("FormINV",               "Symptom0179")  },  // Myokarditis
        { "Q15", ("Fragebogen",            "Erbrechen")    },  // no official INV field
        { "Q16", ("FormINV",               "Symptom0062")  },  // Durchfall
        { "Q17", ("FormINV",               "Symptom0088")  },  // Fieber ≥38.5°C
        { "Q18", ("FormINV",               "Symptom0999")  },  // andere Symptome

        // ── Section 2c: disease course (Q19–Q23) ──────────────────────────
        { "Q19", ("Fragebogen",            "Arzt_aufgesucht")                   },  // no official field
        // IdField 1161 · XmlName StatusHospitalization
        { "Q20", ("StatusHospitalization", "StatusHospitalization")             },
        // IdField 137115 · XmlName Symptom0158 (Pneumonie) — child of FormINV
        { "Q21", ("FormINV",               "Symptom0158")                       },
        // IdField 137116 · XmlName Symptom0393 (beatmungspfl. Atemwegserkr.)
        { "Q22", ("FormINV",               "Symptom0393")                       },
        // IdField 137081 · XmlName StatusVaccination
        { "Q23", ("StatusVaccination",     "StatusVaccination")                 },

        // ── Section 3: infection route (Q24–Q27) ──────────────────────────
        { "Q24", ("Fragebogen",            "Kontakt_Familie_aehnliche_Symptome") },  // no official field
        { "Q25", ("Fragebogen",            "Kontakt_Arbeitskollegen_Symptome")  },  // no official field
        // IdField 1151 · XmlName StatusPlaceOfInf
        { "Q26", ("StatusPlaceOfInf",      "StatusPlaceOfInf")                  },
        // IdField 137530 · XmlName RiskINV0001 (zoonotic animal-contact group)
        { "Q27", ("RiskINV0001",           "RiskINV0001")                       },
    };

    public static string Generate(CaseData d)
    {
        var now    = DateTime.Now;
        var nowStr = now.ToString("yyyy-MM-ddTHH:mm:ss.") + now.Millisecond.ToString("D3");

        var guidTransport = NewGuid();
        var guidInv       = NewGuid();
        var guidTrackInv  = NewGuid();
        var guidAddPers   = NewGuid();
        var guidTrackPers = NewGuid();

        XNamespace ns = NS;

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "Transport",
                new XAttribute("GuidTransport",   guidTransport),
                new XAttribute("CodeSiteSender",  d.CodeSiteSender),
                new XAttribute("CodeSiteReceiver", d.CodeSiteReceiver),
                new XAttribute("TransportNumber",  1),
                new XAttribute("CreatedAt",        nowStr),

                BuildInv(ns, d, guidInv, guidTrackInv, nowStr),
                BuildAddPers(ns, d, guidAddPers, guidTrackPers, nowStr)
            )
        );

        return FormatXml(doc);
    }

    // ── INV — disease / questionnaire record ─────────────────────────────
    private static XElement BuildInv(XNamespace ns, CaseData d,
                                     string guidRecord, string guidTrack, string nowStr)
    {
        var reportingDate = d.NotificationDate.ToString("yyyy-MM-ddT00:00:00.000");

        var inv = new XElement(ns + "INV",
            new XAttribute("GuidRecord",          guidRecord),
            new XAttribute("VersionNo",            1),
            new XAttribute("Schema",               11),
            new XAttribute("CodeRecordOwner",      d.CodeSiteSender),
            new XAttribute("CodeVersionEditor",    d.CodeSiteSender),
            new XAttribute("Token",                d.CaseId),
            new XAttribute("ToTransportManual",    "false"),
            new XAttribute("ChangedAt",            nowStr),
            new XAttribute("TransportRequestType", 1),
            new XAttribute("ReportingDate",        reportingDate),
            new XAttribute("ReportingCounty",      d.ReportingCounty),
            new XAttribute("CaseDefEpiManual",     "false")
        );

        // Build groups from questionnaire answers.
        // Official group order (matches SurvNet3MetaPublikation XmlName hierarchy):
        //   AngabenZurPerson, ClinicalInfoAvailable, FormINV,
        //   StatusHospitalization, StatusVaccination,
        //   StatusPlaceOfInf, RiskINV0001, Fragebogen
        var groupOrder = new[]
        {
            "AngabenZurPerson",
            "ClinicalInfoAvailable",
            "FormINV",
            "StatusHospitalization",
            "StatusVaccination",
            "StatusPlaceOfInf",
            "RiskINV0001",
            "Fragebogen",
        };

        // Collect fields per group (unknown group names collect into "Fragebogen")
        var groups = groupOrder.ToDictionary(g => g, _ => new List<(string Key, string Value)>());

        foreach (var qa in d.QuestionnaireAnswers)
        {
            if (QMap.TryGetValue(qa.QuestionId, out var mapping))
            {
                var val = qa.Answer?.Trim() ?? "";
                var grpName = groups.ContainsKey(mapping.Group) ? mapping.Group : "Fragebogen";
                groups[grpName].Add((mapping.FieldKey, val));
            }
        }

        // Q02-Q06 go to AddPers, not INV — skip them.
        // Ensure AngabenZurPerson always has at least Aktenzeichen.
        if (groups["AngabenZurPerson"].Count == 0)
            groups["AngabenZurPerson"].Add(("Aktenzeichen", d.CaseId));

        // Prepend FormINV disease-form field if the group has any symptom entries
        // so the catalogue value appears first.
        if (groups["FormINV"].Count > 0)
            groups["FormINV"].Insert(0, ("FormINV", "Influenza (saisonal oder pandemisch)"));

        foreach (var gName in groupOrder)
        {
            var fields = groups[gName];
            // Only emit groups that have at least one field
            if (fields.Count == 0) continue;

            var grp = new XElement(ns + "Group", new XAttribute("Name", gName));
            foreach (var (fn, fv) in fields)
                grp.Add(new XElement(ns + "Field",
                    new XAttribute("Name",  fn),
                    new XAttribute("Value", fv)));

            inv.Add(grp);
        }

        inv.Add(new XElement(ns + "Addressee",
            new XAttribute("CodeSite", d.CodeSiteSender),
            new XAttribute("Implicit", "true"),
            new XAttribute("Explicit", "false")));

        inv.Add(new XElement(ns + "Track",
            new XAttribute("GuidTrack",     guidTrack),
            new XAttribute("Action",        1),
            new XAttribute("TrackedAt",     nowStr),
            new XAttribute("CodeSite",      d.CodeSiteSender),
            new XAttribute("VersionNoSite", 1),
            new XAttribute("Software",      "SurvNetTool 1.0.0")));

        return inv;
    }

    // ── AddPers — person / demographics record ───────────────────────────
    private static XElement BuildAddPers(XNamespace ns, CaseData d,
                                         string guidRecord, string guidTrack, string nowStr)
    {
        var addPers = new XElement(ns + "AddPers",
            new XAttribute("GuidRecord",          guidRecord),
            new XAttribute("VersionNo",            1),
            new XAttribute("Schema",               11),
            new XAttribute("CodeRecordOwner",      d.CodeSiteSender),
            new XAttribute("CodeVersionEditor",    d.CodeSiteSender),
            new XAttribute("ToTransportManual",    "false"),
            new XAttribute("ChangedAt",            nowStr),
            new XAttribute("TransportRequestType", 1),
            // Required enum attributes — 0 = unbekannt for all three
            new XAttribute("Salutation",           0),
            new XAttribute("Sex",                  0),
            new XAttribute("CountryofBirthMYT",    0)
        );

        // Optional string attributes — prefer values from questionnaire answers (Q02–Q06),
        // fall back to the form fields.
        var qAnswers = d.QuestionnaireAnswers
            .ToDictionary(q => q.QuestionId, q => q.Answer?.Trim() ?? "",
                          StringComparer.OrdinalIgnoreCase);

        string Qa(string id, string fallback) =>
            qAnswers.TryGetValue(id, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

        var surname   = Qa("Q02", d.LastName);
        var forename  = Qa("Q03", d.FirstName);
        var dobRaw    = Qa("Q04", string.Empty);   // DD.MM.YYYY from questionnaire
        var addrRaw   = Qa("Q05", string.Empty);   // "Street, PLZ City"
        var phone     = Qa("Q06", d.Phone);

        if (!string.IsNullOrWhiteSpace(surname))   addPers.Add(new XAttribute("Surname",  surname));
        if (!string.IsNullOrWhiteSpace(forename))  addPers.Add(new XAttribute("Forename", forename));

        // Parse DOB from DD.MM.YYYY if present; fall back to form DateOfBirth
        if (TryParseDobToIso(dobRaw, out var dobIso) && !string.IsNullOrWhiteSpace(dobIso))
            addPers.Add(new XAttribute("DateOfBirth", dobIso));
        else if (d.DateOfBirth != default)
            addPers.Add(new XAttribute("DateOfBirth",
                d.DateOfBirth.ToString("yyyy-MM-ddT00:00:00.000")));

        // Parse address from "Street, PLZ City" if present; fall back to form fields
        if (!string.IsNullOrWhiteSpace(addrRaw))
        {
            ParseAddress(addrRaw, out var street, out var zip, out var city);
            if (!string.IsNullOrWhiteSpace(street)) addPers.Add(new XAttribute("Street",  street));
            if (!string.IsNullOrWhiteSpace(zip))    addPers.Add(new XAttribute("ZipCode", zip));
            if (!string.IsNullOrWhiteSpace(city))   addPers.Add(new XAttribute("Place",   city));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(d.Street))     addPers.Add(new XAttribute("Street",  d.Street));
            if (!string.IsNullOrWhiteSpace(d.PostalCode)) addPers.Add(new XAttribute("ZipCode", d.PostalCode));
            if (!string.IsNullOrWhiteSpace(d.City))       addPers.Add(new XAttribute("Place",   d.City));
        }

        if (!string.IsNullOrWhiteSpace(phone)) addPers.Add(new XAttribute("PhoneNumber1", phone));

        addPers.Add(new XElement(ns + "Addressee",
            new XAttribute("CodeSite", d.CodeSiteSender),
            new XAttribute("Implicit", "true"),
            new XAttribute("Explicit", "false")));

        addPers.Add(new XElement(ns + "Track",
            new XAttribute("GuidTrack",     guidTrack),
            new XAttribute("Action",        1),
            new XAttribute("TrackedAt",     nowStr),
            new XAttribute("CodeSite",      d.CodeSiteSender),
            new XAttribute("VersionNoSite", 1),
            new XAttribute("Software",      "SurvNetTool 1.0.0")));

        return addPers;
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private static bool TryParseDobToIso(string raw, out string iso)
    {
        iso = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (DateTime.TryParseExact(raw.Trim(), "dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        {
            iso = dt.ToString("yyyy-MM-ddT00:00:00.000");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Parses "Musterstr. 123, 65183 Wiesbaden" → (street, zip, city).
    /// </summary>
    private static void ParseAddress(string raw, out string street, out string zip, out string city)
    {
        street = zip = city = string.Empty;
        var commaIdx = raw.IndexOf(',');
        if (commaIdx < 0) { street = raw.Trim(); return; }

        street = raw[..commaIdx].Trim();
        var rest = raw[(commaIdx + 1)..].Trim();
        // rest = "65183 Wiesbaden"
        var spaceIdx = rest.IndexOf(' ');
        if (spaceIdx < 0) { city = rest; return; }
        zip  = rest[..spaceIdx].Trim();
        city = rest[(spaceIdx + 1)..].Trim();
    }

    private static string NewGuid() =>
        "{" + Guid.NewGuid().ToString().ToUpper() + "}";

    private static string FormatXml(XDocument doc)
    {
        using var ms = new System.IO.MemoryStream();
        var settings = new XmlWriterSettings
        {
            Indent         = true,
            IndentChars    = "  ",
            NewLineChars   = "\r\n",
            Encoding       = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
        using (var writer = XmlWriter.Create(ms, settings))
            doc.Save(writer);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
