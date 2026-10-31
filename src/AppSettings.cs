using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Hemograma
{
    internal static class AppTheme
    {
        static readonly string ThemeFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eDelphyn.Hemograma", "theme.txt");
        internal static bool LoadDark() { try { return File.Exists(ThemeFile) && File.ReadAllText(ThemeFile).Trim() == "dark"; } catch (IOException) { return false; } }
        internal static void Save(bool dark) { Directory.CreateDirectory(Path.GetDirectoryName(ThemeFile)); File.WriteAllText(ThemeFile, dark ? "dark" : "light"); }
        internal static void Apply(Control control, bool dark)
        {
            Color background = dark ? Color.FromArgb(25, 34, 47) : Color.FromArgb(244, 247, 250);
            Color surface = dark ? Color.FromArgb(37, 49, 65) : Color.White;
            Color foreground = dark ? Color.FromArgb(230, 238, 248) : Color.FromArgb(27, 58, 86);
            control.BackColor = background; control.ForeColor = foreground;
            var button = control as Button;
            if (button != null)
            {
                button.BackColor = Color.FromArgb(27, 91, 145); button.ForeColor = Color.White;
                button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = dark ? Color.FromArgb(80, 126, 164) : Color.FromArgb(200, 218, 235);
            }
            var icon = control as HeaderButton;
            if (icon != null) { icon.Dark = dark; icon.Invalidate(); }
            var action = control as AccentButton;
            if (action != null) { action.Dark = dark; action.Invalidate(); }
            if (control is TextBoxBase || control is ListBox) control.BackColor = surface;
            var link = control as LinkLabel;
            if (link != null) { link.LinkColor = dark ? Color.SkyBlue : Color.FromArgb(20, 101, 175); link.ActiveLinkColor = link.LinkColor; }
            var date = control as DateTimePicker;
            if (date != null) { date.CalendarMonthBackground = surface; date.CalendarForeColor = foreground; }
            var themedDate = control as ThemedDatePicker;
            if (themedDate != null) { themedDate.Dark = dark; themedDate.Invalidate(); }
            var themedCheck = control as ThemedCheckBox;
            if (themedCheck != null) { themedCheck.Dark = dark; themedCheck.ForeColor = dark ? Color.White : foreground; themedCheck.Invalidate(); }
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = surface;
                grid.DefaultCellStyle.BackColor = surface; grid.DefaultCellStyle.ForeColor = foreground;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(27, 110, 172); grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(31, 43, 58) : Color.FromArgb(246, 249, 252);
                grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(45, 64, 84) : Color.FromArgb(226, 234, 242);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = foreground;
                grid.GridColor = dark ? Color.FromArgb(65, 82, 101) : Color.LightGray;
                return;
            }
            foreach (Control child in control.Controls) Apply(child, dark);
        }
    }
    internal sealed class AccentButton : Button
    {
        internal bool Dark;
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled) { base.OnPaint(e); return; }
            e.Graphics.Clear(Dark ? Color.FromArgb(49, 65, 82) : Color.FromArgb(206, 218, 230));
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                Dark ? Color.FromArgb(164, 181, 199) : Color.FromArgb(79, 101, 122), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    internal sealed class HeaderButton : Button
    {
        internal bool Dark;
        bool hover;
        public HeaderButton(string glyph, string label)
        {
            Text = glyph; AccessibleName = label; AccessibleDescription = label;
            Size = new Size(42, 42); Font = new Font("Segoe MDL2 Assets", 17);
            FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = new GraphicsPath())
            {
                int d = 20;
                path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
                using (var brush = new SolidBrush(Dark ? Color.FromArgb(40, 59, 80) : hover ? Color.FromArgb(218, 236, 253) : Color.FromArgb(234, 242, 252))) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(Dark ? Color.FromArgb(69, 100, 133) : Color.FromArgb(210, 227, 244))) e.Graphics.DrawPath(pen, path);
            }
            using (var glyph = new GraphicsPath())
            {
                glyph.AddString(Text, Font.FontFamily, (int)Font.Style, Font.SizeInPoints * e.Graphics.DpiY / 72f, PointF.Empty, StringFormat.GenericTypographic);
                RectangleF bounds = glyph.GetBounds();
                using (var transform = new Matrix())
                {
                    transform.Translate(r.Left + r.Width / 2f - bounds.Left - bounds.Width / 2f, r.Top + r.Height / 2f - bounds.Top - bounds.Height / 2f);
                    glyph.Transform(transform);
                }
                using (var brush = new SolidBrush(Enabled ? (Dark ? Color.SkyBlue : Color.FromArgb(30, 137, 235)) : Color.Gray)) e.Graphics.FillPath(brush, glyph);
            }
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 10, Height - 10));
        }
    }
}
