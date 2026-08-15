param(
    [string]$GameRoot = ''
)

$ErrorActionPreference = 'Stop'

$BepInExVersion = '5.4.23.5'
$BepInExUrl = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip'
$BepInExSha256 = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'

function Resolve-StonewardsRoot([string]$RequestedRoot) {
    $candidates = @()

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        $candidates += $RequestedRoot
    }

    if ($PSScriptRoot) {
        $candidates += $PSScriptRoot
        $parent = Split-Path -Parent $PSScriptRoot
        if ($parent) { $candidates += $parent }
    }

    $candidates += (Get-Location).Path
    $candidates += 'C:\Program Files (x86)\Steam\steamapps\common\Stonewards'
    $candidates += 'C:\Program Files\Steam\steamapps\common\Stonewards'

    foreach ($candidate in $candidates | Select-Object -Unique) {
        try {
            $clean = ([string]$candidate).Trim().Trim('"')
            if ([string]::IsNullOrWhiteSpace($clean)) { continue }

            $full = [System.IO.Path]::GetFullPath($clean).TrimEnd('\','/')

            if ((Test-Path -LiteralPath (Join-Path $full 'Stonewards.exe')) -and
                (Test-Path -LiteralPath (Join-Path $full 'Stonewards_Data\Managed\Assembly-CSharp.dll'))) {
                return $full
            }
        } catch {}
    }

    throw 'Stonewards installation folder could not be detected. Put this package next to Stonewards.exe.'
}

function Ensure-BepInEx([string]$Root) {
    $bepDll = Join-Path $Root 'BepInEx\core\BepInEx.dll'

    if (Test-Path -LiteralPath $bepDll) {
        Write-Host 'BepInEx already installed.' -ForegroundColor DarkGray
        return
    }

    Write-Host "Downloading official BepInEx $BepInExVersion x64..." -ForegroundColor Cyan

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $tempZip = Join-Path $env:TEMP "BepInEx_win_x64_$BepInExVersion.zip"
    if (Test-Path -LiteralPath $tempZip) {
        Remove-Item -LiteralPath $tempZip -Force
    }

    Invoke-WebRequest -UseBasicParsing -Uri $BepInExUrl -OutFile $tempZip

    $actualHash = (Get-FileHash -LiteralPath $tempZip -Algorithm SHA256).Hash.ToUpperInvariant()

    if ($actualHash -ne $BepInExSha256) {
        Remove-Item -LiteralPath $tempZip -Force -ErrorAction SilentlyContinue
        throw "BepInEx SHA-256 mismatch. Expected $BepInExSha256 but got $actualHash"
    }

    Expand-Archive -LiteralPath $tempZip -DestinationPath $Root -Force
    Remove-Item -LiteralPath $tempZip -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path -LiteralPath $bepDll)) {
        throw 'BepInEx extraction finished, but BepInEx.dll is missing.'
    }

    Write-Host 'BepInEx installed and hash verified.' -ForegroundColor Green
}

function Find-CSharpCompiler {
    $frameworkDirs = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319"
    )

    foreach ($dir in $frameworkDirs) {
        $candidate = Join-Path $dir 'csc.exe'
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw 'Microsoft .NET Framework csc.exe was not found.'
}

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required reference is missing: $Path"
    }
}

