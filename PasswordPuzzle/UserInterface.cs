using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PasswordPuzzle
{
    static class Ui
    {
        public static void SetIcon(Form form) { form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); form.BackColor = Cream; ThemeManager.Register(form); }
        public static readonly Color Cream = Color.FromArgb(236, 233, 216);
        public static readonly Color Ink = Color.FromArgb(20, 20, 20);
        public static readonly Color Blue = Color.FromArgb(0, 70, 180);
        public static void Credit(Form form)
        {
            var bar = new StatusStrip { BackColor = Cream, RenderMode = ToolStripRenderMode.System, SizingGrip = false, Dock = DockStyle.Bottom };
            bar.Items.Add(new ToolStripStatusLabel("Created by @Jef_Dawg") { Spring = true, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Tahoma", 9, FontStyle.Bold) });
            form.Controls.Add(bar);
        }
        public static Label Label(Control parent, string text, int x, int y, int width, int height = 24)
        {
            var c = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = Ink, BackColor = Color.Transparent };
            parent.Controls.Add(c); return c;
        }
        public static Button Button(Control parent, string text, int x, int y, int width, EventHandler handler, bool primary = false)
        {
            var c = new XpButton { Text = text, Location = new Point(x, y), Size = new Size(width, 36), Primary = primary,
                BackColor = Cream, ForeColor = Ink, Cursor = Cursors.Hand };
            c.FlatAppearance.BorderColor = primary ? Blue : Color.FromArgb(205, 215, 228);
            c.Click += handler; parent.Controls.Add(c); return c;
        }
        public static TextBox Text(Control parent, int x, int y, int width, bool secret = false)
        {
            var c = new TextBox { Location = new Point(x, y), Width = width, UseSystemPasswordChar = secret, MaxLength = 200 };
            parent.Controls.Add(c); return c;
        }
        public static void Error(IWin32Window owner, Exception e) { MessageBox.Show(owner, e.Message, "Password Puzzle", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    public sealed class UnlockDialog : Form
    {
        readonly bool change;
        readonly bool create;
        readonly TextBox current;
        readonly TextBox password;
        readonly TextBox confirm;
        readonly Label message;

        UnlockDialog(bool changing)
        {
            Ui.SetIcon(this);
            change = changing;
            create = string.IsNullOrEmpty(Storage.ReadLocked().PasswordHash);
            Text = change ? "Change app password" : create ? "Welcome to Password Puzzle" : "Unlock Password Puzzle";
            Font = new Font("Tahoma", 10);
            ClientSize = new Size(450, change || create ? 385 : 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.Cream;
            Ui.Label(this, create ? "Create your app password" : change ? "Choose a new app password" : "Your accounts, behind one lock", 24, 24, 400, 32).Font = new Font("Tahoma", 16, FontStyle.Bold);
            Ui.Label(this, create || change ? "Use 8 or more characters. This is separate from Windows." : "Enter the password you set for this app.", 24, 66, 400, 44);
            int y = 116;
            if (!create)
            {
                Ui.Label(this, "Current app password", 24, y, 390);
                current = Ui.Text(this, 24, y + 25, 400, true); y += 65;
            }
            if (create || change)
            {
                Ui.Label(this, "New app password", 24, y, 390);
                password = Ui.Text(this, 24, y + 25, 400, true); y += 65;
                Ui.Label(this, "Repeat new password", 24, y, 390);
                confirm = Ui.Text(this, 24, y + 25, 400, true); y += 65;
                if (change) ClientSize = new Size(450, 445);
            }
            message = Ui.Label(this, "", 24, y, 400, 45); message.ForeColor = Color.Firebrick;
            var submit = Ui.Button(this, create ? "Create password" : change ? "Change password" : "Unlock", 24, y + 48, 400, Submit, true);
            AcceptButton = submit;
            ClientSize = new Size(450, y + 104);
            Shown += (s, e) => { if (current != null) current.Focus(); else password.Focus(); };
        }

        void Submit(object sender, EventArgs e)
        {
            try
            {
                using (Storage.Lock())
                {
                    var vault = Storage.Read();
                    if (create && !string.IsNullOrEmpty(vault.PasswordHash)) throw new InvalidOperationException("An app password was already created. Close this window and reopen the app.");
                    if (!create)
                    {
                        if (vault.BlockedUntilUtc > DateTime.UtcNow)
                            throw new InvalidOperationException("Try again in " + Math.Ceiling((vault.BlockedUntilUtc - DateTime.UtcNow).TotalSeconds) + " seconds.");
                        if (!AppPassword.Verify(vault, current.Text))
                        {
                            vault.FailedUnlocks++;
                            if (vault.FailedUnlocks >= 5) vault.BlockedUntilUtc = DateTime.UtcNow.AddSeconds(Math.Min(300, 30 * (vault.FailedUnlocks - 4)));
                            Storage.Save(vault);
                            current.Clear();
                            throw new InvalidOperationException("Incorrect app password.");
                        }
                    }
                    if (create || change)
                    {
                        if (password.Text != confirm.Text) throw new InvalidOperationException("The new passwords do not match.");
                        AppPassword.Set(vault, password.Text);
                    }
                    vault.FailedUnlocks = 0; vault.BlockedUntilUtc = DateTime.MinValue;
                    Storage.Save(vault);
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error) { message.Text = error.Message; }
        }

        public static bool Open(IWin32Window owner, bool change)
        {
            using (var dialog = new UnlockDialog(change))
                return dialog.ShowDialog(owner) == DialogResult.OK;
        }
    }

    sealed class EnrollmentDialog : Form
    {
        public string ExistingPassword;
        readonly CheckBox known;
        readonly TextBox password;
        public EnrollmentDialog(string account)
        {
            Ui.SetIcon(this);
            Text = "First change for " + account;
            Font = new Font("Tahoma", 10); ClientSize = new Size(520, 335);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Ui.Label(this, "Set up " + account, 22, 20, 475, 32).Font = new Font("Tahoma", 15, FontStyle.Bold);
            known = new CheckBox { Text = "I know this account's current Windows password", Checked = true,
                Location = new Point(22, 68), Size = new Size(475, 30) };
            Controls.Add(known);
            password = Ui.Text(this, 22, 104, 475, true);
            Ui.Label(this, "Leave the field blank if this account currently has no password.", 22, 137, 475, 30);
            Ui.Label(this, "If you uncheck the box, the first change is an administrator reset. That can make EFS-encrypted files and saved credentials inaccessible. Later changes use the saved current password.", 22, 175, 475, 86);
            known.CheckedChanged += (s, e) => password.Enabled = known.Checked;
            Ui.Button(this, "Continue", 337, 275, 160, (s, e) => { ExistingPassword = known.Checked ? password.Text : null; DialogResult = DialogResult.OK; }, true);
            Ui.Button(this, "Cancel", 22, 275, 125, (s, e) => DialogResult = DialogResult.Cancel);
        }
    }

    public sealed class MainWindow : Form, IMessageFilter
    {
        readonly ComboBox accounts = new ComboBox();
        readonly ComboBox style = new ComboBox();
        readonly NumericUpDown digits = new NumericUpDown();
        readonly NumericUpDown interval = new NumericUpDown();
        readonly ComboBox units = new ComboBox();
        readonly CheckBox enabled = new CheckBox();
        readonly CheckBox custom = new CheckBox();
        readonly TextBox hint;
        readonly TextBox emergency;
        readonly TextBox password;
        readonly TextBox manualPassword;
        readonly TextBox manualConfirm;
        readonly TextBox manualHint;
        readonly CheckBox showManual;
        readonly Label manualTarget;
        readonly Label currentHint;
        readonly Label status;
        readonly Label schedule;
        readonly Label heartbeat;
        readonly Panel body;
        readonly Timer timer = new Timer();
        DateTime lastInput = DateTime.UtcNow;
        DateTime revealUntil;
        bool locking;
        bool loading;

        public MainWindow()
        {
            Ui.SetIcon(this);
            Text = "Password Puzzle"; Font = new Font("Tahoma", 10);
            BackColor = Ui.Cream;
            ClientSize = new Size(1000, 900); MinimumSize = new Size(920, 660);
            StartPosition = FormStartPosition.CenterScreen;
            var header = new XpBanner { Dock = DockStyle.Top, Height = 85 };
            Controls.Add(header);
            Ui.Label(header, "Password Puzzle", 24, 14, 480, 36).ForeColor = Color.White;
            header.Controls[0].Font = new Font("Tahoma", 22, FontStyle.Bold);
            Ui.Label(header, "A fresh password. A clue at sign-in.", 26, 53, 450).ForeColor = Color.FromArgb(192, 210, 232);
            var lockButton = Ui.Button(header, "Lock app", 841, 25, 130, (s, e) => LockApp());
            lockButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22) };
            Controls.Add(body); body.BringToFront();
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 6, Padding = new Padding(0, 0, 0, 20) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.Controls.Add(layout);
            var accountPanel = new Panel { Dock = DockStyle.Fill, Height = 74, Margin = new Padding(0, 0, 0, 12) };
            layout.Controls.Add(accountPanel, 0, 0); layout.SetColumnSpan(accountPanel, 2);
            Ui.Label(accountPanel, "LOCAL WINDOWS ACCOUNT", 0, 0, 400).Font = new Font("Tahoma", 9, FontStyle.Bold);
            accounts.SetBounds(0, 29, 430, 30); accounts.DropDownStyle = ComboBoxStyle.DropDownList;
            accountPanel.Controls.Add(accounts);
            Ui.Button(accountPanel, "Refresh accounts", 444, 25, 155, (s, e) => Guard(RefreshAccounts));
            Ui.Button(accountPanel, "All account passwords", 617, 25, 270, (s, e) => Guard(ShowAllPasswords));

            var generator = Card(layout, "1   Password & hint", 0, 1, 348);
            Ui.Label(generator, "Password style", 18, 32, 180);
            style.SetBounds(18, 58, 260, 30); style.DropDownStyle = ComboBoxStyle.DropDownList;
            style.Items.AddRange(new object[] { "Multi-step math problem", "One-word riddle", "Random mix" }); generator.Controls.Add(style);
            Ui.Label(generator, "Digits", 310, 32, 90);
            digits.SetBounds(310, 58, 100, 30); digits.Minimum = 4; digits.Maximum = 8; digits.Value = 4; generator.Controls.Add(digits);
            custom.SetBounds(18, 103, 400, 28); custom.Text = "Use my own hint instead of a generated clue"; generator.Controls.Add(custom);
            hint = Ui.Text(generator, 18, 138, 392); hint.Multiline = true; hint.Height = 70;
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            custom.CheckedChanged += (s, e) => hint.Enabled = custom.Checked;
            Ui.Label(generator, "Your own hint stays the same until you edit it. Automatic clues change with each new password.", 18, 220, 395, 52);
            Ui.Button(generator, "Update hint only", 18, 286, 190, (s, e) => Guard(() => Save("Hint")));

            var timing = Card(layout, "2   Schedule", 1, 1, 348);
            enabled.SetBounds(18, 34, 385, 30); enabled.Text = "Change this account's password automatically"; timing.Controls.Add(enabled);
            Ui.Label(timing, "Change every", 18, 83, 220);
            interval.SetBounds(18, 112, 110, 30); interval.Minimum = 1; interval.Maximum = 365; interval.Value = 1; timing.Controls.Add(interval);
            units.SetBounds(144, 112, 175, 30); units.DropDownStyle = ComboBoxStyle.DropDownList;
            units.Items.AddRange(new object[] { "Minutes", "Hours", "Days" }); units.SelectedIndex = 2; timing.Controls.Add(units);
            Ui.Label(timing, "Runs while the app is closed. If the PC is off or asleep, a missed change runs after it is back on.", 18, 160, 395, 57);
            schedule = Ui.Label(timing, "No schedule saved.", 18, 225, 395, 51);
            Ui.Button(timing, "Save schedule", 18, 286, 178, (s, e) => Guard(() => Save(null)), true);
            Ui.Button(timing, "Pause", 211, 286, 112, (s, e) => Guard(Pause));

            var result = Card(layout, "3   Latest password", 0, 2, 260);
            Ui.Label(result, "Last password set by this app", 18, 34, 380);
            password = Ui.Text(result, 18, 64, 276, true); password.ReadOnly = true; password.Font = new Font("Consolas", 17);
            Ui.Button(result, "Reveal", 304, 62, 106, (s, e) => { revealUntil = DateTime.UtcNow.AddSeconds(15); password.UseSystemPasswordChar = false; });
            currentHint = Ui.Label(result, "No hint set yet.", 18, 111, 392, 65);
            Ui.Label(result, "Changes made outside this app are not tracked.", 18, 174, 395, 25).Font = new Font("Tahoma", 9);
            Ui.Button(result, "Change password now", 18, 205, 235, (s, e) => Guard(() => Save("Rotate")), true);

            var panic = Card(layout, "4   Emergency reset", 1, 2, 260);
            Ui.Label(panic, "Emergency password", 18, 34, 380);
            emergency = Ui.Text(panic, 18, 65, 260, true); emergency.Text = "1234"; emergency.MaxLength = 24;
            Ui.Button(panic, "Show", 293, 61, 117, (s, e) => emergency.UseSystemPasswordChar = !emergency.UseSystemPasswordChar);
            Ui.Label(panic, "The panic button resets the selected account and pauses its schedule. Default: 1234. Windows password rules still apply.", 18, 112, 392, 82);
            var panicButton = Ui.Button(panic, "PANIC - reset password", 18, 205, 280, (s, e) => Guard(Panic));
            panicButton.ForeColor = Color.Firebrick; panicButton.FlatAppearance.BorderColor = Color.Firebrick;

            var manual = Card(layout, "5   Type your own password", 0, 3, 260);
            layout.SetColumnSpan(manual, 2); manual.Margin = new Padding(0, 0, 0, 16);
            manualTarget = Ui.Label(manual, "For the account selected above", 18, 28, 830, 24);
            Ui.Label(manual, "New Windows password", 18, 60, 380);
            Ui.Label(manual, "Confirm new password", 462, 60, 380);
            manualPassword = Ui.Text(manual, 18, 88, 390, true); manualPassword.MaxLength = 128;
            manualConfirm = Ui.Text(manual, 462, 88, 390, true); manualConfirm.MaxLength = 128;
            showManual = new CheckBox { Text = "Show typed passwords", Left = 18, Top = 122, Width = 300, Height = 25 };
            manual.Controls.Add(showManual);
            showManual.CheckedChanged += (s, e) => { manualPassword.UseSystemPasswordChar = !showManual.Checked; manualConfirm.UseSystemPasswordChar = !showManual.Checked; };
            Ui.Label(manual, "Login hint (optional; blank removes the old hint)", 18, 154, 820);
            manualHint = Ui.Text(manual, 18, 183, 584);
            Ui.Button(manual, "Set typed password", 625, 176, 227, (s, e) => Guard(ApplyManual), true);
            Ui.Label(manual, "Automatic changes pause when you apply a typed password. Resume them in Schedule when you are ready.", 18, 222, 840, 27);
            var jumpManual = Ui.Button(header, "Type a password...", 617, 25, 210, (s, e) => { body.ScrollControlIntoView(manual); manualPassword.Focus(); });
            jumpManual.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var statePanel = new Panel { Dock = DockStyle.Fill, Height = 126, Margin = new Padding(0, 8, 0, 0) };
            layout.Controls.Add(statePanel, 0, 4); layout.SetColumnSpan(statePanel, 2);
            status = Ui.Label(statePanel, "Select a local account to get started.", 0, 0, 890, 66);
            status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            heartbeat = Ui.Label(statePanel, "", 0, 71, 890, 43); heartbeat.Font = new Font("Tahoma", 9);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 92, AutoSize = true, WrapContents = true };
            layout.Controls.Add(footer, 0, 5); layout.SetColumnSpan(footer, 2);
            AddFooter(footer, "Change app password", () => UnlockDialog.Open(this, true));
            AddFooter(footer, "Recovery details", Recover);
            AddFooter(footer, "Sync current password", SyncPassword);
            AddFooter(footer, "Repair scheduler", () => { Installation.RegisterTask(); Installation.StartWorker(); RefreshStatus(); });
            AddFooter(footer, "About / help", Help);
            Ui.Credit(this);
            var appearance = new AppearanceBar();
            Controls.Add(appearance); Controls.SetChildIndex(appearance, 1);
            body.BringToFront();

            accounts.SelectedIndexChanged += (s, e) => Guard(LoadSelection);
            style.SelectedIndexChanged += (s, e) => digits.Enabled = style.SelectedIndex != 1;
            Shown += (s, e) => Guard(RefreshAccounts);
            timer.Interval = 3000;
            timer.Tick += (s, e) =>
            {
                if (locking) return;
                if (DateTime.UtcNow - lastInput > TimeSpan.FromMinutes(5)) { LockApp(); return; }
                if (DateTime.UtcNow > revealUntil) password.UseSystemPasswordChar = true;
                try { RefreshStatus(); } catch (Exception error) { status.Text = "Could not read current status: " + error.Message; }
            };
            timer.Start();
            Application.AddMessageFilter(this);
            FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); Application.RemoveMessageFilter(this); };
        }

        static GroupBox Card(TableLayoutPanel layout, string title, int column, int row, int height)
        {
            var card = new ThemeGroupBox { Text = title, Dock = DockStyle.Fill, Height = height, MinimumSize = new Size(435, height),
                BackColor = Ui.Cream, ForeColor = Ui.Blue, Margin = new Padding(column == 0 ? 0 : 8, 0, column == 0 ? 8 : 0, 16) };
            layout.Controls.Add(card, column, row); return card;
        }
        void AddFooter(FlowLayoutPanel footer, string text, Action action)
        {
            var button = Ui.Button(footer, text, 0, 0, text == "Change app password" ? 210 : 170, (s, e) => Guard(action));
            button.Margin = new Padding(0, 0, 12, 10);
        }
        void Guard(Action action) { try { action(); } catch (Exception e) { Ui.Error(this, e); } }
        LocalAccount Selected()
        {
            var account = accounts.SelectedItem as LocalAccount;
            if (account == null) throw new InvalidOperationException("Select a local Windows account first. Microsoft and disabled accounts are excluded.");
            return account;
        }
        void RefreshAccounts()
        {
            string sid = (accounts.SelectedItem as LocalAccount)?.Sid;
            var list = WindowsBackend.ListAccounts();
            accounts.Items.Clear(); accounts.Items.AddRange(list.ToArray());
            if (list.Count > 0) accounts.SelectedIndex = Math.Max(0, list.FindIndex(a => a.Sid == sid));
            else { password.Clear(); currentHint.Text = ""; status.Text = "No enabled local-only accounts found. Microsoft, domain, and built-in system accounts are excluded."; }
        }
        void LoadSelection()
        {
            if (accounts.SelectedItem == null) return;
            loading = true;
            try
            {
                var account = Selected();
                manualTarget.Text = "Change password for: " + account.Name;
                ClearManualFields();
                var p = Storage.ReadLocked().Accounts.FirstOrDefault(a => a.Sid == account.Sid) ?? new AccountPlan();
                style.SelectedIndex = p.Style == "Word" ? 1 : p.Style == "Mixed" ? 2 : 0;
                digits.Value = p.Digits; custom.Checked = p.CustomHint; hint.Text = p.HintText; hint.Enabled = custom.Checked;
                emergency.Text = p.EmergencyPassword; emergency.UseSystemPasswordChar = true; enabled.Checked = p.Enabled;
                if (p.IntervalMinutes % 1440 == 0) { units.SelectedIndex = 2; interval.Value = p.IntervalMinutes / 1440; }
                else if (p.IntervalMinutes % 60 == 0 && p.IntervalMinutes / 60 <= 365) { units.SelectedIndex = 1; interval.Value = p.IntervalMinutes / 60; }
                else { units.SelectedIndex = 0; interval.Maximum = 525600; interval.Value = p.IntervalMinutes; }
                password.UseSystemPasswordChar = true; RefreshStatus();
            }
            finally { loading = false; }
        }
        AccountPlan ReadControls(LocalAccount account)
        {
            var p = new AccountPlan { Sid = account.Sid, Name = account.Name,
                Style = style.SelectedIndex == 1 ? "Word" : style.SelectedIndex == 2 ? "Mixed" : "Math",
                Digits = (int)digits.Value, CustomHint = custom.Checked, HintText = hint.Text.Trim(),
                IntervalMinutes = checked((int)interval.Value * (units.SelectedIndex == 2 ? 1440 : units.SelectedIndex == 1 ? 60 : 1)),
                Enabled = enabled.Checked, EmergencyPassword = emergency.Text };
            Puzzles.Validate(p); return p;
        }

        void Save(string request, string typedPassword = null, string typedHint = null)
        {
            if (loading) return;
            var account = Selected();
            var existing = Storage.ReadLocked().Accounts.FirstOrDefault(a => a.Sid == account.Sid);
            var settings = request == "Manual" ? (existing ?? new AccountPlan { Name = account.Name, Sid = account.Sid }) : ReadControls(account);
            if (request == "Manual") ManualPasswords.Validate(typedPassword, typedHint);
            string suppliedPassword = null;
            bool enrolled = false;
            bool firstChange = existing == null || existing.LastChangedUtc == DateTime.MinValue;
            if (firstChange && request == null && settings.Enabled) request = "Rotate";
            if (firstChange && (request == "Rotate" || request == "Manual"))
            {
                using (var dialog = new EnrollmentDialog(account.Name))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    suppliedPassword = dialog.ExistingPassword; enrolled = true;
                }
            }
            if (request == "Hint" && !settings.CustomHint && (existing == null || existing.CurrentPassword == null))
                throw new InvalidOperationException("First change the password, or enter your own hint. Windows cannot reveal the existing password to generate a clue.");
            using (Storage.Lock())
            {
                var vault = Storage.Read();
                var p = vault.Accounts.FirstOrDefault(a => a.Sid == account.Sid);
                if (p == null) { p = new AccountPlan { Sid = account.Sid, Name = account.Name }; vault.Accounts.Add(p); }
                if (!string.IsNullOrEmpty(p.Stage) && request != "Panic") throw new InvalidOperationException("Resolve the interrupted change in Recovery details, or use the emergency reset first.");
                if (!string.IsNullOrEmpty(p.Request)) throw new InvalidOperationException("An action is already queued. Wait for it to complete or use Pause to cancel it before it starts.");
                bool changedInterval = p.IntervalMinutes != settings.IntervalMinutes;
                bool resuming = settings.Enabled && !p.Enabled;
                p.Name = account.Name; p.Style = settings.Style; p.Digits = settings.Digits;
                p.CustomHint = settings.CustomHint; p.HintText = settings.HintText;
                p.EmergencyPassword = settings.EmergencyPassword; p.IntervalMinutes = settings.IntervalMinutes;
                // Only Save schedule controls automatic scheduling. Other buttons preserve its saved state.
                if (request == null || (enrolled && settings.Enabled)) p.Enabled = settings.Enabled;
                if (request == "Panic" || request == "Manual") p.Enabled = false;
                if (enrolled && p.LastChangedUtc == DateTime.MinValue) p.CurrentPassword = suppliedPassword;
                if (p.NextUtc == DateTime.MinValue || (request == null && (resuming || changedInterval))) p.NextUtc = DateTime.UtcNow.AddMinutes(p.IntervalMinutes);
                p.Request = request;
                p.RequestedPassword = request == "Manual" ? typedPassword : null;
                p.RequestedHint = request == "Manual" ? typedHint : null;
                p.Status = request == null ? (p.Enabled ? "Schedule saved." : "Settings saved; schedule paused.") : "Queued: " + request + ". Waiting for Windows worker...";
                Storage.Save(vault);
            }
            if (request == "Manual") { ClearManualFields(); enabled.Checked = false; }
            // A scheduler failure cancels a still-queued action rather than allowing a surprise later change.
            try { Installation.StartWorker(); }
            catch
            {
                using (Storage.Lock())
                {
                    var vault = Storage.Read(); var p = vault.Accounts.First(a => a.Sid == account.Sid);
                    if (!string.IsNullOrEmpty(p.Request)) p.ClearRequest();
                    p.Enabled = false; p.Status = "Could not start the Windows worker. Schedule paused. Use Repair scheduler."; Storage.Save(vault);
                }
                throw;
            }
            RefreshStatus();
        }

        void Panic()
        {
            var account = Selected();
            if (MessageBox.Show(this, "Reset the Windows password for " + account.Name + " to the emergency password and pause its schedule?\n\nAn administrator reset can affect EFS-encrypted files and saved credentials.",
                "Emergency reset: " + account.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Save("Panic"); enabled.Checked = false;
        }
        void Pause()
        {
            var account = Selected();
            using (Storage.Lock())
            {
                var vault = Storage.Read(); var p = vault.Accounts.FirstOrDefault(a => a.Sid == account.Sid);
                if (p != null) { p.Enabled = false; p.ClearRequest(); p.Status = "Automatic changes paused. Any action that had not started was cancelled."; Storage.Save(vault); }
            }
            enabled.Checked = false; RefreshStatus();
        }
        void RefreshStatus()
        {
            var vault = Storage.ReadLocked(); ThemeManager.Sync(vault.Theme);
            if (accounts.SelectedItem == null) return;
            var account = Selected(); var p = vault.Accounts.FirstOrDefault(a => a.Sid == account.Sid);
            string value = p == null || p.CurrentPassword == null || p.LastChangedUtc == DateTime.MinValue ? "" : p.CurrentPassword;
            if (password.Text != value) { password.UseSystemPasswordChar = true; password.Text = value; }
            currentHint.Text = "Login hint: " + (p == null || p.CurrentHint == null ? "not yet set by this app" : p.CurrentHint);
            status.Text = p == null ? "Choose a style, then Change password now or enable and save a schedule." : account.Name + ": " + p.Status;
            schedule.Text = p == null || !p.Enabled ? "Automatic changes: paused" : "Next change: " + p.NextUtc.ToLocalTime().ToString("g");
            if (p != null && p.LastChangedUtc != DateTime.MinValue) schedule.Text += "\nLast change: " + p.LastChangedUtc.ToLocalTime().ToString("g");
            heartbeat.Text = vault.WorkerSeenUtc == DateTime.MinValue ? "Worker has not checked in yet. The first run may take a minute." :
                "Worker last checked: " + vault.WorkerSeenUtc.ToLocalTime().ToString("G") +
                (DateTime.UtcNow - vault.WorkerSeenUtc > TimeSpan.FromMinutes(3) ? " - worker is overdue; use Repair scheduler." : "");
        }

        void Recover()
        {
            var account = Selected();
            var p = Storage.ReadLocked().Accounts.FirstOrDefault(a => a.Sid == account.Sid);
            if (p == null || string.IsNullOrEmpty(p.Stage)) { MessageBox.Show(this, "No interrupted change needs recovery for this account.", "Recovery details"); return; }
            if (!UnlockDialog.Open(this, false)) return;
            if (p.Stage == "Prepared")
            {
                string candidate = p.PendingPassword ?? "(none)";
                var choice = MessageBox.Show(this, "A change was interrupted before its outcome was saved.\n\nNew candidate password: " + candidate +
                    "\nPrevious known password: " + (p.CurrentPassword ?? "unknown") +
                    "\n\nVerify the NEW candidate by signing in through another session before answering. Has the new candidate been confirmed to work?\n\nYes: record it and repair its hint. No/Cancel: leave recovery paused.",
                    "Verify the password for " + account.Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button3);
                if (choice != DialogResult.Yes) return;
            }
            else if (MessageBox.Show(this, "Windows password: " + (p.CurrentPassword ?? "not changed") + "\n\nRetry writing the saved hint? The password will not be reset.",
                "Repair login hint", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            using (Storage.Lock())
            {
                var vault = Storage.Read(); var latest = vault.Accounts.First(a => a.Sid == account.Sid);
                if (latest.Stage != p.Stage || latest.PendingPassword != p.PendingPassword) throw new InvalidOperationException("Recovery state changed. Open recovery details again.");
                if (latest.Stage == "Prepared") { latest.CurrentPassword = latest.PendingPassword; latest.LastChangedUtc = DateTime.UtcNow; latest.Stage = "PasswordChanged"; }
                latest.Enabled = false; latest.Request = "RepairHint"; Storage.Save(vault);
            }
            Installation.StartWorker(); RefreshStatus();
        }

        void SyncPassword()
        {
            var account = Selected();
            using (var dialog = new EnrollmentDialog(account.Name))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                using (Storage.Lock())
                {
                    var vault = Storage.Read(); var p = vault.Accounts.FirstOrDefault(a => a.Sid == account.Sid);
                    if (p == null) { p = new AccountPlan { Name = account.Name, Sid = account.Sid }; vault.Accounts.Add(p); }
                    if (!string.IsNullOrEmpty(p.Stage) || !string.IsNullOrEmpty(p.Request))
                        throw new InvalidOperationException("An action is queued or needs recovery. Resolve it before updating the known password.");
                    p.CurrentPassword = dialog.ExistingPassword; p.LastChangedUtc = DateTime.MinValue;
                    p.Enabled = false; p.Status = "Current password supplied; not yet verified. Change password now to use it. Schedule paused.";
                    Storage.Save(vault);
                }
            }
            enabled.Checked = false; RefreshStatus();
        }

        void Help()
        {
            MessageBox.Show(this, "Password Puzzle 1.4\nCreated by @Jef_Dawg\n\n" +
                "Select Password in Windows sign-in options. PIN, face, and fingerprint sign-in are separate. Windows normally shows a password hint after an incorrect password.\n\n" +
                "Automatic hints use an undocumented Windows setting. Registry readback verifies storage, not what a particular Windows sign-in screen displays. Windows updates may affect this feature.\n\n" +
                "Simple clues intentionally make passwords easy to solve. Password policy may reject numeric or single-word passwords; the app reports that and pauses.\n\n" +
                "The app locks after five minutes without interaction. Secrets are encrypted on this PC and restricted to administrators and SYSTEM. The app lock cannot prevent another administrator from inspecting the app or its data.\n\n" +
                "Keep a separate administrator account available: the app's panic button requires an existing Windows session and administrator access.\n\n" +
                "To stop changes, use Pause for each account or disable PasswordPuzzle-Rotation in Task Scheduler. Uninstall instructions are in README.md.",
                "About Password Puzzle", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void LockApp()
        {
            if (locking) return;
            locking = true; body.Visible = false; password.Clear(); hint.Clear(); emergency.Clear(); currentHint.Text = "";
            ClearManualFields();
            try
            {
                if (!UnlockDialog.Open(this, false)) { Close(); return; }
                LoadSelection(); lastInput = DateTime.UtcNow; body.Visible = true;
            }
            finally { locking = false; }
        }
        public bool PreFilterMessage(ref Message m)
        {
            if ((m.Msg >= 0x100 && m.Msg <= 0x109) || (m.Msg >= 0x201 && m.Msg <= 0x20E)) lastInput = DateTime.UtcNow;
            return false;
        }
        void ClearManualFields()
        {
            manualPassword.Clear(); manualConfirm.Clear(); manualHint.Clear(); showManual.Checked = false;
        }
        void ShowAllPasswords()
        {
            if (!UnlockDialog.Open(this, false)) return;
            // The owned window has its own five-minute lifetime and masks passwords on deactivation.
            locking = true;
            try { using (var window = new AllPasswordsWindow()) window.ShowDialog(this); }
            finally { locking = false; lastInput = DateTime.UtcNow; }
        }
        void ApplyManual()
        {
            Selected();
            if (manualPassword.Text != manualConfirm.Text) throw new InvalidOperationException("The typed passwords do not match.");
            ManualPasswords.Validate(manualPassword.Text, manualHint.Text);
            Save("Manual", manualPassword.Text, manualHint.Text);
        }
    }
}
