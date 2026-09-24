# Password Puzzle for Windows 11

A real Windows desktop app for scheduled local-account password changes and matching login clues. There is no demo mode. Password changes affect the selected Windows account.

## Open the app

The release has two executable files. **PasswordPuzzle.exe** is the complete main app; **PasswordPuzzle-Reset.exe** opens only the emergency reset menu. Neither needs a separate DLL or configuration file beside it. Both use the .NET Framework included with Windows 11. Their encrypted settings are created on the PC at runtime.

Open the main app once to create your app password and configure accounts. Afterwards, the reset EXE can be kept anywhere on that same PC. It asks for the same app password, lets you choose a configured account, resets it to its saved emergency password (default `1234`), and pauses that account's schedule. It uses the installed main app's background worker; it is not an app-password bypass or a tool that runs from the Windows sign-in screen.

1. Copy `PasswordPuzzle-Windows11.zip` to your Windows 11 PC and extract it.
2. Double-click **PasswordPuzzle.exe** and accept Windows' administrator prompt.
3. Create the app's own password. It must have at least eight characters.
4. Select a local account. Choose **Math problem**, **One-word riddle**, or **Random mix**.
5. Click **Change password now**, or enable automatic changes and click **Save schedule**. Enabling a schedule for a new account performs its first change immediately.

The app installs itself in `%ProgramFiles%\Password Puzzle` and creates desktop and Start menu shortcuts. No SDK, Python, subscription, internet connection, or separate download is needed to run it on a standard Windows 11 installation with .NET Framework 4.8. The executable is unsigned, so Windows may show an unknown-publisher or SmartScreen warning. This is a Windows application; it cannot run on macOS.

On the first change, enter the existing Windows password (blank if the account currently has no password). If you don't know it, uncheck that option to use an administrator reset. Resets can make EFS-encrypted files and saved credentials inaccessible. Subsequent normal changes use the app's saved current password; they do not silently fall back to an administrator reset if that password is wrong.

## Features

- **All account passwords**, beside the account selector, opens a read-only list after you re-enter your app password. It lists local Windows account records, including disabled and Microsoft-connected accounts, and shows the latest passwords successfully recorded by this app. Click **Reveal all (15 seconds)** to view them together. Passwords hide when you switch windows, and the list closes after five minutes.
- Windows cannot supply the existing plaintext password for an arbitrary account. Accounts without a successful password change recorded by this app show **Unknown**; interrupted changes show **Uncertain** and direct you to recovery. Changes outside the app are not tracked. Domain and online-only accounts are not included. Opening this window does not reset any passwords.

- **Theme selector** below the header: Light, Dark, High Contrast, Windows XP, Windows 7, and Cat. The choice is saved for both apps and restored when you reopen them, including the unlock window. Windows XP remains the default for existing installations. Changing a theme does not change passwords or schedules.
- **Cat theme** embeds the supplied white cat and blue yarn image unchanged, with green, pink, and blue accents. Select Cat to show the cat panel; there is no extra image file to manage.
- High Contrast uses black surfaces, white text, yellow focus/selection accents, and solid fills. Windows XP uses blue gradients and cream panels; Windows 7 uses pale blue, glass-like gradients. These style the app's contents; Windows still supplies the outer window frame, system message boxes, and administrator prompts. **Created by @Jef_Dawg** stays visible in the bottom bar of both apps.
- **Type a password...** jumps to the manual password form for the selected account. Enter the new password twice, optionally add a hint, then click **Set typed password**. Leaving the hint blank clears the previous clue. Typed passwords are used exactly as entered, including spaces, and must satisfy Windows policy. Automatic changes pause so the typed password remains in place until you choose to resume the schedule.

- Select enabled local-only Windows accounts. Microsoft accounts, domain accounts, and reserved system accounts are excluded. Each managed account has its own schedule.
- Math passwords have **4–8 digits**, with no leading zero. Harder clues combine parentheses, multiplication or exact division, and addition/subtraction in three steps. The answer is always the password, without decimals. For example, `(127 + 86) * 24 - 367` gives `4745`. The hint explains that `*` means multiply and `/` means divide.
- Word passwords are simple, single lowercase words with a matching clue. Random mix chooses between math and words.
- Automatic clues change when the password changes. Select **Use my own hint** for a hint that stays as you set it. **Update hint only** applies the hint without changing the password. A custom hint is your responsibility; it may not describe a later generated password.
- Schedule intervals use minutes, hours, or days, up to one year. The background worker checks every minute; changes can occur about a minute after their due time. A missed change runs once after startup or wake, then the interval starts again. The app does not wake a sleeping PC.
- **Reveal** shows the last password successfully set by this app for 15 seconds. Changes made elsewhere are not tracked. Use **Sync current password** to supply a password changed outside the app; then change the password through the app to verify it.
- **PANIC** resets the selected account to the emergency password (default **1234**) and pauses its schedule. It asks you to confirm the account. Change the emergency field and save settings to keep a different emergency password. Numeric emergency passwords require at least four digits; a single word is also accepted.
- **Change app password** requires the current app password. **Lock app** locks immediately; five minutes without app interaction also locks it. Failed unlock attempts trigger a persisted cooldown.

The app's panic button is available after you sign into Windows and unlock the app as an administrator. It is not a button on the Windows login screen. Keep a separate administrator account available to access it if the managed account cannot sign in.

## Windows login hints: important limitation

Windows does not provide a supported public password-hint setter. This build writes only the selected account's `UserPasswordHint` value as UTF-16 binary data under its existing SAM user key, from the SYSTEM worker. It does **not** change SAM permissions, password hashes, or other SAM account values. Microsoft does not support direct SAM modification. This feature is therefore **unverified on your Windows 11 build and may break after Windows updates**.

