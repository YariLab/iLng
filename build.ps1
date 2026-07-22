#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$srcDir = Join-Path $root "src"
$assetsDir = Join-Path $root "assets"
$binDir = Join-Path $root "bin"
if (-not (Test-Path (Join-Path $srcDir "Program.cs"))) {
  throw "Sources not found in $srcDir"
}
New-Item -ItemType Directory -Force -Path $binDir | Out-Null
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$fw = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $fw "csc.exe"
if (-not (Test-Path $csc)) {
  throw "csc.exe not found: $csc"
}

function Find-GacAssembly([string]$name) {
  foreach ($gac in @(
    (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_MSIL\$name"),
    (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_64\$name"),
    (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_32\$name")
  )) {
    if (Test-Path $gac) {
      $dll = Get-ChildItem $gac -Recurse -Filter "$name.dll" | Select-Object -First 1
      if ($dll) { return $dll.FullName }
    }
  }
  throw "GAC assembly not found: $name"
}

function New-ILngIcon([string]$icoPath, [string]$assetsDir) {
  Add-Type -AssemblyName System.Drawing
  $typeName = "ILngIconBuilder_" + [guid]::NewGuid().ToString("N")
  $code = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class $typeName
{
    public static void WritePng(string path, int size)
    {
        using (Bitmap bmp = Render(size, false))
        {
            bmp.Save(path, ImageFormat.Png);
        }
    }

    // Build .ico from master PNG: BMP for <=256 (csc-friendly), PNG for 512/1024.
    public static void WriteIcoFromPng(string pngPath, string icoPath, int[] sizes)
    {
        using (var master = (Bitmap)Image.FromFile(pngPath))
        {
            var images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap scaled = ResizeBitmap(master, sizes[i]))
                {
                    if (sizes[i] > 256)
                    {
                        images[i] = EncodePngBytes(scaled);
                    }
                    else
                    {
                        images[i] = EncodeBmpIcon(scaled);
                    }
                }
            }

            using (var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)images.Length);

                int offset = 6 + 16 * images.Length;
                for (int i = 0; i < images.Length; i++)
                {
                    int size = sizes[i];
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write(images[i].Length);
                    bw.Write(offset);
                    offset += images[i].Length;
                }

                for (int i = 0; i < images.Length; i++)
                {
                    bw.Write(images[i]);
                }
            }
        }
    }

    private static byte[] EncodePngBytes(Bitmap bmp)
    {
        using (var ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    private static Bitmap ResizeBitmap(Bitmap source, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, size, size));
        }
        return bmp;
    }

    private static byte[] EncodeBmpIcon(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        int xorStride = w * 4;
        int andStride = ((w + 31) / 32) * 4;
        byte[] xor = new byte[xorStride * h];
        byte[] and = new byte[andStride * h];

        for (int y = 0; y < h; y++)
        {
            int destY = h - 1 - y;
            for (int x = 0; x < w; x++)
            {
                Color c = bmp.GetPixel(x, y);
                int i = destY * xorStride + x * 4;
                xor[i] = c.B;
                xor[i + 1] = c.G;
                xor[i + 2] = c.R;
                xor[i + 3] = c.A;
                if (c.A < 128)
                {
                    int maskIndex = destY * andStride + (x / 8);
                    and[maskIndex] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40);
            bw.Write(w);
            bw.Write(h * 2);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write(0);
            bw.Write(xor.Length);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(xor);
            bw.Write(and);
            return ms.ToArray();
        }
    }

    private static Bitmap Render(int size, bool supersample)
    {
        int drawSize = size;
        if (supersample && size < 512) drawSize = size * 4;
        else if (!supersample && size >= 512) drawSize = size * 2;

        using (Bitmap hi = DrawMark(drawSize, size))
        {
            if (drawSize == size) return (Bitmap)hi.Clone();

            var lo = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(lo))
            {
                g.Clear(Color.Transparent);
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(hi, new Rectangle(0, 0, size, size));
            }
            return lo;
        }
    }

    private static Bitmap DrawMark(int size, int logicalSize)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            float pad = size * 0.08f;
            float radius = size * 0.22f;
            var card = new RectangleF(pad, pad, size - pad * 2f, size - pad * 2f);

            using (var path = Rounded(card, radius))
            {
                if (logicalSize >= 128)
                {
                    using (var shadowPath = Rounded(
                        new RectangleF(card.X, card.Y + size * 0.018f, card.Width, card.Height), radius))
                    using (var sb = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
                    {
                        g.FillPath(sb, shadowPath);
                    }
                }

                using (var fill = new LinearGradientBrush(
                    card, Color.FromArgb(255, 30, 52, 84), Color.FromArgb(255, 16, 26, 44), 90f))
                {
                    g.FillPath(fill, path);
                }

                var highlight = new RectangleF(
                    card.X + card.Width * 0.08f,
                    card.Y + card.Height * 0.08f,
                    card.Width * 0.84f,
                    card.Height * 0.40f);
                using (var hiPath = Rounded(highlight, radius * 0.55f))
                using (var hiBrush = new LinearGradientBrush(
                    highlight, Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    g.FillPath(hiBrush, hiPath);
                }

                using (var border = new Pen(Color.FromArgb(210, 145, 195, 255), Math.Max(1.5f, size * 0.018f)))
                {
                    g.DrawPath(border, path);
                }
            }

            float fontPx = logicalSize <= 32 ? size * 0.36f : size * 0.30f;
            string family = "Segoe UI";
            try { using (var ff = new FontFamily("Segoe UI Semibold")) { family = "Segoe UI Semibold"; } }
            catch { family = "Segoe UI"; }

            using (var font = new Font(family, fontPx, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Color.White))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            })
            {
                string text = logicalSize <= 20 ? "iL" : "iLng";
                g.DrawString(text, font, brush, new RectangleF(0, -size * 0.012f, size, size), sf);
            }
        }
        return bmp;
    }

    private static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        float r = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
        float d = r * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
