using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Hemograma
{
    internal sealed class ThemedDatePicker : DateTimePicker
    {
        internal bool Dark;
        public ThemedDatePicker()
        { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = Dark ? Color.FromArgb(37, 49, 65) : Color.White;
            Color foreground = Dark ? Color.White : Color.FromArgb(27, 58, 86);
            e.Graphics.Clear(background);
            using (var pen = new Pen(Dark ? Color.FromArgb(118, 140, 162) : Color.FromArgb(150, 165, 179)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            int buttonWidth = SystemInformation.VerticalScrollBarWidth + 4;
            var textArea = new Rectangle(3, 1, Math.Max(0, Width - buttonWidth - 6), Height - 2);
            string value = String.IsNullOrWhiteSpace(CustomFormat) ? "" : Value.ToString("dd/MM/yyyy");
            TextRenderer.DrawText(e.Graphics, value, Font, textArea, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            int x = Width - buttonWidth / 2 - 1, y = Height / 2;
            using (var brush = new SolidBrush(foreground)) e.Graphics.FillPolygon(brush, new[] { new Point(x - 4, y - 2), new Point(x + 4, y - 2), new Point(x, y + 2) });
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, textArea, foreground, background);
        }
        protected override void OnValueChanged(EventArgs e) { base.OnValueChanged(e); Invalidate(); }
        protected override void OnFormatChanged(EventArgs e) { base.OnFormatChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
    internal sealed class ThemedCheckBox : CheckBox
    {
        internal bool Dark;
        public ThemedCheckBox()
        { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        public override Size GetPreferredSize(Size proposedSize)
        { var text = TextRenderer.MeasureText(Text, Font); return new Size(text.Width + 22, Math.Max(20, text.Height + 4)); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Color foreground = Dark ? Color.White : ForeColor;
            var box = new Rectangle(1, Math.Max(1, (Height - 13) / 2), 13, 13);
            using (var brush = new SolidBrush(Checked ? Color.FromArgb(27, 110, 172) : Dark ? Color.FromArgb(37, 49, 65) : Color.White)) e.Graphics.FillRectangle(brush, box);
            using (var pen = new Pen(Dark ? Color.Silver : Color.Gray)) e.Graphics.DrawRectangle(pen, box);
            if (Checked)
                using (var pen = new Pen(Color.White, 2)) e.Graphics.DrawLines(pen, new[] { new Point(box.X + 3, box.Y + 6), new Point(box.X + 6, box.Y + 9), new Point(box.X + 11, box.Y + 3) });
            var textArea = new Rectangle(19, 0, Width - 19, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textArea, foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, textArea, foreground, BackColor);
        }
    }
    internal class PopupForm : Form
    {
        readonly Panel chrome;
        internal DialogResult ShowDimmed(Form owner)
        {
            using (var backdrop = new DimBackdrop())
            {
                backdrop.FormBorderStyle = FormBorderStyle.None;
                backdrop.ShowInTaskbar = false;
                backdrop.StartPosition = FormStartPosition.Manual;
                backdrop.Bounds = owner.Bounds;
                backdrop.BackColor = Color.Black;
                backdrop.Opacity = 0.45;
                backdrop.Text = "Fondo del diálogo";
                EventHandler followOwner = (s, e) => { if (backdrop.Bounds != owner.Bounds) backdrop.Bounds = owner.Bounds; };
                owner.LocationChanged += followOwner;
                owner.SizeChanged += followOwner;
                Form previousOwner = Owner;
                try
                {
                    backdrop.Owner = owner; backdrop.Show();
                    Owner = owner;
                    return ShowDialog(owner);
                }
                finally
                {
                    owner.LocationChanged -= followOwner;
                    owner.SizeChanged -= followOwner;
                    backdrop.Hide();
                    Owner = previousOwner;
                    backdrop.Close();
                }
            }
        }
        sealed class DimBackdrop : Form
        {
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override CreateParams CreateParams
            {
                get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000 | 0x00000080; return parameters; }
            }
        }
        protected PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; KeyPreview = true; Padding = new Padding(1);
            chrome = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 3, 8, 3) };
            var close = new HeaderButton("\uE711", "Cerrar ventana") { Width = 32, Dock = DockStyle.Right, Font = new Font("Segoe MDL2 Assets", 12) };
            close.Click += (s, e) => Close(); chrome.Controls.Add(close); Controls.Add(chrome);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); } };
        }
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (chrome != null && e.Control != chrome) chrome.SendToBack();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (ClientSize.Width < 24 || ClientSize.Height < 24) return;
            using (var path = new GraphicsPath())
            {
                const int diameter = 28;
                path.AddArc(0, 0, diameter, diameter, 180, 90);
                path.AddArc(ClientSize.Width - diameter, 0, diameter, diameter, 270, 90);
                path.AddArc(ClientSize.Width - diameter, ClientSize.Height - diameter, diameter, diameter, 0, 90);
                path.AddArc(0, ClientSize.Height - diameter, diameter, diameter, 90, 90); path.CloseFigure();
                var old = Region; Region = new Region(path); if (old != null) old.Dispose();
            }
        }
    }
}

