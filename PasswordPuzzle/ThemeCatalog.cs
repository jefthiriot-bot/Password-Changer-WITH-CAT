using System;
using System.Linq;

namespace PasswordPuzzle
{
    public sealed class ThemePalette
    {
        public string Name;
        public int Background, Surface, Input, Text, Accent, HeaderTop, HeaderBottom, HeaderText;
        public int ButtonTop, ButtonBottom, ButtonText, Border, Danger, Focus;
        public bool Beveled;
        public bool HighContrast;
    }

    public static class ThemeCatalog
    {
        public static readonly string[] Names = { "Light", "Dark", "High Contrast", "Windows XP", "Windows 7", "Cat" };
        public static string Normalize(string name) { return Names.Contains(name) ? name : "Windows XP"; }
        public static ThemePalette Get(string name)
        {
            name = Normalize(name);
            var p = new ThemePalette { Name = name, Background = 0xF4F6FA, Surface = 0xFFFFFF, Input = 0xFFFFFF,
                Text = 0x17243A, Accent = 0x174C9E, HeaderTop = 0x2159AA, HeaderBottom = 0x2159AA, HeaderText = 0xFFFFFF,
                ButtonTop = 0xF1F4FA, ButtonBottom = 0xF1F4FA, ButtonText = 0x17243A, Border = 0x65758A, Danger = 0xAC2020, Focus = 0x174C9E };
            if (name == "Dark")
            {
                p.Background = 0x171B23; p.Surface = 0x232A36; p.Input = 0x10151C; p.Text = 0xF2F5FA;
                p.Accent = 0x9CC6FF; p.HeaderTop = p.HeaderBottom = 0x142B4B; p.ButtonTop = p.ButtonBottom = 0x303C4E;
                p.ButtonText = p.Text; p.Border = 0x8EA0B6; p.Danger = 0xFFB2B2; p.Focus = 0xFFDA82;
            }
            else if (name == "High Contrast")
            {
                p.HighContrast = true; p.Background = p.Surface = p.Input = p.HeaderTop = p.HeaderBottom = p.ButtonTop = p.ButtonBottom = 0;
                p.Text = p.HeaderText = p.ButtonText = p.Border = 0xFFFFFF; p.Accent = p.Focus = 0xFFFF00; p.Danger = 0xFFAAAA;
            }
            else if (name == "Windows XP")
            {
                p.Beveled = true; p.Background = p.Surface = 0xECE9D8; p.Text = p.ButtonText = 0x141414;
                p.Accent = 0x0046B4; p.HeaderTop = 0x2374D9; p.HeaderBottom = 0x0040B9;
                p.ButtonTop = 0xFFFFFF; p.ButtonBottom = 0xE1E0D0; p.Border = 0x003C74; p.Focus = 0x825800;
            }
            else if (name == "Windows 7")
            {
                p.Beveled = true; p.Background = 0xDCEBFA; p.Surface = 0xF5F9FD; p.Text = p.ButtonText = 0x153451;
                p.Accent = 0x174C7C; p.HeaderTop = 0xB9D9F5; p.HeaderBottom = 0x78A9D2; p.HeaderText = 0x102D4C;
                p.ButtonTop = 0xFFFFFF; p.ButtonBottom = 0xC9DEF2; p.Border = 0x536F8A; p.Focus = 0x124D85;
            }
            else if (name == "Cat")
            {
                // Colors echo the supplied cat's green eyes, pink paws and blue yarn.
                p.Background = 0x171717; p.Surface = 0x202B35; p.Input = 0x101B25; p.Text = 0xF7F8FA;
                p.Accent = 0x94E2BA; p.HeaderTop = 0x17416C; p.HeaderBottom = 0x083565;
                p.ButtonTop = p.ButtonBottom = 0x254B70; p.ButtonText = p.Text; p.Border = 0x91B8D9; p.Danger = 0xFFB0BD; p.Focus = 0xFFBDD0;
            }
            return p;
        }
    }
}
