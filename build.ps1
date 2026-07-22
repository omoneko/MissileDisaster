$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild が見つかりません" }

& $msbuild "src\MissileDisaster\MissileDisaster.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "ビルド失敗" }

$dll = "src\MissileDisaster\bin\Release\MissileDisaster.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\MissileDisaster"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force

# モデル資産(Models/*.obj,*.mtl)を配布する。実行時に MissileModelProvider が読み込む。
$modelsSrc = "src\MissileDisaster\Models"
if (Test-Path $modelsSrc) {
    $modelsDst = Join-Path $modDir "Models"
    New-Item -ItemType Directory -Force -Path $modelsDst | Out-Null
    Copy-Item (Join-Path $modelsSrc "*") $modelsDst -Include *.obj, *.mtl -Force
    Write-Host "モデル配置完了: $modelsDst"
}

# サウンド資産(Sounds/*.mp3)を配布する。実行時に SoundLibrary が読み込む。
$soundsSrc = "src\MissileDisaster\Sounds"
if (Test-Path $soundsSrc) {
    $soundsDst = Join-Path $modDir "Sounds"
    New-Item -ItemType Directory -Force -Path $soundsDst | Out-Null
    Copy-Item (Join-Path $soundsSrc "*") $soundsDst -Include *.wav -Force
    Write-Host "サウンド配置完了: $soundsDst"
}

# タブ内アイコン(災害パネル)。前処理済みの透過アイコン icon_tab.png(格子背景を除去・被写体に切り抜き・512px)を配置する。
# 無ければ原画 icon.png、それも無ければ手続き生成シルエットにフォールバックする。
$iconDst = Join-Path $modDir "icon.png"
if (Test-Path "icon_tab.png") {
    Copy-Item "icon_tab.png" $iconDst -Force
    Write-Host "icon_tab.png(透過タブアイコン)を配置しました"
} elseif (Test-Path "icon.png") {
    Copy-Item "icon.png" $iconDst -Force
    Write-Host "icon.png(原画)を配置しました(icon_tab.png なし)"
} else {
    Write-Host "警告: アイコンが見つかりません。タブアイコンは手続き生成シルエットになります。"
}

Write-Host "配置完了: $modDir"
