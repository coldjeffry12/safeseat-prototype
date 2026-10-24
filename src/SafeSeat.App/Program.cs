using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SafeSeat.Core;

namespace SafeSeat.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        if (args.Length == 2 && args[0] == "--smoke-ui") return SmokeUi(app, args[1]);
        if (args.Length != 0) return 2;
        app.DispatcherUnhandledException += (_, e) =>
        {
            if (app.MainWindow is MainWindow main) main.Stop("Unexpected UI error; observation stopped.");
            MessageBox.Show("Observation stopped. Error type: " + e.Exception.GetType().Name,
                "SafeSeat", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        app.Run(new MainWindow());
        return 0;
    }

    private static int SmokeUi(Application app, string evidencePath)
    {
        try
        {
            var router = new SeatRouter();
            var manager = new MainWindow(discoveryEnabled: false);
            app.MainWindow = manager;
            var a = new SeatWindow(SeatId.A, () => router.GetSnapshot(SeatId.A), () => { });
            var b = new SeatWindow(SeatId.B, () => router.GetSnapshot(SeatId.B), () => { });
            foreach (Window window in new Window[] { manager, a, b })
            {
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(1000, 760));
                content.Arrange(new Rect(0, 0, 1000, 760));
                content.UpdateLayout();
                if (content.ActualWidth <= 0) throw new InvalidOperationException("Content did not arrange.");
            }
            if (router.GetSnapshot(SeatId.A).IsRunning) throw new InvalidOperationException("Started by default.");
            // Exercise real WPF routed keyboard events on the seat windows. A Space/Enter intended
            // for raw routing must never activate the focused native Stop button as well.
            using (var source = new HwndSource(new HwndSourceParameters("SafeSeat offscreen test")
                   { Width = 1, Height = 1, WindowStyle = 0 }))
            {
                foreach (var window in new[] { a, b })
                foreach (var key in new[] { Key.Space, Key.Enter })
                foreach (var routed in new[] { Keyboard.PreviewKeyDownEvent, Keyboard.PreviewKeyUpEvent })
                {
                    var input = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = routed };
                    window.RaiseEvent(input);
                    if (!input.Handled) throw new InvalidOperationException("Legacy native button activation was not consumed.");
                }
            }
            a.Refresh(); b.Refresh();
            a.Close(); b.Close(); manager.Close();
            System.IO.File.WriteAllText(evidencePath,
                "PASS: manager and two seat windows constructed; their contents measured and arranged offscreen.\n" +
                "PASS: 8 WPF routed-event checks consume native Space/Enter button activation in seat windows.\n" +
                "PASS: observation remained off; no Raw Input registration and no hardware input read.\n" +
                "NOT TESTED: visible layout, physical monitors, keyboard/mouse operation, DPI changes.\n");
            return 0;
        }
        catch (Exception e)
        {
            System.IO.File.WriteAllText(evidencePath, "FAIL: " + e.GetType().Name + ": " + e.Message + "\n");
            return 1;
        }
    }
}
