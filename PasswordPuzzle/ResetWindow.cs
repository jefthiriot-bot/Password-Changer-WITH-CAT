using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PasswordPuzzle
{
    // A separate executable entry point uses the same vault, authentication and SYSTEM worker.
    public sealed class ResetWindow : Form, IMessageFilter
    {
        readonly ComboBox accounts;
        readonly TextBox emergency;
        readonly TextBox current;
        readonly Label status;
        readonly Timer timer = new Timer();
        DateTime lastInput = DateTime.UtcNow;
        DateTime revealUntil;

        public ResetWindow()
        {
            Ui.SetIcon(this);
            Text = "Password Puzzle - Emergency Reset";
            Font = new Font("Tahoma", 10);
            ClientSize = new Size(570, 650);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Cream;
            var banner = new XpBanner { Left = 0, Top = 0, Width = 570, Height = 64 };
            Controls.Add(banner);
            var title = Ui.Label(banner, "Emergency password reset", 24, 18, 520, 38);
            title.Font = new Font("Tahoma", 19, FontStyle.Bold); title.ForeColor = Color.White;
            Ui.Label(this, "Choose an account configured in the main app. Resetting pauses its automatic password changes.", 24, 70, 520, 50);
            Ui.Label(this, "Local Windows account", 24, 125, 520);
            accounts = new ComboBox { Left = 24, Top = 152, Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(accounts);
            Ui.Label(this, "Saved emergency password", 24, 198, 520);
            emergency = Ui.Text(this, 24, 225, 365, true); emergency.ReadOnly = true;
            Ui.Button(this, "Reveal", 405, 222, 139, (s, e) => Reveal());
            var reset = Ui.Button(this, "RESET SELECTED ACCOUNT", 24, 273, 520, (s, e) => Guard(Reset));
            reset.ForeColor = Color.Firebrick; reset.FlatAppearance.BorderColor = Color.Firebrick;
            Ui.Label(this, "Last password set by the app", 24, 329, 520);
            current = Ui.Text(this, 24, 356, 520, true); current.ReadOnly = true;
            status = Ui.Label(this, "", 24, 405, 520, 80);
            Ui.Label(this, "Closes after five idle minutes. Requires Windows administrator access.", 24, 495, 520, 30).Font = new Font("Tahoma", 9);
            Ui.Credit(this);
            // Keep the form usable when the Cat theme adds its image panel.
            var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(545, 468) };
            foreach (Control control in Controls.Cast<Control>().Where(c => c != banner && !(c is StatusStrip)).ToArray())
            {
                Controls.Remove(control); control.Top -= 64; content.Controls.Add(control);
            }
            banner.Dock = DockStyle.Top;
            Controls.Add(content); content.BringToFront();
            var appearance = new AppearanceBar();
            Controls.Add(appearance); Controls.SetChildIndex(appearance, 1);
            accounts.SelectedIndexChanged += (s, e) => Guard(RefreshStatus);
            Shown += (s, e) => Guard(() =>
            {
                var vault = Storage.ReadLocked();
                var eligible = WindowsBackend.ListAccounts().Where(a => vault.Accounts.Any(p => p.Sid == a.Sid)).ToArray();
                accounts.Items.AddRange(eligible);
                reset.Enabled = eligible.Length > 0;
                if (eligible.Length > 0) accounts.SelectedIndex = 0;
                else status.Text = "No configured local accounts found. Select and save an account in PasswordPuzzle.exe first.";
            });
            timer.Interval = 2000;
            timer.Tick += (s, e) =>
            {
                if (DateTime.UtcNow - lastInput > TimeSpan.FromMinutes(5)) { Close(); return; }
                if (DateTime.UtcNow > revealUntil) { emergency.UseSystemPasswordChar = true; current.UseSystemPasswordChar = true; }
                try { RefreshStatus(); } catch (Exception error) { status.Text = error.Message; }
            };
            timer.Start(); Application.AddMessageFilter(this);
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); Application.RemoveMessageFilter(this); };
        }

        void Guard(Action action) { try { action(); } catch (Exception error) { Ui.Error(this, error); } }
        void Reveal()
        {
            revealUntil = DateTime.UtcNow.AddSeconds(15);
            emergency.UseSystemPasswordChar = false; current.UseSystemPasswordChar = false;
        }
        void RefreshStatus()
        {
            var vault = Storage.ReadLocked(); ThemeManager.Sync(vault.Theme);
            var account = accounts.SelectedItem as LocalAccount;
            if (account == null) return;
            var p = vault.Accounts.FirstOrDefault(a => a.Sid == account.Sid);
            if (p == null) throw new InvalidOperationException("Account settings were removed. Reopen the main app.");
            if (emergency.Text != p.EmergencyPassword) { emergency.UseSystemPasswordChar = true; emergency.Text = p.EmergencyPassword; }
            string saved = p.LastChangedUtc == DateTime.MinValue ? "" : p.CurrentPassword;
            if (current.Text != saved) { current.UseSystemPasswordChar = true; current.Text = saved; }
            status.Text = p.Status;
        }
        void Reset()
        {
            var account = accounts.SelectedItem as LocalAccount;
            if (account == null) throw new InvalidOperationException("Select an account first.");
            if (MessageBox.Show(this, "Reset " + account.Name + " to its saved emergency password and pause automatic changes?\n\nThis administrator reset can affect EFS-encrypted files and saved credentials.",
                "Reset " + account.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            using (Storage.Lock())
            {
                var vault = Storage.Read();
                var p = vault.Accounts.First(a => a.Sid == account.Sid);
                p.Name = account.Name;
                new WindowsBackend().ValidateAccount(p);
                Puzzles.Validate(p);
                p.Enabled = false;
                p.ClearRequest();
                p.Request = "Panic";
                p.Status = "Emergency reset queued. Waiting for the Windows worker...";
                Storage.Save(vault);
            }
            try { Installation.StartWorker(); }
            catch
            {
                using (Storage.Lock())
                {
                    var vault = Storage.Read(); var p = vault.Accounts.First(a => a.Sid == account.Sid);
                    if (p.Request == "Panic")
                    {
                        p.ClearRequest();
                        p.Status = "Could not start the reset. Open the main app and use Repair scheduler. Automatic changes are paused.";
                        Storage.Save(vault);
                    }
                }
                throw;
            }
            RefreshStatus();
        }
        public bool PreFilterMessage(ref Message m)
        {
            if ((m.Msg >= 0x100 && m.Msg <= 0x109) || (m.Msg >= 0x201 && m.Msg <= 0x20E)) lastInput = DateTime.UtcNow;
            return false;
        }
    }
}
