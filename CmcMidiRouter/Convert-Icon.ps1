Add-Type -AssemblyName System.Drawing
$pngPath = "g:\Meu Drive\X\GitHub - robsonbmartins\MIDI-Routing-USD-VID-PID\CmcMidiRouter\Icones\Icone.png"
$icoPath = "g:\Meu Drive\X\GitHub - robsonbmartins\MIDI-Routing-USD-VID-PID\CmcMidiRouter\app.ico"

$bmp = [System.Drawing.Bitmap]::FromFile($pngPath)
$icoStream = [System.IO.File]::Create($icoPath)

# Escrevemos o cabeçalho do arquivo ICO manualmente
$writer = New-Object System.IO.BinaryWriter $icoStream

$writer.Write([int16]0) # Reserved
$writer.Write([int16]1) # Type (1 = ICO)
$writer.Write([int16]1) # Count

$writer.Write([byte]0)   # Width (0 = 256)
$writer.Write([byte]0)   # Height (0 = 256)
$writer.Write([byte]0)   # Color count
$writer.Write([byte]0)   # Reserved
$writer.Write([int16]1)  # Color planes
$writer.Write([int16]32) # Bits per pixel

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$writer.Write([int]$pngBytes.Length) # Size of image data
$writer.Write([int]22)               # Offset of image data

$writer.Write($pngBytes)

$writer.Flush()
$writer.Close()
$icoStream.Close()
$bmp.Dispose()
$ms.Dispose()

Write-Host "Ícone gerado com sucesso em: $icoPath"
