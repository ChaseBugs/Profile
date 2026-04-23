using SurvNetTool.Models;
using SurvNetTool.Services;

namespace SurvNetTool;

/// <summary>
/// Main form — SurvNet XML Transport File Generator
///
/// WORKFLOW (mirrors the real SurvNet desktop import):
///   1. Fill in case reference (CaseId = existing Meldungs-ID in SurvNet)
///   2. Fill questionnaire data for the infected person
///   3. Click "Generate XML" → creates the transport file
///   4. Click "Validate" → checks against XSD schema
///   5. Click "Save XML File" → saves the file
///   6. In SurvNet desktop app: Datei → Import → select saved XML file
///      SurvNet matches by CaseId and merges questionnaire data into the existing Meldung
/// </summary>
public partial class MainForm : Form
{
    // ── Sender fields ────────────────────────────────────────────────────
    private TextBox txtSenderName = null!;
    private TextBox txtSenderAGS = null!;

    // ── Case reference fields ────────────────────────────────────────────
    private TextBox txtCaseId = null!;
    private ComboBox cmbActionType = null!;
    private TextBox txtDiseaseCode = null!;
    private TextBox txtDiseaseName = null!;

    // ── Person fields ────────────────────────────────────────────────────
    private TextBox txtFirstName = null!;
    private TextBox txtLastName = null!;
    private DateTimePicker dtpDateOfBirth = null!;
    private ComboBox cmbSex = null!;
    private TextBox txtStreet = null!;
    private TextBox txtPostalCode = null!;
    private TextBox txtCity = null!;
    private TextBox txtState = null!;
    private TextBox txtPhone = null!;
    private TextBox txtEmail = null!;

    // ── Notification fields ──────────────────────────────────────────────
    private DateTimePicker dtpNotificationDate = null!;
    private TextBox txtReportingPhysician = null!;
    private TextBox txtReportingFacility = null!;
    private TextBox txtHealthOfficeId = null!;
    private TextBox txtHealthOfficeName = null!;

    // ── Clinical fields ──────────────────────────────────────────────────
    private DateTimePicker dtpOnsetDate = null!;
    private CheckBox chkOnsetDate = null!;
    private DateTimePicker dtpDiagnosisDate = null!;
    private CheckBox chkDiagnosisDate = null!;
    private CheckBox chkHospitalization = null!;
    private CheckBox chkDeceased = null!;
    private TextBox txtSymptoms = null!;

    // ── Lab fields ───────────────────────────────────────────────────────
    private CheckBox chkLabConfirmed = null!;
    private TextBox txtPathogenName = null!;
    private DateTimePicker dtpSampleDate = null!;
    private CheckBox chkSampleDate = null!;
    private TextBox txtSampleType = null!;
    private TextBox txtTestMethod = null!;
    private TextBox txtLaboratory = null!;

    // ── Questionnaire ────────────────────────────────────────────────────
    private DataGridView dgvQuestionnaire = null!;
    private Label lblDocxFile = null!;

    // ── Output area ──────────────────────────────────────────────────────
    private RichTextBox rtbXmlOutput = null!;
    private Label lblStatus = null!;

    public MainForm()
    {
        InitializeComponent();
        LoadDefaultSampleData();
    }

