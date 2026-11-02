$ErrorActionPreference = "Stop"

$fwDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpfDir = "$fwDir\WPF"
$csc = "$fwDir\csc.exe"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outExe = Join-Path $projectRoot "DeskBoxes.exe"
$oldExe = Join-Path $projectRoot "DeskBoxes.old.exe"

Get-Process -Name "DeskBoxes" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path $oldExe) {
    Remove-Item $oldExe -Force -ErrorAction SilentlyContinue
}
if (Test-Path $outExe) {
    Move-Item $outExe $oldExe -Force -ErrorAction SilentlyContinue
}

$refs = @(
    "/r:$wpfDir\PresentationCore.dll",
    "/r:$wpfDir\PresentationFramework.dll",
    "/r:$wpfDir\WindowsBase.dll",
    "/r:$fwDir\System.Xaml.dll",
    "/r:$fwDir\System.Drawing.dll",
    "/r:$fwDir\System.Windows.Forms.dll",
    "/r:$fwDir\System.Web.Extensions.dll"
)

$sources = @(
    Join-Path $projectRoot "src\Models.cs"
    Join-Path $projectRoot "src\NativeInterop.cs"
    Join-Path $projectRoot "src\BoxWindow.cs"
    Join-Path $projectRoot "src\ControlCenterWindow.cs"
    Join-Path $projectRoot "src\App.cs"
)

Write-Host "Compiling DeskBoxes.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ "/out:$outExe" @refs @sources

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully built: $outExe" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
