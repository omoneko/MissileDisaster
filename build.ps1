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

# タブ内アイコン(災害パネル)。配置時に icon.png を <=256px へ縮小する(元画像は保持・小容量・省メモリ)。
# 縮小に失敗した場合は原寸コピーにフォールバックする。
$iconSrc = "icon.png"
$iconDst = Join-Path $modDir "icon.png"
if (Test-Path $iconSrc) {
    $maxIcon = 256
    try {
        Add-Type -AssemblyName System.Drawing
        $img = [System.Drawing.Image]::FromFile((Get-Item $iconSrc).FullName)
        $scale = [Math]::Min($maxIcon / $img.Width, $maxIcon / $img.Height)
        if ($scale -gt 1) { $scale = 1 }
        $nw = [int][Math]::Max(1, [Math]::Round($img.Width * $scale))
        $nh = [int][Math]::Max(1, [Math]::Round($img.Height * $scale))
        $bmp = New-Object System.Drawing.Bitmap $nw, $nh
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($img, 0, 0, $nw, $nh)
        $g.Dispose(); $img.Dispose()
        $bmp.Save($iconDst, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "icon.png(タブアイコン)を ${nw}x${nh} に縮小して配置しました"
    } catch {
        Copy-Item $iconSrc $iconDst -Force
        Write-Host "icon.png 縮小に失敗したため原寸で配置しました: $_"
    }
} else {
    Write-Host "警告: $iconSrc が見つかりません。タブアイコンは手続き生成シルエットになります。"
}

Write-Host "配置完了: $modDir"
