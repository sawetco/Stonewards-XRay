$ErrorActionPreference = 'Stop'

function Find-Root {
    $candidates = @(
        $PSScriptRoot,
        (Split-Path -Parent $PSScriptRoot),
        'C:\Program Files (x86)\Steam\steamapps\common\Stonewards',
        'C:\Program Files\Steam\steamapps\common\Stonewards'
    )

    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath (Join-Path $c 'Stonewards.exe'))) {
            return ([System.IO.Path]::GetFullPath($c)).TrimEnd('\','/')
        }
    }

    throw 'Stonewards folder not found.'
}

try {
    $root = Find-Root
    $log = Join-Path $root 'BepInEx\LogOutput.log'
    if (-not (Test-Path -LiteralPath $log)) {
        throw 'BepInEx\LogOutput.log does not exist yet. Start the game once first.'
    }

    $desktop = [Environment]::GetFolderPath('Desktop')
    $dest = Join-Path $desktop 'Stonewards-XRay-Log.txt'
    Copy-Item -LiteralPath $log -Destination $dest -Force

    Write-Host 'Created:' -ForegroundColor Green
    Write-Host $dest -ForegroundColor Cyan
    Start-Process explorer.exe -ArgumentList "/select,`"$dest`""
}
catch {
    Write-Host 'ERROR:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
finally {
    Read-Host 'Press Enter to close'
}
