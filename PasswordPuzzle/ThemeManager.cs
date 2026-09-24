using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace PasswordPuzzle
{
    static class ThemeManager
    {
        static readonly List<Form> forms = new List<Form>();
        sealed class Role { public bool Danger; }
        static readonly ConditionalWeakTable<Control, Role> roles = new ConditionalWeakTable<Control, Role>();
        static readonly ConditionalWeakTable<ComboBox, object> combos = new ConditionalWeakTable<ComboBox, object>();
        public static ThemePalette Palette = ThemeCatalog.Get("Windows XP");
        public static Color ColorOf(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static void Register(Form form)
        {
            forms.Add(form);
            form.Load += (s, e) => Apply(form);
            form.FormClosed += (s, e) => forms.Remove(form);
        }
        public static void Sync(string name)
        {
            string normalized = ThemeCatalog.Normalize(name);
            if (normalized == Palette.Name) return;
            Palette = ThemeCatalog.Get(normalized);
            foreach (var form in forms.ToArray()) if (!form.IsDisposed) Apply(form);
        }
        public static void Save(string name)
        {
            using (Storage.Lock())
            {
                var vault = Storage.Read(); vault.Theme = ThemeCatalog.Normalize(name); Storage.Save(vault);
            }
            Sync(name);
        }
        public static void Apply(Control control)
        {
            var p = Palette;
            Role role = roles.GetValue(control, c => new Role { Danger = c.ForeColor == Color.Firebrick });
            bool banner = control is XpBanner || control.Parent is XpBanner;
            control.ForeColor = ColorOf(role.Danger ? p.Danger : banner ? p.HeaderText : p.Text);
            if (control is Label || control is CheckBox) control.BackColor = Color.Transparent;
            else if (control is TextBoxBase || control is NumericUpDown || control is ComboBox) control.BackColor = ColorOf(p.Input);
            else control.BackColor = ColorOf(control is GroupBox ? p.Surface : banner ? p.HeaderBottom : p.Background);

            var button = control as XpButton;
            if (button != null) button.ForeColor = ColorOf(role.Danger ? p.Danger : p.ButtonText);
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.EnableHeadersVisualStyles = false; grid.BackgroundColor = ColorOf(p.Background); grid.GridColor = ColorOf(p.Border);
                grid.DefaultCellStyle.BackColor = ColorOf(p.Input); grid.DefaultCellStyle.ForeColor = ColorOf(p.Text);
                grid.DefaultCellStyle.SelectionBackColor = ColorOf(p.Surface); grid.DefaultCellStyle.SelectionForeColor = ColorOf(p.Text);
                grid.ColumnHeadersDefaultCellStyle.BackColor = ColorOf(p.HeaderBottom); grid.ColumnHeadersDefaultCellStyle.ForeColor = ColorOf(p.HeaderText);
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorOf(p.HeaderBottom); grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorOf(p.HeaderText);
            }
            var check = control as CheckBox;
            if (check != null) { check.UseVisualStyleBackColor = false; check.FlatStyle = FlatStyle.Flat; }
            var combo = control as ComboBox;
            if (combo != null)
            {
                object marker;
                if (!combos.TryGetValue(combo, out marker))
                {
                    combos.Add(combo, new object());
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += DrawCombo;
                }
            }
            var status = control as StatusStrip;
            if (status != null)
            {
                status.Renderer = new ToolStripProfessionalRenderer(new ThemeToolStripColors());
                foreach (ToolStripItem item in status.Items) { item.ForeColor = ColorOf(p.Text); item.BackColor = ColorOf(p.Background); }
            }
            foreach (Control child in control.Controls) Apply(child);
            var appearance = control as AppearanceBar;
            if (appearance != null) appearance.UpdateTheme();
            control.Invalidate(true);
        }
        static void DrawCombo(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender; var p = Palette;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color bg = ColorOf(selected ? p.Accent : p.Input);
            Color fg = ColorOf(selected ? p.Background : p.Text);
            // Light accent colors use dark text; dark accent colors use white text.
            if (selected && bg.GetBrightness() < .5f) fg = Color.White;
            using (var brush = new SolidBrush(bg)) e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, fg, bg);
        }
        sealed class ThemeToolStripColors : ProfessionalColorTable
        {
            public override Color StatusStripGradientBegin { get { return ColorOf(Palette.Background); } }
            public override Color StatusStripGradientEnd { get { return ColorOf(Palette.Background); } }
        }
    }

    sealed class AppearanceBar : Panel
    {
        readonly ComboBox choices;
        readonly PictureBox cat;
        readonly Label catTitle, catText;
        bool syncing;
        public AppearanceBar()
        {
            Dock = DockStyle.Top; Height = 54;
            Ui.Label(this, "Theme", 22, 18, 64);
            choices = new ComboBox { Left = 90, Top = 13, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Application theme" };
            choices.Items.AddRange(ThemeCatalog.Names); Controls.Add(choices);
            Ui.Label(this, "Saved for both apps", 308, 18, 230);
            cat = new PictureBox { Left = 22, Top = 59, Width = 108, Height = 140, SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "Your white cat with a blue ball of yarn", TabStop = false };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PasswordPuzzle.Cat.png"))
            {
                if (stream == null) throw new InvalidDataException("The embedded cat image is missing.");
                using (var source = Image.FromStream(stream)) cat.Image = new Bitmap(source);
            }
            Controls.Add(cat);
            catTitle = Ui.Label(this, "Cat & yarn", 152, 81, 340, 35); catTitle.Font = new Font("Tahoma", 20, FontStyle.Bold);
            catText = Ui.Label(this, "Your cat is on password duty.\nGreen eyes, pink paws, blue yarn.", 154, 127, 340, 60);
            choices.SelectedIndexChanged += (s, e) =>
            {
                if (syncing || choices.SelectedItem == null) return;
                try { ThemeManager.Save((string)choices.SelectedItem); }
                catch (Exception error) { UpdateTheme(); Ui.Error(FindForm(), error); }
            };
            Disposed += (s, e) => { if (cat.Image != null) cat.Image.Dispose(); };
            UpdateTheme();
        }
        public void UpdateTheme()
        {
            syncing = true;
            try
            {
                choices.SelectedItem = ThemeManager.Palette.Name;
                bool showCat = ThemeManager.Palette.Name == "Cat";
                cat.Visible = catTitle.Visible = catText.Visible = showCat;
                cat.BackColor = Color.FromArgb(23, 23, 23);
                Height = showCat ? 210 : 54;
            }
            finally { syncing = false; }
        }
    }
}
