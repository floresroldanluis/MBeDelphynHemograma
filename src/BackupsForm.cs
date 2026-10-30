using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hemograma
{
    internal sealed class BackupsForm : PopupForm
    {
        readonly Repository repository;
        readonly ComboBox files = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, EnableHeadersVisualStyles = false };
        readonly Label status = new Label { Dock = DockStyle.Fill };
        readonly Button restore = new AccentButton { Text = "Restaurar seleccionadas", AutoSize = true, Height = 36, Enabled = false };
        readonly Button refresh = new AccentButton { Text = "Actualizar estado", AutoSize = true, Height = 36 };
        readonly Button select = new AccentButton { Text = "Seleccionar disponibles", AutoSize = true, Height = 36 };
        readonly Button clear = new AccentButton { Text = "Retirar selección", AutoSize = true, Height = 36 };
        DataSet backup;
        bool busy;
        internal bool Changed { get; private set; }
        sealed class BackupItem
        {
            internal string Path;
            public override string ToString() { return File.GetLastWriteTime(Path).ToString("dd/MM/yyyy HH:mm:ss") + " — Respaldo " + String.Concat(System.IO.Path.GetFileNameWithoutExtension(Path).Split('-').Last().Take(6)); }
        }
        public BackupsForm(Repository repository, string folder, bool dark, Icon icon)
        {
            this.repository = repository;
            Text = "Respaldos · eDelphyn"; Icon = icon; Font = new Font("Segoe UI", 10);
            var area = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(1100, area.Width - 60), Math.Min(680, area.Height - 60));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 12), ColumnCount = 1, RowCount = 7 };
            foreach (int height in new[] { 40, 34, 54 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 44, 46, 46 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label { Text = "Respaldos de hemograma", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true }, 0, 0);
            layout.Controls.Add(files, 0, 1);
            layout.Controls.Add(new Label { Text = "Cada fila muestra una prueba guardada antes de intentar eliminarla. El estado se consulta en la base actual: solo pueden restaurarse pruebas ausentes. El archivo original se conserva.", Dock = DockStyle.Fill }, 0, 2);
            layout.Controls.Add(grid, 0, 3);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill }; actions.Controls.Add(select); actions.Controls.Add(clear); actions.Controls.Add(refresh); actions.Controls.Add(restore); layout.Controls.Add(actions, 0, 4);
            layout.Controls.Add(status, 0, 5);
            layout.Controls.Add(new Label { Text = "Restaurar recupera el registro completo, incluidos resultado y validación. No sustituye datos existentes. SQL Server puede actualizar la fecha de modificación de la donación.", Dock = DockStyle.Fill }, 0, 6);
            Controls.Add(layout); AppTheme.Apply(this, dark);
            files.SelectedIndexChanged += async (s, e) => await LoadAsync();
            refresh.Click += async (s, e) => await LoadAsync();
            select.Click += (s, e) => SelectRows(true); clear.Click += (s, e) => SelectRows(false);
            restore.Click += async (s, e) => await RestoreAsync();
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += (s, e) => UpdateSelection();
            grid.CellBeginEdit += (s, e) => { if (busy || !(bool)((DataRowView)grid.Rows[e.RowIndex].DataBoundItem)["Available"]) e.Cancel = true; };
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };
            Shown += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    foreach (string path in Directory.GetFiles(folder, "hemograma-*.xml").OrderByDescending(File.GetLastWriteTime)) files.Items.Add(new BackupItem { Path = path });
                    if (files.Items.Count > 0) files.SelectedIndex = 0;
                    else { status.Text = "Todavía no hay respaldos guardados en este equipo."; refresh.Enabled = select.Enabled = clear.Enabled = false; }
                }
                catch (Exception ex) { status.Text = "No se pudieron abrir los respaldos: " + ex.Message; }
            };
        }
        int[] Selected()
        {
            var table = grid.DataSource as DataTable;
            return table == null ? new int[0] : table.AsEnumerable().Where(r => (bool)r["Seleccionar"] && (bool)r["Available"]).Select(r => (int)r["Index"]).ToArray();
        }
        void UpdateSelection() { restore.Enabled = !busy && backup != null && Selected().Length > 0; }
        void SetBusy(bool value)
        {
            busy = value; files.Enabled = grid.Enabled = refresh.Enabled = select.Enabled = clear.Enabled = !value;
            UseWaitCursor = value; UpdateSelection();
        }
        void SelectRows(bool selected)
        {
            var table = grid.DataSource as DataTable; if (table == null || busy) return;
            foreach (DataRow row in table.Rows) row["Seleccionar"] = selected && (bool)row["Available"];
            UpdateSelection();
        }
        async Task LoadAsync()
        {
            if (busy || files.SelectedItem == null) return;
            string path = ((BackupItem)files.SelectedItem).Path;
            backup = null; grid.DataSource = null; SetBusy(true); status.Text = "Leyendo respaldo y comprobando la base de datos…";
            try
            {
                var loaded = await Task.Run(() => Repository.LoadBackup(path));
                DataTable display; string connectionError = null;
                try { display = await Task.Run(() => repository.PreviewBackup(loaded)); }
                catch (Exception ex) { display = Repository.BackupDisplay(loaded); connectionError = ex.Message; }
                backup = loaded; grid.DataSource = display;
                grid.Columns["Index"].Visible = grid.Columns["Available"].Visible = false;
                foreach (DataGridViewColumn col in grid.Columns) col.ReadOnly = col.Name != "Seleccionar";
                grid.Columns["Seleccionar"].FillWeight = 40; grid.Columns["Seleccionar"].HeaderText = "✓";
                grid.Columns["Fecha donación"].DefaultCellStyle.Format = "dd/MM/yyyy";
                grid.Columns["Estado actual"].FillWeight = 170;
                grid.Columns["Resultado respaldado"].FillWeight = 110;
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                status.Text = display.Rows.Count + " pruebas respaldadas · " + display.AsEnumerable().Count(r => (bool)r["Available"]) + " disponibles para restaurar. Marque las que desea recuperar.";
                if (connectionError != null) status.Text = "Solo lectura: " + connectionError;
            }
            catch (Exception ex) { status.Text = "No se pudo consultar este respaldo: " + ex.Message; }
            finally { SetBusy(false); }
        }
        async Task RestoreAsync()
        {
            if (busy || backup == null) return;
            grid.EndEdit(); int[] indexes = Selected(); if (indexes.Length == 0) return;
            var display = (DataTable)grid.DataSource;
            var selected = display.AsEnumerable().Where(r => (bool)r["Seleccionar"] && (bool)r["Available"]).ToArray();
            string details = String.Join("\n", selected.Take(12).Select(r => r["Número donación"] + " · " + r["Prueba"] + " · " + r["Resultado respaldado"]));
            if (selected.Length > 12) details += "\n…";
            if (MessageBox.Show(this, "¿Restaurar " + indexes.Length + " pruebas en eDelphyn?\n\n" + details + "\n\nSe recuperará el registro completo con su validación original. Si alguna prueba ya existe, se cancelará toda la operación.", "Confirmar restauración", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            SetBusy(true); status.Text = "Verificando y restaurando…";
            try
            {
                int count = await Task.Run(() => repository.RestoreBackup(backup, indexes)); Changed = true;
                MessageBox.Show(this, "Se restauraron " + count + " pruebas. El respaldo original se conserva.", "Restauración completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, "No se confirmó la restauración. Actualice el estado antes de reintentar.\n\n" + ex.Message, "Restauración no completada", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { SetBusy(false); }
            await LoadAsync();
        }
    }
}
