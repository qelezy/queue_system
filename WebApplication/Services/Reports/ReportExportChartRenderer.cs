using System.Globalization;
using System.Text;
using SkiaSharp;
using WebApplication.Models;

namespace WebApplication.Services.Reports;

public static class ReportExportChartRenderer
{
    private const string EmbeddedFontResourceSuffix = "NotoSans-Regular.ttf";

    private static SKTypeface? _labelTypeface;
    private static readonly object FontLock = new();

    public static IReadOnlyList<ReportPreviewChartDescriptor> GetDescriptors(ReportResultViewModel result)
    {
        if (result.PreviewCharts is { Count: > 0 } list)
            return list.Where(DescriptorHasPositiveData).ToList();

        var pie = result.PreviewPieChart;
        if (pie?.Labels is { Count: > 0 } && pie.Values is { Count: > 0 } && SumValues(pie.Values) > 0)
        {
            return
            [
                new ReportPreviewChartDescriptor
                {
                    Kind = "doughnut",
                    Labels = [..pie.Labels],
                    Values = [..pie.Values],
                    ValueUnit = "мин",
                    AriaLabel = "Соотношение длительности обслуживания и простоя",
                    CanvasElementId = "report-preview-chart-0"
                }
            ];
        }

        return [];
    }

    public static IReadOnlyList<byte[]> RenderChartPngs(ReportResultViewModel result)
    {
        var list = new List<byte[]>();
        foreach (var d in GetDescriptors(result))
        {
            if (IsPieLikeKind(d.Kind) && TryRenderPieLikePng(d, out var png) && png is not null)
                list.Add(png);
        }

        return list;
    }

    /// <summary>Векторные SVG для встраивания в HTML/PDF (качество при масштабировании).</summary>
    public static IReadOnlyList<string> RenderChartSvgs(ReportResultViewModel result)
    {
        var list = new List<string>();
        foreach (var d in GetDescriptors(result))
        {
            if (IsPieLikeKind(d.Kind) && TryBuildPieLikeSvg(d, out var pieSvg) && !string.IsNullOrEmpty(pieSvg))
                list.Add(pieSvg);
            else if (IsGroupedBarKind(d.Kind) && TryBuildGroupedBarSvg(d, out var barSvg) && !string.IsNullOrEmpty(barSvg))
                list.Add(barSvg);
        }

        return list;
    }

    private static bool DescriptorHasPositiveData(ReportPreviewChartDescriptor d)
    {
        if (IsGroupedBarKind(d.Kind))
            return SumGroupedBarValues(d) > 0;

        return SumValues(d.Values) > 0;
    }

    private static double SumGroupedBarValues(ReportPreviewChartDescriptor d)
    {
        if (d.Datasets is not { Count: > 0 } sets)
            return 0;

        var sum = 0.0;
        foreach (var ds in sets)
        {
            sum += SumValues(ds.Values);
            if (ds.NormValues is not null)
                sum += SumValues(ds.NormValues);
        }

        return sum;
    }

