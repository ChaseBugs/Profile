# SurvNetTool

A Windows desktop application (.NET 8 / WinForms) that converts German influenza (INV) questionnaire data into a valid **SurvNet 3.0 Transport XML** file, which can be imported directly into the RKI SurvNet desktop client (*Datei → Import*).

---

## What This Tool Does

German public-health authorities use **SurvNet** (Robert Koch-Institut) to record IfSG disease notifications. When a physician fills out a paper/Word questionnaire about an Influenza case, the data must be entered into SurvNet. This tool bridges that gap:

1. You fill in the case form inside the app **or** import a filled DOCX questionnaire.
2. The tool generates a correctly structured XML file that matches the official RKI SurvNet 3.0 schema.
3. You import that XML file into SurvNet desktop → the case is updated automatically.

---

## Application Workflow

```
┌──────────────────────────────────────────────────────────────────┐
│ INPUT                                                            │
│  Option A — Manual entry in the form tabs (Sender, Person,      │
│             Clinical, Lab, Questionnaire grid)                   │
│  Option B — Import a filled INV questionnaire DOCX file          │
│             (reads Q01–Q27 question/answer pairs from tables)    │
│  Option C — Transform an existing source XML file                │
│             (client's proprietary <InfectionReport> format)      │
└──────────────────────┬───────────────────────────────────────────┘
                       │ CaseData model
                       ▼
┌──────────────────────────────────────────────────────────────────┐
│ XmlGeneratorService — builds SurvNet 3.0 Transport XML           │
│  • <Transport> root with GUIDs, site codes, timestamps           │
│  • <INV> element: Groups (FormINV / Symptome / etc.) + Fields    │
│  • <AddPers> element: person demographics as attributes          │
└──────────────────────┬───────────────────────────────────────────┘
                       │ XML string
                       ▼
┌──────────────────────────────────────────────────────────────────┐
│ OUTPUT                                                           │
│  • Validate against XSD (✔ Validate XSD button)                 │
│  • Save as .xml file (💾 Save XML File button)                   │
│  • Import into SurvNet desktop: Datei → Import → select file    │
└──────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
SurvNetTool/
├── Program.cs                  Entry point
├── MainForm.cs                 All UI — tabs, buttons, questionnaire grid
├── Models/
│   ├── CaseData.cs             Central data model (one INV case)
│   └── DocxSection.cs          Parsed DOCX tree (sections, fields, placeholders)
├── Services/
│   ├── XmlGeneratorService.cs  ★ Core: CaseData → Transport XML
│   ├── XsdValidatorService.cs  Validates XML against embedded official XSD
│   ├── DocxImportService.cs    Filled DOCX → List<QuestionnaireAnswer>
│   ├── DocxStructureParser.cs  DOCX template → DocxQuestionnaire model
│   ├── TemplateXmlService.cs   DocxQuestionnaire → mapping reference XML
│   └── XmlTransformService.cs  Client's source XML → Transport XML
└── Resources/                  ← see detailed descriptions below
```

---

## Resource Files — What Each One Is and When You Use It

### XSD Schema Files

| File | When you use it |
|------|----------------|
| `IfSG_VG-Nr. 8f SchemaSurvNet_3.0_FD2025.xsd` | **The official RKI schema.** Read this to understand what attributes and elements are legally valid in a Transport XML. This is the authoritative source of truth for the XML structure. |
| `SurvNet30Transport.xsd` | **The working copy used by the app.** Identical content to the official schema above, but with no spaces in the filename so it can be: (1) embedded in the app assembly, (2) referenced by `xsi:schemaLocation`, and (3) used by the `<?xml-model?>` IDE hint. Use this when validating XML files in VS Code or running `XsdValidatorService`. |
| `SurvNetTransport.xsd` | **Legacy schema — kept for reference only.** Describes the old `<SurvNetTransfer>` XML format used before the 2025 schema update. Do **not** use this to validate new output files. Matches the format of `sample_survnet_standard.xml`. |
| `IfSG_VG-Nr. 8e SchemaReceipt.xsd` | **RKI receipt/acknowledgement schema (IfSG VG-Nr. 8e).** Describes the XML format of the confirmation message SurvNet sends back after a successful import. Not used in app code — reference it only if you need to parse import receipts. |

> **Rule of thumb:** For anything related to generating or validating output XML, use `SurvNet30Transport.xsd`. Open the `IfSG_VG-Nr. 8f ...xsd` file only to read the schema specification itself.

---

### Metadata / Catalogue File

