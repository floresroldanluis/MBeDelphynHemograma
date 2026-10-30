using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Hemograma
{
    internal sealed class AboutForm : PopupForm
    {
        const string Website = "https://diagnosticoual.com/";
        public AboutForm(bool dark, Icon icon)
        {
            Text = "Acerca de · eDelphyn Hemograma"; Icon = icon; ClientSize = new Size(480, 410);
            Font = new Font("Segoe UI", 10);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 4, 24, 16), ColumnCount = 1, RowCount = 6 };
            foreach (int height in new[] { 90, 30, 48, 60, 40, 50 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var logo = Picture("Hemograma.Logo.png", 80); layout.Controls.Add(logo, 0, 0);
            layout.Controls.Add(new Label { Text = "VERSIÓN " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, 0, 1);
            layout.Controls.Add(new Label { Text = "eDelphyn Hemograma", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 22, FontStyle.Bold) }, 0, 2);
            layout.Controls.Add(new Label { Text = "Eliminación de las pruebas de hemograma\ndel sistema eDelphyn", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 3);
            var link = new LinkLabel { Text = Website, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            link.LinkClicked += (s, e) => { try { Process.Start(new ProcessStartInfo(Website) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo abrir el enlace"); } };
            layout.Controls.Add(link, 0, 4);
            var done = new AccentButton { Text = "Listo", Size = new Size(100, 38), Anchor = AnchorStyles.None, DialogResult = DialogResult.OK };
            layout.Controls.Add(done, 0, 5); AcceptButton = done; CancelButton = done;
            Controls.Add(layout); AppTheme.Apply(this, dark);
            FormClosed += (s, e) => logo.Image.Dispose();
        }
        static PictureBox Picture(string resource, int size)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using (var source = Image.FromStream(stream))
                return new PictureBox { Image = new Bitmap(source), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(size, size), Anchor = AnchorStyles.None };
        }
    }
}
