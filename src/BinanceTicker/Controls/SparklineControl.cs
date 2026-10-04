using System.Windows;
using System.Windows.Media;
using BinanceTicker.Core.Models;
namespace BinanceTicker.Controls;

public sealed class SparklineControl : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series),
        typeof(SparklineSeries), typeof(SparklineControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PositiveStrokeProperty = DependencyProperty.Register(nameof(PositiveStroke),
        typeof(Brush), typeof(SparklineControl), new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty NegativeStrokeProperty = DependencyProperty.Register(nameof(NegativeStroke),
        typeof(Brush), typeof(SparklineControl), new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));
    public SparklineSeries? Series { get => (SparklineSeries?)GetValue(SeriesProperty); set => SetValue(SeriesProperty,value); }
    public Brush PositiveStroke { get => (Brush)GetValue(PositiveStrokeProperty); set => SetValue(PositiveStrokeProperty,value); }
    public Brush NegativeStroke { get => (Brush)GetValue(NegativeStrokeProperty); set => SetValue(NegativeStrokeProperty,value); }
    public SparklineControl()
    {
        SetResourceReference(PositiveStrokeProperty,"ThemePositive");
        SetResourceReference(NegativeStrokeProperty,"ThemeNegative");
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        var series = Series;
        if (ActualWidth <= 2 || ActualHeight <= 4 || series is null || !series.HasData || series.End <= series.Start) return;
        var points = series.Segments.SelectMany(s=>s.Points).ToArray();
        var low = points.Min(p=>p.Price); var high = points.Max(p=>p.Price);
        var span = high - low;
        var pen = new Pen(series.IsPositive ? PositiveStroke : NegativeStroke,1.5) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
        drawing.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        foreach(var segment in series.Segments.Where(s=>s.Points.Count >= 2))
        {
            Point Map(SparklinePoint point) => new(
                1 + (point.Time-series.Start).TotalSeconds/(series.End-series.Start).TotalSeconds*(ActualWidth-2),
                span == 0 ? ActualHeight/2 : 2+(1-(double)((point.Price-low)/span))*(ActualHeight-4));
            var geometry = new StreamGeometry();
            using(var context=geometry.Open())
            {
                context.BeginFigure(Map(segment.Points[0]),false,false);
                foreach(var point in segment.Points.Skip(1)) context.LineTo(Map(point),true,false);
            }
            geometry.Freeze(); drawing.DrawGeometry(null,pen,geometry);
        }
        drawing.Pop();
    }
}
