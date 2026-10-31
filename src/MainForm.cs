using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SortOrder = System.Windows.Forms.SortOrder;

namespace Hemograma
{
    internal sealed class MainForm : Form
    {
        readonly Repository repository;
        readonly string databaseName;
        readonly TextBox fromCode = new TextBox { MaxLength = 20 }, toCode = new TextBox { MaxLength = 20 };
        readonly DateTimePicker fromDate = DatePicker(), toDate = DatePicker();
        readonly Button search = Button("Buscar", Color.FromArgb(27, 91, 145));
        readonly Button delete = Button("Eliminar", Color.FromArgb(27, 91, 145));
        readonly CheckBox[] testChecks = new CheckBox[9];
        readonly CheckBox selectAll = new ThemedCheckBox { Text = "Seleccionar Todo", AutoSize = true };
        readonly CheckBox selectNone = new ThemedCheckBox { Text = "Retirar Selección Todo", AutoSize = true };
        bool syncingSelection;
        readonly Label status = new Label { AutoSize = true, ForeColor = Color.FromArgb(50, 68, 85) };
        readonly DataGridView grid = new DataGridView();
        readonly Panel filters = new Panel { Dock = DockStyle.Fill };
        readonly FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), WrapContents = false };
        readonly string backupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eDelphyn.Hemograma", "Respaldos");
        SearchResult snapshot;
        bool busy;
        bool darkTheme = AppTheme.LoadDark();
        readonly Panel headerTools = new Panel { Size = new Size(166, 42), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        sealed class DateState
        {
            public bool Active = true;
            public bool Explicit;
        }
        static DateTimePicker DatePicker()
        {
            var picker = new ThemedDatePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = false, Value = DateTime.Today, MinDate = new DateTime(1753, 1, 1), MaxDate = new DateTime(9998, 12, 31) };
            picker.Tag = new DateState();
            // Native DateTimePicker has no TextAlign. Literal padding preserves its
            // calendar and keyboard editing while centering the date in the text area.
            EventHandler center = (s, e) => CenterDate(picker);
            picker.SizeChanged += center;
            picker.FontChanged += center;
            picker.HandleCreated += center;
            picker.ValueChanged += (s, e) => ActivateDate(picker);
            CenterDate(picker);
            return picker;
        }
        static void CenterDate(DateTimePicker picker)
        {
            if (!((DateState)picker.Tag).Active) { picker.CustomFormat = " "; return; }
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            int dateWidth = TextRenderer.MeasureText(picker.Value.ToString("dd/MM/yyyy"), picker.Font, Size.Empty, flags).Width;
            // Measuring whitespace alone uses a minimum glyph width in GDI.
            int spaceWidth = Math.Max(1, TextRenderer.MeasureText("x x", picker.Font, Size.Empty, flags).Width
                - TextRenderer.MeasureText("xx", picker.Font, Size.Empty, flags).Width);
            int textWidth = picker.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6;
            int spaces = Math.Max(0, (int)Math.Round((textWidth - dateWidth) / (2.0 * spaceWidth)));
            string format = (spaces == 0 ? "" : "'" + new string(' ', spaces) + "'") + "dd/MM/yyyy";
            if (picker.CustomFormat != format) picker.CustomFormat = format;
        }
        static void ActivateDate(DateTimePicker picker)
        {
            var state = (DateState)picker.Tag;
            state.Active = true; state.Explicit = true;
            CenterDate(picker);
        }
        static Button Button(string text, Color color) { return new AccentButton { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 125, Height = 37, Cursor = Cursors.Hand }; }
        public MainForm(string connectionString) : this(connectionString, null) { }
        internal MainForm(string connectionString, LoginIdentity identity)
        {
            repository = new Repository(connectionString);
            if (identity != null) { repository.OperatorId = identity.Id; repository.OperatorName = identity.Name; }
            databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            using (var iconStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Hemograma.App.ico"))
                if (iconStream != null) using (var appIcon = new Icon(iconStream)) Icon = (Icon)appIcon.Clone();
            Text = "eDelphyn · Gestión de hemograma"; Size = new Size(1260, 850); MinimumSize = new Size(1000, 740);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(244, 247, 250);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(24, 12, 24, 12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 143));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.Controls.Add(ProductInfo.SupportLabel(), 0, 5);
            Controls.Add(layout);
            var tools = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 166, WrapContents = false };
            var about = new HeaderButton("\uE946", "Acerca de");
            var theme = new HeaderButton("\uE706", "Tema claro / oscuro");
            var settings = new HeaderButton("\uE713", "Configuración");
            var tips = new ToolTip();
            foreach (var button in new[] { about, theme, settings })
            { button.Margin = new Padding(6, 0, 6, 0); tools.Controls.Add(button); tips.SetToolTip(button, button.AccessibleName); }
            headerTools.Controls.Add(tools);
            about.Click += (s, e) => { using (var popup = new AboutForm(darkTheme, Icon)) popup.ShowDimmed(this); };
            theme.Click += (s, e) =>
            {
                darkTheme = !darkTheme; AppTheme.Apply(this, darkTheme);
                try { AppTheme.Save(darkTheme); }
                catch (Exception ex) { MessageBox.Show(this, "El tema se aplicó, pero no se pudo guardar: " + ex.Message, "Tema"); }
            };
            settings.Click += (s, e) => ShowSettings();
            FormClosed += (s, e) => tips.Dispose();
            var heading = new Panel { Dock = DockStyle.Fill };
            var titleLabel = new Label { Text = "Resultados Inmunohematológicos de Donantes", AutoSize = false, Height = 42, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 21, FontStyle.Bold), ForeColor = Color.FromArgb(27, 58, 86) };
            heading.Controls.Add(titleLabel);
            var cs = new SqlConnectionStringBuilder(connectionString);
            heading.Controls.Add(new Label { Text = "Visualización y eliminación de pruebas de hemograma del eDelphyn", AutoSize = false, Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter });
            heading.Controls.Add(headerTools);
            headerTools.BringToFront();
            heading.SizeChanged += (s, e) =>
            {
                headerTools.Location = new Point(Math.Max(0, heading.ClientSize.Width - headerTools.Width), 0);
                titleLabel.Bounds = new Rectangle(166, 0, Math.Max(0, heading.ClientSize.Width - 332), 42);
                float size = 21;
                using (var fullFont = new Font("Segoe UI", 21, FontStyle.Bold))
                    size = Math.Min(21, Math.Max(12, 21f * titleLabel.Width / Math.Max(1, TextRenderer.MeasureText(titleLabel.Text, fullFont).Width)));
                if (Math.Abs(titleLabel.Font.Size - size) > 0.2f) { var oldFont = titleLabel.Font; titleLabel.Font = new Font("Segoe UI", size, FontStyle.Bold); oldFont.Dispose(); }
            };
            layout.Controls.Add(heading, 0, 0); layout.Controls.Add(filters, 0, 1);
            var codeGroup = new GroupBox { Text = "Donación número", Bounds = new Rectangle(0, 0, 315, 115) };
            var dateGroup = new GroupBox { Text = "Fecha donación", Bounds = new Rectangle(333, 0, 315, 115) };
            AddRange(codeGroup, fromCode, toCode); AddRange(dateGroup, fromDate, toDate);
            filters.Controls.Add(codeGroup); filters.Controls.Add(dateGroup); filters.Controls.Add(search);
            search.Location = new Point(672, 38); search.Click += async (s, e) => await SearchAsync();
            filters.Controls.Add(new Label { Text = "Busque por número, fecha o ambos. Fecha vacía: sin límite. Supr borra una fecha; Hasta incluye el día completo.", AutoSize = true, Location = new Point(0, 120), ForeColor = Color.DimGray });
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            selectAll.CheckedChanged += (s, e) => { if (!syncingSelection && !busy) { if (selectAll.Checked) SelectRows(true); else UpdateSelection(); } };
            selectNone.CheckedChanged += (s, e) => { if (!syncingSelection && !busy) { if (selectNone.Checked) SelectRows(false); else UpdateSelection(); } };
            toolbar.Controls.Add(selectAll); toolbar.Controls.Add(selectNone); layout.Controls.Add(toolbar, 0, 2);
            grid.Dock = DockStyle.Fill; grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.ColumnHeadersHeight = 44;
            grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(226, 234, 242);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(27, 58, 86);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 252); grid.RowTemplate.Height = 33;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", DataPropertyName = "Selected", HeaderText = "✓", FillWeight = 35, MinimumWidth = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DAT_DONATION", HeaderText = "Fecha donación", ReadOnly = true, FillWeight = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "COD_DONATION", HeaderText = "Número donación", ReadOnly = true, FillWeight = 125 });
            for (int i = 0; i < Repository.TestIds.Length; i++) grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "T" + Repository.TestIds[i], HeaderText = Repository.TestNames[i], ReadOnly = true, FillWeight = 83 });
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.Programmatic;
            grid.ColumnHeaderMouseClick += (s, e) =>
            {
                if (!busy && e.ColumnIndex >= 0)
                    SortColumn(e.ColumnIndex, grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection != SortOrder.Ascending);
            };
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, e) => UpdateSelection();
            grid.ShowCellToolTips = true;
            grid.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var row = grid.Rows[e.RowIndex].DataBoundItem as DataRowView;
                string kindColumn = grid.Columns[e.ColumnIndex].DataPropertyName + "_Kind";
                if (row == null || !row.Row.Table.Columns.Contains(kindColumn)) return;
                int kind = Convert.ToInt32(row[kindColumn]);
                e.ToolTipText = kind == 3 ? "No tiene Prueba" : kind == 2 ? "Sin Resultado" : "";
            };
            layout.Controls.Add(grid, 0, 3);
            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var testSection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8, 5, 0, 0) };
            testSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            testSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            testSection.Controls.Add(new Label { Text = "Pruebas de Hemograma a eliminar:", AutoSize = true, Margin = new Padding(0, 0, 0, 3) }, 0, 0);
            var testPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
            testSection.Controls.Add(testPanel, 0, 1);
            for (int i = 0; i < Repository.TestIds.Length; i++)
            {
                var check = new ThemedCheckBox { Text = Repository.TestNames[i], Tag = Repository.TestIds[i], AutoSize = true,
                    Checked = Repository.TestIds[i] != 93, Margin = new Padding(0, 2, 16, 6) };
                testChecks[i] = check;
                check.CheckedChanged += (s, e) => UpdateSelection();
                testPanel.Controls.Add(check);
            }
            bottom.Controls.Add(testSection, 0, 0);
            status.AutoSize = false; status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft; status.Padding = new Padding(8, 0, 0, 0);
            bottom.Controls.Add(status, 0, 1); bottom.Controls.Add(actions, 0, 2);
            actions.Controls.Add(delete);
            var backupButton = new AccentButton { Text = "Ver respaldos", Width = 135, Height = 37, Margin = new Padding(20, 3, 3, 3) };
            backupButton.Click += async (s, e) =>
            {
                if (busy) return;
                using (var viewer = new BackupsForm(repository, backupRoot, darkTheme, Icon))
                {
                    viewer.ShowDimmed(this);
                    if (viewer.Changed) { InvalidateSearch(s, e); await SearchAsync(); }
                }
            };
            actions.Controls.Add(backupButton); layout.Controls.Add(bottom, 0, 4);
            status.Text = "Ingrese un rango y pulse Buscar. No se ha eliminado ninguna prueba.";
            delete.Enabled = false; delete.Click += async (s, e) => await DeleteAsync();
            fromCode.TextChanged += DonationCodeChanged; toCode.TextChanged += DonationCodeChanged;
            fromDate.ValueChanged += InvalidateSearch; toDate.ValueChanged += InvalidateSearch;
            foreach (var picker in new[] { fromDate, toDate })
            {
                // CloseUp also handles choosing the same day already held internally
                // by a blank picker, which does not raise ValueChanged.
                picker.CloseUp += (s, e) => { ActivateDate((DateTimePicker)s); InvalidateSearch(s, e); };
                picker.KeyDown += (s, e) =>
                {
                    var date = (DateTimePicker)s;
                    if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
                    {
                        var state = (DateState)date.Tag;
                        state.Active = false; state.Explicit = true;
                        CenterDate(date); InvalidateSearch(s, EventArgs.Empty);
                        e.Handled = true; e.SuppressKeyPress = true;
                    }
                    else if (e.KeyCode == Keys.Enter && !((DateState)date.Tag).Active)
                    { ActivateDate(date); InvalidateSearch(s, EventArgs.Empty); e.Handled = true; e.SuppressKeyPress = true; }
                };
            }
            FormClosing += (s, e) => { if (busy) { e.Cancel = true; MessageBox.Show(this, "Espere a que termine la operación.", "Operación en curso"); } };
            AppTheme.Apply(this, darkTheme);
        }
        void ShowSettings()
        {
            if (busy) return;
            string installer = null;
            using (var settings = new SettingsForm(darkTheme, Icon))
                if (settings.ShowDimmed(this) == DialogResult.OK) installer = settings.InstallerPath;
            if (!String.IsNullOrEmpty(installer)) Deployment.InstallAndRestart(this, installer);
        }
        static void AddRange(GroupBox box, Control from, Control to)
        {
            box.Controls.Add(new Label { Text = "Desde", Location = new Point(18, 32), AutoSize = true });
            box.Controls.Add(new Label { Text = "Hasta", Location = new Point(18, 73), AutoSize = true });
            from.Bounds = new Rectangle(90, 28, 205, 29); to.Bounds = new Rectangle(90, 69, 205, 29);
            box.Controls.Add(from); box.Controls.Add(to);
        }
        void InvalidateSearch(object sender, EventArgs e)
        { snapshot = null; grid.DataSource = null;  UpdateSelection(); status.Text = "Filtros modificados. Pulse Buscar para actualizar los resultados."; }
        void DonationCodeChanged(object sender, EventArgs e)
        {
            bool hasCode = !String.IsNullOrWhiteSpace(fromCode.Text) || !String.IsNullOrWhiteSpace(toCode.Text);
            foreach (var picker in new[] { fromDate, toDate })
            {
                var state = (DateState)picker.Tag;
                if (!state.Explicit) { state.Active = !hasCode; CenterDate(picker); }
            }
            InvalidateSearch(sender, e);
        }
        Filter CurrentFilter() { return new Filter { FromCode = fromCode.Text, ToCode = toCode.Text,
            FromDate = ((DateState)fromDate.Tag).Active ? (DateTime?)fromDate.Value.Date : null,
            ToDate = ((DateState)toDate.Tag).Active ? (DateTime?)toDate.Value.Date : null }; }
        decimal[] SelectedIds()
        {
            var t = grid.DataSource as DataTable;
            return t == null ? new decimal[0] : t.AsEnumerable().Where(r => (bool)r["Selected"]).Select(r => Convert.ToDecimal(r["ID_DONATION"])).ToArray();
        }
        void SelectRows(bool selected)
        { if (busy) return; var t = grid.DataSource as DataTable; if (t != null) foreach (DataRow r in t.Rows) r["Selected"] = selected; UpdateSelection(); }
        void UpdateSelection()
        {
            var ids = SelectedIds();
            var selectedTests = SelectedTests();
            syncingSelection = true;
            var display = grid.DataSource as DataTable;
            bool hasRows = display != null && display.Rows.Count > 0;
            selectAll.Enabled = selectNone.Enabled = !busy && hasRows;
            selectAll.Checked = hasRows && ids.Length == display.Rows.Count;
            selectNone.Checked = hasRows && ids.Length == 0;
            syncingSelection = false;
            foreach (var check in testChecks) if (check != null) check.Enabled = !busy;
            delete.Enabled = !busy && snapshot != null && ids.Length > 0 && selectedTests.Length > 0;
            if (snapshot != null && !busy) status.Text = snapshot.Donations.Rows.Count + " donaciones · " + ids.Length + " seleccionadas · " + selectedTests.Length + " pruebas de hemograma seleccionadas";
        }
        int[] SelectedTests() { return testChecks.Where(c => c != null && c.Checked).Select(c => (int)c.Tag).ToArray(); }
        void SetBusy(bool value, string message)
        { busy = value; headerTools.Enabled = !value; filters.Enabled = !value; actions.Enabled = !value; grid.Enabled = !value; UseWaitCursor = value; UpdateSelection(); if (message != null) status.Text = message; }
        void SortColumn(int index, bool ascending)
        {
            var table = grid.DataSource as DataTable;
            if (table == null) return;
            grid.EndEdit();
            string field = grid.Columns[index].DataPropertyName;
            string direction = ascending ? " ASC" : " DESC";
            // Numeric results sort by value (2, 9, 10); text and empty results follow.
            string order = table.Columns.Contains(field + "_Number")
                ? "[" + field + "_Kind] ASC, [" + field + "_Number]" + direction + ", [" + field + "]" + direction
                : "[" + field + "]" + direction;
            table.DefaultView.Sort = order + ", [ID_DONATION] ASC";
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderCell.SortGlyphDirection = SortOrder.None;
            grid.Columns[index].HeaderCell.SortGlyphDirection = ascending ? SortOrder.Ascending : SortOrder.Descending;
        }
        void Bind(SearchResult result)
        {
            snapshot = result;
            var display = result.Donations.Copy(); display.Columns.Add("Selected", typeof(bool));
            foreach (int id in Repository.TestIds)
            {
                display.Columns.Add("T" + id, typeof(string));
                display.Columns.Add("T" + id + "_Number", typeof(decimal));
                display.Columns.Add("T" + id + "_Kind", typeof(int));
            }
            foreach (DataRow row in display.Rows)
            {
                row["Selected"] = true;
                foreach (int testId in Repository.TestIds)
                {
                    var test = result.Tests.AsEnumerable().FirstOrDefault(r => Convert.ToDecimal(r["ID_DONATION"]) == Convert.ToDecimal(row["ID_DONATION"]) && Convert.ToInt32(r["ID_TEST"]) == testId);
                    row["T" + testId] = test == null ? "***" : String.IsNullOrWhiteSpace(Convert.ToString(test["DES_RESULT"])) ? "" : Convert.ToString(test["DES_RESULT"]);
                    decimal number;
                    string raw = test == null ? "" : Convert.ToString(test["DES_RESULT"]).Trim();
                    bool numeric = Decimal.TryParse(raw.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
                    row["T" + testId + "_Number"] = numeric ? (object)number : DBNull.Value;
                    row["T" + testId + "_Kind"] = numeric ? 0 : test == null ? 3 : raw.Length == 0 ? 2 : 1;
                }
            }
            grid.DataSource = display; 
            SortColumn(2, true);
        }
        async Task SearchAsync()
        {
            if (busy) return;
            try
            {
                var filter = CurrentFilter(); filter.Validate();
                snapshot = null; grid.DataSource = null; SetBusy(true, "Consultando SQL Server…");
                Bind(await Task.Run(() => repository.Search(filter)));
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo buscar", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { SetBusy(false, snapshot == null ? "Búsqueda no completada. Revise los filtros o la conexión." : null); }
        }
        async Task DeleteAsync()
        {
            if (busy || snapshot == null) return;
            grid.EndEdit(); var ids = SelectedIds(); if (ids.Length == 0) return;
            var selectedTests = SelectedTests();
            if (selectedTests.Length == 0) return;
            int count = snapshot.Tests.AsEnumerable().Count(r => ids.Contains(Convert.ToDecimal(r["ID_DONATION"])) && selectedTests.Contains(Convert.ToInt32(r["ID_TEST"])));
            if (count == 0)
            {
                MessageBox.Show(this, "Las donaciones marcadas no tienen registros de las pruebas seleccionadas.\n\nEl símbolo *** indica que esa prueba no existe en la donación. No hay registros que eliminar para esta selección.",
                    "Sin registros para eliminar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var codes = snapshot.Donations.AsEnumerable().Where(r => ids.Contains(Convert.ToDecimal(r["ID_DONATION"]))).Select(r => Convert.ToString(r["COD_DONATION"])).ToArray();
            string message = "¿Deseas eliminar las pruebas seleccionadas?\n\n" + ids.Length + " donaciones / " + count + " registros de hemograma.\n" +
                String.Join(", ", codes.Take(12)) + (codes.Length > 12 ? "…" : "") + "\n\n" + String.Join(", ", testChecks.Where(c => c.Checked).Select(c => c.Text)) +
                "\n\nSe eliminarán los registros de estas pruebas, incluidos los que tienen resultados o están validados.\nSe guardará un respaldo local antes de eliminar.\n\nEsta operación modifica " + databaseName + ".";
            if (MessageBox.Show(this, message, "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            { status.Text = "Operación cancelada. No se eliminó ningún registro."; return; }
            var captured = snapshot; var filter = CurrentFilter(); bool committed = false;
            try
            {
                SetBusy(true, "Verificando y eliminando las pruebas seleccionadas…");
                var result = await Task.Run(() => repository.Delete(captured, ids, backupRoot, selectedTests)); committed = true;
                snapshot = null; grid.DataSource = null; 
                MessageBox.Show(this, "Se eliminaron " + result.Count + " registros de hemograma.\n\nRespaldo: " + (result.Backup ?? "No fue necesario."), "Operación completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Bind(await Task.Run(() => repository.Search(filter)));
            }
            catch (Exception ex)
            {
                snapshot = null; grid.DataSource = null; 
                MessageBox.Show(this, (committed ? "La eliminación se completó, pero no se pudo actualizar la lista.\n" : "No se pudo confirmar la operación. Vuelva a buscar para verificar el estado de los registros.\n") + ex.Message, "Revisar operación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { SetBusy(false, snapshot == null ? "Pulse Buscar para consultar el estado actual." : null); }
        }
    }
}