The worker checks access before normal rotations and reads the hint value back after writing it. This verifies the stored value, not the Windows sign-in UI. If a password succeeds but its hint fails, the app keeps the new password, pauses automatic changes, and provides recovery details. An emergency reset can still reset the password if hint preflight is unavailable; a subsequent hint failure is reported.

Choose **Password** under Windows sign-in options. Windows Hello PIN, fingerprint, and face sign-in are separate; this app does not change them. A hint normally appears after an incorrect password rather than permanently beneath every sign-in box. Test the displayed hint and actual password on your specific Windows build before depending on automatic changes.

Short passwords and solvable hints are intentional in this app: anyone who solves the clue can sign in. Windows password length, complexity, history, and minimum-age policy still apply. The app reports rejected changes and pauses; it never disables those policies. A PC requiring complex passwords may reject both numeric and simple-word choices, including `1234`.

## Interrupted changes and recovery

Before changing a password, the app durably stores the candidate password in its encrypted vault. After Windows accepts it, the app records that success before updating the hint. A failure or interruption pauses the affected account; there are no automatic repeated password attempts.

- If only the hint needs repair, **Recovery details** retries the saved hint without changing the password.
- If the change outcome is uncertain, **Recovery details** shows both the new candidate and the previous known password after another app-password check. Record the candidate as current only after verifying that it works in Windows.
- The panic button can resolve an uncertain change by explicitly setting the emergency password. If Windows rejects that reset, the earlier recovery candidate is retained.
- The app never treats an old backup as the current password or silently rolls back a password.

## Storage and background task

- Program: `%ProgramFiles%\Password Puzzle\PasswordPuzzle.exe`
- Encrypted vault: `%ProgramData%\PasswordPuzzle\vault.dat`
- Task Scheduler entry: `PasswordPuzzle-Rotation`

The entire vault is encrypted using Windows machine-scoped DPAPI. Its folder is restricted to SYSTEM and administrators. App passwords use salted PBKDF2-SHA256 with 210,000 iterations. Account passwords and hints are never written to plaintext logs or process command lines. The background task runs as SYSTEM from the protected installation directory, including when the user is logged out.

The app password is a UI lock, not protection against another Windows administrator. An administrator or SYSTEM can access the machine-scoped vault or change the software. Do not move the vault to another computer; DPAPI protection is machine-specific.

If the worker becomes overdue, click **Repair scheduler**. Check Windows Task Scheduler's last result if the worker still does not check in. Vault corruption or decryption failure stops work rather than creating a replacement vault. There is no app-password bypass or password recovery backdoor.

## Stop or remove the app

1. Pause every configured account and close the app.
2. In Task Scheduler, disable `PasswordPuzzle-Rotation`. Wait for a running worker to finish, then delete the task.
3. Remove `%ProgramFiles%\Password Puzzle` and its desktop / Start menu shortcuts using an administrator account.
4. Keep `%ProgramData%\PasswordPuzzle` if you want to retain your encrypted password records. Delete it only after recording the passwords you need and deciding to discard the app password and schedules.

Uninstalling does not restore earlier Windows passwords or hints. Pausing leaves the last password in effect.

## Build and verification

Version 1.4 adds the all-account saved-password viewer, retaining the themes, creator credit, and manual password changes. It also retains the blue password-lock/rotation icon with a math symbol in the main EXE and the orange reset icon in the reset EXE. No separate image or icon file needs to travel with either EXE. Open the new main EXE to update an older installed copy; close the old app first. Updating preserves your app password and account settings. Existing passwords and hints change only on the next requested or scheduled change (or use Update hint only to make a fresh math clue for the current numeric password).

Source is in `PasswordPuzzle/`. Build with the .NET SDK:

```text
dotnet build PasswordPuzzle/PasswordPuzzle.csproj -c Release
dotnet build PasswordPuzzle.Reset/PasswordPuzzle.Reset.csproj -c Release
dotnet run --project Tests/Tests.csproj -c Release
```

The Windows app targets .NET Framework 4.8. Tests target .NET 8 and exercise the production core with a substitute OS boundary; they do not change accounts. Test coverage includes generated clue answers, nonrepetition, app password verification, catch-up scheduling, hint-only edits, emergency pause, policy rejection, journal ordering, storage failure before mutation, interrupted-operation recovery, theme persistence, default theme fallback, and text-color contrast. Color checks are not a substitute for on-device visual or accessibility testing.

The release was cross-compiled on macOS. Compilation and core tests passed. Windows UI rendering, UAC, task registration, DPAPI/ACL behavior, native account operations, and visible Windows 11 hints **have not been executed or verified on a Windows machine here**. See `WINDOWS-VERIFICATION.md` for the remaining on-device checks. Do not interpret the passing core tests as Windows integration certification.

## Implementation references

- [Microsoft: NetUserChangePassword](https://learn.microsoft.com/en-us/windows/win32/api/lmaccess/nf-lmaccess-netuserchangepassword)
- [Microsoft: NetUserSetInfo](https://learn.microsoft.com/en-us/windows/win32/api/lmaccess/nf-lmaccess-netusersetinfo)
- [Microsoft: USER_INFO_24 and connected identities](https://learn.microsoft.com/en-us/windows/win32/api/lmaccess/ns-lmaccess-user_info_24)
- [Microsoft: Task Scheduler repetition](https://learn.microsoft.com/en-us/windows/win32/taskschd/repeating-a-task)
- [Microsoft: SAM modification is unsupported](https://learn.microsoft.com/en-us/troubleshoot/windows-server/windows-security/ntlm-user-authentication)
- [Original research documenting the password-hint value](https://claudijd.github.io/2012/08/22/all-your-password-hints-are-belong-to-us/)
