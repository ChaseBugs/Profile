using System.Xml;
using System.Xml.Schema;

namespace SurvNetTool.Services;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Validates an XML string against the official RKI SurvNet 3.0 schema.
///   IfSG_VG-Nr. 8f SchemaSurvNet_3.0_FD2025.xsd
///   Target namespace: http://www3.rki.de/ns/SurvNet/2025/01/Transport
/// </summary>
public static class XsdValidatorService
{
    private const string TargetNamespace = "http://www3.rki.de/ns/SurvNet/2025/01/Transport";

    // LogicalName set in .csproj for the official RKI schema
    private static readonly string XsdLogicalName =
        "SurvNetTool.Resources.OfficialSurvNetSchema.xsd";

    public static ValidationResult Validate(string xmlContent)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            var assembly = typeof(XsdValidatorService).Assembly;

            // Try the LogicalName first; if not found, scan for any embedded XSD
            // that contains the official namespace.
            Stream? xsdStream = assembly.GetManifestResourceStream(XsdLogicalName);

            if (xsdStream == null)
            {
                // Diagnostic: report which resources ARE available
                var names = assembly.GetManifestResourceNames();
                var officialRes = names.FirstOrDefault(n =>
                    n.Contains("SchemaSurvNet") || n.Contains("OfficialSurvNet"));
                if (officialRes != null)
                    xsdStream = assembly.GetManifestResourceStream(officialRes);

                if (xsdStream == null)
                {
                    result.IsValid = false;
                    result.Errors.Add(
                        $"Cannot locate embedded XSD schema. " +
                        $"Expected: '{XsdLogicalName}'. " +
                        $"Available resources: {string.Join(", ", names)}");
                    return result;
                }
            }

            var schemaSet = new XmlSchemaSet();

            // Capture schema-load errors (silent by default without this handler)
            schemaSet.ValidationEventHandler += (_, e) =>
            {
                var msg = $"[Schema load] {e.Severity}: {e.Message}";
                if (e.Severity == XmlSeverityType.Error)
                {
                    result.IsValid = false;
                    result.Errors.Add(msg);
                }
                else
                {
                    result.Warnings.Add(msg);
                }
            };

            // Explicitly pass the target namespace so it is registered correctly
            // (passing null can cause the schema to register under the wrong key)
            schemaSet.Add(TargetNamespace, XmlReader.Create(xsdStream));
            schemaSet.Compile();   // forces full compilation; surfaces hidden errors

            var settings = new XmlReaderSettings
            {
                ValidationType  = ValidationType.Schema,
                Schemas         = schemaSet,
                ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints
                                | XmlSchemaValidationFlags.ReportValidationWarnings
            };

            settings.ValidationEventHandler += (_, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    result.IsValid = false;
                    result.Errors.Add(
                        $"[Line {e.Exception.LineNumber}:{e.Exception.LinePosition}] ERROR: {e.Message}");
                }
                else
                {
                    result.Warnings.Add(
                        $"[Line {e.Exception.LineNumber}:{e.Exception.LinePosition}] WARNING: {e.Message}");
                }
            };

            using var xmlReader = XmlReader.Create(
                new System.IO.StringReader(xmlContent), settings);

            while (xmlReader.Read()) { }
        }
        catch (XmlSchemaException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"[Line {ex.LineNumber}] Schema error: {ex.Message}");
        }
        catch (XmlException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"[Line {ex.LineNumber}] XML Parse Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Unexpected error: {ex.GetType().Name}: {ex.Message}");
        }

        return result;
    }
}
