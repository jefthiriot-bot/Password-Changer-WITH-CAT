using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace PasswordPuzzle
{
    public sealed class LocalAccount
    {
        public string Name;
        public string Sid;
        public string Description;
        public override string ToString() { return Name; }
    }

    public sealed class WindowsBackend : IAccountBackend
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct User0 { [MarshalAs(UnmanagedType.LPWStr)] public string Name; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct User23
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string Name;
            [MarshalAs(UnmanagedType.LPWStr)] public string FullName;
            [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
            public uint Flags;
            public IntPtr Sid;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct User24
        {
            [MarshalAs(UnmanagedType.Bool)] public bool InternetIdentity;
            public uint Flags;
            public IntPtr Provider;
            public IntPtr Principal;
            public IntPtr Sid;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PasswordInfo { [MarshalAs(UnmanagedType.LPWStr)] public string Password; }

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserEnum(string server, int level, int filter, out IntPtr buffer,
            int preferredLength, out int entries, out int total, ref uint resume);
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserGetInfo(string server, string user, int level, out IntPtr buffer);
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserSetInfo(string server, string user, int level, ref PasswordInfo info, out uint parameterError);
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserChangePassword(string server, string user, string oldPassword, string newPassword);
        [DllImport("Netapi32.dll")] static extern int NetApiBufferFree(IntPtr buffer);

        static bool IsLocalOnly(string name)
        {
            IntPtr buffer;
            int result = NetUserGetInfo(null, name, 24, out buffer);
            try
            {
                if (result != 0) throw new Win32Exception(result, "Could not verify whether " + name + " is a local-only account.");
                return !((User24)Marshal.PtrToStructure(buffer, typeof(User24))).InternetIdentity;
            }
            finally { if (buffer != IntPtr.Zero) NetApiBufferFree(buffer); }
        }

        static bool IsEligible(User23 info)
        {
            if ((info.Flags & 2) != 0 || (info.Flags & 0x200) == 0) return false;
            string sid = new SecurityIdentifier(info.Sid).Value;
            uint rid = uint.Parse(sid.Substring(sid.LastIndexOf('-') + 1), CultureInfo.InvariantCulture);
            return (rid >= 1000 || rid == 500) && IsLocalOnly(info.Name);
        }

        public static List<LocalAccount> ListAccounts(bool includeAll = false)
        {
            var users = new List<LocalAccount>();
            uint resume = 0;
            int result;
            do
            {
                IntPtr buffer;
                int entries, total;
                result = NetUserEnum(null, 0, 2, out buffer, -1, out entries, out total, ref resume);
                try
                {
                    if (result != 0 && result != 234) throw new Win32Exception(result);
                    int size = Marshal.SizeOf(typeof(User0));
                    for (int i = 0; i < entries; i++)
                    {
                        var name = (User0)Marshal.PtrToStructure(IntPtr.Add(buffer, i * size), typeof(User0));
                        IntPtr details;
                        int detailResult = NetUserGetInfo(null, name.Name, 23, out details);
                        try
                        {
                            if (detailResult == 2221) continue; // Deleted during enumeration.
                            if (detailResult != 0) throw new Win32Exception(detailResult);
                            var user = (User23)Marshal.PtrToStructure(details, typeof(User23));
                            if (includeAll)
                            {
                                string type;
                                try { type = IsLocalOnly(user.Name) ? "Local" : "Microsoft / connected"; }
                                catch (Win32Exception) { type = "Identity not verified"; }
                                users.Add(new LocalAccount { Name = user.Name, Sid = new SecurityIdentifier(user.Sid).Value,
                                    Description = type + ((user.Flags & 2) != 0 ? " (disabled)" : "") });
                            }
                            else if (IsEligible(user)) users.Add(new LocalAccount { Name = user.Name, Sid = new SecurityIdentifier(user.Sid).Value });
                        }
                        finally { if (details != IntPtr.Zero) NetApiBufferFree(details); }
                    }
                }
                finally { if (buffer != IntPtr.Zero) NetApiBufferFree(buffer); }
            } while (result == 234);
            return users.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public void ValidateAccount(AccountPlan plan)
        {
            IntPtr buffer;
            int result = NetUserGetInfo(null, plan.Name, 23, out buffer);
            try
            {
                if (result != 0) throw new Win32Exception(result, "The selected local account is no longer available. Refresh the account list.");
                var info = (User23)Marshal.PtrToStructure(buffer, typeof(User23));
                if (new SecurityIdentifier(info.Sid).Value != plan.Sid || !IsEligible(info))
                    throw new InvalidOperationException("Account identity changed, is disabled, or is no longer a local-only account.");
            }
            finally { if (buffer != IntPtr.Zero) NetApiBufferFree(buffer); }
        }

        static RegistryKey OpenHint(AccountPlan plan)
        {
            if (!WindowsIdentity.GetCurrent().IsSystem) throw new InvalidOperationException("The hint worker must run as SYSTEM through its installed scheduled task.");
            var sid = new SecurityIdentifier(plan.Sid);
            uint rid = uint.Parse(sid.Value.Substring(sid.Value.LastIndexOf('-') + 1), CultureInfo.InvariantCulture);
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                // Only this value is edited. Never alter account records, hashes, or SAM permissions.
                var key = machine.OpenSubKey(@"SAM\SAM\Domains\Account\Users\" + rid.ToString("X8", CultureInfo.InvariantCulture), true);
                if (key == null) throw new InvalidOperationException("Windows did not expose the selected account's password-hint setting.");
                return key;
            }
        }

        public void CheckHintAccess(AccountPlan plan)
        {
            using (var key = OpenHint(plan))
            {
                object value = key.GetValue("UserPasswordHint");
                if (value != null && key.GetValueKind("UserPasswordHint") != RegistryValueKind.Binary)
                    throw new InvalidOperationException("This Windows build uses an unrecognized hint format. Password was not changed.");
            }
        }

        public void SetHint(AccountPlan plan, string hint)
        {
            if (hint == null || hint.Length > 200 || hint.IndexOf('\0') >= 0) throw new InvalidOperationException("Invalid password hint.");
            using (var key = OpenHint(plan))
            {
                var bytes = Encoding.Unicode.GetBytes(hint + "\0");
                key.SetValue("UserPasswordHint", bytes, RegistryValueKind.Binary);
                key.Flush();
                var actual = key.GetValue("UserPasswordHint") as byte[];
                if (actual == null || !actual.SequenceEqual(bytes)) throw new InvalidOperationException("Windows did not retain the new hint.");
            }
        }

        public void SetPassword(AccountPlan plan, string password, bool forceReset)
        {
            ValidateAccount(plan);
            var info = new PasswordInfo { Password = password };
            uint parameterError;
            int result = !forceReset && plan.CurrentPassword != null ?
                NetUserChangePassword(Environment.MachineName, plan.Name, plan.CurrentPassword, password) :
                NetUserSetInfo(null, plan.Name, 1003, ref info, out parameterError);
            if (result != 0)
            {
                string details = (result == 2245 || result == 1325) ?
                    "Windows password policy rejected this password. Your PC may require more characters or complexity. This app does not weaken Windows policy." :
                    new Win32Exception(result).Message;
                throw new PasswordRejectedException(details + " (Windows error " + result + ").");
            }
        }
    }
}
