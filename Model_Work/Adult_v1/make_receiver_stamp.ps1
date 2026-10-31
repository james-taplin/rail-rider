Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(2048,768)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::Black)
$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$f=[Drawing.Font]::new('Arial',58,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$lines=@('JT DAD.  GUNSMITHS.  SPRINGER HOUSE E.17','CAL: .177 / 4.5MM SOLID STEEL BB','FOR AGES 8+ WITH ADULT SUPERVISION','THIS IS A WEAPON - EXCERCISE CAUTION !')
foreach($i in 0..3){$g.DrawString($lines[$i],$f,[Drawing.Brushes]::White,350,(218+80*$i))}
$bmp.Save((Join-Path $PSScriptRoot 'receiver_stamp_mask.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$f.Dispose()
$bmp.Dispose()
