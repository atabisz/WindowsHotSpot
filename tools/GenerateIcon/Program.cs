// Icon generator for WindowsHotSpot.
// Run from repo root: dotnet run --project tools/GenerateIcon
// Writes: WindowsHotSpot/Resources/app.ico  (16, 32, 48 px PNG frames)

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var outputPath = Path.GetFullPath("WindowsHotSpot/Resources/app.ico");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

int[] sizes = [16, 32, 48];
var frames = sizes.Select(RenderFrame).ToArray();
WriteIco(outputPath, frames);
Console.WriteLine($"Written: {outputPath}");

// ── Render one icon frame ────────────────────────────────────────────────────
static byte[] RenderFrame(int size)
{
    float s = size / 16f;

    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    g.SmoothingMode      = SmoothingMode.AntiAlias;
    g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
    g.InterpolationMode  = InterpolationMode.HighQualityBicubic;

    // ── 1. Corner glow (top-left quadrant, amber) ──────────────────────────
    // Opacity varies by size: 35% at 16px, 30% at 32px, 25% at 48px
    int glowAlpha = size >= 48 ? 64 : size >= 32 ? 77 : 89;
    using (var b = new SolidBrush(Color.FromArgb(glowAlpha, 232, 160, 32)))
        g.FillRectangle(b, 0, 0, 6 * s, 6 * s);

    // Accent circle (32px and 48px only — too small at 16px)
    // Spec positions are absolute pixels (not scaled): center(4,4) r=3 at 32px; center(6,6) r=5/2.5 at 48px
    if (size >= 32)
    {
        (float cx, float outerR, float innerR) = size >= 48
            ? (6f, 5f, 2.5f)   // 48px spec: center(6,6), outer r=5, inner r=2.5
            : (4f, 3f, 2f);    // 32px spec: center(4,4), outer r=3, inner r=2
        using (var b = new SolidBrush(Color.FromArgb(140, 232, 160, 32)))
            g.FillEllipse(b, cx - outerR, cx - outerR, outerR * 2, outerR * 2);
        using (var b = new SolidBrush(Color.FromArgb(232, 160, 32)))
            g.FillEllipse(b, cx - innerR, cx - innerR, innerR * 2, innerR * 2);
    }

    // ── 2. Mouse cursor (arrow pointing toward top-left) ──────────────────
    // Reference polygon on 16×16 grid, scaled by s
    PointF[] pts =
    [
        new(1 * s, 1 * s),
        new(1 * s, 8 * s),
        new(3 * s, 6 * s),
        new(5 * s, 10 * s),
        new(6 * s, 9 * s),
        new(4 * s, 5 * s),
        new(7 * s, 5 * s),
    ];

    // Stroke width: absolute pixel values per spec (not scaled) — 0.5/1.0/1.5px
    float strokeW = size >= 48 ? 1.5f : size >= 32 ? 1f : 0.5f;
    using (var fill = new SolidBrush(Color.White))
        g.FillPolygon(fill, pts);
    using (var pen = new Pen(Color.FromArgb(17, 17, 17), strokeW))
        g.DrawPolygon(pen, pts);

    // ── 3. Task View mini-windows (2×2 grid, bottom-right) ────────────────
    // Grid origin, cell size, and gap — all scaled from 16px reference
    float wx = 9 * s, wy = 9 * s;
    float ww = 3 * s;
    // wh: spec wants 2px@16, 5px@32, 7px@48 — formula 2.4*s gives 2.4, 4.8, 7.2 (GDI+ rounds naturally)
    float wh  = 2.4f * s;
    float gap = 4 * s;

    (float x, float y, int alpha)[] cells =
    [
        (wx,       wy,           204),  // top-left,     80% opacity
        (wx + gap, wy,           204),  // top-right,    80%
        (wx,       wy + 3 * s,   166),  // bottom-left,  65%
        (wx + gap, wy + 3 * s,   166),  // bottom-right, 65%
    ];

    foreach (var (x, y, alpha) in cells)
        using (var b = new SolidBrush(Color.FromArgb(alpha, 74, 144, 217)))
            g.FillRectangle(b, x, y, ww, wh);

    // ── Encode to PNG bytes ────────────────────────────────────────────────
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

// ── Pack PNG frames into an ICO binary ──────────────────────────────────────
// ICO format: ICONDIR(6) + N×ICONDIRENTRY(16) + N×<image data>
// Windows Vista+ supports PNG-compressed ICO frames natively.
static void WriteIco(string path, byte[][] frames)
{
    using var fs  = new FileStream(path, FileMode.Create);
    using var w   = new BinaryWriter(fs);
    int count     = frames.Length;
    int dataOffset = 6 + count * 16;

    // ICONDIR
    w.Write((ushort)0);      // reserved
    w.Write((ushort)1);      // type = ICO
    w.Write((ushort)count);

    // ICONDIRENTRY × count
    int offset = dataOffset;
    foreach (var png in frames)
    {
        // PNG layout: 8-byte signature + 4-byte IHDR length + 4-byte "IHDR" tag
        //            + 4-byte width (big-endian) + 4-byte height (big-endian)
        // → width at bytes 16-19, height at 20-23. Valid for all conforming PNG encoders.
        int pw = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int ph = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];

        w.Write((byte)(pw >= 256 ? 0 : pw));   // width  (0 = 256)
        w.Write((byte)(ph >= 256 ? 0 : ph));   // height (0 = 256)
        w.Write((byte)0);                       // color count
        w.Write((byte)0);                       // reserved
        w.Write((ushort)1);                     // planes
        w.Write((ushort)32);                    // bit count
        w.Write((uint)png.Length);              // bytes in resource
        w.Write((uint)offset);                  // offset from file start
        offset += png.Length;
    }

    // Image data
    foreach (var png in frames)
        w.Write(png);
}
