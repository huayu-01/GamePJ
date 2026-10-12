# 生成「能力卡」界面的 4 条音效（16-bit PCM 单声道 22050Hz WAV，Godot 原生支持）。
#
# 为什么是脚本而不是直接入库二进制：
#   这 4 条是**项目自制**（不是 Kenney 那批 CC0 素材），入库二进制没法 review、也没法复现。
#   把生成脚本一起入库，改音色 = 改下面的频率/时长再跑一次。
#
# 用法（在仓库任意位置）：
#   pwsh -NoProfile -File F:\Godot项目\项目a\_tools\make_ui_card_sfx.ps1
#
# 产物：Assets/Audio/ui/ui_card_{unlock,upgrade,fail,reset}.wav
# 之后要做的：`Godot --headless --path <项目> --import`（生成 .import），音效表行已经接好（见 docs/ability-cards.md）。
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'Assets\Audio\ui'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sampleRate = 22050

function New-Wav {
    param(
        [string]$Path,
        [double]$Seconds,
        [object[]]$Segments   # @{ Freq=Hz; Start=秒; Dur=秒; Gain=0..1; Harm=谐波比例; Bend=尾部降调比例 }
    )
    $count = [int]($Seconds * $sampleRate)
    $samples = New-Object 'double[]' $count
    foreach ($seg in $Segments) {
        $start = [int]($seg.Start * $sampleRate)
        $len = [int]($seg.Dur * $sampleRate)
        $harm = if ($seg.ContainsKey('Harm')) { $seg.Harm } else { 0.0 }
        $bend = if ($seg.ContainsKey('Bend')) { $seg.Bend } else { 0.0 }
        for ($i = 0; $i -lt $len; $i++) {
            $idx = $start + $i
            if ($idx -ge $count) { break }
            $t = $i / $sampleRate
            $u = $i / [double]$len
            $freq = $seg.Freq * (1.0 + $bend * $u)
            # 4ms 起音 + 指数衰减：两个包络都是为了避免爆音（点一下听不出"咔"）。
            $env = [Math]::Min(1.0, ($i / [double]($sampleRate * 0.004)))
            $env *= [Math]::Exp(-4.2 * $u)
            $value = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
            if ($harm -gt 0) { $value += $harm * [Math]::Sin(4.0 * [Math]::PI * $freq * $t) }
            $samples[$idx] += $value * $env * $seg.Gain
        }
    }

    $bytes = New-Object 'byte[]' ($count * 2)
    for ($i = 0; $i -lt $count; $i++) {
        $s = [int][Math]::Round([Math]::Tanh($samples[$i]) * 30000)   # 软削波，叠加后也不爆
        if ($s -gt 32767) { $s = 32767 }
        if ($s -lt -32768) { $s = -32768 }
        $bytes[($i * 2)] = [byte]($s -band 0xFF)
        $bytes[($i * 2) + 1] = [byte](($s -shr 8) -band 0xFF)
    }

    $stream = [System.IO.File]::Create($Path)
    $w = New-Object System.IO.BinaryWriter $stream
    $w.Write([char[]]'RIFF')
    $w.Write([int](36 + $bytes.Length))
    $w.Write([char[]]'WAVE')
    $w.Write([char[]]'fmt ')
    $w.Write([int]16)
    $w.Write([int16]1)          # PCM
    $w.Write([int16]1)          # 单声道
    $w.Write([int]$sampleRate)
    $w.Write([int]($sampleRate * 2))
    $w.Write([int16]2)          # block align
    $w.Write([int16]16)         # bits
    $w.Write([char[]]'data')
    $w.Write([int]$bytes.Length)
    $w.Write($bytes)
    $w.Flush()
    $w.Close()
    Write-Output ("{0}  {1} bytes  {2:0.###}s" -f $Path, (36 + $bytes.Length), $Seconds)
}

New-Wav -Path (Join-Path $outDir 'ui_card_unlock.wav') -Seconds 0.32 -Segments @(
    @{ Freq = 523.25; Start = 0.00; Dur = 0.16; Gain = 0.55; Harm = 0.18 },
    @{ Freq = 783.99; Start = 0.07; Dur = 0.16; Gain = 0.50; Harm = 0.16 },
    @{ Freq = 1046.5; Start = 0.14; Dur = 0.18; Gain = 0.45; Harm = 0.12 }
)

New-Wav -Path (Join-Path $outDir 'ui_card_upgrade.wav') -Seconds 0.26 -Segments @(
    @{ Freq = 880.0;  Start = 0.00; Dur = 0.10; Gain = 0.50; Harm = 0.20 },
    @{ Freq = 1318.5; Start = 0.06; Dur = 0.20; Gain = 0.45; Harm = 0.14 }
)

New-Wav -Path (Join-Path $outDir 'ui_card_fail.wav') -Seconds 0.24 -Segments @(
    @{ Freq = 165.0; Start = 0.00; Dur = 0.24; Gain = 0.60; Harm = 0.45; Bend = -0.25 }
)

New-Wav -Path (Join-Path $outDir 'ui_card_reset.wav') -Seconds 0.34 -Segments @(
    @{ Freq = 1046.5; Start = 0.00; Dur = 0.10; Gain = 0.45; Harm = 0.14 },
    @{ Freq = 659.25; Start = 0.08; Dur = 0.10; Gain = 0.45; Harm = 0.16 },
    @{ Freq = 440.0;  Start = 0.16; Dur = 0.18; Gain = 0.42; Harm = 0.18 }
)
