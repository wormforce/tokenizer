param(
    [switch]$SkipRestore,
    [switch]$SkipTests,
    [switch]$Launch,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$platform = "x64"

if ($Launch) {
    Get-Process Tokenizer.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    $fallbackPaths = @(
        'C:\Program Files\dotnet\dotnet.exe',
        'C:\Program Files (x86)\dotnet\dotnet.exe'
    )

    $fallback = $fallbackPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($fallback) {
        $dotnet = [pscustomobject]@{ Source = $fallback }
    }
}

if (-not $dotnet) {
    Write-Error ".NET SDK was not found in PATH or standard install locations. Install the system-level .NET 8 SDK and WinUI/Windows SDK workloads before running this script."
}

if (-not $SkipRestore) {
    & $dotnet.Source restore "$root\\Tokenizer.sln" -p:Platform=$platform
}

& $dotnet.Source build "$root\\Tokenizer.sln" -c $Configuration -p:Platform=$platform

if (-not $SkipTests) {
    & $dotnet.Source test "$root\\Tokenizer.Tests\\Tokenizer.Tests.csproj" -c $Configuration --no-build -p:Platform=$platform
}

if ($Launch) {
    $exe = Join-Path $root 'Tokenizer.App\\bin\\x64\\Debug\\net8.0-windows10.0.19041.0\\win-x64\\Tokenizer.App.exe'
    if (-not (Test-Path $exe)) {
        Write-Error "App executable was not found at $exe"
    }

    $process = Start-Process $exe -PassThru
    Start-Sleep -Seconds 2

    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class RunPsUser32 {
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
}
'@

    $script:targetProcId = [uint32]$process.Id
    $callback = [RunPsUser32+EnumWindowsProc]{
        param($hWnd, $lParam)

        $procId = [uint32]0
        [void][RunPsUser32]::GetWindowThreadProcessId($hWnd, [ref]$procId)
        if ($procId -eq $script:targetProcId) {
            $className = New-Object System.Text.StringBuilder 256
            [void][RunPsUser32]::GetClassName($hWnd, $className, $className.Capacity)
            if ($className.ToString() -ne 'WinUIDesktopWin32WindowClass') {
                return $true
            }

            [RunPsUser32]::ShowWindow($hWnd, 5) | Out-Null
            [RunPsUser32]::SetForegroundWindow($hWnd) | Out-Null
        }

        return $true
    }

    [RunPsUser32]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    Write-Host "Launched Tokenizer.App (PID: $($process.Id))"
}