| File | When you use it |
|------|----------------|
| `IfSG_VG-Nr. 8c SurvNet3MetaPublikation-3.47.0.xlsx` | **Official RKI field catalogue (IfSG VG-Nr. 8c).** The master reference for all field names, allowed values, and XML mappings in SurvNet 3.0. Open this when you need to know the official `XmlName` for a field, which catalogue values are valid, or whether a field exists in Schema 11. |

This Excel workbook has 7 sheets:

| Sheet | Content | When to read it |
|-------|---------|----------------|
| **Field** | 4,216 rows — every SurvNet field with its `XmlName`, parent group, data type, GUI label | When adding or changing a field mapping in `XmlGeneratorService.QMap` |
| **Catalogue** | 11,529 rows — all allowed text values for lookup fields (e.g. "Ja", "Nein", "-nicht erhoben-", symptom names, vaccine names) | When you need to know what string values are valid for a specific field |
| **Disease** | 929 rows — all reportable diseases with their codes, IfSG paragraph, case definition categories | When looking up INV's `IdType` (137), schema version support, or incubation periods |
| **DataType** | 16 rows — data type definitions (GuidDataType, DateTimeDataType, Boolean, etc.) | When you need to know the exact format for a field value |
| **TransmittingSite** | 980 rows — all German Gesundheitsämter with their `CodeSite` codes and addresses | When setting `CodeSiteSender` / `ReportingCounty` for a specific health authority |
| **FieldType** | 9 rows — field type taxonomy (Simple, Group, Table, Reference, etc.) | When you need to understand why a field is a container vs. a leaf value |
| **Type** | ~130 rows — record type taxonomy (Fall, Ausbruch, AddPers, etc.) | When you need to understand which XML element type (INV, AddPers, etc.) a field belongs to |

---

### DOCX Questionnaire Files

| File | When you use it |
|------|----------------|
| `INV_Fragebogen_Erw_FULL.docx` | **Main source questionnaire — Influenza, adults, full version.** This is the Word form that health-authority staff fill out. Contains Q01–Q27 across three sections. `DocxImportService` reads this to extract question/answer pairs when you click "Import DOCX". |
| `INV_Fragebogen_Erw.docx` | **Simplified INV questionnaire — fewer fields than FULL.** An earlier/shorter version of the same form. Also readable by `DocxImportService`. Use this to understand the minimal field set required for an INV case. |
| `CVD_Fragebogen_Erwachsene.docx` | **COVID-19 questionnaire — adults.** Included for future extension of the tool to support CVD (COVID-19) cases. The app does not currently process this file. |
| `CVD_Fragebogen_Kind.docx` | **COVID-19 questionnaire — children.** Same as above but for paediatric cases. Not yet processed by the app. |

**How the DOCX format works:**
The questionnaires are Word tables with a left column (German field label) and a right column (`«mail-merge placeholder»` or empty). `DocxImportService` reads both table and paragraph formats. `DocxStructureParser` additionally extracts the placeholder tokens (e.g. `«AddressPerson.Surname»`) to build a structured field map.

---

### XML Files

| File | When you use it |
|------|----------------|
| `INV_Fragebogen_Erw_FULL_final.xml` | **The canonical sample Transport XML.** A complete, valid, import-ready SurvNet 3.0 XML file populated with sample data from `INV_Fragebogen_Erw_FULL.docx`. Uses official SurvNet `XmlName` field codes (e.g. `Symptom0088` for fever). Validated 0 errors against `SurvNet30Transport.xsd`. Use this as the reference for what correctly generated output should look like. |
| `INV_Fragebogen_Erw_FULL_template.xml` | **Field-mapping reference document — NOT a transport file.** Shows the mapping chain: DOCX label → `«mail-merge token»` → Transport XML XPath location, for every Q01–Q27. Open this when you need to understand where a questionnaire answer ends up in the XML, or when updating `QMap` in `XmlGeneratorService`. |
| `source_questionnaire.xml` | **Sample of the client's existing proprietary XML format.** Uses a custom `<InfectionReport>` root element with German tags (Vorname, Nachname, Geburtsdatum, etc.) and DD.MM.YYYY dates. This is the format clients already have. The "🔄 Transform File" button reads this format and converts it via `XmlTransformService` → `XmlGeneratorService` into a valid Transport XML. |
| `sample_survnet_standard.xml` | **Legacy sample XML using the old `<SurvNetTransfer>` format.** This was the XML structure before the RKI's 2025 schema update. Root element is `<SurvNetTransfer>` (not `<Transport>`). Matches `SurvNetTransport.xsd`. The "🔄 Transform Sample" button loads this. Keep for historical reference — do not use this format for new imports. |

