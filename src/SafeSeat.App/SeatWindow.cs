using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SafeSeat.Core;

namespace SafeSeat.App;

internal sealed class SeatWindow : Window
{
    private readonly SeatCanvas _canvas;
    internal SeatWindow(SeatId seat, Func<SeatSnapshot> read, Action stop)
    {
        Title = $"SafeSeat — Seat {seat} — app-owned demonstration";
        Width = 900; Height = 680; MinWidth = 600; MinHeight = 440;
        Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        Foreground = Brushes.White;
        var root = new DockPanel();
        var bar = new StackPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(bar, Dock.Top);
        var button = new Button { Content = "Stop / Clear temporary text", Padding = new Thickness(15, 8, 15, 8), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => stop();
        bar.Children.Add(button);
        bar.Children.Add(new TextBlock { Text = "Native Windows input continues. Esc stops both seats. Only this app's internal state is separate.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        root.Children.Add(bar);
        _canvas = new SeatCanvas(seat, read);
        root.Children.Add(new Viewbox { Child = _canvas, Stretch = Stretch.Uniform });
        Content = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape) { stop(); e.Handled = true; }
            else if (e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter) e.Handled = true;
        };
        PreviewKeyUp += (_, e) =>
        {
            if (e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter) e.Handled = true;
        };
    }
    internal void Refresh() => _canvas.InvalidateVisual();
}

internal sealed class SeatCanvas : FrameworkElement
{
    private readonly SeatId _seat;
    private readonly Func<SeatSnapshot> _read;
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(168, 183, 207));
    private static readonly Brush Panel = new SolidColorBrush(Color.FromRgb(30, 41, 59));
    private readonly Brush _accent;
    internal SeatCanvas(SeatId seat, Func<SeatSnapshot> read)
    {
        _seat = seat; _read = read; Width = 800; Height = 500;
        _accent = seat == SeatId.A ? Brushes.Turquoise : Brushes.LightSalmon;
        Focusable = false;
        // Native legacy mouse/key events never update demo state; Raw Input routes it explicitly.
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var state = _read();
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, new Rect(0, 0, 800, 500));
        Text(dc, $"SEAT {_seat}", 40, 20, 28, _accent);
        Text(dc, state.IsRunning ? "RUNNING • independent app state" : "STOPPED • text and held keys cleared", 40, 61, 16, Muted);
        PanelBox(dc, new Rect(40, 100, 720, 170));
        Text(dc, "Temporary text · US ASCII · 4096-character limit", 55, 111, 14, Muted);
        // Render only an in-memory suffix, bounded to this app's text rectangle.
        dc.PushClip(new RectangleGeometry(new Rect(55, 142, 690, 115)));
        string visible = state.Text.Length > 350 ? "…" + state.Text[^350..] : state.Text;
        Text(dc, visible.Length == 0 ? "Type with this seat's assigned keyboard" : visible, 55, 147, 21,
            visible.Length == 0 ? Muted : Brushes.White, 680, bottomLimit: 252);
        dc.Pop();
        PanelBox(dc, new Rect(40, 290, 180, 60));
        Text(dc, $"Counter: {state.ClickCount}", 53, 307, 19, Brushes.White);
        PanelBox(dc, new Rect(240, 290, 180, 60));
        Text(dc, "Clear own text", 254, 307, 18, Brushes.White);
        PanelBox(dc, new Rect(460, 290, 300, 170));
        Text(dc, "Scroll here", 475, 305, 18, _accent);
        Text(dc, $"Offset: {state.ScrollOffset:0}", 475, 336, 17, Brushes.White);
        dc.PushClip(new RectangleGeometry(new Rect(475, 365, 270, 80)));
        int firstRow = (int)(state.ScrollOffset / 24);
        for (int row = firstRow; row < firstRow + 5; row++)
            Text(dc, $"Seat {_seat} · scroll row {row + 1}", 475,
                365 + row * 24 - state.ScrollOffset, 15, Muted);
        dc.Pop();
        Text(dc, $"Held keys: {state.KeysDown}   {state.LastAction}", 40, 385, 14, Muted, 400);
        Text(dc, "No clipboard, IME, dead keys or third-party app routing.", 40, 467, 14, Muted);
        dc.DrawEllipse(_accent, new Pen(Brushes.Black, 2), new Point(state.PointerX, state.PointerY), 8, 8);
        dc.DrawLine(new Pen(_accent, 2), new Point(state.PointerX - 13, state.PointerY), new Point(state.PointerX + 13, state.PointerY));
        dc.DrawLine(new Pen(_accent, 2), new Point(state.PointerX, state.PointerY - 13), new Point(state.PointerX, state.PointerY + 13));
    }

    private static void PanelBox(DrawingContext dc, Rect rect) => dc.DrawRoundedRectangle(Panel, new Pen(Muted, 1), rect, 8, 8);
    private void Text(DrawingContext dc, string text, double x, double y, double size, Brush brush,
        double width = 740, double? bottomLimit = null)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = width };
        // Keep the newest wrapped/multiline text within its existing clip instead of drawing
        // every line below the top edge and hiding the latest input after several Enter keys.
        double top = bottomLimit.HasValue ? Math.Min(y, bottomLimit.Value - formatted.Height) : y;
        dc.DrawText(formatted, new Point(x, top));
    }
}
