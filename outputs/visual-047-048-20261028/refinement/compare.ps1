param([string]$Round='round-1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='F:\Godot项目\项目a\outputs\visual-047-048-20261028'
$out=Join-Path $root "refinement\$Round"
$font=[Drawing.Font]::new('Segoe UI',20,[Drawing.FontStyle]::Bold)
function Pair([string]$Name,[string]$Reference,[string]$Implementation,[Drawing.Rectangle]$Crop) {
    $source=[Drawing.Image]::FromFile((Join-Path $root "boards\$Reference"));$actual=[Drawing.Image]::FromFile((Join-Path $out $Implementation))
    $normal=[Drawing.Bitmap]::new(1920,1080);$g=[Drawing.Graphics]::FromImage($normal);$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$g.DrawImage($source,0,0,1920,1080);$g.Dispose()
    $full=$Crop.Width -eq 1920
    $width=if($full){960}else{$Crop.Width};$height=if($full){540}else{$Crop.Height}
    $canvas=[Drawing.Bitmap]::new($width*2,$height+44);$g=[Drawing.Graphics]::FromImage($canvas);$g.Clear([Drawing.Color]::FromArgb(18,30,44));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawString('REFERENCE',$font,[Drawing.Brushes]::White,12,6);$g.DrawString("GODOT / $Round",$font,[Drawing.Brushes]::White,($width+12),6)
    $g.DrawImage($normal,[Drawing.Rectangle]::new(0,44,$width,$height),$Crop,[Drawing.GraphicsUnit]::Pixel);$g.DrawImage($actual,[Drawing.Rectangle]::new($width,44,$width,$height),$Crop,[Drawing.GraphicsUnit]::Pixel)
    $canvas.Save((Join-Path $out "$Name.png"),[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$canvas.Dispose();$normal.Dispose();$actual.Dispose();$source.Dispose()
}
try {
    Pair 'compare-reward' 'ui-reward-edge-frost-v3.png' '048-reward-glass.png' ([Drawing.Rectangle]::new(0,0,1920,1080))
    Pair 'compare-build' 'ui-build-glass-v2.png' '048-build-glass.png' ([Drawing.Rectangle]::new(0,0,1920,1080))
    Pair 'compare-codex' 'ui-codex-glass-v2.png' '048-codex-creature.png' ([Drawing.Rectangle]::new(0,0,1920,1080))
    Pair 'detail-card' 'ui-reward-edge-frost-v3.png' '048-reward-glass.png' ([Drawing.Rectangle]::new(236,224,472,532))
    Pair 'detail-footer' 'ui-reward-edge-frost-v3.png' '048-reward-glass.png' ([Drawing.Rectangle]::new(236,772,1448,270))
} finally {$font.Dispose()}
