using System.Windows;
using System.Windows.Threading;
using USBreeze.App;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            Texts.Current.Set("en");
            var number = new AnimatedSpeed(); number.Reset();
            var host = new Window { Content = number, Width = 260, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            host.Show(); Pump(180);
            if (number.Text != "—") throw new Exception("Empty state");
            number.SetSpeed(100);
            Pump(90);
            if (SystemParameters.ClientAreaAnimation && !(number.Amount > 0 && number.Amount < 100)) throw new Exception("No intermediate animated value");
            number.SetSpeed(200); Pump(330);
            if (Math.Abs(number.Amount - 200) > .001 || number.Text != "200.0") throw new Exception("Animation did not settle on exact score");
            number.Reset(); Pump(40);
            if (number.Text != "—" || number.Amount != 0) throw new Exception("Old animation survived reset");
            number.SetSpeed(42.5, false);
            if (number.Text != "42.5") throw new Exception("Reduced-motion update");
            host.Close();
            Console.WriteLine("PASS: intermediate number animation, retargeting, exact final score, reset and nonanimated updates");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
