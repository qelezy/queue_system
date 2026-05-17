using System.Globalization;
using SkiaSharp;

namespace WebApplication.Services.Reports;

/// <summary>
/// Курируемая палитра серий (24 mixed-цвета). Синхронизировать с
/// <c>REPORT_CHART_BASE_RGB</c> в <c>wwwroot/js/reports-index.js</c>.
/// </summary>
public static class ReportChartPalette
{
    public const int SeriesColorCount = 24;

    private const double FillAlpha = 0.88;
    private const double NormAlpha = 0.45;

    /// <summary>0–5: legacy UI; 6–23: разные семейства и яркости.</summary>
    private static readonly (byte R, byte G, byte B)[] BaseRgb =
    [
        (0, 179, 184),     // бирюза
        (148, 163, 184),   // slate
        (251, 191, 36),    // янтарь
        (239, 68, 68),     // красный
        (99, 102, 241),    // индиго
        (16, 185, 129),    // изумруд
        (37, 99, 235),     // синий
        (168, 85, 247),    // фиолетовый
        (236, 72, 153),    // розовый
        (6, 182, 212),     // циан
        (234, 88, 12),     // оранжевый
        (255, 159, 67),    // персиковый
        (132, 204, 22),    // оливковый/лайм
        (153, 27, 27),     // бордовый
        (202, 138, 4),     // золотистый
        (133, 77, 14),     // коричневый
        (192, 38, 211),    // пурпурный
        (13, 148, 136),    // морской
        (244, 63, 94),     // коралл
        (109, 40, 217),    // сливовый
        (101, 163, 13),    // хаки
        (157, 23, 77),     // малиновый
        (52, 211, 153),    // мятный
        (217, 119, 6)      // песочный
    ];

    private static readonly string[] RgbaFills = BuildRgbaArray(FillAlpha);
    private static readonly string[] RgbaFillsNorm = BuildRgbaArray(NormAlpha);
    private static readonly string[] RgbaStrokes = BuildStrokeArray();
    private static readonly SKColor[] SkFills = BuildSkColors(RgbaFills);
    private static readonly SKColor[] SkStrokes = BuildSkColors(RgbaStrokes);

    public static string Fill(int index) => RgbaFills[index % RgbaFills.Length];

    public static string FillNorm(int index) => RgbaFillsNorm[index % RgbaFillsNorm.Length];

    public static string Stroke(int index) => RgbaStrokes[index % RgbaStrokes.Length];

    public static SKColor SkFill(int index) => SkFills[index % SkFills.Length];

    public static SKColor SkStroke(int index) => SkStrokes[index % SkStrokes.Length];

    private static string[] BuildRgbaArray(double alpha)
    {
        var result = new string[SeriesColorCount];
        for (var i = 0; i < SeriesColorCount; i++)
        {
            var (r, g, b) = BaseRgb[i];
            result[i] = string.Create(CultureInfo.InvariantCulture, $"rgba({r},{g},{b},{alpha:0.##})");
        }

        return result;
    }

    private static string[] BuildStrokeArray()
    {
        var result = new string[SeriesColorCount];
        for (var i = 0; i < SeriesColorCount; i++)
        {
            var (r, g, b) = Darken(BaseRgb[i], 0.84);
            result[i] = string.Create(CultureInfo.InvariantCulture, $"rgba({r},{g},{b},1)");
        }

        return result;
    }

    private static (byte R, byte G, byte B) Darken((byte R, byte G, byte B) c, double factor) =>
        (
            (byte)Math.Clamp((int)Math.Round(c.R * factor), 0, 255),
            (byte)Math.Clamp((int)Math.Round(c.G * factor), 0, 255),
            (byte)Math.Clamp((int)Math.Round(c.B * factor), 0, 255));

    private static SKColor[] BuildSkColors(IReadOnlyList<string> rgbaList)
    {
        var result = new SKColor[rgbaList.Count];
        for (var i = 0; i < rgbaList.Count; i++)
            result[i] = ParseRgbaToSkColor(rgbaList[i]);
        return result;
    }

    private static SKColor ParseRgbaToSkColor(string rgba)
    {
        var inner = rgba.AsSpan(5, rgba.Length - 6);
        var parts = inner.ToString().Split(',');
        if (parts.Length < 4)
            return SKColors.Gray;

        var r = (byte)Math.Clamp(int.Parse(parts[0], CultureInfo.InvariantCulture), 0, 255);
        var g = (byte)Math.Clamp(int.Parse(parts[1], CultureInfo.InvariantCulture), 0, 255);
        var b = (byte)Math.Clamp(int.Parse(parts[2], CultureInfo.InvariantCulture), 0, 255);
        var a = (byte)Math.Clamp((int)Math.Round(double.Parse(parts[3], CultureInfo.InvariantCulture) * 255), 0, 255);
        return new SKColor(r, g, b, a);
    }
}
