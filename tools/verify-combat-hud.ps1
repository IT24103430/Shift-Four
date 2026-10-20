param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$UnityPath = 'E:/Unity/6000.6.4f1/Editor/Unity.exe',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$ResultsName = 'CombatHud',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$verificationProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$sourceProject = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
if ($verificationProject -eq $sourceProject) { throw 'Use an isolated project copy, not the open working project.' }
$resultsPath = Join-Path $verificationProject ('Logs/' + $ResultsName)
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
function Invoke-UnityCheck([string[]]$CheckArguments, [string]$LogName) {
    $arguments = @('-batchmode', '-projectPath', ('"' + $verificationProject + '"'), '-logFile', ('"' + (Join-Path $resultsPath $LogName) + '"')) + $CheckArguments
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode): $LogName" }
}
# The supplied copy must already contain the current Assets, Packages and ProjectSettings.
if (-not $BuildOnly) {
    Invoke-UnityCheck @('-runTests', '-testPlatform', 'EditMode', '-testFilter', 'ShiftFour.Tests', '-testResults', ('"' + (Join-Path $resultsPath 'EditMode.xml') + '"')) 'editmode.log'
    $filter = 'CombatHudPlayModeTests|CombatAuditPlayModeTests|FourRoomIntegrationPlayModeTests|MainLabProximityPlayModeTests|WeaponArchitecturePlayModeTests|FiveSevenPlayablePlayModeTests|PlayerJumpPlayModeTests'
    Invoke-UnityCheck @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', ('"' + $filter + '"'), '-testResults', ('"' + (Join-Path $resultsPath 'PlayMode.xml') + '"')) 'playmode.log'
}
# ScreenCapture includes OnGUI in the framebuffer. These modules and the probe are QA-only.
$manifestPath = Join-Path $verificationProject 'Packages/manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($module in @('com.unity.modules.screencapture', 'com.unity.modules.imageconversion')) {
    $manifest.dependencies | Add-Member -MemberType NoteProperty -Name $module -Value '1.0.0' -Force
}
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 20))
Copy-Item -LiteralPath "$sourceProject/tools/verification/CombatHudBuildProbe.cs.txt" -Destination "$verificationProject/Assets/CombatHudBuildProbe.cs"
Invoke-UnityCheck @('-executeMethod', 'CombatHudVerification.BuildDesktop') 'build.log'
if ($ResultsName -ne 'CombatHud') {
    Copy-Item -LiteralPath "$verificationProject/Logs/CombatHud/build-result.txt" -Destination $resultsPath -Force
}
$executable = Join-Path $verificationProject 'Builds/CombatHud/ShiftFour.exe'
$captures = Join-Path $resultsPath 'standalone'
New-Item -ItemType Directory -Path $captures -Force | Out-Null
$process = Start-Process -FilePath $executable -ArgumentList @('-combatHudProbe', '-combatHudOutput', ('"' + $captures + '"'), '-logFile', ('"' + (Join-Path $resultsPath 'standalone.log') + '"'), '-screen-fullscreen', '0') -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Standalone probe exited with $($process.ExitCode)." }
Write-Output "Combat HUD verification completed: $resultsPath"