"@
  Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
  $t = $typeName

  $sizes = @(32, 64, 128, 256, 512, 1024)
  foreach ($s in $sizes) {
    $png = Join-Path $assetsDir ("iLng-logo-{0}.png" -f $s)
    Invoke-Expression "[$t]::WritePng(`$png, $s)"
    Write-Host ("  assets\iLng-logo-{0}.png" -f $s)
  }

  $logo1024 = Join-Path $assetsDir "iLng-logo-1024.png"
  Copy-Item $logo1024 (Join-Path $assetsDir "iLng-logo.png") -Force

  # Full multi-size ICO for assets / GitHub.
  Invoke-Expression "[$t]::WriteIcoFromPng(`$logo1024, `$icoPath, @($($sizes -join ',')))"
  Write-Host "  assets\iLng.ico"

  # Shell/EXE icon: classic BMP only (32..256). More reliable for csc /win32icon + Explorer.
  $shellIco = Join-Path $assetsDir "iLng-shell.ico"
  Invoke-Expression "[$t]::WriteIcoFromPng(`$logo1024, `$shellIco, @(32, 64, 128, 256))"
  Write-Host "  assets\iLng-shell.ico"
}

$iconPath = Join-Path $assetsDir "iLng.ico"
$shellIconPath = Join-Path $assetsDir "iLng-shell.ico"
Write-Host "Generating icon and logos..."
New-ILngIcon $iconPath $assetsDir

$refArgs = @(
  "/r:`"$(Join-Path $fw 'System.dll')`"",
  "/r:`"$(Join-Path $fw 'System.Core.dll')`"",
  "/r:`"$(Join-Path $fw 'System.Windows.Forms.dll')`"",
  "/r:`"$(Join-Path $fw 'System.Drawing.dll')`"",
  "/r:`"$(Find-GacAssembly 'PresentationFramework')`"",
  "/r:`"$(Find-GacAssembly 'PresentationCore')`"",
  "/r:`"$(Find-GacAssembly 'WindowsBase')`"",
  "/r:`"$(Find-GacAssembly 'System.Xaml')`""
)

$sources = @(Get-ChildItem $srcDir -Filter "*.cs" | ForEach-Object { "`"$($_.FullName)`"" })
$out = Join-Path $binDir "iLng.exe"
$manifest = Join-Path $srcDir "app.manifest"

Write-Host "Stopping iLng and deleting old exe..."
Stop-Process -Name iLng -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700
Get-ChildItem $binDir -Filter "iLng*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Remove-Item $out -Force -ErrorAction SilentlyContinue

# New path first — forces Explorer to treat it as a new file (breaks icon cache by path).
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$outFresh = Join-Path $binDir ("iLng-" + $stamp + ".exe")

$logo256 = Join-Path $assetsDir "iLng-logo-256.png"

$winExeArgs = @(
  "/nologo",
  "/noconfig",
  "/optimize+",
  "/target:winexe",
  "/platform:anycpu",
  "/win32icon:`"$shellIconPath`"",
  "/win32manifest:`"$manifest`"",
  "/resource:`"$logo256`",iLng.Logo256.png",
  "/out:`"$outFresh`""
) + $refArgs + $sources

Write-Host "Building iLng..."
& $csc @winExeArgs
if ($LASTEXITCODE -ne 0) {
  throw "Build failed with exit code $LASTEXITCODE"
}

# Rename to final name
Move-Item $outFresh $out -Force
Get-ChildItem $binDir -File | Where-Object { $_.Name -ne "iLng.exe" } | Remove-Item -Force

# Tell Windows Shell the icon association/file changed
Add-Type -Namespace Native -Name Shell32 -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("Shell32.dll")]
public static extern void SHChangeNotify(int wEventId, uint uFlags, System.IntPtr dwItem1, System.IntPtr dwItem2);
"@
# SHCNE_ASSOCCHANGED=0x08000000, SHCNE_UPDATEITEM=0x00002000, SHCNF_IDLIST=0, SHCNF_PATHW=0x0005, SHCNF_FLUSH=0x1000
[Native.Shell32]::SHChangeNotify(0x08000000, 0x1000, [IntPtr]::Zero, [IntPtr]::Zero)
$bytes = [System.Text.Encoding]::Unicode.GetBytes($out + [char]0)
$ptr = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($bytes.Length)
try {
  [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $ptr, $bytes.Length)
  [Native.Shell32]::SHChangeNotify(0x00002000, 0x0005 -bor 0x1000, $ptr, [IntPtr]::Zero)
} finally {
  [System.Runtime.InteropServices.Marshal]::FreeHGlobal($ptr)
}

# Drop Explorer icon cache files (safe; Explorer rebuilds them)
Get-ChildItem "$env:LOCALAPPDATA\Microsoft\Windows\Explorer" -Filter "iconcache*" -Force -ErrorAction SilentlyContinue |
  Remove-Item -Force -ErrorAction SilentlyContinue
ie4uinit.exe -show 2>$null

Write-Host "OK: $out"
Write-Host "Run: `"$out`""
Write-Host "If icon still looks old: close the Explorer window and reopen the bin folder (F5)."
