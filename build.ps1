$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found" }

& $msbuild "src\MissileDisaster\MissileDisaster.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = "src\MissileDisaster\bin\Release\MissileDisaster.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\MissileDisaster"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force

# Deploy the model assets (Models/*.obj and *.mtl), which MissileModelProvider loads at runtime.
$modelsSrc = "src\MissileDisaster\Models"
if (Test-Path $modelsSrc) {
    $modelsDst = Join-Path $modDir "Models"
    New-Item -ItemType Directory -Force -Path $modelsDst | Out-Null
    Copy-Item (Join-Path $modelsSrc "*") $modelsDst -Include *.obj, *.mtl, *.png -Force
    Write-Host "Deployed models: $modelsDst"
}

# Deploy the sound assets, which SoundLibrary loads at runtime.
$soundsSrc = "src\MissileDisaster\Sounds"
if (Test-Path $soundsSrc) {
    $soundsDst = Join-Path $modDir "Sounds"
    New-Item -ItemType Directory -Force -Path $soundsDst | Out-Null
    Copy-Item (Join-Path $soundsSrc "*") $soundsDst -Include *.wav -Force
    Write-Host "Deployed sounds: $soundsDst"
}

# Icon for the disasters panel. Deploys the pre-processed transparent icon_tab.png, which has
# had its checkerboard background keyed out, been cropped to the subject and sized to 512px.
# Falls back to the raw icon.png, and then to the procedural silhouette.
$iconDst = Join-Path $modDir "icon.png"
if (Test-Path "icon_tab.png") {
    Copy-Item "icon_tab.png" $iconDst -Force
    Write-Host "Deployed icon_tab.png (transparent panel icon)"
} elseif (Test-Path "icon.png") {
    Copy-Item "icon.png" $iconDst -Force
    Write-Host "Deployed icon.png (raw; no processed icon_tab.png found)"
} else {
    Write-Host "Note: no icon found; the panel icon falls back to the procedural silhouette."
}

# LocaleLoader reads Locales\<lang>.txt at runtime. en.txt is regenerated automatically when
# missing, but shipping it keeps the Workshop copy in step with the repo.
$localesSrc = "Locales"
if (Test-Path $localesSrc) {
    $localesDst = Join-Path $modDir "Locales"
    New-Item -ItemType Directory -Force -Path $localesDst | Out-Null
    Copy-Item (Join-Path $localesSrc "*") $localesDst -Include *.txt -Force
    Write-Host "Deployed locales"
}

Write-Host "Deploy complete: $modDir"
