# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
cd SurvNetTool
dotnet build

# Run (Windows only — WinForms)
dotnet run

# Validate a specific XML against the embedded schema (PowerShell)
$xsd = New-Object System.Xml.Schema.XmlSchemaSet
$xsd.Add('http://www3.rki.de/ns/SurvNet/2025/01/Transport',
         'SurvNetTool/Resources/SurvNet30Transport.xsd')
$xsd.Compile()
$errors = @()
$s = New-Object System.Xml.XmlReaderSettings
$s.ValidationType = [System.Xml.ValidationType]::Schema
$s.Schemas = $xsd
$s.add_ValidationEventHandler({ $errors += "$($_.Severity): $($_.Message)" })
$r = [System.Xml.XmlReader]::Create('SurvNetTool/Resources/INV_Fragebogen_Erw_FULL_final.xml', $s)
while ($r.Read()) {}; $r.Dispose()
if ($errors.Count -eq 0) { 'VALID' } else { $errors }
```

Target: `net8.0-windows`, WinForms, no test project. There is no lint step.

## Project Purpose

SurvNetTool is a Windows Forms app that generates **SurvNet 3.0 Transport XML** for German IfSG disease case reporting (RKI). Users fill in a questionnaire (manually or by importing a DOCX), then export a valid Transport XML that can be imported into the SurvNet desktop client via *Datei → Import*.

## Architecture

```
SurvNetTool/
  Program.cs               Entry point — STAThread, runs MainForm
  MainForm.cs              All UI; wires buttons to services; builds WinForms layout
  Models/
    CaseData.cs            Flat data model for one case; includes both form fields
                           and QuestionnaireAnswers (Q01–Q27)
    DocxSection.cs         DocxQuestionnaire / DocxSection / DocxField — parsed DOCX tree
  Services/
    XmlGeneratorService    ★ Core: CaseData → SurvNet 3.0 Transport XML string
    XsdValidatorService    Validates an XML string against embedded SurvNet30Transport.xsd
    DocxImportService      Filled questionnaire DOCX → List<QuestionnaireAnswer>
    DocxStructureParser    DOCX template → DocxQuestionnaire (labels + «placeholders»)
    TemplateXmlService     DocxQuestionnaire → template mapping XML (reference doc)
    XmlTransformService    Client's proprietary source XML → Transport XML via Generate()
  Resources/
    SurvNet30Transport.xsd      Official RKI schema (embedded; LogicalName below)
    IfSG_VG-Nr. 8f ....xsd     Original official schema (spaces in name — not embedded)
    IfSG_VG-Nr. 8c ....xlsx    Official field catalogue (SurvNet3MetaPublikation-3.47.0)
    INV_Fragebogen_Erw_FULL_final.xml    Sample Transport XML (validated, import-ready)
    INV_Fragebogen_Erw_FULL_template.xml Reference mapping document (not a transport file)
    INV_Fragebogen_Erw_FULL.docx / INV_Fragebogen_Erw.docx   Source questionnaire forms
```

The central data flow is:
```
DOCX / form input
    → CaseData (Models/CaseData.cs)
    → XmlGeneratorService.Generate(CaseData)
    → Transport XML string
    → XsdValidatorService.Validate() → shown in UI / saved to file
```

## SurvNet 3.0 XML Schema Rules

**Namespace:** `http://www3.rki.de/ns/SurvNet/2025/01/Transport`

**Structure:**
```xml
<Transport GuidTransport="{GUID}" CodeSiteSender="1.01." CodeSiteReceiver="1."
           TransportNumber="1" CreatedAt="YYYY-MM-DDTHH:mm:ss.000">
  <INV GuidRecord="{GUID}" VersionNo="1" Schema="11"
       Token="CaseId" ReportingCounty="11001001" ...>
    <Group Name="GroupXmlName">
      <Field Name="FieldXmlName" Value="..."/>
    </Group>
    <Addressee CodeSite="1.01." Implicit="true" Explicit="false"/>
    <Track GuidTrack="{GUID}" Action="1" .../>
  </INV>
  <AddPers GuidRecord="{GUID}" Surname="..." Forename="..." DateOfBirth="..."
           Salutation="0" Sex="0" CountryofBirthMYT="0" ...>
    <Addressee .../> <Track .../>
  </AddPers>
</Transport>
```

