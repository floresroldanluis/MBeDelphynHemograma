param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$source=[Drawing.Image]::FromFile((Join-Path $root 'assets\edelphyn-logo.png'))
function Resize-Logo([int]$width,[int]$height) {
    $bitmap=[Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($source,[Drawing.Rectangle]::new(0,0,$width,$height))
    } finally {$g.Dispose()}
    return $bitmap
}
try {
    # Multi-resolution PNG frames inside the Windows ICO container.
    $sizes=@(16,20,24,32,40,48,64,128,256)
    $frames=@()
    foreach($size in $sizes) {
        $bitmap=Resize-Logo $size $size
        $stream=[IO.MemoryStream]::new()
        try { $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png); $frames+=,@($stream.ToArray()) } finally {$bitmap.Dispose();$stream.Dispose()}
    }
    $icoStream=[IO.File]::Create((Join-Path $root 'assets\edelphyn.ico'))
    $writer=[IO.BinaryWriter]::new($icoStream)
    try {
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
        $offset=6+16*$sizes.Count
        for($i=0;$i -lt $sizes.Count;$i++) {
            $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
            $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
            $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset)
            $offset+=$frames[$i].Length
        }
        foreach($frame in $frames){$writer.Write([byte[]]$frame)}
    } finally {$writer.Dispose()}
    foreach($small in @($false,$true)) {
        $width=if($small){55}else{164};$height=if($small){55}else{314}
        $bitmap=[Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g=[Drawing.Graphics]::FromImage($bitmap)
        try {
            $g.Clear($(if($small){[Drawing.Color]::White}else{[Drawing.Color]::FromArgb(234,240,255)}))
            $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $bounds=if($small){[Drawing.Rectangle]::new(0,0,55,55)}else{[Drawing.Rectangle]::new(8,66,148,148)}
            $g.DrawImage($source,$bounds)
            $name=if($small){'wizard-small.bmp'}else{'wizard.bmp'}
            $bitmap.Save((Join-Path $root ('assets\'+$name)),[Drawing.Imaging.ImageFormat]::Bmp)
        } finally {$g.Dispose();$bitmap.Dispose()}
    }
} finally {$source.Dispose()}
