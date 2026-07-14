using System.Security;

namespace OrderBuddy.ScreenManager.Core.Lifecycle;

public static class StartupTaskXmlBuilder
{
    public static string Build(
        string taskName,
        string description,
        string author,
        string userId,
        string command,
      string arguments,
      bool useHighestPrivileges = true)
    {
        static string Esc(string value) => SecurityElement.Escape(value) ?? string.Empty;

        return $"""
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Date>{DateTime.UtcNow:O}</Date>
    <Author>{Esc(author)}</Author>
    <Description>{Esc(description)}</Description>
    <URI>\\{Esc(taskName)}</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{Esc(userId)}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>{Esc(userId)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>{(useHighestPrivileges ? "HighestAvailable" : "LeastPrivilege")}</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>3</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{Esc(command)}</Command>
      <Arguments>{Esc(arguments)}</Arguments>
    </Exec>
  </Actions>
</Task>
""";
    }
}