---

### PDF Document

| File | When you use it |
|------|----------------|
| `IfSG_VG-Nr. 8a SurvNet_Schnittstelle.pdf` | **Official RKI interface specification (IfSG VG-Nr. 8a).** The technical documentation for the SurvNet 3.0 XML data exchange interface. Read this when you need to understand the overall import/export protocol, how SurvNet matches incoming Transport XML to existing cases (by `GuidRecord`/`Token`), or what `TransportRequestType`, `CaseDefEpiManual`, and other non-obvious attributes mean. |

---

## How the Files Relate to Each Other

```
Official RKI documents (read-only reference)
  ├── IfSG_VG-Nr. 8a ...pdf        Protocol specification
  ├── IfSG_VG-Nr. 8c ...xlsx       Field names + allowed values
  ├── IfSG_VG-Nr. 8e ...xsd        Receipt format (not used in app)
  └── IfSG_VG-Nr. 8f ...xsd        Official schema ──┐
                                                      │ copied (no spaces)
App working files                                     ▼
  ├── SurvNet30Transport.xsd ◄── embedded in assembly + IDE hint
  │         ▲ validates against
  ├── INV_Fragebogen_Erw_FULL_final.xml    ← sample output / reference
  ├── INV_Fragebogen_Erw_FULL_template.xml ← field mapping reference
  │
  │   Source questionnaire files
  ├── INV_Fragebogen_Erw_FULL.docx  ──► DocxImportService ──► CaseData
  ├── INV_Fragebogen_Erw.docx        (same pipeline, fewer fields)
  ├── source_questionnaire.xml ──► XmlTransformService ──► CaseData
  │
  │   Legacy / future reference
  ├── SurvNetTransport.xsd           old schema (not used in generation)
  ├── sample_survnet_standard.xml    old XML format (Transform Sample)
  ├── CVD_Fragebogen_Erwachsene.docx future: COVID adult
  └── CVD_Fragebogen_Kind.docx       future: COVID children
```

---

## Questionnaire Field Mapping (Q01–Q27)

This table shows exactly where each questionnaire question ends up in the Transport XML.

| Q# | German Label | Transport XML Location | SurvNet XmlName |
|----|-------------|----------------------|-----------------|
| Q01 | Aktenzeichen | `INV/@Token` + `Group[AngabenZurPerson]/Field[Aktenzeichen]` | — |
| Q02 | Name (Nachname) | `AddPers/@Surname` | — |
| Q03 | Vorname | `AddPers/@Forename` | — |
| Q04 | Geburtsdatum | `AddPers/@DateOfBirth` (ISO 8601, converted from DD.MM.YYYY) | — |
| Q05 | Adresse | `AddPers/@Street` + `@ZipCode` + `@Place` | — |
| Q06 | Telefonnummer | `AddPers/@PhoneNumber1` | — |
| Q07 | Beruf / Arbeitgeber | `Group[AngabenZurPerson]/Field[Beruf_Arbeitgeber]` | — |
| Q08 | Hatte Symptome? | `Group[ClinicalInfoAvailable]/Field[ClinicalInfoAvailable]` | IdField 1109 |
| Q09 | Schnupfen | `Group[FormINV]/Field[Symptom0225]` | IdField 137134 |
| Q10 | Husten | `Group[FormINV]/Field[Symptom0130]` | IdField 137131 |
| Q11 | Halsschmerzen | `Group[FormINV]/Field[Symptom0103]` | IdField 137132 |
| Q12 | Atemnot / Dyspnoe | `Group[FormINV]/Field[Symptom0064]` | IdField 137133 |
| Q13 | Bindehautentzündung | `Group[FormINV]/Field[Symptom0145]` | IdField 137122 |
| Q14 | Herzmuskelentzündung | `Group[FormINV]/Field[Symptom0179]` | IdField 137120 |
| Q15 | Erbrechen | `Group[Fragebogen]/Field[Erbrechen]` | *(no official INV field)* |
| Q16 | Durchfall | `Group[FormINV]/Field[Symptom0062]` | IdField 137118 |
| Q17 | Fieber | `Group[FormINV]/Field[Symptom0088]` | IdField 137142 |
| Q18 | Weitere Symptome | `Group[FormINV]/Field[Symptom0999]` | IdField 137299 |
| Q19 | Arzt aufgesucht? | `Group[Fragebogen]/Field[Arzt_aufgesucht]` | *(no official field)* |
| Q20 | Krankenhausbehandlung | `Group[StatusHospitalization]/Field[StatusHospitalization]` | IdField 1161 |
| Q21 | Pneumonie | `Group[FormINV]/Field[Symptom0158]` | IdField 137115 |
| Q22 | Intensivstation | `Group[FormINV]/Field[Symptom0393]` | IdField 137116 |
| Q23 | Influenza-Impfung | `Group[StatusVaccination]/Field[StatusVaccination]` | IdField 137081 |
| Q24 | Kontakt Familie | `Group[Fragebogen]/Field[Kontakt_Familie_aehnliche_Symptome]` | *(no official field)* |
| Q25 | Kontakt Kollegen | `Group[Fragebogen]/Field[Kontakt_Arbeitskollegen_Symptome]` | *(no official field)* |
| Q26 | Reise unternommen | `Group[StatusPlaceOfInf]/Field[StatusPlaceOfInf]` | IdField 1151 |
| Q27 | Tierkontakt | `Group[RiskINV0001]/Field[RiskINV0001]` | IdField 137530 |

