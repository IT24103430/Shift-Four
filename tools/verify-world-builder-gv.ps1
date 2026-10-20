param(
    [string]$ProjectPath = "$PSScriptRoot/../Temp/WorldBuilderVerification",
    [string]$UnityPath = 'E:/Unity/6000.6.4f1/Editor/Unity.exe',
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
$verificationProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$sourceProject = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
if ($verificationProject -eq $sourceProject) { throw 'Use an isolated project copy, not the open working project.' }
$resultsPath = Join-Path $verificationProject 'Logs/WorldBuilderGV'
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
function Invoke-UnityCheck([string[]]$CheckArguments, [string]$LogName) {
    $arguments = @('-batchmode', '-projectPath', ('"' + $verificationProject + '"'), '-logFile', ('"' + (Join-Path $resultsPath $LogName) + '"')) + $CheckArguments
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode): $LogName" }
}
if (-not $BuildOnly) {
Invoke-UnityCheck @('-runTests', '-testPlatform', 'EditMode', '-testFilter', 'ShiftFour.Tests', '-testResults', ('"' + (Join-Path $resultsPath 'EditMode.xml') + '"')) 'editmode.log'
Write-Output 'Edit Mode completed.'
$filter = 'FourRoomIntegrationPlayModeTests|MainLabProximityPlayModeTests|MainLabHeightPlayModeTests|Student4UnifiedPlayModeTests|WeaponArchitecturePlayModeTests|FiveSevenPlayablePlayModeTests|PlayerJumpPlayModeTests|ControlPredictionMainLabPlayModeTests'
Invoke-UnityCheck @('-runTests', '-testPlatform', 'PlayMode', '-testFilter', ('"' + $filter + '"'), '-testResults', ('"' + (Join-Path $resultsPath 'PlayMode.xml') + '"')) 'playmode.log'
Write-Output 'Environment, navigation, mission, player and weapon Play Mode regressions completed.'
}
# The standalone-only probe is deliberately absent from production Assets.
Copy-Item -LiteralPath "$sourceProject/tools/verification/WorldBuilderBuildProbe.cs.txt" -Destination "$verificationProject/Assets/WorldBuilderBuildProbe.cs"
Invoke-UnityCheck @('-executeMethod', 'WorldBuilderEnvironmentBuilder.BuildDesktop') 'build.log'
Write-Output 'Windows development build completed.'
