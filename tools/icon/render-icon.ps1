param([string]$Out)
Add-Type -AssemblyName System.Drawing
$N = 1024
$bmp = New-Object System.Drawing.Bitmap($N, $N, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

function RoundRect([float]$x,[float]$y,[float]$w,[float]$h,[float]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r*2
  $p.AddArc($x, $y, $d, $d, 180, 90)
  $p.AddArc($x+$w-$d, $y, $d, $d, 270, 90)
  $p.AddArc($x+$w-$d, $y+$h-$d, $d, $d, 0, 90)
  $p.AddArc($x, $y+$h-$d, $d, $d, 90, 90)
  $p.CloseFigure(); return $p
}

# 배경: 둥근 사각형, 남색→보라 그라디언트
$bgPath = RoundRect 0 0 $N $N ($N*0.22)
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
  (New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point($N,$N)),
  [System.Drawing.Color]::FromArgb(255, 37, 99, 235), [System.Drawing.Color]::FromArgb(255, 124, 58, 237))
$g.FillPath($bgBrush, $bgPath)

# 왼쪽: 사이드바 타일 3개 (모아 놓은 서비스)
$tileBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 255, 255, 255))
$tw = $N*0.17; $th = $N*0.17; $tx = $N*0.19; $gap = $N*0.065
$totalH = 3*$th + 2*$gap; $ty0 = ($N - $totalH)/2
for ($i=0; $i -lt 3; $i++) {
  $ty = $ty0 + $i*($th+$gap)
  $g.FillPath($tileBrush, (RoundRect $tx $ty $tw $th ($tw*0.25)))
}

# 오른쪽: 재생 삼각형 (둥근 모서리)
$cx = $N*0.63; $cy = $N*0.5; $h = $N*0.46
$pts = @(
  (New-Object System.Drawing.PointF(($cx - $h*0.36), ($cy - $h*0.5))),
  (New-Object System.Drawing.PointF(($cx + $h*0.50), $cy)),
  (New-Object System.Drawing.PointF(($cx - $h*0.36), ($cy + $h*0.5)))
)
$tri = New-Object System.Drawing.Drawing2D.GraphicsPath
$tri.AddPolygon($pts)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, ($N*0.08))
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawPath($pen, $tri)
$g.FillPath([System.Drawing.Brushes]::White, $tri)
$g.Dispose()

$sizes = 256,128,64,48,32,24,16
foreach ($s in $sizes) {
  $small = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $sg = [System.Drawing.Graphics]::FromImage($small)
  $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $sg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $sg.Clear([System.Drawing.Color]::Transparent)
  $sg.DrawImage($bmp, 0, 0, $s, $s)
  $sg.Dispose()
  $small.Save("$Out\icon_$s.png", [System.Drawing.Imaging.ImageFormat]::Png)
  # BGRA raw (ICO의 BMP 항목용)
  $rect = New-Object System.Drawing.Rectangle(0,0,$s,$s)
  $data = $small.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object byte[] ($data.Stride * $s)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $small.UnlockBits($data)
  [System.IO.File]::WriteAllBytes("$Out\icon_$s.raw", $bytes)
  $small.Dispose()
}
$bmp.Save("$Out\icon_1024.png", [System.Drawing.Imaging.ImageFormat]::Png)
"rendered"
