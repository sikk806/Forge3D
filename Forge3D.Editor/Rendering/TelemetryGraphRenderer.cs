using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Forge3D.Core.Simulation.Telemetry;
using MediaColor = System.Windows.Media.Color;

namespace Forge3D.Editor.Rendering;

public sealed class TelemetryGraphRenderer
{
    public void Draw(Canvas canvas, IReadOnlyList<TelemetrySample> samples)
    {
        canvas.Children.Clear();

        if (samples.Count < 2 || canvas.ActualWidth <= 1.0 || canvas.ActualHeight <= 1.0)
        {
            return;
        }

        DrawSeries(canvas, samples.Select(sample => (double)sample.PositionY).ToList(), Colors.DeepSkyBlue);
        DrawSeries(canvas, samples.Select(sample => (double)sample.Speed).ToList(), Colors.LightGreen);
        DrawSeries(canvas, samples.Select(sample => (double)sample.KineticEnergy).ToList(), Colors.Orange);
        AddLabel(canvas, "Y", Colors.DeepSkyBlue, 8);
        AddLabel(canvas, "Speed", Colors.LightGreen, 42);
        AddLabel(canvas, "Energy", Colors.Orange, 92);
    }

    private static void DrawSeries(Canvas canvas, IReadOnlyList<double> values, MediaColor color)
    {
        var min = values.Min();
        var max = values.Max();
        var range = Math.Max(0.0001, max - min);
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5
        };

        for (var i = 0; i < values.Count; i++)
        {
            var x = i / Math.Max(1.0, values.Count - 1.0) * width;
            var y = height - ((values[i] - min) / range * (height - 10.0)) - 5.0;
            polyline.Points.Add(new Point(x, y));
        }

        canvas.Children.Add(polyline);
    }

    private static void AddLabel(Canvas canvas, string text, MediaColor color, double left)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = 11
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, 4);
        canvas.Children.Add(label);
    }
}
