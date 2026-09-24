using System;
using System.Security;

namespace PasswordPuzzle
{
    public static class TaskDefinition
    {
        public static string Create(string executable, string folder, DateTime startUtc)
        {
            string start = startUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            return @"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.3"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>Applies the password schedules saved in Password Puzzle, including missed changes after startup.</Description></RegistrationInfo>
  <Triggers>
    <TimeTrigger><Enabled>true</Enabled><StartBoundary>" + start + @"</StartBoundary><Repetition><Interval>PT1M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition></TimeTrigger>
    <BootTrigger><Enabled>true</Enabled><Delay>PT30S</Delay></BootTrigger>
  </Triggers>
  <Principals><Principal id=""System""><UserId>S-1-5-18</UserId><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>false</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT5M</ExecutionTimeLimit><Priority>7</Priority></Settings>
  <Actions Context=""System""><Exec><Command>" + SecurityElement.Escape(executable) + @"</Command><Arguments>--worker</Arguments><WorkingDirectory>" + SecurityElement.Escape(folder) + @"</WorkingDirectory></Exec></Actions>
</Task>";
        }
    }
}
