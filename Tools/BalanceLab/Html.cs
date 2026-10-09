using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BalanceLab
{
    /// <summary>Tiny helpers for building the report page: escaping and inline SVG charts (no scripts, no downloads).</summary>
    public static class Html
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static string Esc(string text)
        {
            return (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string N(double value)
        {
            return value.ToString("0.##", Culture);
        }

        public sealed class Series
        {
            public string Name;
            public string Color;
            public List<(double X, double Y)> Points = new List<(double, double)>();
        }

        /// <summary>A line chart of values between 0 and 1 against a number, with optional guide lines and a marked x.</summary>
        public static string LineChart(
            IReadOnlyList<Series> series,
            double xMin,
            double xMax,
            string xLabel,
            string yLabel,
            IReadOnlyList<(double Y, string Label, string Color)> guides = null,
            double? markX = null,
            string markLabel = null)
        {
            const int width = 560, height = 260, left = 46, right = 14, top = 14, bottom = 40;
            double X(double value) => left + (value - xMin) / Math.Max(1e-9, xMax - xMin) * (width - left - right);
            double Y(double value) => height - bottom - Math.Max(0, Math.Min(1, value)) * (height - top - bottom);

            var svg = new StringBuilder();
            svg.Append($"<svg viewBox=\"0 0 {width} {height}\" class=\"chart\" role=\"img\" aria-label=\"{Esc(yLabel)} by {Esc(xLabel)}\">");

            // grid and y axis
            for (var tick = 0; tick <= 4; tick++)
            {
                var value = tick / 4d;
                svg.Append($"<line x1=\"{left}\" y1=\"{N(Y(value))}\" x2=\"{width - right}\" y2=\"{N(Y(value))}\" class=\"grid\"/>");
                svg.Append($"<text x=\"{left - 6}\" y=\"{N(Y(value) + 4)}\" class=\"tick\" text-anchor=\"end\">{(int)(value * 100)}%</text>");
            }

            // x axis ticks every 0.5 of the multiplier
            for (var x = Math.Ceiling(xMin * 2) / 2; x <= xMax + 1e-9; x += 0.5)
            {
                svg.Append($"<line x1=\"{N(X(x))}\" y1=\"{height - bottom}\" x2=\"{N(X(x))}\" y2=\"{height - bottom + 4}\" class=\"axis\"/>");
                svg.Append($"<text x=\"{N(X(x))}\" y=\"{height - bottom + 17}\" class=\"tick\" text-anchor=\"middle\">x{N(x)}</text>");
            }

            svg.Append($"<line x1=\"{left}\" y1=\"{height - bottom}\" x2=\"{width - right}\" y2=\"{height - bottom}\" class=\"axis\"/>");
            svg.Append($"<text x=\"{(left + width - right) / 2}\" y=\"{height - 6}\" class=\"label\" text-anchor=\"middle\">{Esc(xLabel)}</text>");

            if (guides != null)
            {
                foreach (var guide in guides)
                {
                    svg.Append($"<line x1=\"{left}\" y1=\"{N(Y(guide.Y))}\" x2=\"{width - right}\" y2=\"{N(Y(guide.Y))}\" stroke=\"{guide.Color}\" stroke-dasharray=\"4 4\" opacity=\"0.8\"/>");
                    svg.Append($"<text x=\"{left + 4}\" y=\"{N(Y(guide.Y) - 3)}\" class=\"tick\" text-anchor=\"start\" fill=\"{guide.Color}\">{Esc(guide.Label)}</text>");
                }
            }

            if (markX.HasValue && markX.Value >= xMin && markX.Value <= xMax)
            {
                svg.Append($"<line x1=\"{N(X(markX.Value))}\" y1=\"{top}\" x2=\"{N(X(markX.Value))}\" y2=\"{height - bottom}\" class=\"mark\"/>");
                svg.Append($"<text x=\"{N(X(markX.Value) + 4)}\" y=\"{height - bottom - 6}\" class=\"tick\" fill=\"#3d9a5b\">{Esc(markLabel)}</text>");
            }

            foreach (var line in series)
            {
                if (line.Points.Count == 0)
                {
                    continue;
                }

                var points = string.Join(" ", line.Points.Select(point => $"{N(X(point.X))},{N(Y(point.Y))}"));
                svg.Append($"<polyline points=\"{points}\" fill=\"none\" stroke=\"{line.Color}\" stroke-width=\"2.2\" stroke-linejoin=\"round\"/>");
                foreach (var point in line.Points)
                {
                    svg.Append($"<circle cx=\"{N(X(point.X))}\" cy=\"{N(Y(point.Y))}\" r=\"2.3\" fill=\"{line.Color}\"/>");
                }
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        /// <summary>Vertical bars with a label under each.</summary>
        public static string Bars(IReadOnlyList<int> counts, IReadOnlyList<string> labels, string color)
        {
            const int width = 270, height = 120, top = 14, bottom = 22, gap = 4;
            var max = Math.Max(1, counts.Max());
            var barWidth = (width - gap * (counts.Count + 1)) / (double)counts.Count;
            var svg = new StringBuilder();
            svg.Append($"<svg viewBox=\"0 0 {width} {height}\" class=\"bars\" role=\"img\">");
            for (var index = 0; index < counts.Count; index++)
            {
                var barHeight = counts[index] / (double)max * (height - top - bottom);
                var x = gap + index * (barWidth + gap);
                svg.Append($"<rect x=\"{N(x)}\" y=\"{N(height - bottom - barHeight)}\" width=\"{N(barWidth)}\" height=\"{N(barHeight)}\" fill=\"{color}\" rx=\"2\"/>");
                if (counts[index] > 0)
                {
                    svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{N(height - bottom - barHeight - 3)}\" class=\"tick\" text-anchor=\"middle\">{counts[index]}</text>");
                }

                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{height - 7}\" class=\"tick\" text-anchor=\"middle\">{Esc(labels[index])}</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        /// <summary>Vertical bars of decimal values (not whole counts), with the value above each bar and a label below.</summary>
        public static string ValueBars(IReadOnlyList<double> values, IReadOnlyList<string> labels, string color, string format)
        {
            const int width = 270, height = 120, top = 16, bottom = 22, gap = 6;
            var max = Math.Max(1e-9, values.Max());
            var barWidth = Math.Min(48d, (width - gap * (values.Count + 1)) / (double)values.Count);
            var offset = (width - values.Count * barWidth - (values.Count - 1) * gap) / 2d;
            var svg = new StringBuilder();
            svg.Append($"<svg viewBox=\"0 0 {width} {height}\" class=\"bars\" role=\"img\">");
            for (var index = 0; index < values.Count; index++)
            {
                var barHeight = values[index] / max * (height - top - bottom);
                var x = offset + index * (barWidth + gap);
                svg.Append($"<rect x=\"{N(x)}\" y=\"{N(height - bottom - barHeight)}\" width=\"{N(barWidth)}\" height=\"{N(barHeight)}\" fill=\"{color}\" rx=\"2\"/>");
                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{N(height - bottom - barHeight - 3)}\" class=\"tick\" text-anchor=\"middle\">{values[index].ToString(format, Culture)}</text>");
                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{height - 7}\" class=\"tick\" text-anchor=\"middle\">{Esc(labels[index])}</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        /// <summary>One horizontal stacked bar (for the share of wins, defeats and stalemates).</summary>
        public static string Stack(IReadOnlyList<(string Label, double Share, string Color)> parts)
        {
            var bar = new StringBuilder("<div class=\"stack\">");
            foreach (var part in parts.Where(part => part.Share > 0.0005))
            {
                bar.Append($"<span style=\"width:{N(part.Share * 100)}%;background:{part.Color}\" title=\"{Esc(part.Label)} {N(part.Share * 100)}%\">{(part.Share >= 0.12 ? Esc(part.Label) + " " + (int)Math.Round(part.Share * 100) + "%" : string.Empty)}</span>");
            }

            bar.Append("</div>");
            return bar.ToString();
        }
    }
}