    // ── Build UI ─────────────────────────────────────────────────────────
    private void InitializeComponent()
    {
        Text = "SurvNet XML Transport File Generator  |  IfSG / RKI";
        Size = new Size(1400, 900);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        ForeColor = Color.Black;
        BackColor = Color.FromArgb(245, 247, 250);

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        Controls.Add(mainSplit);

        // All size-dependent properties must be set after the form has a real width
        Load += (_, _) =>
        {
            mainSplit.Panel1MinSize = 400;
            mainSplit.Panel2MinSize = 300;
            mainSplit.SplitterDistance = (int)(ClientSize.Width * 0.52);
        };

        // ── Left: Tab-based input ────────────────────────────────────────
        var tabs = new TabControl { Dock = DockStyle.Fill };
        mainSplit.Panel1.Controls.Add(tabs);

        tabs.TabPages.Add(BuildSenderCaseTab());
        tabs.TabPages.Add(BuildPersonTab());
        tabs.TabPages.Add(BuildNotificationTab());
        tabs.TabPages.Add(BuildClinicalLabTab());
        tabs.TabPages.Add(BuildQuestionnaireTab());

        // ── Right: TableLayoutPanel (3 rows: buttons / xml / status) ────────
        var rightTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // row 0: buttons
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // row 1: file path bar
        rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // row 2: xml output
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));   // row 3: status
        rightTable.RowCount = 4;
        mainSplit.Panel2.Controls.Add(rightTable);

        // Row 0 — buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 8, 4, 4),
            FlowDirection = FlowDirection.LeftToRight
        };

        var btnGenerate        = MakeButton("⚡ Generate XML",       Color.FromArgb(0, 120, 212));
        var btnTransformSample = MakeButton("🔄 Transform Sample",   Color.FromArgb(120, 60, 0));
        var btnTransformFile   = MakeButton("🔄 Transform File",     Color.FromArgb(160, 90, 0));
        var btnValidate        = MakeButton("✔ Validate XSD",        Color.FromArgb(16, 124, 16));
        var btnSave            = MakeButton("💾 Save XML File",       Color.FromArgb(102, 45, 145));
        var btnClear           = MakeButton("✕ Clear",               Color.FromArgb(160, 0, 0));
        var btnSample          = MakeButton("📋 Load Sample",        Color.FromArgb(0, 90, 130));

        btnGenerate.Click        += OnGenerateXml;
        btnTransformSample.Click += OnTransformDirect;
        btnTransformFile.Click   += OnTransformFromPath;
        btnValidate.Click        += OnValidateXml;
        btnSave.Click            += OnSaveXml;
        btnClear.Click           += (_, _) => rtbXmlOutput.Clear();
        btnSample.Click          += (_, _) => LoadDefaultSampleData();

        btnPanel.Controls.AddRange(new Control[]
            { btnGenerate, btnTransformSample, btnTransformFile, btnValidate, btnSave, btnSample, btnClear });

        // Row 1 — file path bar (paste or type path, then click Transform File)
        var pathBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(230, 230, 230),
            Padding = new Padding(4, 4, 4, 4)
        };
        var pathLabel = new Label
        {
            Text = "Source XML Path:", Left = 6, Top = 8, Width = 110, ForeColor = Color.Black
        };
        var txtFilePath = new TextBox
        {
            Left = 120, Top = 5, Width = 500, ForeColor = Color.Black,
            PlaceholderText = "Paste full path to source XML file here..."
        };
        var btnBrowse = new Button
        {
            Text = "...", Left = 624, Top = 4, Width = 36, Height = 24,
            FlatStyle = FlatStyle.Flat
        };
        btnBrowse.Click += (_, _) => BrowseForSourceFile(txtFilePath);
        pathBar.Controls.AddRange(new Control[] { pathLabel, txtFilePath, btnBrowse });
        // Store reference for use in handler
        Tag = txtFilePath;

        // Row 1 — XML output
        rtbXmlOutput = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 11f),
            BackColor = Color.White,
            ForeColor = Color.Black,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both
        };

        // Row 2 — status bar
        lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.FromArgb(235, 235, 235)
        };

        rightTable.Controls.Add(btnPanel,      0, 0);
        rightTable.Controls.Add(pathBar,       0, 1);
        rightTable.Controls.Add(rtbXmlOutput,  0, 2);
        rightTable.Controls.Add(lblStatus,     0, 3);

        SetStatus("Ready — click ⚡ Generate XML or 🔄 Load & Transform", Color.Gray);
    }

    // ── Tab 1: Sender + Case Reference ──────────────────────────────────
    private TabPage BuildSenderCaseTab()
    {
        var page = new TabPage("1. Sender + Case");
        var panel = MakeScrollPanel();
        page.Controls.Add(panel);

        int y = 10;
        AddGroupLabel(panel, "Sender (Gesundheitsamt)", ref y);
        txtSenderName = AddField(panel, "Sender Name:", ref y);
        txtSenderAGS  = AddField(panel, "Sender AGS:", ref y, tooltip:
            "Amtlicher Gemeindeschlüssel — 8-digit ID of the reporting health office");

        y += 10;
        AddGroupLabel(panel, "Case Reference — Existing SurvNet Meldung", ref y);

        var noteLabel = new Label
        {
            Text = "⚠ CaseId must match an EXISTING case in SurvNet for actionType=update.",
            Left = 10, Top = y, Width = 560, Height = 32,
            ForeColor = Color.FromArgb(200, 130, 0),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
        };
        panel.Controls.Add(noteLabel);
        y += 38;

        txtCaseId = AddField(panel, "Case ID (Meldungs-ID):", ref y,
            tooltip: "Must be the GUID of the existing SurvNet Meldung (e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6)");

        // GUID helper — for demo/test; in production the GUID comes FROM SurvNet
        var btnGuid = new Button
        {
            Text = "🔑 Generate Test GUID", Left = 510, Top = y - 30, Width = 160, Height = 24,
            Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(200, 200, 200), ForeColor = Color.Black,
            Cursor = Cursors.Hand
        };
        btnGuid.Click += (_, _) =>
        {
            txtCaseId.Text = Guid.NewGuid().ToString();
            txtCaseId.BackColor = Color.FromArgb(255, 255, 200);
        };
        panel.Controls.Add(btnGuid);

        var guidNote = new Label
        {
            Text = "⚠ In production: CaseId GUID comes FROM SurvNet (not generated here)",
            Left = 10, Top = y, Width = 580, Height = 20,
            ForeColor = Color.FromArgb(180, 100, 0),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
        };
        panel.Controls.Add(guidNote);
        y += 26;

        cmbActionType  = AddComboField(panel, "Action Type:", ref y,
            new[] { "update", "create", "delete" });
        txtDiseaseCode = AddField(panel, "Disease Code (ICD-10):", ref y);
        txtDiseaseName = AddField(panel, "Disease Name:", ref y);

        return page;
    }

    // ── Tab 2: Person ────────────────────────────────────────────────────
    private TabPage BuildPersonTab()
    {
        var page = new TabPage("2. Person");
        var panel = MakeScrollPanel();
        page.Controls.Add(panel);

        int y = 10;
        AddGroupLabel(panel, "Betroffene Person (Infected Person)", ref y);
        txtFirstName = AddField(panel, "First Name:", ref y);
        txtLastName  = AddField(panel, "Last Name:", ref y);

        AddLabel(panel, "Date of Birth:", y);
        dtpDateOfBirth = new DateTimePicker { Left = 200, Top = y, Width = 180, Format = DateTimePickerFormat.Short };
        panel.Controls.Add(dtpDateOfBirth);
        y += 30;

        cmbSex = AddComboField(panel, "Sex:", ref y,
            new[] { "M – männlich", "W – weiblich", "D – divers", "U – unbekannt" });

        y += 8;
        AddGroupLabel(panel, "Address", ref y);
        txtStreet     = AddField(panel, "Street:", ref y);
        txtPostalCode = AddField(panel, "Postal Code:", ref y);
        txtCity       = AddField(panel, "City:", ref y);
        txtState      = AddField(panel, "State (Bundesland):", ref y);

        y += 8;
        AddGroupLabel(panel, "Contact", ref y);
        txtPhone = AddField(panel, "Phone:", ref y);
        txtEmail = AddField(panel, "Email:", ref y);

        return page;
    }

    // ── Tab 3: Notification ──────────────────────────────────────────────
    private TabPage BuildNotificationTab()
    {
        var page = new TabPage("3. Meldung");
        var panel = MakeScrollPanel();
        page.Controls.Add(panel);

        int y = 10;
        AddGroupLabel(panel, "Notification (Meldungsangaben)", ref y);

        AddLabel(panel, "Notification Date:", y);
        dtpNotificationDate = new DateTimePicker
            { Left = 200, Top = y, Width = 180, Format = DateTimePickerFormat.Short };
        panel.Controls.Add(dtpNotificationDate);
        y += 30;

        txtReportingPhysician = AddField(panel, "Reporting Physician:", ref y);
        txtReportingFacility  = AddField(panel, "Reporting Facility:", ref y);
        txtHealthOfficeId     = AddField(panel, "Health Office ID:", ref y);
        txtHealthOfficeName   = AddField(panel, "Health Office Name:", ref y);

        return page;
    }

    // ── Tab 4: Clinical + Lab ────────────────────────────────────────────
    private TabPage BuildClinicalLabTab()
    {
        var page = new TabPage("4. Clinical + Lab");
        var panel = MakeScrollPanel();
        page.Controls.Add(panel);

        int y = 10;
        AddGroupLabel(panel, "Klinische Angaben (Clinical Data)", ref y);

        chkOnsetDate = new CheckBox { Text = "Onset Date:", Left = 10, Top = y, Width = 120, Checked = true };
        dtpOnsetDate = new DateTimePicker { Left = 200, Top = y, Width = 180, Format = DateTimePickerFormat.Short };
        chkOnsetDate.CheckedChanged += (_, _) => dtpOnsetDate.Enabled = chkOnsetDate.Checked;
        panel.Controls.Add(chkOnsetDate); panel.Controls.Add(dtpOnsetDate);
        y += 30;

        chkDiagnosisDate = new CheckBox { Text = "Diagnosis Date:", Left = 10, Top = y, Width = 120, Checked = true };
        dtpDiagnosisDate = new DateTimePicker { Left = 200, Top = y, Width = 180, Format = DateTimePickerFormat.Short };
        chkDiagnosisDate.CheckedChanged += (_, _) => dtpDiagnosisDate.Enabled = chkDiagnosisDate.Checked;
        panel.Controls.Add(chkDiagnosisDate); panel.Controls.Add(dtpDiagnosisDate);
        y += 30;

        chkHospitalization = new CheckBox { Text = "Hospitalization Required", Left = 10, Top = y };
        panel.Controls.Add(chkHospitalization); y += 28;

        chkDeceased = new CheckBox { Text = "Deceased", Left = 10, Top = y };
        panel.Controls.Add(chkDeceased); y += 32;

        AddLabel(panel, "Symptoms (one per line):", y);
        txtSymptoms = new TextBox
        {
            Left = 10, Top = y + 20, Width = 540, Height = 80,
            Multiline = true, ScrollBars = ScrollBars.Vertical
        };
        panel.Controls.Add(txtSymptoms);
        y += 110;

        AddGroupLabel(panel, "Laborangaben (Laboratory Data)", ref y);

        chkLabConfirmed = new CheckBox { Text = "Lab Confirmed", Left = 10, Top = y, Checked = true };
        panel.Controls.Add(chkLabConfirmed); y += 28;

        txtPathogenName = AddField(panel, "Pathogen Name:", ref y);

        chkSampleDate = new CheckBox { Text = "Sample Date:", Left = 10, Top = y, Width = 120, Checked = true };
        dtpSampleDate = new DateTimePicker { Left = 200, Top = y, Width = 180, Format = DateTimePickerFormat.Short };
        chkSampleDate.CheckedChanged += (_, _) => dtpSampleDate.Enabled = chkSampleDate.Checked;
        panel.Controls.Add(chkSampleDate); panel.Controls.Add(dtpSampleDate);
        y += 30;

        txtSampleType  = AddField(panel, "Sample Type:", ref y);
        txtTestMethod  = AddField(panel, "Test Method:", ref y);
        txtLaboratory  = AddField(panel, "Laboratory:", ref y);

        return page;
    }

    // ── Tab 5: Questionnaire ─────────────────────────────────────────────
    private TabPage BuildQuestionnaireTab()
    {
        var page = new TabPage("5. Questionnaire");

        // ── Row 1: Import DOCX bar ────────────────────────────────────────
        var importBar = new Panel
        {
            Dock = DockStyle.Top, Height = 36,
            BackColor = Color.FromArgb(220, 230, 245),
            Padding = new Padding(4, 4, 4, 4)
        };

        var lblImport = new Label
        {
            Text = "Import DOCX:", Left = 6, Top = 9,
            AutoSize = true, ForeColor = Color.Black
        };

        var txtDocxPath = new TextBox
        {
            Left = 110, Top = 6, Width = 340,
            PlaceholderText = "Select a questionnaire .docx file...",
            ForeColor = Color.Black
        };

        var btnBrowseDocx = new Button
        {
            Text = "📂 Browse", Left = 456, Top = 5, Width = 90, Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 100, 180), ForeColor = Color.White,
            Cursor = Cursors.Hand
        };

        var btnImportDocx = new Button
        {
            Text = "⬇ Import", Left = 552, Top = 5, Width = 80, Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 130, 0), ForeColor = Color.White,
            Cursor = Cursors.Hand
        };

        lblDocxFile = new Label
        {
            Left = 640, Top = 9, AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
        };

        btnBrowseDocx.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Select questionnaire DOCX file",
                Filter = "Word Document (*.docx)|*.docx",
                InitialDirectory = Path.Combine(
                    AppContext.BaseDirectory, "Resources")
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtDocxPath.Text = dlg.FileName;
        };

        btnImportDocx.Click += (_, _) => ImportDocxQuestionnaire(txtDocxPath.Text);

        importBar.Controls.AddRange(new Control[]
            { lblImport, txtDocxPath, btnBrowseDocx, btnImportDocx, lblDocxFile });

        // ── Row 2: manual-edit toolbar ────────────────────────────────────
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 36,
            Padding = new Padding(4, 4, 4, 4)
        };

        var btnAdd      = MakeButton("+ Add Question",   Color.FromArgb(0, 100, 0),   height: 26);
        var btnDel      = MakeButton("- Remove Selected", Color.FromArgb(140, 0, 0),  height: 26);
        var btnTemplate = MakeButton("📄 Template XML",  Color.FromArgb(90, 50, 140), height: 26);
        btnAdd.Click      += (_, _) => AddQuestionnaireRow();
        btnDel.Click      += (_, _) =>
        {
            if (dgvQuestionnaire.SelectedRows.Count > 0)
                dgvQuestionnaire.Rows.Remove(dgvQuestionnaire.SelectedRows[0]);
        };
        btnTemplate.Click += (_, _) => OnGenerateTemplateXml();
        toolbar.Controls.AddRange(new Control[] { btnAdd, btnDel, btnTemplate });

        // ── Grid ──────────────────────────────────────────────────────────
        dgvQuestionnaire = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        dgvQuestionnaire.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Id", HeaderText = "Q#", FillWeight = 6 });
        dgvQuestionnaire.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Question", HeaderText = "Feldbezeichnung (Label)", FillWeight = 34 });
        dgvQuestionnaire.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Placeholder", HeaderText = "Platzhalter (Mapping)", FillWeight = 34,
              ReadOnly = true,
              DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(0, 80, 160) } });
        dgvQuestionnaire.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Answer", HeaderText = "Antwort (Answer)", FillWeight = 26 });

        // DockStyle.Fill added first (index 0), Top panels added after (processed first
        // by WinForms layout engine which iterates in reverse collection order).
        page.Controls.Add(dgvQuestionnaire);
        page.Controls.Add(toolbar);
        page.Controls.Add(importBar);

        return page;
    }

    // Holds the parsed DOCX structure so Template XML can reference sections/headings
    private SurvNetTool.Models.DocxQuestionnaire? _currentDocxQuestionnaire;

    private void ImportDocxQuestionnaire(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("✗ No DOCX path specified.", Color.Red);
            return;
        }
        if (!File.Exists(path))
        {
            SetStatus($"✗ File not found: {path}", Color.Red);
            return;
        }

        try
        {
            var questionnaire = SurvNetTool.Services.DocxStructureParser.Parse(path);
            var allFields     = questionnaire.AllFields.ToList();

            if (allFields.Count == 0)
            {
                SetStatus("⚠ No fields found in the selected DOCX file.", Color.FromArgb(160, 100, 0));
                return;
            }

            _currentDocxQuestionnaire = questionnaire;

            dgvQuestionnaire.Rows.Clear();
            foreach (var f in allFields)
                dgvQuestionnaire.Rows.Add(f.QuestionId, f.Label, f.Placeholder, f.Answer);

            lblDocxFile.Text = $"Loaded: {Path.GetFileName(path)}  " +
                               $"({questionnaire.Sections.Count} sections, {allFields.Count} fields)";
            SetStatus(
                $"✔ Imported {allFields.Count} fields from {Path.GetFileName(path)} — " +
                "click '📄 Template XML' to view mapping, '⚡ Generate XML' for final SurvNet XML.",
                Color.FromArgb(0, 120, 0));
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Import failed: {ex.Message}", Color.Red);
            MessageBox.Show($"Failed to import DOCX:\n\n{ex.Message}", "Import Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnGenerateTemplateXml()
    {
        // Build questionnaire from grid if no DOCX was imported
        var q = _currentDocxQuestionnaire ?? BuildQuestionnaireFromGrid();

        if (!q.AllFields.Any())
        {
            SetStatus("⚠ No fields to build template from — import a DOCX first.", Color.FromArgb(160, 100, 0));
            return;
        }

        var xml = SurvNetTool.Services.TemplateXmlService.GenerateTemplate(q);
        rtbXmlOutput.Text = xml;
        ColorizeXml();
        SetStatus($"📄 Template XML generated ({q.AllFields.Count()} fields). " +
                  "Use '💾 Save XML File' to save it.", Color.FromArgb(90, 50, 140));
    }

    private SurvNetTool.Models.DocxQuestionnaire BuildQuestionnaireFromGrid()
    {
        var q = new SurvNetTool.Models.DocxQuestionnaire { SourceFile = "manual" };
        var section = new SurvNetTool.Models.DocxSection { Heading = "Fragebogen" };
        q.Sections.Add(section);
        foreach (DataGridViewRow row in dgvQuestionnaire.Rows)
        {
            var id   = row.Cells["Id"].Value?.ToString() ?? string.Empty;
            var lbl  = row.Cells["Question"].Value?.ToString() ?? string.Empty;
            var ph   = row.Cells["Placeholder"].Value?.ToString() ?? string.Empty;
            var ans  = row.Cells["Answer"].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lbl)) continue;
            section.Fields.Add(new SurvNetTool.Models.DocxField
                { QuestionId = id, Label = lbl, Placeholder = ph, Answer = ans });
        }
        return q;
    }

    // ── Event Handlers ────────────────────────────────────────────────────
    private void OnGenerateXml(object? sender, EventArgs e)
    {
        try
        {
            var data = CollectFormData();
            var xml  = XmlGeneratorService.Generate(data);
            rtbXmlOutput.Text = xml;
            ColorizeXml();
            SetStatus($"✔ XML generated — {xml.Length:N0} chars | {data.QuestionnaireAnswers.Count} questionnaire answers.", Color.FromArgb(0, 120, 0));
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Error: {ex.Message}", Color.Red);
            MessageBox.Show($"Generate XML failed:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnValidateXml(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(rtbXmlOutput.Text))
        {
            SetStatus("Nothing to validate — generate XML first.", Color.OrangeRed);
            return;
        }

        SetStatus("Validating...", Color.Gray);
        var xmlContent = rtbXmlOutput.Text;

        var result = await Task.Run(() => XsdValidatorService.Validate(xmlContent));

        if (result.IsValid)
        {
            SetStatus("✔ XSD Validation PASSED — ready for SurvNet import.", Color.FromArgb(0, 128, 0));
            MessageBox.Show(
                "XML is valid against the official RKI SurvNet 3.0 schema.\n\n" +
                "To import into SurvNet desktop:\n" +
                "  Datei → Import → [select this file]\n" +
                "  SurvNet matches by Token (Aktenzeichen) and merges data into the existing Meldung.",
                "Validation Passed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            var msg = string.Join("\n", result.Errors);
            if (result.Warnings.Count > 0)
                msg += "\n\nWarnings:\n" + string.Join("\n", result.Warnings);
            SetStatus($"✗ Validation FAILED — {result.Errors.Count} error(s).", Color.Red);
            MessageBox.Show(msg, "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Synchronous browse — called from UI thread only
    private void BrowseForSourceFile(TextBox target)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select Source XML File",
            Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            target.Text = dlg.FileName;
        dlg.Dispose();
    }

    // Direct load — no file dialog, loads sample directly
    private async void OnTransformDirect(object? sender, EventArgs e)
    {
        try
        {
            // Look for source_questionnaire.xml next to the exe
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(exeDir, "Resources", "source_questionnaire.xml"),
                Path.Combine(exeDir, "..", "..", "..", "..", "Resources", "source_questionnaire.xml"),
                @"E:\Github\Jude0629\Profile\report\SurvNetTool\Resources\source_questionnaire.xml"
            };

            var filePath = candidates.FirstOrDefault(File.Exists);
            if (filePath == null)
            {
                MessageBox.Show(
                    "source_questionnaire.xml not found.\n\nExpected location:\n" +
                    candidates[0],
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetStatus($"Loading: {Path.GetFileName(filePath)}...", Color.Gray);
            var sourceXml  = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            var survNetXml = await Task.Run(() => XmlTransformService.TransformFromSource(sourceXml));
            rtbXmlOutput.Text = survNetXml;
            SetStatus($"✔ Transformed: {Path.GetFileName(filePath)} → SurvNet XML ({survNetXml.Length:N0} chars)", Color.FromArgb(160, 90, 0));
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Error: {ex.Message}", Color.Red);
            MessageBox.Show($"Transform failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Transform using path from the path bar textbox
    private async void OnTransformFromPath(object? sender, EventArgs e)
    {
        var txtFilePath = Tag as TextBox;
        var filePath = txtFilePath?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show("Please paste a valid XML file path in the path bar above, or click '...' to browse.",
                "No File", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            SetStatus($"Transforming {Path.GetFileName(filePath)}...", Color.Gray);
            var sourceXml  = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            var survNetXml = await Task.Run(() => XmlTransformService.TransformFromSource(sourceXml));
            rtbXmlOutput.Text = survNetXml;
            SetStatus($"✔ Transformed → SurvNet XML ({survNetXml.Length:N0} chars)", Color.FromArgb(160, 90, 0));
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Error: {ex.Message}", Color.Red);
            MessageBox.Show($"Transform failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnTransformSourceXml(object? sender, EventArgs e)
    {
        try
        {
            string filePath = string.Empty;
            using (var dlg = new OpenFileDialog
            {
                Title  = "Select Source XML (Client's existing format)",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    filePath = dlg.FileName;
            }
            if (string.IsNullOrWhiteSpace(filePath)) return;

            SetStatus("Transforming...", Color.Gray);
            var sourceXml  = await Task.Run(() => File.ReadAllText(filePath, System.Text.Encoding.UTF8));
            var survNetXml = await Task.Run(() => XmlTransformService.TransformFromSource(sourceXml));
            rtbXmlOutput.Text = survNetXml;
            SetStatus($"✔ Transformed: {Path.GetFileName(filePath)} → SurvNet XML ({survNetXml.Length:N0} chars)",
                Color.FromArgb(160, 90, 0));
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Transform error: {ex.Message}", Color.Red);
            MessageBox.Show($"Transform failed:\n\n{ex.Message}", "Transform Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSaveXml(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(rtbXmlOutput.Text))
        {
            SetStatus("Nothing to save — generate XML first.", Color.OrangeRed);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = "Save SurvNet XML Transport File",
            Filter = "XML Transport Files (*.xml)|*.xml|All Files (*.*)|*.*",
            FileName = $"survnet_transport_{DateTime.Now:yyyyMMdd_HHmm}.xml",
            DefaultExt = "xml"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            // UTF-8 without BOM — SurvNet import is BOM-sensitive
            File.WriteAllText(dlg.FileName, rtbXmlOutput.Text,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            SetStatus($"Saved: {dlg.FileName}", Color.FromArgb(0, 100, 200));
        }
    }

    // ── Data collection from form ─────────────────────────────────────────
    private CaseData CollectFormData()
    {
        var data = new CaseData
        {
            SenderName         = txtSenderName.Text.Trim(),
            SenderAGS          = txtSenderAGS.Text.Trim(),
            CaseId             = txtCaseId.Text.Trim(),
            ActionType         = cmbActionType.Text.Split(' ')[0],
            DiseaseCode        = txtDiseaseCode.Text.Trim(),
            DiseaseName        = txtDiseaseName.Text.Trim(),
            FirstName          = txtFirstName.Text.Trim(),
            LastName           = txtLastName.Text.Trim(),
            DateOfBirth        = dtpDateOfBirth.Value,
            Sex                = cmbSex.Text.Split(' ')[0],
            Street             = txtStreet.Text.Trim(),
            PostalCode         = txtPostalCode.Text.Trim(),
            City               = txtCity.Text.Trim(),
            State              = txtState.Text.Trim(),
            Country            = "DE",
            Phone              = txtPhone.Text.Trim(),
            Email              = txtEmail.Text.Trim(),
            NotificationDate   = dtpNotificationDate.Value,
            ReportingPhysician = txtReportingPhysician.Text.Trim(),
            ReportingFacility  = txtReportingFacility.Text.Trim(),
            HealthOfficeId     = txtHealthOfficeId.Text.Trim(),
            HealthOfficeName   = txtHealthOfficeName.Text.Trim(),
            OnsetDate          = chkOnsetDate.Checked     ? dtpOnsetDate.Value     : null,
            DiagnosisDate      = chkDiagnosisDate.Checked ? dtpDiagnosisDate.Value : null,
            HospitalizationRequired = chkHospitalization.Checked,
            Deceased           = chkDeceased.Checked,
            LabConfirmed       = chkLabConfirmed.Checked,
            PathogenName       = txtPathogenName.Text.Trim(),
            SampleDate         = chkSampleDate.Checked ? dtpSampleDate.Value : null,
            SampleType         = txtSampleType.Text.Trim(),
            TestMethod         = txtTestMethod.Text.Trim(),
            Laboratory         = txtLaboratory.Text.Trim()
        };

        // Symptoms — split by newline
        data.Symptoms = txtSymptoms.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        // Questionnaire from grid
        foreach (DataGridViewRow row in dgvQuestionnaire.Rows)
        {
            var id  = row.Cells["Id"].Value?.ToString() ?? string.Empty;
            var q   = row.Cells["Question"].Value?.ToString() ?? string.Empty;
            var ans = row.Cells["Answer"].Value?.ToString() ?? string.Empty;   // column index 3
            if (!string.IsNullOrWhiteSpace(q))
                data.QuestionnaireAnswers.Add(new QuestionnaireAnswer
                    { QuestionId = id, QuestionText = q, Answer = ans });
        }

        return data;
    }

    // ── Sample data ───────────────────────────────────────────────────────
    private void LoadDefaultSampleData()
    {
        txtSenderName.Text        = "Gesundheitsamt Beispielkreis";
        txtSenderAGS.Text         = "01234567";
        txtCaseId.Text            = "3fa85f64-5717-4562-b3fc-2c963f66afa6"; // sample GUID from SurvNet
        cmbActionType.SelectedIndex = 0; // update
        txtDiseaseCode.Text       = "A02";
        txtDiseaseName.Text       = "Salmonellose";
        txtFirstName.Text         = "Max";
        txtLastName.Text          = "Mustermann";
        dtpDateOfBirth.Value      = new DateTime(1990, 5, 15);
        cmbSex.SelectedIndex      = 0; // M
        txtStreet.Text            = "Musterstraße 1";
        txtPostalCode.Text        = "12345";
        txtCity.Text              = "Musterstadt";
        txtState.Text             = "Berlin";
        txtPhone.Text             = "+49 30 12345678";
        txtEmail.Text             = "max.mustermann@example.de";
        dtpNotificationDate.Value = DateTime.Today;
        txtReportingPhysician.Text = "Dr. Beispiel";
        txtReportingFacility.Text  = "Krankenhaus Muster GmbH";
        txtHealthOfficeId.Text     = "01234567";
        txtHealthOfficeName.Text   = "Gesundheitsamt Beispielkreis";
        dtpOnsetDate.Value         = DateTime.Today.AddDays(-5);
        dtpDiagnosisDate.Value     = DateTime.Today.AddDays(-3);
        chkLabConfirmed.Checked    = true;
        txtPathogenName.Text       = "Salmonella Typhimurium";
        dtpSampleDate.Value        = DateTime.Today.AddDays(-3);
        txtSampleType.Text         = "Stuhl";
        txtTestMethod.Text         = "Kultur";
        txtLaboratory.Text         = "Labor Mustermann GmbH";
        txtSymptoms.Text           = "Diarrhoe\nErbrechen\nFieber";

        dgvQuestionnaire.Rows.Clear();
        var questions = new[]
        {
            ("Q01", "Lebensmittelexposition in den letzten 7 Tagen?", "«Exposure.Food»",   "Ja"),
            ("Q02", "Auslandsreise in den letzten 14 Tagen?",         "«Travel.Abroad»",   "Nein"),
            ("Q03", "Weitere Erkrankte im Haushalt?",                 "«Household.Cases»", "Nein"),
            ("Q04", "Kontakt zu Tieren (Geflügel, Reptilien)?",       "«Contact.Animal»",  "Ja"),
            ("Q05", "Verzehr von rohen Eiern oder Fleisch?",          "«Food.RawEggs»",    "Ja")
        };
        foreach (var (id, q, ph, a) in questions)
            dgvQuestionnaire.Rows.Add(id, q, ph, a);

        SetStatus("Sample data loaded.", Color.Gray);
    }

    // ── UI helpers ────────────────────────────────────────────────────────
    private void AddQuestionnaireRow()
    {
        int next = dgvQuestionnaire.Rows.Count + 1;
        dgvQuestionnaire.Rows.Add($"Q{next:D2}", "", "", "");
        dgvQuestionnaire.CurrentCell =
            dgvQuestionnaire.Rows[dgvQuestionnaire.Rows.Count - 1].Cells["Question"];
        dgvQuestionnaire.BeginEdit(true);
    }

    private static Panel MakeScrollPanel() =>
        new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(4) };

    private static TextBox AddField(Panel p, string label, ref int y,
        string? tooltip = null, bool wide = false)
    {
        AddLabel(p, label, y, tooltip);
        var txt = new TextBox { Left = 200, Top = y, Width = wide ? 360 : 300 };
        p.Controls.Add(txt);
        y += 30;
        return txt;
    }

    private static ComboBox AddComboField(Panel p, string label, ref int y, string[] items)
    {
        AddLabel(p, label, y);
        var cmb = new ComboBox { Left = 200, Top = y, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        cmb.Items.AddRange(items);
        cmb.SelectedIndex = 0;
        p.Controls.Add(cmb);
        y += 30;
        return cmb;
    }

    private static void AddLabel(Panel p, string text, int y, string? tooltip = null)
    {
        var lbl = new Label
        {
            Text = text, Left = 10, Top = y + 4,
            Width = 185, TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        p.Controls.Add(lbl);
        if (tooltip != null)
            new ToolTip().SetToolTip(lbl, tooltip);
    }

    private static void AddGroupLabel(Panel p, string text, ref int y)
    {
        var lbl = new Label
        {
            Text = "  " + text, Left = 10, Top = y,
            Width = 580, Height = 26,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 80, 160),
            BorderStyle = BorderStyle.None
        };
        p.Controls.Add(lbl);
        y += 32;
    }

    private static Button MakeButton(string text, Color color, int height = 30) =>
        new Button
        {
            Text = text, Height = height, AutoSize = true,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = color, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Margin = new Padding(2, 0, 4, 0),
            Cursor = Cursors.Hand
        };

    private void SetStatus(string msg, Color color)
    {
        lblStatus.Text = "  " + msg;
        lblStatus.ForeColor = color;
    }

    // Basic XML colorize: tags in blue, values in white
    private void ColorizeXml()
    {
        rtbXmlOutput.SelectAll();
        rtbXmlOutput.SelectionColor = Color.Black;
    }
}