    private static bool TryBuildPieLikeSvg(ReportPreviewChartDescriptor d, out string? svg)
    {
        svg = null;
        var (labels, values) = NormalizeLabelsValues(d);
        var sum = SumValues(values);
        if (sum <= 0 || labels.Count == 0)
            return false;

        var isDoughnut = string.Equals(d.Kind?.Trim(), "doughnut", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(d.Kind);

        const double chartR = 118;
        const double legendIconW = 14;
        const double legendGap = 10;
        const double legendCharWidth = 6.6;
        var innerR = isDoughnut ? chartR * 0.5 : 0;
        var unit = string.IsNullOrWhiteSpace(d.ValueUnit) ? "" : d.ValueUnit.Trim();
        var maxLegendChars = 0;
        for (var i = 0; i < labels.Count; i++)
        {
            var v = values[i];
            var pct = sum > 0 ? 100.0 * v / sum : 0;
            var valStr = unit.Length > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{v:0.##} {unit}")
                : string.Create(CultureInfo.InvariantCulture, $"{v:0.##}");
            var line = $"{labels[i]}: {valStr} ({pct:0.#}%)";
            maxLegendChars = Math.Max(maxLegendChars, line.Length);
        }

        var legendBlockW = legendIconW + legendGap + maxLegendChars * legendCharWidth;
        var minCanvasW = chartR * 2 + 48;
        var canvasW = Math.Clamp(Math.Max(minCanvasW, Math.Ceiling(legendBlockW + 56)), 280d, 960d);
        var cx = canvasW / 2;
        var legendLeft = cx - legendBlockW / 2;
        const double cy = 118;
        const double legendRow = 26;
        var legendY = cy + chartR + 22;
        var h = Math.Max(200, legendY + labels.Count * legendRow + 12);

        var sb = new StringBuilder(2048);
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {canvasW:0.##} {h:0.##}\" width=\"{canvasW:0.##}\" height=\"{h:0.##}\" shape-rendering=\"geometricPrecision\">");

        var startDeg = -90.0;
        for (var i = 0; i < labels.Count; i++)
        {
            var sweep = 360.0 * values[i] / sum;
            if (sweep <= 0.001)
            {
                startDeg += sweep;
                continue;
            }

            var fill = SvgSegmentFill(i);
            var large = sweep > 180 ? 1 : 0;
            if (isDoughnut && innerR > 0.5)
                AppendSvgAnnulusSector(sb, cx, cy, innerR, chartR, startDeg, sweep, large, fill);
            else
            {
                var p0 = SvgPoint(cx, cy, chartR, startDeg);
                var p1 = SvgPoint(cx, cy, chartR, startDeg + sweep);
                sb.Append(CultureInfo.InvariantCulture,
                    $"<path fill=\"{fill}\" stroke=\"none\" d=\"M {cx:0.##} {cy:0.##} L {p0.X:0.##} {p0.Y:0.##} A {chartR:0.##} {chartR:0.##} 0 {large} 1 {p1.X:0.##} {p1.Y:0.##} Z\"/>");
            }

            startDeg += sweep;
        }

        for (var i = 0; i < labels.Count; i++)
        {
            var y = legendY + i * legendRow;
            var fill = SvgSegmentFill(i);
            var v = values[i];
            var pct = sum > 0 ? 100.0 * v / sum : 0;
            var valStr = unit.Length > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{v:0.##} {unit}")
                : string.Create(CultureInfo.InvariantCulture, $"{v:0.##}");
            var line = $"{labels[i]}: {valStr} ({pct:0.#}%)";
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{legendLeft:0.##}\" y=\"{y - 12:0.##}\" width=\"{legendIconW:0.##}\" height=\"{legendIconW:0.##}\" fill=\"{fill}\" rx=\"1\"/>");
            sb.Append("<text xml:space=\"preserve\" x=\"")
                .Append((legendLeft + legendIconW + legendGap).ToString("0.##", CultureInfo.InvariantCulture))
                .Append("\" y=\"")
                .Append(y.ToString("0.##", CultureInfo.InvariantCulture))
                .Append("\" font-family=\"system-ui,Segoe UI,sans-serif\" font-size=\"14\" fill=\"#334155\">")
                .Append(EscapeSvgText(line))
                .Append("</text>");
        }

        sb.Append("</svg>");
        svg = sb.ToString();
        return true;
    }

    /// <summary>
    /// Сектор кольца без радиусов к центру — в QuestPDF на стыке «лучей» из центра часто даётся вертикальная чёрная линия.
    /// </summary>
    private static void AppendSvgAnnulusSector(
        StringBuilder sb,
        double cx,
        double cy,
        double innerR,
        double outerR,
        double startDeg,
        double sweep,
        int large,
        string fill)
    {
        var endDeg = startDeg + sweep;
        var o0 = SvgPoint(cx, cy, outerR, startDeg);
        var o1 = SvgPoint(cx, cy, outerR, endDeg);
        var i1 = SvgPoint(cx, cy, innerR, endDeg);
        var i0 = SvgPoint(cx, cy, innerR, startDeg);
        sb.Append(CultureInfo.InvariantCulture,
            $"<path fill=\"{fill}\" stroke=\"none\" d=\"M {o0.X:0.##} {o0.Y:0.##} A {outerR:0.##} {outerR:0.##} 0 {large} 1 {o1.X:0.##} {o1.Y:0.##} L {i1.X:0.##} {i1.Y:0.##} A {innerR:0.##} {innerR:0.##} 0 {large} 0 {i0.X:0.##} {i0.Y:0.##} Z\"/>");
    }

    private static (double X, double Y) SvgPoint(double cx, double cy, double r, double deg)
    {
        var rad = deg * (Math.PI / 180.0);
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    private static string SvgSegmentFill(int index) => ReportChartPalette.Fill(index);

    private static string SvgSegmentFillNorm(int index) => ReportChartPalette.FillNorm(index);

    private static SKColor SkSegmentFill(int index) => ReportChartPalette.SkFill(index);

    private static SKColor SkSegmentStroke(int index) => ReportChartPalette.SkStroke(index);

    private static string EscapeSvgText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static bool TryRenderPieLikePng(ReportPreviewChartDescriptor d, out byte[]? pngBytes)
    {
        pngBytes = null;
        var (labels, values) = NormalizeLabelsValues(d);
        var sum = SumValues(values);
        if (sum <= 0 || labels.Count == 0)
            return false;

        var isDoughnut = string.Equals(d.Kind?.Trim(), "doughnut", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(d.Kind);

        var unit = string.IsNullOrWhiteSpace(d.ValueUnit) ? "" : d.ValueUnit.Trim();
        var maxLegendChars = 0;
        for (var i = 0; i < labels.Count; i++)
        {
            var v = values[i];
            var pct = sum > 0 ? 100.0 * v / sum : 0;
            var valStr = unit.Length > 0
                ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{v:0.##} {unit}")
                : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{v:0.##}");
            var line = $"{labels[i]}: {valStr} ({pct:0.#}%)";
            maxLegendChars = Math.Max(maxLegendChars, line.Length);
        }

        var canvasW = Math.Clamp((float)Math.Ceiling(40 + maxLegendChars * 6.4f), 380f, 760f);
        const float padX = 16f;
        const float chartR = 72f;
        var innerR = isDoughnut ? chartR * 0.5f : 0f;
        var cx = canvasW / 2f;
        const float cy = 88f;
        const float legendRow = 22f;
        var legendY = cy + chartR + 18f;
        var legendH = labels.Count * legendRow + 12f;
        const float aspect = 1.15f;
        var canvasH = (float)Math.Ceiling(canvasW / aspect);
        var h = Math.Max(canvasH, legendY + legendH + 8f);

        var info = new SKImageInfo((int)Math.Ceiling(canvasW), (int)Math.Ceiling(h), SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var tf = GetLabelTypeface();
        using var legendFont = new SKFont(tf, 12f);
        using var legendPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(51, 65, 85),
        };

        var oval = SKRect.Create(cx - chartR, cy - chartR, chartR * 2f, chartR * 2f);
        var startDeg = -90f;
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

        for (var i = 0; i < labels.Count; i++)
        {
            var sweep = (float)(360.0 * values[i] / sum);
            if (sweep <= 0.001f)
            {
                startDeg += sweep;
                continue;
            }

            var fill = SkSegmentFill(i);
            var stroke = SkSegmentStroke(i);
            using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = fill };

            var p0 = PointOnCircle(cx, cy, chartR, startDeg);
            using var path = new SKPath();
            path.MoveTo(cx, cy);
            path.LineTo(p0.X, p0.Y);
            path.ArcTo(oval, startDeg, sweep, false);
            path.Close();
            canvas.DrawPath(path, fillPaint);

            strokePaint.Color = stroke;
            canvas.DrawPath(path, strokePaint);

            startDeg += sweep;
        }

        if (innerR > 0.5f)
        {
            using var hole = new SKPaint { IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Fill };
            canvas.DrawCircle(cx, cy, innerR, hole);
            using var ringStroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = new SKColor(226, 232, 240),
                StrokeWidth = 1f
            };
            canvas.DrawCircle(cx, cy, innerR, ringStroke);
        }

        for (var i = 0; i < labels.Count; i++)
        {
            var y = legendY + i * legendRow;
            var fill = SkSegmentFill(i);
            var r = SKRect.Create(padX, y - 10f, 12f, 12f);
            using var sq = new SKPaint { IsAntialias = true, Color = fill, Style = SKPaintStyle.Fill };
            canvas.DrawRect(r, sq);

            var v = values[i];
            var pct = sum > 0 ? 100.0 * v / sum : 0;
            var valStr = unit.Length > 0
                ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{v:0.##} {unit}")
                : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{v:0.##}");
            var line = $"{labels[i]}: {valStr} ({pct:0.#}%)";
            var clippedLine = ClipText(line, legendFont, legendPaint, canvasW - padX * 2 - 20f);
            canvas.DrawText(clippedLine, padX + 20f, y, SKTextAlign.Left, legendFont, legendPaint);
        }

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, quality: 100);
        pngBytes = data.ToArray();
        return true;
    }

    private static string ClipText(string text, SKFont font, SKPaint paint, float maxWidth)
    {
        if (font.MeasureText(text, paint) <= maxWidth)
            return text;
        const string ell = "…";
        var lo = 0;
        var hi = text.Length;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            var s = text[..mid] + ell;
            if (font.MeasureText(s, paint) <= maxWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo <= 0 ? ell : text[..lo] + ell;
    }

    private static SKTypeface GetLabelTypeface()
    {
        if (_labelTypeface is not null)
            return _labelTypeface;

        lock (FontLock)
        {
            if (_labelTypeface is not null)
                return _labelTypeface;

            var asm = typeof(ReportExportChartRenderer).Assembly;
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(EmbeddedFontResourceSuffix, StringComparison.Ordinal));
            if (resourceName is null)
            {
                _labelTypeface = SKTypeface.Default;
                return _labelTypeface;
            }

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                _labelTypeface = SKTypeface.Default;
                return _labelTypeface;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            using var skData = SKData.CreateCopy(bytes);
            _labelTypeface = SKTypeface.FromData(skData, 0) ?? SKTypeface.Default;
            return _labelTypeface;
        }
    }

    private static (List<string> Labels, List<double> Values) NormalizeLabelsValues(ReportPreviewChartDescriptor d)
    {
        var labels = (d.Labels ?? []).Select(static x => x?.Trim() ?? "").ToList();
        var values = (d.Values ?? []).Select(static v => double.IsFinite(v) ? v : 0).ToList();
        while (values.Count < labels.Count)
            values.Add(0);
        if (values.Count > labels.Count)
            values = values.Take(labels.Count).ToList();
        return (labels, values);
    }

    private static double SumValues(IReadOnlyList<double> values) =>
        values.Sum(static v => double.IsFinite(v) && v > 0 ? v : 0);

    private static bool IsPieLikeKind(string? kind)
    {
        var k = (kind ?? "doughnut").Trim().ToLowerInvariant();
        return k is "pie" or "doughnut";
    }

    private static bool IsGroupedBarKind(string? kind) =>
        string.Equals(kind?.Trim(), "groupedBar", StringComparison.OrdinalIgnoreCase);

    private static bool TryBuildGroupedBarSvg(ReportPreviewChartDescriptor d, out string? svg)
    {
        svg = null;
        if (!TryNormalizeGroupedBarData(d, out var dayLabels, out var series))
            return false;

        var maxVal = series
            .SelectMany(s => s.NormValues is not null
                ? s.Values.Concat(s.NormValues)
                : s.Values)
            .DefaultIfEmpty(0)
            .Max();
        if (maxVal <= 0)
            return false;

        var layout = ComputeGroupedBarLayout(dayLabels.Count, series.Count);
        var unit = string.IsNullOrWhiteSpace(d.ValueUnit) ? "" : d.ValueUnit.Trim();

        var sb = new StringBuilder(8192);
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {layout.CanvasW:0.##} {layout.CanvasH:0.##}\" width=\"{layout.CanvasW:0.##}\" height=\"{layout.CanvasH:0.##}\" shape-rendering=\"geometricPrecision\">");

        AppendGroupedBarPlotFrame(sb, layout, maxVal, unit);
        AppendGroupedBarBars(sb, layout, dayLabels, series, maxVal, unit);
        AppendGroupedBarLegend(sb, layout, series);

        sb.Append("</svg>");
        svg = sb.ToString();
        return true;
    }

    private sealed record GroupedBarLayout(
        double PadL,
        double PadR,
        double PadT,
        double PadB,
        double PlotH,
        double CanvasW,
        double CanvasH,
        double PlotW,
        double OriginY,
        double GroupW,
        double BarGap,
        double BarW,
        int LegendCols,
        double LegendColWidth,
        double LegendRowHeight,
        int LegendFontSize,
        double LegendY,
        double LegendOffsetX);

    private static GroupedBarLayout ComputeGroupedBarLayout(int numDays, int numSeries)
    {
        const double padL = 52;
        const double padR = 20;
        const double padT = 20;
        const double padB = 72;
        const double plotH = 280;
        var fullPageContentWidth = ReportTabularExporter.PdfLandscapeContentWidthPoints();
        var canvasW = Math.Max(fullPageContentWidth, 560 + numDays * 32);
        canvasW = Math.Min(canvasW, 1150);
        var plotW = canvasW - padL - padR;
        var compactLegend = numSeries > 12;
        var legendColWidth = compactLegend ? 120.0 : 140.0;
        var legendCols = Math.Max(1, (int)Math.Floor(plotW / legendColWidth));
        var legendBlockW = legendCols * legendColWidth;
        var legendOffsetX = padL + (plotW - legendBlockW) / 2;
        var legendRowHeight = compactLegend ? 16.0 : 20.0;
        var legendFontSize = compactLegend ? 10 : 11;
        var legendRows = (int)Math.Ceiling(numSeries / (double)legendCols);
        var legendH = Math.Max(28, legendRows * legendRowHeight + 12);
        var canvasH = padT + plotH + padB + legendH;
        var originY = padT + plotH;
        var numBarSeries = Math.Max(1, numSeries);
        var groupW = plotW / Math.Max(1, numDays);
        const double barGap = 1.5;
        var barW = Math.Max(1.5, (groupW - barGap * (numBarSeries + 1)) / numBarSeries);
        var legendY = padT + plotH + padB;

        return new GroupedBarLayout(
            padL, padR, padT, padB, plotH, canvasW, canvasH, plotW, originY, groupW, barGap, barW,
            legendCols, legendColWidth, legendRowHeight, legendFontSize, legendY, legendOffsetX);
    }

    private static void AppendGroupedBarPlotFrame(
        StringBuilder sb,
        GroupedBarLayout layout,
        double maxVal,
        string unit)
    {
        sb.Append("<line x1=\"").Append(F(layout.PadL))
            .Append("\" y1=\"").Append(F(layout.PadT))
            .Append("\" x2=\"").Append(F(layout.PadL))
            .Append("\" y2=\"").Append(F(layout.OriginY))
            .Append("\" stroke=\"#cbd5e1\" stroke-width=\"1\"/>");
        sb.Append("<line x1=\"").Append(F(layout.PadL))
            .Append("\" y1=\"").Append(F(layout.OriginY))
            .Append("\" x2=\"").Append(F(layout.PadL + layout.PlotW))
            .Append("\" y2=\"").Append(F(layout.OriginY))
            .Append("\" stroke=\"#cbd5e1\" stroke-width=\"1\"/>");

        for (var tick = 0; tick <= 4; tick++)
        {
            var frac = tick / 4.0;
            var yVal = maxVal * frac;
            var y = layout.OriginY - frac * layout.PlotH;
            sb.Append("<line x1=\"").Append(F(layout.PadL))
                .Append("\" y1=\"").Append(F(y))
                .Append("\" x2=\"").Append(F(layout.PadL + layout.PlotW))
                .Append("\" y2=\"").Append(F(y))
                .Append("\" stroke=\"#e2e8f0\" stroke-width=\"1\"/>");
            var tickLabel = unit.Length > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{yVal:0.#} {unit}")
                : string.Create(CultureInfo.InvariantCulture, $"{yVal:0.#}");
            sb.Append("<text x=\"").Append(F(layout.PadL - 6))
                .Append("\" y=\"").Append(F(y + 4))
                .Append("\" text-anchor=\"end\" font-family=\"system-ui,Segoe UI,sans-serif\" font-size=\"11\" fill=\"#64748b\">")
                .Append(EscapeSvgText(tickLabel))
                .Append("</text>");
        }
    }

    private static void AppendGroupedBarDayLabels(
        StringBuilder sb,
        GroupedBarLayout layout,
        IReadOnlyList<string> dayLabels)
    {
        for (var di = 0; di < dayLabels.Count; di++)
        {
            var groupX = layout.PadL + di * layout.GroupW;
            var labelX = groupX + layout.GroupW / 2;
            sb.Append("<text x=\"").Append(F(labelX))
                .Append("\" y=\"").Append(F(layout.OriginY + 18))
                .Append("\" text-anchor=\"middle\" font-family=\"system-ui,Segoe UI,sans-serif\" font-size=\"10\" fill=\"#334155\">")
                .Append(EscapeSvgText(dayLabels[di]))
                .Append("</text>");
        }
    }

    private static void AppendGroupedBarBars(
        StringBuilder sb,
        GroupedBarLayout layout,
        IReadOnlyList<string> dayLabels,
        IReadOnlyList<GroupedBarSeries> barSeries,
        double maxVal,
        string unit)
    {
        AppendGroupedBarDayLabels(sb, layout, dayLabels);

        for (var di = 0; di < dayLabels.Count; di++)
        {
            var groupX = layout.PadL + di * layout.GroupW;
            var dayLabel = dayLabels[di];
            foreach (var bar in barSeries)
            {
                var slotX = groupX + layout.BarGap + bar.BarGroupIndex * (layout.BarW + layout.BarGap);
                if (bar.NormValues is not null)
                {
                    var normVal = bar.NormValues[di];
                    if (normVal > 0)
                    {
                        var normH = normVal / maxVal * layout.PlotH;
                        var normY = layout.OriginY - normH;
                        sb.Append("<rect x=\"").Append(F(slotX))
                            .Append("\" y=\"").Append(F(normY))
                            .Append("\" width=\"").Append(F(layout.BarW))
                            .Append("\" height=\"").Append(F(normH))
                            .Append("\" fill=\"").Append(SvgSegmentFillNorm(bar.ColorIndex))
                            .Append("\" rx=\"1\">");
                        AppendGroupedBarRectTitle(sb, bar.Label, dayLabel, normVal, unit);
                        sb.Append("</rect>");
                    }
                }

                var val = bar.Values[di];
                if (val <= 0)
                    continue;

                var barH = val / maxVal * layout.PlotH;
                var factW = bar.NormValues is not null ? layout.BarW * 0.65 : layout.BarW;
                var factX = bar.NormValues is not null ? slotX + (layout.BarW - factW) / 2 : slotX;
                var y = layout.OriginY - barH;
                sb.Append("<rect x=\"").Append(F(factX))
                    .Append("\" y=\"").Append(F(y))
                    .Append("\" width=\"").Append(F(factW))
                    .Append("\" height=\"").Append(F(barH))
                    .Append("\" fill=\"").Append(SvgSegmentFill(bar.ColorIndex))
                    .Append("\" rx=\"1\">");
                AppendGroupedBarRectTitle(sb, bar.Label, dayLabel, val, unit);
                sb.Append("</rect>");
            }
        }
    }

    private static void AppendGroupedBarRectTitle(
        StringBuilder sb,
        string seriesLabel,
        string dayLabel,
        double value,
        string unit)
    {
        var valStr = unit.Length > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {unit}")
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.#}");
        sb.Append("<title>")
            .Append(EscapeSvgText($"{seriesLabel}, {dayLabel}: {valStr}"))
            .Append("</title>");
    }

    private static void AppendGroupedBarLegend(
        StringBuilder sb,
        GroupedBarLayout layout,
        IReadOnlyList<GroupedBarSeries> series)
    {
        var fs = layout.LegendFontSize.ToString(CultureInfo.InvariantCulture);
        for (var li = 0; li < series.Count; li++)
        {
            var item = series[li];
            var col = li % layout.LegendCols;
            var row = li / layout.LegendCols;
            var lx = layout.LegendOffsetX + col * layout.LegendColWidth;
            var ly = layout.LegendY + row * layout.LegendRowHeight;
            if (item.NormValues is not null)
            {
                sb.Append("<rect x=\"").Append(F(lx))
                    .Append("\" y=\"").Append(F(ly - 10))
                    .Append("\" width=\"10\" height=\"10\" fill=\"").Append(SvgSegmentFillNorm(item.ColorIndex))
                    .Append("\" rx=\"1\"/>");
                sb.Append("<rect x=\"").Append(F(lx + 14))
                    .Append("\" y=\"").Append(F(ly - 8))
                    .Append("\" width=\"7\" height=\"7\" fill=\"").Append(SvgSegmentFill(item.ColorIndex))
                    .Append("\" rx=\"1\"/>");
                sb.Append("<text x=\"").Append(F(lx + 26))
                    .Append("\" y=\"").Append(F(ly))
                    .Append("\" font-family=\"system-ui,Segoe UI,sans-serif\" font-size=\"").Append(fs)
                    .Append("\" fill=\"#334155\">")
                    .Append(EscapeSvgText(item.Label))
                    .Append("</text>");
            }
            else
            {
                sb.Append("<rect x=\"").Append(F(lx))
                    .Append("\" y=\"").Append(F(ly - 10))
                    .Append("\" width=\"10\" height=\"10\" fill=\"").Append(SvgSegmentFill(item.ColorIndex))
                    .Append("\" rx=\"1\"/>");
                sb.Append("<text x=\"").Append(F(lx + 16))
                    .Append("\" y=\"").Append(F(ly))
                    .Append("\" font-family=\"system-ui,Segoe UI,sans-serif\" font-size=\"").Append(fs)
                    .Append("\" fill=\"#334155\">")
                    .Append(EscapeSvgText(item.Label))
                    .Append("</text>");
            }
        }
    }

    private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryNormalizeGroupedBarData(
        ReportPreviewChartDescriptor d,
        out List<string> dayLabels,
        out List<GroupedBarSeries> series)
    {
        dayLabels = (d.Labels ?? []).Select(static x => x?.Trim() ?? "").ToList();
        series = [];
        if (dayLabels.Count == 0 || d.Datasets is not { Count: > 0 } sets)
            return false;

        for (var si = 0; si < sets.Count; si++)
        {
            var ds = sets[si];
            if (string.Equals(ds.ChartSeriesType, "norm", StringComparison.OrdinalIgnoreCase))
                continue;

            var vals = (ds.Values ?? []).Select(static v => double.IsFinite(v) ? Math.Max(0, v) : 0).ToList();
            while (vals.Count < dayLabels.Count)
                vals.Add(0);
            if (vals.Count > dayLabels.Count)
                vals = vals.Take(dayLabels.Count).ToList();

            List<double>? normVals = null;
            if (ds.NormValues is { Count: > 0 })
            {
                normVals = ds.NormValues.Select(static v => double.IsFinite(v) ? Math.Max(0, v) : 0).ToList();
                while (normVals.Count < dayLabels.Count)
                    normVals.Add(0);
                if (normVals.Count > dayLabels.Count)
                    normVals = normVals.Take(dayLabels.Count).ToList();
            }

            var colorIndex = si;

            series.Add(new GroupedBarSeries(
                string.IsNullOrWhiteSpace(ds.Label) ? $"Серия {si + 1}" : ds.Label.Trim(),
                vals,
                normVals,
                colorIndex,
                si));
        }

        return series.Count > 0;
    }

    private sealed record GroupedBarSeries(
        string Label,
        List<double> Values,
        List<double>? NormValues,
        int ColorIndex,
        int BarGroupIndex);

    private static SKPoint PointOnCircle(float cx, float cy, float r, float deg)
    {
        var rad = deg * (MathF.PI / 180f);
        return new SKPoint(cx + r * MathF.Cos(rad), cy + r * MathF.Sin(rad));
    }
}
