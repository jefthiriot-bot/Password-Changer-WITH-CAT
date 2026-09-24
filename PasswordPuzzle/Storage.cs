using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace PasswordPuzzle
{
    public static class Storage
    {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PasswordPuzzle");
        public static readonly string VaultPath = Path.Combine(Root, "vault.dat");
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PasswordPuzzle/v1/local-vault");

        public static void EnsureDirectory()
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            var admin = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(admin);
            foreach (var sid in new[] { admin, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            if (!Directory.Exists(Root)) Directory.CreateDirectory(Root, security);
            RejectLink(Root);
            // Never open an existing vault in a directory a standard user could have populated.
            var current = Directory.GetAccessControl(Root);
            foreach (FileSystemAccessRule rule in current.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                string id = rule.IdentityReference.Value;
                if (rule.AccessControlType == AccessControlType.Allow && id != "S-1-5-18" && id != "S-1-5-32-544")
                    throw new InvalidOperationException("The PasswordPuzzle data folder has unexpected permissions. An administrator must inspect " + Root + " before the app can continue.");
            }
        }

        public static void RejectLink(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Refusing a redirected application file: " + path);
        }

        public static FileStream Lock()
        {
            string path = Path.Combine(Root, "vault.lock");
            RejectLink(path);
            for (int i = 0; ; i++)
            {
                try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { if (i >= 40) throw new IOException("Another password operation is still running. Try again in a moment."); Thread.Sleep(50); }
            }
        }

        public static Vault Read()
        {
            if (!File.Exists(VaultPath)) return new Vault();
            RejectLink(VaultPath);
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(VaultPath), Entropy, DataProtectionScope.LocalMachine);
            try
            {
                using (var stream = new MemoryStream(plain))
                {
                    var vault = (Vault)new DataContractJsonSerializer(typeof(Vault)).ReadObject(stream);
                    if (vault.Version != 1 || vault.Accounts == null) throw new InvalidDataException("Unsupported or damaged vault. No passwords were changed.");
                    return vault;
                }
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }

        public static void Save(Vault vault)
        {
            byte[] plain;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Vault)).WriteObject(stream, vault);
                plain = stream.ToArray();
            }
            byte[] encrypted;
            try { encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine); }
            finally { Array.Clear(plain, 0, plain.Length); }
            string temp = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(encrypted, 0, encrypted.Length); stream.Flush(true); }
                RejectLink(VaultPath);
                if (File.Exists(VaultPath)) File.Replace(temp, VaultPath, null);
                else File.Move(temp, VaultPath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static Vault ReadLocked() { using (Lock()) return Read(); }

        public static void Worker()
        {
            if (!WindowsIdentity.GetCurrent().IsSystem) throw new InvalidOperationException("Worker must run as SYSTEM.");
            EnsureDirectory();
            using (Lock())
            {
                var vault = Read();
                if (string.IsNullOrEmpty(vault.PasswordHash)) return;
                vault.WorkerSeenUtc = DateTime.UtcNow;
                Save(vault);
                var backend = new WindowsBackend();
                foreach (var plan in vault.Accounts) Rotation.Run(plan, DateTime.UtcNow, backend, () => Save(vault));
            }
        }
    }
}