- `Schema` attribute must be `11` (only valid value in Schema 11).
- `GuidDataType`: `{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}` (uppercase hex, curly braces).
- `DateTimeDataType`: `YYYY-MM-DDTHH:mm:ss.000` (exactly 3 ms digits).
- `GuidRecord` must be unique across all `INV`/`AddPers` children of a Transport.
- `GuidTrack` must be unique across all `Track` elements in a Transport.
- `Addressee/@CodeSite` must be unique within each parent element.
- `ReportingCounty`: `LandkreisCatalogueSimpleType` integer (e.g. `11001001` = Berlin Mitte; `0` = unknown).
- `CodeSiteSender`/`CodeSiteReceiver`: dot-separated strings (e.g. `"1.01."`, `"1."`).

## Official Field Name Mapping (Q01–Q27)

Group Name and Field Name must use the official `XmlName` values from
`IfSG_VG-Nr. 8c SurvNet3MetaPublikation-3.47.0.xlsx` (Sheet "Field").
The `QMap` dictionary in `XmlGeneratorService` encodes this mapping.

| Questions | Group (XmlName) | Field (XmlName) | Notes |
|-----------|----------------|-----------------|-------|
| Q01, Q07 | `AngabenZurPerson` | `Aktenzeichen`, `Beruf_Arbeitgeber` | questionnaire-specific |
| Q02–Q06 | — | — | → `AddPers` attributes (Surname, Forename, DOB, Address, Phone) |
| Q08 | `ClinicalInfoAvailable` | `ClinicalInfoAvailable` | IdField 1109 |
| Q09 Schnupfen | `FormINV` | `Symptom0225` | IdField 137134 |
| Q10 Husten | `FormINV` | `Symptom0130` | IdField 137131 |
| Q11 Halsschmerzen | `FormINV` | `Symptom0103` | IdField 137132 |
| Q12 Dyspnoe/Atemnot | `FormINV` | `Symptom0064` | IdField 137133 |
| Q13 Konjunktivitis | `FormINV` | `Symptom0145` | IdField 137122 |
| Q14 Myokarditis | `FormINV` | `Symptom0179` | IdField 137120 |
| Q15 Erbrechen | `Fragebogen` | `Erbrechen` | no official INV field |
| Q16 Durchfall | `FormINV` | `Symptom0062` | IdField 137118 |
| Q17 Fieber | `FormINV` | `Symptom0088` | IdField 137142 |
| Q18 andere Symptome | `FormINV` | `Symptom0999` | IdField 137299 |
| Q19 Arztbesuch | `Fragebogen` | `Arzt_aufgesucht` | no official field |
| Q20 Krankenhaus | `StatusHospitalization` | `StatusHospitalization` | IdField 1161 |
| Q21 Pneumonie | `FormINV` | `Symptom0158` | IdField 137115 |
| Q22 Beatmung/ICU | `FormINV` | `Symptom0393` | IdField 137116 |
| Q23 Impfung | `StatusVaccination` | `StatusVaccination` | IdField 137081 |
| Q24, Q25 Kontakt | `Fragebogen` | `Kontakt_*` | no official field |
| Q26 Reise | `StatusPlaceOfInf` | `StatusPlaceOfInf` | IdField 1151 |
| Q27 Tierkontakt | `RiskINV0001` | `RiskINV0001` | IdField 137530 |

`FormINV` group must include `<Field Name="FormINV" Value="Influenza (saisonal oder pandemisch)"/>` as the first field (auto-prepended by `BuildInv` when symptom data is present).

## Embedded XSD Resource

`SurvNet30Transport.xsd` is embedded with a LogicalName to avoid spaces-in-filename issues:
```xml
<!-- SurvNetTool.csproj -->
<EmbeddedResource Include="Resources\SurvNet30Transport.xsd"
                  LogicalName="SurvNetTool.Resources.OfficialSurvNetSchema.xsd" />
```
`XsdValidatorService` loads it by that logical name and falls back to scanning manifest resource names. Always pass the explicit target namespace to `schemaSet.Add(TargetNamespace, reader)` — passing `null` causes the schema to register under the wrong key.

## IDE Schema Binding for XML Files

`INV_Fragebogen_Erw_FULL_final.xml` uses both mechanisms to bind the schema in VS Code and Visual Studio:
1. `<?xml-model href="SurvNet30Transport.xsd" schematypens="http://www.w3.org/2001/XMLSchema"?>` (line 2 — VS Code Red Hat XML extension)
2. `xsi:schemaLocation="http://www3.rki.de/ns/SurvNet/2025/01/Transport SurvNet30Transport.xsd"` (on `<Transport>`)

Both reference `SurvNet30Transport.xsd` (no spaces) in the same `Resources/` directory. The original RKI file `IfSG_VG-Nr. 8f SchemaSurvNet_3.0_FD2025.xsd` is kept as-is but not referenced directly.
