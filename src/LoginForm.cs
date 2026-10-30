using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hemograma
{
    internal sealed class LoginForm : Form
    {
        readonly TextBox user = new TextBox { Dock = DockStyle.Fill, MaxLength = 100 };
        readonly TextBox password = new TextBox { Dock = DockStyle.Fill, MaxLength = 256, UseSystemPasswordChar = true };
        readonly Button access = new AccentButton { Text = "Acceder", Width = 180, Height = 38, Anchor = AnchorStyles.None };
        readonly Label state = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        bool working;
        int failures;
        internal LoginIdentity Identity { get; private set; }
        public LoginForm(string connectionString)
        {
            Text = "Iniciar sesión · eDelphyn Hemograma";
            ClientSize = new Size(520, 570); FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 11);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Hemograma.App.ico")) Icon = new Icon(stream);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(38, 18, 38, 14), ColumnCount = 1, RowCount = 10 };
            foreach (int height in new[] { 82, 44, 34, 28, 36, 28, 36, 52, 54, 136 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var logo = new PictureBox { Size = new Size(76, 76), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.None };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Hemograma.Logo.png"))
            using (var source = Image.FromStream(stream)) logo.Image = new Bitmap(source);
            layout.Controls.Add(logo, 0, 0);
            layout.Controls.Add(new Label { Text = "eDelphyn Hemograma", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 23, FontStyle.Bold) }, 0, 1);
            layout.Controls.Add(new Label { Text = "Ingrese con su cuenta de eDelphyn", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 2);
            layout.Controls.Add(new Label { Text = "Usuario", AutoSize = true }, 0, 3); layout.Controls.Add(user, 0, 4);
            layout.Controls.Add(new Label { Text = "Contraseña", AutoSize = true }, 0, 5); layout.Controls.Add(password, 0, 6);
            layout.Controls.Add(access, 0, 7); layout.Controls.Add(state, 0, 8); layout.Controls.Add(ProductInfo.SupportLabel(), 0, 9);
            Controls.Add(layout); AppTheme.Apply(this, AppTheme.LoadDark()); AcceptButton = access;
            access.Click += async (s, e) =>
            {
                if (working) return;
                working = true; access.Enabled = user.Enabled = password.Enabled = false; state.Text = "Validando acceso…";
                string login = user.Text, secret = password.Text;
                try
                {
                    Identity = await Task.Run(() => Authentication.Authenticate(connectionString, login, secret));
                    password.Clear(); DialogResult = DialogResult.OK; working = false; Close();
                }
                catch (Exception ex)
                {
                    password.Clear(); failures++;
                    state.Text = ex is InvalidOperationException ? ex.Message : "No se pudo validar el acceso. Revise la conexión o consulte al administrador.";
                }
                secret = null;
                if (Identity == null) await Task.Delay(failures >= 5 ? 30000 : 1500);
                working = false;
                if (!IsDisposed) { access.Enabled = user.Enabled = password.Enabled = true; password.Focus(); }
            };
            FormClosing += (s, e) => { if (working) e.Cancel = true; };
            FormClosed += (s, e) => { password.Clear(); logo.Image.Dispose(); };
        }
    }
}
