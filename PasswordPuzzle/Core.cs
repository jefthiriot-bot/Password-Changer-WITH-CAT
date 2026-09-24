using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PasswordPuzzle
{
    [DataContract]
    public sealed class Vault
    {
        [DataMember] public int Version = 1;
        [DataMember] public string Salt;
        [DataMember] public string PasswordHash;
        [DataMember] public int Iterations = 210000;
        [DataMember] public int FailedUnlocks;
        [DataMember] public DateTime BlockedUntilUtc;
        [DataMember] public List<AccountPlan> Accounts = new List<AccountPlan>();
        [DataMember] public DateTime WorkerSeenUtc;
        [DataMember] public string Theme = "Windows XP";
    }

    [DataContract]
    public sealed class AccountPlan
    {
        [DataMember] public string Sid;
        [DataMember] public string Name;
        [DataMember] public string Style = "Math";
        [DataMember] public int Digits = 4;
        [DataMember] public bool CustomHint;
        [DataMember] public string HintText = "";
        [DataMember] public string EmergencyPassword = "1234";
        [DataMember] public int IntervalMinutes = 1440;
        [DataMember] public bool Enabled;
        [DataMember] public DateTime NextUtc;
        [DataMember] public DateTime LastChangedUtc;
        [DataMember] public string CurrentPassword;
        [DataMember] public string CurrentHint;
        [DataMember] public string Request;
        [DataMember] public string RequestedPassword;
        [DataMember] public string RequestedHint;
        [DataMember] public string PendingPassword;
        [DataMember] public string PendingHint;
        [DataMember] public string Stage;
        [DataMember] public string Status = "No password has been changed by this app.";
        public void ClearRequest() { Request = null; RequestedPassword = null; RequestedHint = null; }
    }

    public static class ManualPasswords
    {
        public static void Validate(string password, string hint)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length > 128 || password.Any(char.IsControl))
                throw new InvalidOperationException("Enter a password of 1 to 128 characters without control characters. Windows password rules still apply.");
            if (hint == null || hint.Length > 200 || hint.IndexOf('\0') >= 0)
                throw new InvalidOperationException("The optional hint must be no longer than 200 characters.");
        }
    }

    public sealed class SavedPasswordView
    {
        public string Password;
        public string Status;
        public DateTime LastChangedUtc;
        public static SavedPasswordView From(AccountPlan plan)
        {
            if (plan == null) return new SavedPasswordView { Status = "Unknown - not saved by this app" };
            if (plan.Stage == "Prepared") return new SavedPasswordView { Status = "Uncertain - open Recovery details" };
            if (plan.CurrentPassword == null || plan.LastChangedUtc == DateTime.MinValue)
                return new SavedPasswordView { Status = "Unknown - no successful app change recorded" };
            return new SavedPasswordView { Password = plan.CurrentPassword, LastChangedUtc = plan.LastChangedUtc,
                Status = plan.Stage == "PasswordChanged" ? "Last saved password; hint needs repair" : "Last saved password; outside changes not tracked" };
        }
    }

    public sealed class Puzzle
    {
        public readonly string Password;
        public readonly string Hint;
        public Puzzle(string password, string hint) { Password = password; Hint = hint; }
    }

    public static class Puzzles
    {
        public static readonly Puzzle[] Words = {
            new Puzzle("keyboard", "What has keys and a spacebar? One lowercase word."),
            new Puzzle("piano", "What instrument has black and white keys? One lowercase word."),
            new Puzzle("clock", "What has hands and tells the time? One lowercase word."),
            new Puzzle("snow", "What falls from clouds in soft white flakes? One lowercase word."),
            new Puzzle("banana", "What yellow fruit do you peel, often shown with monkeys? One lowercase word."),
            new Puzzle("rainbow", "What colorful arc can appear after rain? One lowercase word."),
            new Puzzle("book", "What has pages that you turn to read a story? One lowercase word."),
            new Puzzle("sun", "What star lights Earth during the day? One lowercase word."),
            new Puzzle("moon", "What natural satellite orbits Earth? One lowercase word."),
            new Puzzle("towel", "What gets wetter as it dries you? One lowercase word."),
            new Puzzle("ice", "What do we call frozen water? One lowercase word."),
            new Puzzle("apple", "What fruit gives its name to the maker of the iPhone? One lowercase word."),
            new Puzzle("cat", "What pet says meow? One lowercase word."),
            new Puzzle("dog", "What pet says woof? One lowercase word."),
            new Puzzle("fish", "What animal has fins and gills? One lowercase word."),
            new Puzzle("spider", "What eight-legged creature spins a web? One lowercase word."),
            new Puzzle("bee", "What buzzing insect makes honey? One lowercase word."),
            new Puzzle("tree", "What tall plant has a trunk and branches? One lowercase word."),
            new Puzzle("shoe", "What do you wear on a foot over a sock? One lowercase word."),
            new Puzzle("door", "What do you open with a doorknob? One lowercase word."),
            new Puzzle("candle", "What has a wick and melts as it burns? One lowercase word."),
            new Puzzle("mirror", "What do you look into to see your reflection? One lowercase word."),
            new Puzzle("pencil", "What do you write with that has graphite inside? One lowercase word."),
            new Puzzle("umbrella", "What do you hold over your head to keep rain off? One lowercase word.")
        };

        public static int RandomInt(int min, int maxExclusive)
        {
            uint range = checked((uint)(maxExclusive - min));
            if (range == 0) throw new ArgumentOutOfRangeException("maxExclusive");
            ulong limit = (1UL << 32) - ((1UL << 32) % range);
            byte[] bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                uint value;
                do { rng.GetBytes(bytes); value = BitConverter.ToUInt32(bytes, 0); } while ((ulong)value >= limit);
                return checked(min + (int)(value % range));
            }
        }

        public static Puzzle Generate(AccountPlan plan)
        {
            Validate(plan);
            string style = plan.Style == "Mixed" ? (RandomInt(0, 2) == 0 ? "Math" : "Word") : plan.Style;
            Puzzle p;
            if (style == "Word")
            {
                var choices = Words.Where(w => w.Password != plan.CurrentPassword).ToArray();
                p = choices[RandomInt(0, choices.Length)];
            }
            else
            {
                int lower = (int)Math.Pow(10, plan.Digits - 1);
                int upper = lower * 10;
                string password;
                do { password = RandomInt(lower, upper).ToString(CultureInfo.InvariantCulture); }
                while (password == plan.CurrentPassword);
                p = new Puzzle(password, HintFor(password));
            }
            return new Puzzle(p.Password, plan.CustomHint ? plan.HintText : p.Hint);
        }

        public static string HintFor(string password)
        {
            if (password != null && Regex.IsMatch(password, @"^[1-9][0-9]{3,7}$"))
            {
                int answer = int.Parse(password, CultureInfo.InvariantCulture);
                int factor = RandomInt(12, 50);
                int offset = RandomInt(101, 1000);
                string expression;
                switch (RandomInt(0, 3))
                {
                    case 0:
                        int productBase = (answer + offset) / factor;
                        int remainder = (answer + offset) % factor;
                        if (remainder == 0) { productBase--; remainder = factor; }
                        expression = string.Format(CultureInfo.InvariantCulture, "({0} * {1}) + {2} - {3}", productBase, factor, remainder, offset);
                        break;
                    case 1:
                        int total = (answer + offset + factor - 1) / factor;
                        int left = RandomInt(1, total);
                        expression = string.Format(CultureInfo.InvariantCulture, "({0} + {1}) * {2} - {3}", left, total - left, factor, total * factor - answer);
                        break;
                    default:
                        // Use long arithmetic: eight-digit answers times 49 can exceed Int32.
                        long dividend = ((long)answer + offset) * factor;
                        int subtract = RandomInt(101, 1000);
                        expression = string.Format(CultureInfo.InvariantCulture, "({0} - {1}) / {2} - {3}", dividend + subtract, subtract, factor, offset);
                        break;
                }
                return "Solve: " + expression + ". Use digits only. * means multiply; / means divide.";
            }
            var word = Words.FirstOrDefault(p => p.Password == password);
            if (word != null) return word.Hint;
            throw new InvalidOperationException("This password has no generated clue. Choose your own hint or change the password now.");
        }

        public static void Validate(AccountPlan p)
        {
            if (p.Style != "Math" && p.Style != "Word" && p.Style != "Mixed") throw new InvalidOperationException("Choose a password style.");
            if (p.Digits < 4 || p.Digits > 8) throw new InvalidOperationException("Choose 4 to 8 digits.");
            if (p.IntervalMinutes < 1 || p.IntervalMinutes > 525600) throw new InvalidOperationException("Choose an interval from one minute to one year.");
            if (p.CustomHint && (string.IsNullOrWhiteSpace(p.HintText) || p.HintText.Length > 200 || p.HintText.IndexOf('\0') >= 0))
                throw new InvalidOperationException("Enter a hint of 1 to 200 characters.");
            if (p.EmergencyPassword == null || !Regex.IsMatch(p.EmergencyPassword, @"^(?:[0-9]{4,20}|[a-zA-Z]{1,24})$"))
                throw new InvalidOperationException("The emergency password must be 4 to 20 digits or one word of 1 to 24 letters.");
        }
    }

    public static class AppPassword
    {
        public static void Set(Vault vault, string password)
        {
            if (password == null || password.Length < 8 || password.Length > 128)
                throw new InvalidOperationException("Use an app password of 8 to 128 characters.");
            byte[] salt = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            vault.Iterations = 210000;
            vault.Salt = Convert.ToBase64String(salt);
            vault.PasswordHash = Derive(password, salt, vault.Iterations);
            vault.FailedUnlocks = 0;
            vault.BlockedUntilUtc = DateTime.MinValue;
        }

        static string Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return Convert.ToBase64String(kdf.GetBytes(32));
        }

        public static bool Verify(Vault vault, string password)
        {
            if (string.IsNullOrEmpty(vault.PasswordHash)) return false;
            if (vault.Iterations < 100000 || vault.Iterations > 1000000) throw new InvalidOperationException("Invalid app password settings.");
            byte[] expected = Convert.FromBase64String(vault.PasswordHash);
            byte[] actual = Convert.FromBase64String(Derive(password, Convert.FromBase64String(vault.Salt), vault.Iterations));
            int diff = expected.Length ^ actual.Length;
            for (int i = 0; i < Math.Min(actual.Length, expected.Length); i++) diff |= actual[i] ^ expected[i];
            Array.Clear(actual, 0, actual.Length);
            return diff == 0;
        }
    }

    public interface IAccountBackend
    {
        void ValidateAccount(AccountPlan plan);
        void CheckHintAccess(AccountPlan plan);
        void SetPassword(AccountPlan plan, string password, bool forceReset);
        void SetHint(AccountPlan plan, string hint);
    }

    // The caller holds the vault's exclusive lock for the whole operation.
    public static class Rotation
    {
        public static void Run(AccountPlan p, DateTime now, IAccountBackend backend, Action save)
        {
            if (p.Request == "RepairHint" && (p.Stage == "PasswordChanged" || p.Stage == "HintPending"))
            {
                p.ClearRequest();
                p.Enabled = false;
                try
                {
                    backend.ValidateAccount(p);
                    backend.SetHint(p, p.PendingHint);
                    p.CurrentHint = p.PendingHint;
                    p.Stage = null;
                    p.PendingPassword = null;
                    p.PendingHint = null;
                    p.Status = "Hint repaired. Automatic changes remain paused; save your schedule to resume.";
                }
                catch (Exception e) { p.Status = "Hint repair failed: " + e.Message; }
                save();
                return;
            }
            if (!string.IsNullOrEmpty(p.Stage) && p.Request != "Panic")
            {
                p.Enabled = false;
                p.ClearRequest();
                p.Status = "Interrupted change: automatic changes paused. Open recovery details before continuing.";
                save();
                return;
            }
            bool requested = !string.IsNullOrEmpty(p.Request);
            if (!requested && (!p.Enabled || p.NextUtc > now)) return;
            string request = p.Request ?? "Rotate";
            string previousStage = p.Stage;
            string previousPendingPassword = p.PendingPassword;
            string previousPendingHint = p.PendingHint;
            bool passwordChanged = false;
            bool passwordAttempted = false;
            bool passwordRejected = false;
            try
            {
                if (request == "Manual") ManualPasswords.Validate(p.RequestedPassword, p.RequestedHint);
                else Puzzles.Validate(p);
                if (request != "Rotate" && request != "Panic" && request != "Hint" && request != "Manual") throw new InvalidOperationException("Unknown action.");
                backend.ValidateAccount(p);
                // Hint editing is not a supported public Windows API. Refuse before changing
                // any password if this Windows build will not let SYSTEM open the hint value.
                if (request != "Panic") backend.CheckHintAccess(p);
                string password = p.CurrentPassword;
                string hint;
                if (request == "Hint")
                    hint = p.CustomHint ? p.HintText : Puzzles.HintFor(password);
                else if (request == "Manual")
                {
                    password = p.RequestedPassword;
                    hint = p.RequestedHint;
                }
                else if (request == "Panic")
                {
                    password = p.EmergencyPassword;
                    // Do not leave an old puzzle after an emergency reset.
                    hint = "Emergency password set. Ask the owner of Password Puzzle.";
                }
                else
                {
                    Puzzle puzzle = Puzzles.Generate(p);
                    password = puzzle.Password;
                    hint = puzzle.Hint;
                }

                p.PendingPassword = request == "Hint" ? null : password;
                p.PendingHint = hint;
                p.Stage = request == "Hint" ? "HintPending" : "Prepared";
                p.ClearRequest();
                // Journal the candidate BEFORE changing Windows. A power loss cannot make it disappear.
                save();
                if (request != "Hint")
                {
                    passwordAttempted = true;
                    try { backend.SetPassword(p, password, request == "Panic"); }
                    catch (PasswordRejectedException) { passwordRejected = true; throw; }
                    passwordChanged = true;
                    p.CurrentPassword = password;
                    p.LastChangedUtc = now;
                    p.Stage = "PasswordChanged";
                    if (request == "Panic" || request == "Manual") p.Enabled = false;
                    save();
                }
                backend.SetHint(p, hint);
                p.CurrentHint = hint;
                p.PendingPassword = null;
                p.PendingHint = null;
                p.Stage = null;
                if (request != "Hint") p.NextUtc = now.AddMinutes(p.IntervalMinutes);
                p.Status = request == "Panic" ? "Emergency password set. Automatic changes are paused." :
                    request == "Manual" ? "Your typed password and hint were applied. Automatic changes are paused." :
                    request == "Hint" ? "Login hint updated; password unchanged." : "Password and login hint updated.";
                save();
            }
            catch (Exception e)
            {
                p.Enabled = false;
                p.ClearRequest();
                if (!passwordChanged && (!passwordAttempted || passwordRejected))
                {
                    p.Stage = previousStage;
                    p.PendingPassword = previousPendingPassword;
                    p.PendingHint = previousPendingHint;
                }
                p.Status = (passwordChanged ? "PASSWORD CHANGED, but the hint or saved state needs attention. " :
                    !string.IsNullOrEmpty(p.Stage) ? "Change interrupted; password outcome is uncertain. " : "No password change completed. ") +
                    e.Message + " Automatic changes are paused.";
                save();
            }
        }
    }

    public sealed class PasswordRejectedException : Exception
    {
        public PasswordRejectedException(string message) : base(message) { }
    }
}
