using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FluxionViewer.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        Version.Text =
            "v"
            + (
                Assembly.GetExecutingAssembly() is { } ass
                && ass.GetName() is { } name
                && name.Version != null
                    ? ""
                      + (name.Version.Major > 0 ? name.Version.Major : "")
                      + (name.Version.Minor > 0 ? "." + name.Version.Minor : "")
                      + (name.Version.Build > 0 ? "." + name.Version.Build : "")
                      + (name.Version.Revision > 0 ? "." + name.Version.Revision : "")
                    : "?"
            );
        License.Text = ReadResource("LICENSE");
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream == null)
            return string.Empty;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private void CarouselButton_Click(object? S, RoutedEventArgs e)
    {
        if (S is not Button { Tag: Control panel })
            return;
        CarouselMenu.SelectedItem = panel;
    }

    private void Navigate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string link })
            return;
        Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = link });
    }
}