function Invoke-Csc(
    [string]$Compiler,
    [string]$Source,
    [string]$Output,
    [string[]]$References,
    [bool]$NoStdLib,
    [bool]$NoConfig,
    [string]$LogPath
) {
    $args = New-Object System.Collections.Generic.List[string]
    $args.Add('/nologo')
    $args.Add('/target:library')
    $args.Add('/optimize+')
    $args.Add('/warn:4')

    if ($NoConfig) {
        $args.Add('/noconfig')
    }

    if ($NoStdLib) {
        $args.Add('/nostdlib+')
    }

    $args.Add('/out:"' + $Output + '"')

    foreach ($ref in $References) {
        $args.Add('/reference:"' + $ref + '"')
    }

    $args.Add('"' + $Source + '"')

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Compiler
    $psi.Arguments = ($args -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()

    $process.WaitForExit()

    @(
        "Compiler: $Compiler",
        "ExitCode: $($process.ExitCode)",
        "",
        "STDOUT:",
        $stdout,
        "",
        "STDERR:",
        $stderr
    ) | Set-Content -LiteralPath $LogPath -Encoding UTF8

    if ($stdout) { Write-Host $stdout }
    if ($stderr) { Write-Host $stderr -ForegroundColor Yellow }

    return $process.ExitCode
}

function Compile-XRay([string]$Root) {
    $managed = Join-Path $Root 'Stonewards_Data\Managed'
    $bepCore = Join-Path $Root 'BepInEx\core'
    $pluginDir = Join-Path $Root 'BepInEx\plugins\StonewardsXRay'
    $sourceCandidates = @(
        (Join-Path $PSScriptRoot 'StonewardsXRayPlugin.cs'),
        (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\StonewardsXRayPlugin.cs')
    )

    $source = $sourceCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $source) {
        throw 'StonewardsXRayPlugin.cs was not found.'
    }

    $output = Join-Path $pluginDir 'StonewardsXRay.dll'
    $compileLog = Join-Path $pluginDir 'compile.log'

    New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Force
    }

    $csc = Find-CSharpCompiler

    # Primary reference set.
    $refsAttempt1 = @(
        (Join-Path $managed 'netstandard.dll'),
        (Join-Path $managed 'System.Runtime.dll'),
        (Join-Path $bepCore 'BepInEx.dll'),
        (Join-Path $managed 'UnityEngine.CoreModule.dll'),
        (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
        (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
        (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'),
        (Join-Path $managed 'UnityEngine.dll'),
        (Join-Path $managed 'UnityEngine.TerrainModule.dll')
    )

    foreach ($ref in $refsAttempt1) { Require-File $ref }

    Write-Host ''
    Write-Host 'Compiling StonewardsXRay.dll...' -ForegroundColor Cyan
    Write-Host 'Compiler pass 1: Framework stdlib + Unity netstandard 2.1 facade' -ForegroundColor DarkGray

    $exit1 = Invoke-Csc `
        -Compiler $csc `
        -Source $source `
        -Output $output `
        -References $refsAttempt1 `
        -NoStdLib $false `
        -NoConfig $false `
        -LogPath $compileLog

    if ($exit1 -eq 0 -and (Test-Path -LiteralPath $output)) {
        $hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
        Write-Host 'Plugin compiled successfully.' -ForegroundColor Green
        Write-Host "Plugin: $output"
        Write-Host "SHA-256: $hash"
        return
    }

    Write-Host ''
    Write-Host 'Compiler pass 1 failed. Retrying against Stonewards/Unity managed framework...' -ForegroundColor Yellow

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Force
    }

    # Fallback reference set using the game runtime assemblies.
    $refsAttempt2 = @(
        (Join-Path $managed 'mscorlib.dll'),
        (Join-Path $managed 'netstandard.dll'),
        (Join-Path $managed 'System.dll'),
        (Join-Path $managed 'System.Core.dll'),
        (Join-Path $managed 'System.Runtime.dll'),
        (Join-Path $bepCore 'BepInEx.dll'),
        (Join-Path $managed 'UnityEngine.CoreModule.dll'),
        (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
        (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
        (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'),
        (Join-Path $managed 'UnityEngine.dll'),
        (Join-Path $managed 'UnityEngine.TerrainModule.dll')
    )

    foreach ($ref in $refsAttempt2) { Require-File $ref }

    $exit2 = Invoke-Csc `
        -Compiler $csc `
        -Source $source `
        -Output $output `
        -References $refsAttempt2 `
        -NoStdLib $true `
        -NoConfig $true `
        -LogPath $compileLog

    if ($exit2 -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw "C# compilation failed. A diagnostic log was saved to: $compileLog"
    }

    $hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash

    Write-Host 'Plugin compiled successfully.' -ForegroundColor Green
    Write-Host "Plugin: $output"
    Write-Host "SHA-256: $hash"
}

try {
    $root = Resolve-StonewardsRoot $GameRoot

    Write-Host ''
    Write-Host 'Stonewards X-Ray by sawet - installer 1.0.0' -ForegroundColor Green
    Write-Host "Game: $root"
    Write-Host ''

    Ensure-BepInEx $root
    Compile-XRay $root

    Write-Host ''
    Write-Host 'INSTALLATION COMPLETE' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Start Stonewards normally from Steam.'
    Write-Host 'F5  Activate / deactivate mod'
    Write-Host 'X   X-Ray ON/OFF'
    Write-Host 'F6  Target mode'
    Write-Host 'F7  Name tags ON/OFF'
    Write-Host 'F8  Opacity'
    Write-Host 'F9  Distance'
    Write-Host 'F10 Overlay ON/OFF'
    Write-Host ''
}
catch {
    Write-Host ''
    Write-Host 'ERROR:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ''
}
finally {
    Read-Host 'Press Enter to close'
}
