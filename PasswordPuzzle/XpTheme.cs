using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PasswordPuzzle
{
    sealed class ThemeGroupBox : GroupBox
    {
        public ThemeGroupBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ThemeManager.Palette;
            e.Graphics.Clear(BackColor);
            int top = Font.Height / 2;
            if (Width < 2 || Height <= top + 1) return;
            using (var border = new Pen(ThemeManager.ColorOf(p.Border), p.HighContrast ? 2 : 1))
                e.Graphics.DrawRectangle(border, 0, top, Width - 1, Height - top - 1);
            int textWidth = Math.Min(Width - 18, TextRenderer.MeasureText(Text, Font).Width + 6);
            using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, 10, 0, textWidth, Font.Height + 2);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(12, 0, textWidth, Font.Height + 3), ThemeManager.ColorOf(p.Accent), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    // Draw palette-aware controls ourselves so Windows 11 does not override the selected appearance.
    sealed class XpButton : Button
    {
        bool hot, pressed;
        public bool Primary;
        public XpButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width < 1 || rect.Height < 1) return;
            var p = ThemeManager.Palette;
            Color top = ThemeManager.ColorOf(pressed ? p.Input : p.ButtonTop);
            Color bottom = ThemeManager.ColorOf(pressed ? p.Input : p.ButtonBottom);
            Color ink = Enabled ? ForeColor : ThemeManager.ColorOf(p.Border);
            if (p.HighContrast && Enabled && (hot || pressed)) { top = bottom = ThemeManager.ColorOf(p.Accent); ink = Color.Black; }
            using (var brush = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical)) e.Graphics.FillRectangle(brush, rect);
            using (var border = new Pen(ThemeManager.ColorOf(p.Border))) e.Graphics.DrawRectangle(border, rect);
            if (p.Beveled && !pressed)
                using (var shine = new Pen(Color.FromArgb(180, Color.White))) e.Graphics.DrawLine(shine, 2, 2, Width - 3, 2);
            if (Enabled && (hot || Primary))
                using (var accent = new Pen(ThemeManager.ColorOf(hot ? p.Focus : p.Accent), p.HighContrast ? 3 : 2))
                    e.Graphics.DrawRectangle(accent, new Rectangle(2, 2, Width - 5, Height - 5));
            var text = new Rectangle(5 + (pressed ? 1 : 0), 3 + (pressed ? 1 : 0), Width - 10, Height - 6);
            TextRenderer.DrawText(e.Graphics, Text, Font, text, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 11, Height - 11), ink, top);
        }
    }

    sealed class XpBanner : Panel
    {
        public XpBanner() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 1 || Height < 1) return;
            var p = ThemeManager.Palette;
            using (var brush = new LinearGradientBrush(ClientRectangle, ThemeManager.ColorOf(p.HeaderTop), ThemeManager.ColorOf(p.HeaderBottom), LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(brush, ClientRectangle);
            if (p.Name == "Windows 7")
                using (var shine = new SolidBrush(Color.FromArgb(35, Color.White))) e.Graphics.FillRectangle(shine, 0, 0, Width, Height / 2);
            using (var pen = new Pen(ThemeManager.ColorOf(p.Border))) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
