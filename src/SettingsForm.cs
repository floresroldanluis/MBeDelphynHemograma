using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hemograma
{
    internal sealed class SettingsForm : PopupForm
    {
        readonly Button check = new SettingsButton { Text = "Buscar actualización", Dock = DockStyle.Fill };
        readonly Button install = new SettingsButton { Text = "Instalar actualización y reiniciar el programa", Dock = DockStyle.Fill, Enabled = false };
        readonly Label state = new Label { Dock = DockStyle.Fill, Text = "Pulse Buscar actualización para consultar GitHub.", AutoSize = false };
        readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Visible = false };
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        UpdateRelease release;
        bool working;
        internal string InstallerPath { get; private set; }
        public SettingsForm(bool dark, Icon icon)
        {
            Text = "Configuración · eDelphyn"; Icon = icon; ClientSize = new Size(660, 410);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 9); MaximizeBox = false; MinimizeBox = false;
            PopupChrome.Height = 60; PopupChrome.Padding = new Padding(0, 16, 18, 2);
            foreach (Control close in PopupChrome.Controls) close.Width = 42;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 8, 22, 16), ColumnCount = 1, RowCount = 9 };
            int[] heights = { 24, 48, 36, 26, 48, 60, 58, 1 };
            foreach (int height in heights) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var eyebrow = new Label { Text = "P R E F E R E N C I A S", Font = new Font("Segoe UI", 8, FontStyle.Bold), Dock = DockStyle.Fill, Margin = new Padding(0) };
            layout.Controls.Add(eyebrow, 0, 0);
            layout.Controls.Add(new Label { Text = "Configuración", Font = new Font("Segoe UI", 21, FontStyle.Bold), Dock = DockStyle.Fill, Margin = new Padding(0) }, 0, 1);
            layout.Controls.Add(new Label { Text = "Actualización de la aplicación", Font = new Font("Segoe UI", 13, FontStyle.Bold), Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) }, 0, 2);
            layout.Controls.Add(new Label { Text = "Repositorio o GitHub Releases", Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 0) }, 0, 3);
            var repositoryBox = new SettingsSurface { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(10, 0, 10, 0), Dark = dark };
            var repository = new LinkLabel { Text = GitHubUpdates.RepositoryUrl, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold), LinkBehavior = LinkBehavior.HoverUnderline, AccessibleName = "Abrir repositorio en GitHub", AutoEllipsis = true };
            repository.LinkClicked += (sender, args) => Process.Start(new ProcessStartInfo(GitHubUpdates.RepositoryUrl) { UseShellExecute = true });
            repositoryBox.Controls.Add(repository); layout.Controls.Add(repositoryBox, 0, 4);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            check.Margin = new Padding(0, 4, 10, 10); install.Margin = new Padding(0, 4, 0, 10);
            check.Font = install.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            buttons.Controls.Add(check, 0, 0); buttons.Controls.Add(install, 1, 0); layout.Controls.Add(buttons, 0, 5);
            state.Text = "Versión actual: " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + ". Busca actualizaciones para comprobar si hay una nueva versión.";
            state.Font = new Font("Segoe UI", 8.5f); state.Margin = new Padding(0, 3, 0, 0);
            layout.Controls.Add(state, 0, 6);
            var divider = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }; layout.Controls.Add(divider, 0, 7);
            progress.Margin = new Padding(0, 5, 0, 0); layout.Controls.Add(progress, 0, 8);
            Controls.Add(layout); AppTheme.Apply(this, dark);
            Color background = dark ? Color.FromArgb(25, 34, 47) : Color.White;
            Color foreground = dark ? Color.FromArgb(230, 238, 248) : Color.FromArgb(17, 24, 39);
            ApplySurface(this, background, foreground);
            eyebrow.ForeColor = dark ? Color.SkyBlue : Color.FromArgb(45, 134, 250);
            state.ForeColor = dark ? Color.FromArgb(168, 188, 211) : Color.FromArgb(109, 131, 165);
            divider.BackColor = dark ? Color.FromArgb(60, 80, 104) : Color.FromArgb(213, 225, 243);
            repository.BackColor = dark ? Color.FromArgb(37, 51, 70) : Color.FromArgb(234, 240, 248);
            repository.LinkColor = foreground; repository.ActiveLinkColor = eyebrow.ForeColor;
            ((SettingsButton)check).Dark = dark; ((SettingsButton)install).Dark = dark;
            check.Click += async (s, e) => await CheckAsync();
            install.Click += async (s, e) => await InstallAsync();

            FormClosing += (s, e) => cancellation.Cancel();
        }
        static void ApplySurface(Control control, Color background, Color foreground)
        {
            control.BackColor = background; control.ForeColor = foreground;
            foreach (Control child in control.Controls) ApplySurface(child, background, foreground);
        }
        static GraphicsPath Rounded(Rectangle r)
        {
            var path = new GraphicsPath(); int d = Math.Min(20, Math.Min(r.Width, r.Height));
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        sealed class SettingsSurface : Panel
        {
            internal bool Dark;
            public SettingsSurface() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Rounded(new Rectangle(1, 1, Width - 3, Height - 3)))
                using (var brush = new SolidBrush(Dark ? Color.FromArgb(37, 51, 70) : Color.FromArgb(234, 240, 248)))
                using (var pen = new Pen(Dark ? Color.FromArgb(60, 80, 104) : Color.FromArgb(213, 225, 243)))
                { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
            }
        }
        sealed class SettingsButton : Button
        {
            internal bool Dark; bool hover;
            public SettingsButton() { Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Rounded(new Rectangle(1, 1, Width - 3, Height - 3)))
                using (var brush = new SolidBrush(Dark ? Color.FromArgb(37, 51, 70) : hover && Enabled ? Color.FromArgb(219, 233, 251) : Color.FromArgb(234, 240, 248)))
                using (var pen = new Pen(Dark ? Color.FromArgb(60, 80, 104) : Color.FromArgb(213, 225, 243)))
                { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
                Color text = Enabled ? ForeColor : Dark ? Color.FromArgb(155, 174, 195) : Color.FromArgb(109, 125, 151);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(6, 6, Width - 12, Height - 12));
            }
        }
        void SetWorking(bool value)
        {
            working = value; check.Enabled = !value;
            install.Enabled = !value && release != null && release.Version > Assembly.GetExecutingAssembly().GetName().Version && !String.IsNullOrEmpty(release.Url);
        }
        async Task CheckAsync()
        {
            if (working) return;
            release = null; SetWorking(true); state.Text = "Consultando versiones publicadas en GitHub…";
            try
            {
                release = await GitHubUpdates.CheckAsync(cancellation.Token);
                if (IsDisposed) return;
                if (release.Version <= Assembly.GetExecutingAssembly().GetName().Version) state.Text = "Ya tienes la versión más reciente. Versión actual: " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                else if (String.IsNullOrEmpty(release.Url)) state.Text = "Versión " + release.Version.ToString(3) + " publicada, pero falta adjuntar su instalador .exe.";
                else state.Text = "Se requiere actualización. Versión actual: " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + ". Nueva versión: " + release.Version.ToString(3) + ". Puede instalarla y reiniciar el programa.";
            }
            catch (OperationCanceledException) { if (!IsDisposed) state.Text = "Consulta cancelada o tiempo de espera agotado."; }
            catch (Exception ex) { if (!IsDisposed) state.Text = "No se pudo completar la consulta: " + ex.Message; }
            finally { if (!IsDisposed) SetWorking(false); }
        }
        async Task InstallAsync()
        {
            if (working || release == null) return;
            if (MessageBox.Show(this, "Se descargará la versión " + release.Version.ToString(3) + ". Tras verificarla, se cerrará el programa y se abrirá el instalador. En la pantalla final, deje marcada la opción Abrir eDelphyn Hemograma y pulse Finalizar para volver a abrir la aplicación. ¿Continuar?", "Instalar actualización", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            SetWorking(true); progress.Visible = true; progress.Value = 0; state.Text = "Descargando y verificando el instalador…";
            try
            {
                string file = await GitHubUpdates.DownloadAsync(release, new Progress<int>(value => { if (!IsDisposed) progress.Value = Math.Max(0, Math.Min(100, value)); }), cancellation.Token);
                if (IsDisposed || cancellation.IsCancellationRequested) return;
                InstallerPath = file; DialogResult = DialogResult.OK; Close();
            }
            catch (OperationCanceledException) { if (!IsDisposed) state.Text = "Descarga cancelada o tiempo de espera agotado."; }
            catch (Exception ex) { if (!IsDisposed) state.Text = "No se instaló la actualización: " + ex.Message; }
            finally { if (!IsDisposed) { progress.Visible = false; SetWorking(false); } }
        }
    }
}