Fields Q02–Q06 go to `<AddPers>` attributes, not `<INV>` groups.
Fields marked *no official field* are stored in the `Fragebogen` group (questionnaire-only data with no matching SurvNet XmlName in the MetaPublikation).

---

## Transport XML Structure Summary

```xml
<?xml version="1.0" encoding="utf-8"?>
<?xml-model href="SurvNet30Transport.xsd" schematypens="http://www.w3.org/2001/XMLSchema"?>
<Transport xmlns="http://www3.rki.de/ns/SurvNet/2025/01/Transport"
           GuidTransport="{GUID}"
           CodeSiteSender="1.01."    <!-- sending health authority -->
           CodeSiteReceiver="1."     <!-- RKI -->
           TransportNumber="1"
           CreatedAt="YYYY-MM-DDTHH:mm:ss.000">

  <!-- Disease / questionnaire data record -->
  <INV GuidRecord="{GUID}" VersionNo="1" Schema="11"
       Token="Aktenzeichen"
       ReportingDate="YYYY-MM-DDT00:00:00.000"
       ReportingCounty="11001001"    <!-- Landkreis code from MetaPublikation TransmittingSite -->
       ...>

    <Group Name="ClinicalInfoAvailable">
      <Field Name="ClinicalInfoAvailable" Value="Ja"/>
    </Group>
    <Group Name="FormINV">
      <Field Name="FormINV" Value="Influenza (saisonal oder pandemisch)"/>
      <Field Name="Symptom0088" Value="Ja"/>   <!-- Fieber -->
      <!-- ... more Symptom fields ... -->
    </Group>
    <Group Name="StatusHospitalization">
      <Field Name="StatusHospitalization" Value="Ja"/>
    </Group>
    <!-- ... more groups ... -->

    <Addressee CodeSite="1.01." Implicit="true" Explicit="false"/>
    <Track GuidTrack="{GUID}" Action="1" TrackedAt="..." CodeSite="1.01."
           VersionNoSite="1" Software="SurvNetTool 1.0.0"/>
  </INV>

  <!-- Person / address record -->
  <AddPers GuidRecord="{GUID}" Surname="..." Forename="..."
           DateOfBirth="YYYY-MM-DDTHH:mm:ss.000"
           Salutation="0" Sex="0" CountryofBirthMYT="0"
           Street="..." ZipCode="..." Place="..." PhoneNumber1="..." ...>
    <Addressee CodeSite="1.01." Implicit="true" Explicit="false"/>
    <Track GuidTrack="{GUID}" .../>
  </AddPers>

</Transport>
```

**Key format rules:**
- `Schema` must be `11` (the only currently valid value)
- `GuidDataType`: `{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}` — uppercase hex with curly braces
- `DateTimeDataType`: `YYYY-MM-DDTHH:mm:ss.000` — exactly 3 millisecond digits
- All `GuidRecord` values must be unique within a single Transport file
- All `GuidTrack` values must be unique within a single Transport file
- Each parent element may have at most one `Addressee` with a given `CodeSite`

---

## Building and Running

```bash
cd SurvNetTool
dotnet build          # build only
dotnet run            # build + launch GUI (Windows only)
```

Requires Windows (WinForms). No test project is included.

**Validate an XML file against the schema (PowerShell):**
```powershell
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
while ($r.Read()) {}
$r.Dispose()
if ($errors.Count -eq 0) { 'VALID — 0 errors' } else { $errors }
```
