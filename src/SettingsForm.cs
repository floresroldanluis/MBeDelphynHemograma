using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hemograma
{
    internal sealed class SettingsForm : PopupForm
    {
        readonly Button check = new AccentButton { Text = "Buscar actualización", AutoSize = true, Height = 36 };
        readonly Button install = new AccentButton { Text = "Instalar actualización y reiniciar", AutoSize = true, Height = 36, Enabled = false };
        readonly Label state = new Label { Dock = DockStyle.Fill, Text = "Pulse Buscar actualización para consultar GitHub.", AutoSize = false };
        readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Visible = false };
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        UpdateRelease release;
        bool working;
        internal string InstallerPath { get; private set; }
        public SettingsForm(bool dark, Icon icon)
        {
            Text = "Configuración · eDelphyn"; Icon = icon; ClientSize = new Size(740, 370);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10); MaximizeBox = false; MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 4, 24, 24), ColumnCount = 1, RowCount = 7 };
            int[] heights = { 42, 30, 36, 35, 50, 56, 23 };
            foreach (int height in heights) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label { Text = "Configuración · Actualizaciones", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Text = "Versión instalada: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3), AutoSize = true }, 0, 1);
            layout.Controls.Add(new TextBox { Text = GitHubUpdates.RepositoryUrl, ReadOnly = true, Dock = DockStyle.Fill }, 0, 2);
            var link = new LinkLabel { Text = "Abrir repositorio en GitHub", AutoSize = true };
            link.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo(GitHubUpdates.RepositoryUrl) { UseShellExecute = true });
            layout.Controls.Add(link, 0, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill }; buttons.Controls.Add(check); buttons.Controls.Add(install); layout.Controls.Add(buttons, 0, 4);
            layout.Controls.Add(state, 0, 5); layout.Controls.Add(progress, 0, 6);
            Controls.Add(layout); AppTheme.Apply(this, dark);
            check.Click += async (s, e) => await CheckAsync();
            install.Click += async (s, e) => await InstallAsync();

            FormClosing += (s, e) => cancellation.Cancel();
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
            if (MessageBox.Show(this, "Se descargará la versión " + release.Version.ToString(3) + ". Tras verificarla, se cerrará el programa y se abrirá el instalador. Al finalizar la instalación se reiniciará la aplicación. ¿Continuar?", "Instalar actualización", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
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

