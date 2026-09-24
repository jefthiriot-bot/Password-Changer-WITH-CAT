using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PasswordPuzzle;

// These tests exercise production logic with a fake OS boundary. There is no demo mode in the app.
static class Program
{
    static int count;
    static void Check(bool condition, string message) { count++; if (!condition) throw new Exception(message); }
    static void Throws(Action action) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, "Expected validation error."); }
    static readonly DateTime Now = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
    static AccountPlan Plan() { return new AccountPlan { Name = "LocalTest", Sid = "S-1-5-21-1-2-3-1001", Enabled = true, NextUtc = Now.AddMinutes(-5), CurrentPassword = "5678" }; }
    static int Main(string[] args)
    {
        for (int digits = 4; digits <= 8; digits++)
        {
            var p = Plan(); p.Digits = digits;
            for (int i = 0; i < 500; i++)
            {
                var puzzle = Puzzles.Generate(p);
                Check(puzzle.Password.Length == digits && Regex.IsMatch(puzzle.Password, "^[1-9][0-9]+$"), "Wrong numeric length.");
                Check(MathAnswer(puzzle.Hint) == decimal.Parse(puzzle.Password, CultureInfo.InvariantCulture), "Hint doesn't solve to password.");
                Check(puzzle.Hint.Length <= 200, "Math hint exceeds Windows hint limit.");
                Check(puzzle.Password != p.CurrentPassword, "Repeated prior password.");
                p.CurrentPassword = puzzle.Password;
            }
        }
        foreach (string boundary in new[] { "1000", "9999", "10000000", "99999999" })
            for (int i = 0; i < 100; i++)
                Check(MathAnswer(Puzzles.HintFor(boundary)) == decimal.Parse(boundary, CultureInfo.InvariantCulture), "Boundary math overflow or incorrect answer.");
        var wordPlan = Plan(); wordPlan.Style = "Word";
        for (int i = 0; i < 250; i++)
        {
            var puzzle = Puzzles.Generate(wordPlan);
            Check(Regex.IsMatch(puzzle.Password, "^[a-z]+$") && puzzle.Password != wordPlan.CurrentPassword, "Word invalid or repeated.");
            Check(Puzzles.HintFor(puzzle.Password) == puzzle.Hint, "Word hint mismatch.");
            wordPlan.CurrentPassword = puzzle.Password;
        }
        var manual = Plan(); manual.CustomHint = true; manual.HintText = "My own clue";
        Check(Puzzles.Generate(manual).Hint == manual.HintText, "Custom hint changed.");
        manual.Digits = 3; Throws(() => Puzzles.Generate(manual));
        manual.Digits = 4; manual.IntervalMinutes = 0; Throws(() => Puzzles.Validate(manual));
        manual.IntervalMinutes = 1440; manual.EmergencyPassword = "123"; Throws(() => Puzzles.Validate(manual));
        manual.EmergencyPassword = "one word"; Throws(() => Puzzles.Validate(manual));
        manual.EmergencyPassword = "apple"; Puzzles.Validate(manual);
        manual.HintText = new string('x', 201); Throws(() => Puzzles.Validate(manual));

        var vault = new Vault(); AppPassword.Set(vault, "app test password");
        Check(AppPassword.Verify(vault, "app test password"), "App password failed.");
        Check(!AppPassword.Verify(vault, "wrong"), "Wrong app password accepted.");
        string oldHash = vault.PasswordHash; AppPassword.Set(vault, "a different password");
        Check(oldHash != vault.PasswordHash && !AppPassword.Verify(vault, "app test password"), "Old app password still works.");
        Throws(() => AppPassword.Set(vault, "short"));

        var backend = new Fake(); var plan = Plan(); var journals = new List<string>();
        Rotation.Run(plan, Now, backend, () => journals.Add(plan.Stage));
        Check(backend.PasswordCalls == 1 && backend.HintCalls == 1, "Expected one complete rotation.");
        Check(journals[0] == "Prepared" && journals[1] == "PasswordChanged" && journals.Last() == null, "Wrong durable journal ordering.");
        Check(plan.CurrentPassword == backend.Password && plan.CurrentHint == backend.Hint, "Wrong saved outcome.");
        Check(plan.NextUtc == Now.AddDays(1) && plan.LastChangedUtc == Now, "Wrong timestamps.");
        Rotation.Run(plan, Now.AddHours(1), backend, () => { }); Check(backend.PasswordCalls == 1, "Rotated before due.");
        Rotation.Run(plan, Now.AddDays(10), backend, () => { }); Check(backend.PasswordCalls == 2, "Missed-run catchup did not rotate exactly once.");
        Check(plan.NextUtc == Now.AddDays(11), "Catchup next time wrong.");

        plan = Plan(); plan.Enabled = false; backend = new Fake();
        Rotation.Run(plan, Now, backend, () => { }); Check(backend.PasswordCalls == 0, "Paused schedule ran.");
        plan.Request = "Rotate"; Rotation.Run(plan, Now, backend, () => { }); Check(backend.PasswordCalls == 1, "Manual change failed while paused.");
        plan = Plan(); plan.Request = "Hint"; plan.CustomHint = true; plan.HintText = "Updated"; backend = new Fake();
        var oldDue = plan.NextUtc; Rotation.Run(plan, Now, backend, () => { });
        Check(backend.PasswordCalls == 0 && backend.Hint == "Updated" && plan.NextUtc == oldDue, "Hint-only changed password or schedule.");

        plan = Plan(); plan.Request = "Panic"; backend = new Fake();
        Rotation.Run(plan, Now, backend, () => { });
        Check(backend.Password == "1234" && backend.ForceReset && !plan.Enabled, "Emergency reset did not pause or reset.");
        Check(!plan.CurrentHint.Contains(" + "), "Panic retained an outdated puzzle.");
        plan = Plan(); plan.Request = "Panic"; backend = new Fake { AccessFails = true };
        Rotation.Run(plan, Now, backend, () => { });
        Check(backend.Password == "1234", "Emergency reset was blocked by hint preflight.");

        plan = Plan(); backend = new Fake { Reject = true };
        Rotation.Run(plan, Now, backend, () => { });
        Check(plan.CurrentPassword == "5678" && plan.Stage == null && !plan.Enabled && backend.HintCalls == 0, "Policy rejection lost old password or wrote hint.");
        plan = Plan(); backend = new Fake { AccessFails = true };
        Rotation.Run(plan, Now, backend, () => { });
        Check(backend.PasswordCalls == 0 && !plan.Enabled, "Hint preflight failed but password changed.");
        plan = Plan(); backend = new Fake { IdentityFails = true };
        Rotation.Run(plan, Now, backend, () => { });
        Check(backend.PasswordCalls == 0 && !plan.Enabled, "Wrong identity changed.");
        plan = Plan(); backend = new Fake { HintFails = true };
        Rotation.Run(plan, Now, backend, () => { });
        Check(plan.CurrentPassword == backend.Password && plan.Stage == "PasswordChanged" && !plan.Enabled && plan.PendingPassword != null, "Partial failure lost recovery data.");
        int calls = backend.PasswordCalls;
        Rotation.Run(plan, Now.AddDays(2), backend, () => { });
        Check(backend.PasswordCalls == calls, "Unresolved recovery rotated again.");
        plan.Request = "RepairHint"; backend.HintFails = false;
        Rotation.Run(plan, Now, backend, () => { });
        Check(plan.Stage == null && backend.PasswordCalls == calls && !plan.Enabled && plan.CurrentHint == backend.Hint, "Hint repair changed password or resumed.");
        plan = Plan(); backend = new Fake { UnknownPasswordOutcome = true };
        Rotation.Run(plan, Now, backend, () => { });
        Check(plan.Stage == "Prepared" && plan.PendingPassword != null && !plan.Enabled, "Unknown outcome discarded candidate.");
        plan.Request = "Rotate"; backend.UnknownPasswordOutcome = false;
        Rotation.Run(plan, Now, backend, () => { });
        Check(backend.PasswordCalls == 1, "Unknown outcome retried a password operation.");
        string uncertain = plan.PendingPassword;
        plan.Request = "Panic"; backend.Reject = true;
        Rotation.Run(plan, Now, backend, () => { });
        Check(plan.Stage == "Prepared" && plan.PendingPassword == uncertain, "Rejected panic erased the earlier recovery candidate.");
        plan.Request = "Panic"; backend.Reject = false;
        Rotation.Run(plan, Now, backend, () => { });
        Check(plan.CurrentPassword == "1234" && plan.Stage == null && !plan.Enabled, "Emergency reset could not resolve an interrupted change.");
        plan = Plan(); backend = new Fake(); int saves = 0;
        Rotation.Run(plan, Now, backend, () => { if (++saves == 1) throw new Exception("disk full"); });
        Check(backend.PasswordCalls == 0, "Password changed without a durable journal.");

        plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = "  My typed P@ss  "; plan.RequestedHint = "My own reminder";
        backend = new Fake(); journals.Clear();
        Rotation.Run(plan, Now, backend, () => journals.Add(plan.Stage));
        Check(backend.Password == "  My typed P@ss  " && plan.CurrentPassword == backend.Password, "Typed password was trimmed or replaced.");
        Check(backend.Hint == "My own reminder" && !backend.ForceReset, "Manual change ignored the hint or forced a reset.");
        Check(!plan.Enabled && plan.RequestedPassword == null && plan.RequestedHint == null, "Manual change did not pause or clear queued secrets.");
        Check(journals[0] == "Prepared" && journals[1] == "PasswordChanged", "Manual change skipped journaling.");
        plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = "some password"; plan.RequestedHint = "";
        backend = new Fake(); Rotation.Run(plan, Now, backend, () => { });
        Check(backend.Hint == "", "Empty manual hint did not remove the old clue.");
        plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = "rejected password"; plan.RequestedHint = "hint";
        backend = new Fake { Reject = true }; Rotation.Run(plan, Now, backend, () => { });
        Check(plan.CurrentPassword == "5678" && backend.HintCalls == 0 && plan.RequestedPassword == null, "Rejected manual password corrupted the known password or left queued secrets.");
        plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = "recover this password"; plan.RequestedHint = "hint";
        backend = new Fake { HintFails = true }; Rotation.Run(plan, Now, backend, () => { });
        Check(plan.CurrentPassword == "recover this password" && plan.PendingPassword == plan.CurrentPassword && plan.Stage == "PasswordChanged" && !plan.Enabled, "Manual hint failure lost the new password.");
        plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = "candidate"; plan.RequestedHint = "hint";
        backend = new Fake { UnknownPasswordOutcome = true }; Rotation.Run(plan, Now, backend, () => { });
        Check(plan.PendingPassword == "candidate" && plan.Stage == "Prepared" && plan.RequestedPassword == null, "Interrupted manual change lost recovery information.");
        foreach (string badPassword in new[] { "", "   ", "bad\0password", "bad\npassword", new string('x', 129) })
        {
            plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = badPassword; plan.RequestedHint = "hint";
            backend = new Fake(); Rotation.Run(plan, Now, backend, () => { });
            Check(backend.PasswordCalls == 0 && plan.RequestedPassword == null, "Invalid manual password reached Windows or persisted in the queue.");
        }
        Throws(() => ManualPasswords.Validate("valid password", new string('h', 201)));
        Throws(() => ManualPasswords.Validate("valid password", "bad\0hint"));
        plan = Plan(); plan.Request = "Manual"; plan.RequestedPassword = "cancel me"; plan.RequestedHint = "clear me";
        plan.ClearRequest(); Check(plan.Request == null && plan.RequestedPassword == null && plan.RequestedHint == null, "Cancellation retained a typed password.");

        Check(ThemeCatalog.Names.Length == 6 && ThemeCatalog.Names.Distinct().Count() == 6, "Missing or duplicate theme choices.");
        Check(ThemeCatalog.Normalize(null) == "Windows XP" && ThemeCatalog.Normalize("old unknown theme") == "Windows XP", "Old settings lack a valid theme fallback.");
        foreach (string name in ThemeCatalog.Names)
        {
            var theme = ThemeCatalog.Get(name);
            Check(theme.Name == name, "Theme selection did not round-trip.");
            foreach (int background in new[] { theme.Background, theme.Surface, theme.Input })
            {
                Check(Contrast(theme.Text, background) >= 4.5, name + " body text has low contrast.");
                Check(Contrast(theme.Danger, background) >= 4.5, name + " warning text has low contrast.");
            }
            Check(Contrast(theme.Accent, theme.Surface) >= 4.5, name + " group heading has low contrast.");
            Check(Contrast(theme.ButtonText, theme.ButtonTop) >= 4.5 && Contrast(theme.ButtonText, theme.ButtonBottom) >= 4.5, name + " button text has low contrast.");
            Check(Contrast(theme.HeaderText, theme.HeaderTop) >= 4.5 && Contrast(theme.HeaderText, theme.HeaderBottom) >= 4.5, name + " banner text has low contrast.");
            // Theme survives vault serialization along with the selected account's password and schedule.
            var saved = new Vault { Theme = name }; saved.Accounts.Add(Plan());
            using (var stream = new System.IO.MemoryStream())
            {
                var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Vault));
                serializer.WriteObject(stream, saved); stream.Position = 0;
                var restored = (Vault)serializer.ReadObject(stream);
                Check(restored.Theme == name && restored.Accounts[0].CurrentPassword == "5678" && restored.Accounts[0].Enabled, "Theme persistence changed account settings.");
            }
        }
        Check(ThemeCatalog.Get("High Contrast").HighContrast && !ThemeCatalog.Get("High Contrast").Beveled, "High contrast should use solid surfaces.");
        Check(SavedPasswordView.From(null).Password == null, "Unmanaged account should not have a guessed password.");
        plan = Plan();
        Check(SavedPasswordView.From(plan).Password == null, "Unverified supplied password was presented as confirmed.");
        plan.LastChangedUtc = Now;
        Check(SavedPasswordView.From(plan).Password == "5678" && SavedPasswordView.From(plan).LastChangedUtc == Now, "Confirmed saved password was missing.");
        plan.Request = "Manual"; plan.RequestedPassword = "not committed";
        Check(SavedPasswordView.From(plan).Password == "5678", "Queued candidate was shown as the current password.");
        plan.Stage = "Prepared"; plan.PendingPassword = "uncertain candidate";
        Check(SavedPasswordView.From(plan).Password == null && SavedPasswordView.From(plan).Status.Contains("Uncertain"), "Interrupted change was presented as confirmed.");
        plan.Stage = "PasswordChanged"; plan.CurrentPassword = "confirmed new password";
        Check(SavedPasswordView.From(plan).Password == "confirmed new password" && SavedPasswordView.From(plan).Status.Contains("hint"), "Hint failure hid a confirmed password.");

        if (args.Length > 0)
        {
            var xml = TaskDefinition.Create(@"C:\Program Files\Password Puzzle\PasswordPuzzle.exe", @"C:\Program Files\Password Puzzle", Now);
            var schemas = new System.Xml.Schema.XmlSchemaSet();
            schemas.Add("http://schemas.microsoft.com/windows/2004/02/mit/task", args[0]);
            var settings = new System.Xml.XmlReaderSettings { ValidationType = System.Xml.ValidationType.Schema, Schemas = schemas };
            settings.ValidationEventHandler += (sender, e) => { throw new Exception("Task XML: " + e.Message); };
            using (var reader = System.Xml.XmlReader.Create(new System.IO.StringReader(xml), settings)) while (reader.Read()) { }
            Check(true, "Official Task Scheduler XSD validation");
        }
        Console.WriteLine("PASS: " + count + " checks covering puzzles, authentication, schedules, policy rejection, journaling, and recovery.");
        return 0;
    }
    static decimal MathAnswer(string hint)
    {
        Check(hint.StartsWith("Solve: ", StringComparison.Ordinal), "Expected a multistep math hint.");
        string expression = hint.Substring(7, hint.IndexOf('.') - 7);
        Check(Regex.Matches(expression, @"[+*/-]").Count >= 3 && expression.Contains("("), "Math problem is too simple.");
        // Evaluate the actual displayed expression using an independent arithmetic engine.
        using (var table = new System.Data.DataTable())
            return Convert.ToDecimal(table.Compute(expression, ""), CultureInfo.InvariantCulture);
    }
    static double Contrast(int a, int b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + .05) / (Math.Min(la, lb) + .05);
    }
    static double Luminance(int rgb)
    {
        Func<int, double> channel = n => { double c = n / 255.0; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); };
        return .2126 * channel((rgb >> 16) & 255) + .7152 * channel((rgb >> 8) & 255) + .0722 * channel(rgb & 255);
    }
    sealed class Fake : IAccountBackend
    {
        public int PasswordCalls, HintCalls;
        public bool Reject, AccessFails, IdentityFails, HintFails, UnknownPasswordOutcome, ForceReset;
        public string Password, Hint;
        public void ValidateAccount(AccountPlan p) { if (IdentityFails) throw new Exception("identity changed"); }
        public void CheckHintAccess(AccountPlan p) { if (AccessFails) throw new Exception("hint inaccessible"); }
        public void SetPassword(AccountPlan p, string password, bool forceReset)
        {
            PasswordCalls++; ForceReset = forceReset;
            if (Reject) throw new PasswordRejectedException("policy rejected");
            if (UnknownPasswordOutcome) throw new Exception("interrupted OS call");
            Password = password;
        }
        public void SetHint(AccountPlan p, string hint) { HintCalls++; if (HintFails) throw new Exception("hint failed"); Hint = hint; }
    }
}
