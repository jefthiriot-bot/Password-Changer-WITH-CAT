using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PasswordPuzzle
{
    public sealed class AllPasswordsWindow : Form
    {
        readonly DataGridView grid;
        readonly Label note;
        readonly Timer timer = new Timer();
        readonly DateTime closesAt = DateTime.UtcNow.AddMinutes(5);
        DateTime revealUntil;
        List<LocalAccount> accounts = new List<LocalAccount>();

        public AllPasswordsWindow()
        {
            Ui.SetIcon(this);
            Text = "All account passwords - Password Puzzle";
            Font = new Font("Tahoma", 10); ClientSize = new Size(1030, 565); MinimumSize = new Size(800, 450);
            StartPosition = FormStartPosition.CenterParent;
            var top = new Panel { Dock = DockStyle.Top, Height = 155 };
            Controls.Add(top);
            Ui.Label(top, "All local accounts - saved passwords", 18, 14, 950, 35).Font = new Font("Tahoma", 18, FontStyle.Bold);
            Ui.Label(top, "These are the latest passwords recorded by this app, not passwords read from Windows. Unknown passwords cannot be displayed. Changes made elsewhere are not tracked. Domain and online-only accounts are not listed.", 18, 53, 970, 54);
            Ui.Button(top, "Reveal all (15 seconds)", 18, 112, 230, (s, e) => Guard(() => { revealUntil = DateTime.UtcNow.AddSeconds(15); RefreshPasswords(); }));
            Ui.Button(top, "Hide passwords", 265, 112, 180, (s, e) => { HidePasswords(); });
            Ui.Button(top, "Refresh accounts", 462, 112, 190, (s, e) => Guard(RefreshAccounts));
            grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToOrderColumns = false, RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false, ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable, BackgroundColor = Ui.Cream };
            grid.Columns.Add("account", "Account"); grid.Columns.Add("type", "Type"); grid.Columns.Add("password", "Last saved password");
            grid.Columns.Add("changed", "Last changed by app"); grid.Columns.Add("status", "Password status");
            grid.Columns["status"].FillWeight = 180;
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            Controls.Add(grid); grid.BringToFront();
            note = new Label { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(18, 8, 8, 0), Text = "Passwords hidden. This window closes after five minutes." };
            Controls.Add(note); Ui.Credit(this); grid.BringToFront();
            Shown += (s, e) => Guard(RefreshAccounts);
            Deactivate += (s, e) => HidePasswords();
            timer.Interval = 1000;
            timer.Tick += (s, e) =>
            {
                if (DateTime.UtcNow >= closesAt) { Close(); return; }
                if (DateTime.UtcNow >= revealUntil) HidePasswords();
                try { RefreshPasswords(); }
                catch (Exception error) { HidePasswords(); note.Text = "Cannot refresh saved passwords: " + error.Message; }
            };
            timer.Start();
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); grid.Rows.Clear(); accounts.Clear(); };
        }
        void Guard(Action action) { try { action(); } catch (Exception error) { HidePasswords(); Ui.Error(this, error); } }
        void RefreshAccounts()
        {
            HidePasswords(); accounts = WindowsBackend.ListAccounts(true);
            grid.Rows.Clear();
            foreach (var account in accounts) grid.Rows.Add(account.Name, account.Description, "Unknown", "", "");
            RefreshPasswords();
        }
        void HidePasswords()
        {
            revealUntil = DateTime.MinValue;
            foreach (DataGridViewRow row in grid.Rows)
                if ((string)row.Cells["password"].Value != "Unknown") row.Cells["password"].Value = "Hidden";
            note.Text = "Passwords hidden. This window closes after five minutes.";
        }
        void RefreshPasswords()
        {
            var vault = Storage.ReadLocked(); ThemeManager.Sync(vault.Theme);
            bool reveal = revealUntil > DateTime.UtcNow;
            for (int i = 0; i < accounts.Count; i++)
            {
                var saved = SavedPasswordView.From(vault.Accounts.FirstOrDefault(p => p.Sid == accounts[i].Sid));
                var row = grid.Rows[i];
                row.Cells["password"].Value = saved.Password == null ? "Unknown" : reveal ? saved.Password : "Hidden";
                row.Cells["changed"].Value = saved.LastChangedUtc == DateTime.MinValue ? "" : saved.LastChangedUtc.ToLocalTime().ToString("g");
                row.Cells["status"].Value = saved.Status;
            }
            note.Text = reveal ? "Passwords visible for " + Math.Ceiling((revealUntil - DateTime.UtcNow).TotalSeconds) + " seconds. Switching windows hides them." : "Passwords hidden. This window closes after five minutes.";
        }
    }
}
