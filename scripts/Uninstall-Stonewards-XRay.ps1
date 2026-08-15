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
    $plugin = Join-Path $root 'BepInEx\plugins\StonewardsXRay'

    if (Test-Path -LiteralPath $plugin) {
        Remove-Item -LiteralPath $plugin -Recurse -Force
        Write-Host 'Stonewards X-Ray plugin removed.' -ForegroundColor Green
    } else {
        Write-Host 'Stonewards X-Ray plugin is not installed.'
    }

    Write-Host 'BepInEx itself was left installed so other mods are not affected.'
}
catch {
    Write-Host 'ERROR:' -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
finally {
    Read-Host 'Press Enter to close'
}
