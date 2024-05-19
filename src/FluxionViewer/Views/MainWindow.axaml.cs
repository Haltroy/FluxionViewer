using Avalonia.Controls;
using Avalonia.Media;

namespace FluxionViewer.Views;

public partial class MainWindow : Window
{
    internal bool AllowClose = false;

    private string[] args = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SetOpacity(true, 0.5);
            if (args.Length > 0) Main?.LoadArgs(args);
        };
    }

    public MainWindow WithArgs(string[] _args)
    {
        args = _args;
        return this;
    }

    private void SetOpacity(bool enabled, double opacity = 1)
    {
        switch (Background)
        {
            case SolidColorBrush scb:
                scb.Opacity = enabled ? opacity : 1;
                break;

            case VisualBrush vb:
                vb.Opacity = enabled ? opacity : 1;
                break;
            case ConicGradientBrush cgb:
                cgb.Opacity = enabled ? opacity : 1;
                break;
            case DrawingBrush db:
                db.Opacity = enabled ? opacity : 1;
                break;
            case ImageBrush ib:
                ib.Opacity = enabled ? opacity : 1;
                break;
            case TileBrush tb:
                tb.Opacity = enabled ? opacity : 1;
                break;
            case RadialGradientBrush rgb:
                rgb.Opacity = enabled ? opacity : 1;
                break;
            case GradientBrush gb:
                gb.Opacity = enabled ? opacity : 1;
                break;
        }
    }

    // ReSharper disable once UnusedParameter.Local
    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (AllowClose) return;
        e.Cancel = true;
        Main.Close();
    }
}