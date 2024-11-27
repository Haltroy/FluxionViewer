using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DialogHostAvalonia;
using FluxionSharp;

namespace FluxionViewer.Views;

public partial class MainView : UserControl
{
    internal static readonly StyledProperty<bool> OnDesktopProperty =
        AvaloniaProperty.Register<MainView, bool>(nameof(OnDesktop));

    internal static readonly StyledProperty<object?> ClipboardProperty =
        AvaloniaProperty.Register<MainView, object?>(nameof(Clipboard));

    private static readonly FilePickerFileType[] fileTypes =
    [
        new(Lang.Lang.FileExtName)
        {
            Patterns = "*.flx".Split('|'),
            MimeTypes = "application/fluxion".Split('|'),
            AppleUniformTypeIdentifiers = "com.haltroy.fluxion".Split('|')
        },
        FilePickerFileTypes.All
    ];

    private bool IsSaving;

    private bool IsSettingsSaving = true;

    private IStorageFile? loadedFile;
    private string loadedFileCompression = "none";
    private bool Saved = true;

    public MainView()
    {
        InitializeComponent();
        Version.Text =
            "v"
            + GetAppVersion();
        if (Design.IsDesignMode) ReadFluxionNode(GenerateExampleFLX(), null);
    }

    internal object? Clipboard
    {
        get => GetValue(ClipboardProperty);
        set => SetValue(ClipboardProperty, value);
    }

    internal bool OnDesktop
    {
        get => GetValue(OnDesktopProperty);
        set => SetValue(OnDesktopProperty, value);
    }

    private static string AppFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "haltroy", "fluxion-viewer");

    private static string AppSettingsFile => Path.Combine(AppFolder, "settings.flx");

    private static FluxionNode GenerateExampleFLX()
    {
        var node = new FluxionNode { Name = "Root" };
        node.Add(new FluxionNode { Name = "TestItem", Value = "This is a test." });
        var subNode = new FluxionNode { Name = "NodeWithAttr", Value = 25 };
        subNode.Attributes.Add(new FluxionAttribute
            { Name = "TestAttr", Value = new byte[] { 0, 1, 2, 4, 8, 16, 64, 128, 255 } });
        node.Add(subNode);
        return node;
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly() is { } ass
               && ass.GetName() is { } name
               && name.Version != null
            ? ""
              + (name.Version.Major > 0 ? name.Version.Major : "")
              + (name.Version.Minor > 0 ? "." + name.Version.Minor : "")
              + (name.Version.Build > 0 ? "." + name.Version.Build : "")
              + (name.Version.Revision > 0 ? "." + name.Version.Revision : "")
            : "?";
    }

    public async void LoadArgs(string[] args)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (args.Length < 1) return;
            var file = args[0];
            if (args.Length >= 3)
                loadedFileCompression = args[Array.IndexOf(args, "--compression") + 1].ToLowerInvariant();
            if (Parent is not TopLevel topLevel) return;
            loadedFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(file);
            if (loadedFile is null) return;
            SavingProgressPanel.IsVisible = true;
            SaveStatusText.Text = Lang.Lang.Status_LoadingFile;
            await using Stream stream = loadedFileCompression.ToLowerInvariant() switch
            {
                "gzip" => new GZipStream(await loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                "brotli" => new BrotliStream(await loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                "deflate" => new DeflateStream(await loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                "zlib" => new ZLibStream(await loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                _ => throw new Exception()
            };
            ReadFluxionNode(Fluxion.Read(stream), null);
            SavingProgressPanel.IsVisible = false;
        });
    }

    // ReSharper disable once UnusedParameter.Local
    private void NewAttribute(object? sender, RoutedEventArgs e)
    {
        if (sender is null || Attributes is null ||
            Nodes?.SelectedItem is not TreeViewItem { Tag: FluxionNode node }) return;
        var attr = new FluxionAttribute { Name = Lang.Lang.Node_NewAttr, Value = null };
        node.Attributes.Add(attr);
        Attributes.Children.Add(GenerateAttributePanel(node, attr));
        SaveState();
    }

    // ReSharper disable UnusedParameter.Local
    private void CloseDialogHost(object? s, RoutedEventArgs e)
    {
        MainDialogHost.CurrentSession?.Close();
    }
    // ReSharper restore UnusedParameter.Local

    private Control GenerateAttributePanel(FluxionNode node, FluxionAttribute attr)
    {
        StackPanel attrPanel = new() { Orientation = Orientation.Vertical, Spacing = 5 };

        DockPanel attributePanel1 = new() { LastChildFill = true };
        attrPanel.Children.Add(attributePanel1);
        Button removeButton = new()
        {
            Margin = new Thickness(5, 0),
            Content = new PathIcon { [!PathIcon.DataProperty] = DeleteRegular[!PathIcon.DataProperty] }
        };
        ToolTip.SetTip(removeButton, Lang.Lang.Attribute_Remove);
        DockPanel.SetDock(removeButton, Dock.Left);
        removeButton.Click += (_, _) =>
        {
            node.Attributes.Remove(attr);
            Attributes.Children.Remove(attrPanel);
            SaveState();
        };
        attributePanel1.Children.Add(removeButton);

        ComboBox attributeValueType = new()
        {
            Margin = new Thickness(5, 0), HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Null });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_True });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_False });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Byte });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_SByte });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Char });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Short });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_UShort });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Int });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_UInt });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Long });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_ULong });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Float });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_Double });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_String });
        attributeValueType.Items.Add(new ComboBoxItem { Content = Lang.Lang.ValueType_ByteArray });
        attributeValueType.SelectedIndex = GetIndex(attr.Value);

        attributePanel1.Children.Add(attributeValueType);

        DockPanel attributePanel2 = new() { LastChildFill = true };
        TextBlock name = new() { Text = Lang.Lang.Attr_Name, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(name, Dock.Left);
        attributePanel2.Children.Add(name);

        TextBox name_value = new() { Margin = new Thickness(5, 0), Text = attr.Name };
        attributePanel2.Children.Add(name_value);

        attrPanel.Children.Add(attributePanel2);

        DockPanel attributePanel3 = new() { LastChildFill = true };
        TextBlock value = new() { Text = Lang.Lang.Attr_Value, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(name, Dock.Left);
        attributePanel3.Children.Add(value);

        TextBox value_value = new()
            { Margin = new Thickness(5, 0), Text = attr.Value + "", IsEnabled = attributeValueType.SelectedIndex > 2 };

        name_value.TextChanged += (_, _) =>
        {
            if (Attributes is null || !Attributes.IsEnabled) return;
            attr.Name = name_value.Text ?? string.Empty;
            SaveState();
        };
        value_value.TextChanged += (_, _) =>
        {
            if (Attributes is null || !Attributes.IsEnabled) return;
            if (!value_value.IsEnabled) return;
            attr.Value = ParseValue(attributeValueType.SelectedIndex, value_value.Text ?? string.Empty);
            SaveState();
        };
        attributeValueType.SelectionChanged += (_, _) =>
        {
            if (Attributes is null || !Attributes.IsEnabled) return;
            if (attributeValueType.SelectedIndex < 3)
            {
                value_value.IsEnabled = false;
                return;
            }

            attr.Value = ConvertValue(attributeValueType.SelectedIndex, attr.Value);
            value_value.IsEnabled = false;
            value_value.Text = attr.Value switch
            {
                bool bool_value => bool_value ? "true" : "false",
                byte[] byte_array => BitConverter.ToString(byte_array).Replace("-", " "),
                _ => "" + attr.Value
            };
            value_value.IsEnabled = true;
            SaveState();
        };
        attributePanel3.Children.Add(value_value);

        attrPanel.Children.Add(attributePanel3);

        return attrPanel;
    }

    private static object? ParseValue(int valueType, string input)
    {
        switch (valueType)
        {
            case 0:
                return null;
            case 1:
                return true;
            case 2:
                return false;
            case 3:
                if (byte.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var byteValue))
                    return byteValue;
                break;
            case 4:
                if (sbyte.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var sbyteValue))
                    return sbyteValue;
                break;
            case 5:
                if (char.TryParse(input, out var charValue))
                    return charValue;
                break;
            case 6:
                if (short.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var shortValue))
                    return shortValue;
                break;
            case 7:
                if (ushort.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var ushortValue))
                    return ushortValue;
                break;
            case 8:
                if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var intValue))
                    return intValue;
                break;
            case 9:
                if (uint.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var uintValue))
                    return uintValue;
                break;
            case 10:
                if (long.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var longValue))
                    return longValue;
                break;
            case 11:
                if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var ulongValue))
                    return ulongValue;
                break;
            case 12:
                if (float.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var floatValue))
                    return floatValue;
                break;
            case 13:
                if (double.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var doubleValue))
                    return doubleValue;
                break;
            case 14:
                return input;
            case 15:
                var currentInput = input.Replace("-", "").Replace(" ", "");
                return Convert.FromHexString(currentInput + (currentInput.Length % 2 > 0 ? "0" : ""));
        }

        throw new Exception();
    }

    private object? ConvertValue(int valueType, object? before)
    {
        switch (valueType)
        {
            case 0:
                return null;
            case 1:
                return true;
            case 2:
                return false;
            case 3:
                byte byteValue = before switch
                {
                    bool bool_to_byte => bool_to_byte ? (byte)1 : (byte)0,
                    byte byte_to_byte => byte_to_byte,
                    sbyte sbyte_to_byte => (byte)sbyte_to_byte,
                    char char_to_byte => (byte)char_to_byte,
                    short short_to_byte => short_to_byte > byte.MaxValue ? byte.MaxValue :
                        short_to_byte < byte.MinValue ? byte.MinValue : (byte)short_to_byte,
                    ushort ushort_to_byte => ushort_to_byte > byte.MaxValue ? byte.MaxValue : (byte)ushort_to_byte,
                    int int_to_byte => int_to_byte > byte.MaxValue ? byte.MaxValue :
                        int_to_byte < byte.MinValue ? byte.MinValue : (byte)int_to_byte,
                    uint uint_to_byte => uint_to_byte > byte.MaxValue ? byte.MaxValue : (byte)uint_to_byte,
                    long long_to_byte => long_to_byte > byte.MaxValue ? byte.MaxValue :
                        long_to_byte < byte.MinValue ? byte.MinValue : (byte)long_to_byte,
                    ulong ulong_to_byte => ulong_to_byte > byte.MaxValue ? byte.MaxValue : (byte)ulong_to_byte,
                    float float_to_byte => float_to_byte > byte.MaxValue ? byte.MaxValue :
                        float_to_byte < byte.MinValue ? byte.MinValue : (byte)float_to_byte,
                    double double_to_byte => double_to_byte > byte.MaxValue ? byte.MaxValue :
                        double_to_byte < byte.MinValue ? byte.MinValue : (byte)double_to_byte,
                    string string_to_byte => byte.TryParse(string_to_byte, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedByte)
                        ? string_convertedByte
                        : (byte)0,
                    byte[] byte_array_to_byte => byte_array_to_byte.Length > 0 ? byte_array_to_byte[0] : (byte)0,
                    _ => 0
                };
                return byteValue;
            case 4:
                sbyte sbyteValue = before switch
                {
                    bool bool_to_sbyte => bool_to_sbyte ? (sbyte)1 : (sbyte)0,
                    byte byte_to_sbyte => (sbyte)byte_to_sbyte,
                    sbyte sbyte_to_sbyte => sbyte_to_sbyte,
                    char char_to_sbyte => char_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue : (sbyte)char_to_sbyte,
                    short short_to_sbyte => short_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        short_to_sbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)short_to_sbyte,
                    ushort ushort_to_sbyte => ushort_to_sbyte > sbyte.MaxValue
                        ? sbyte.MaxValue
                        : (sbyte)ushort_to_sbyte,
                    int int_to_sbyte => int_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        int_to_sbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)int_to_sbyte,
                    uint uint_to_sbyte => uint_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue : (sbyte)uint_to_sbyte,
                    long long_to_sbyte => long_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        long_to_sbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)long_to_sbyte,
                    ulong ulong_to_sbyte => ulong_to_sbyte > (ulong)sbyte.MaxValue
                        ? sbyte.MaxValue
                        : (sbyte)ulong_to_sbyte,
                    float float_to_sbyte => float_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        float_to_sbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)float_to_sbyte,
                    double double_to_sbyte => double_to_sbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        double_to_sbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)double_to_sbyte,
                    string string_to_sbyte => sbyte.TryParse(string_to_sbyte, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedSByte)
                        ? string_convertedSByte
                        : (sbyte)0,
                    byte[] byte_array_to_sbyte => byte_array_to_sbyte.Length > 0
                        ? (sbyte)byte_array_to_sbyte[0]
                        : (sbyte)0,
                    _ => 0
                };
                return sbyteValue;
            case 5:
                var charValue = before switch
                {
                    bool bool_to_char => bool_to_char ? (char)1 : (char)0,
                    byte byte_to_char => (char)byte_to_char,
                    sbyte sbyte_to_char => (char)sbyte_to_char,
                    char char_to_char => char_to_char,
                    short short_to_char => short_to_char < char.MinValue ? char.MinValue : (char)short_to_char,
                    ushort ushort_to_char => ushort_to_char > char.MaxValue ? char.MaxValue : (char)ushort_to_char,
                    int int_to_char => int_to_char > char.MaxValue ? char.MaxValue :
                        int_to_char < char.MinValue ? char.MinValue : (char)int_to_char,
                    uint uint_to_char => uint_to_char > char.MaxValue ? char.MaxValue : (char)uint_to_char,
                    long long_to_char => long_to_char > char.MaxValue ? char.MaxValue :
                        long_to_char < char.MinValue ? char.MinValue : (char)long_to_char,
                    ulong ulong_to_char => ulong_to_char > char.MaxValue ? char.MaxValue : (char)ulong_to_char,
                    float float_to_char => float_to_char > char.MaxValue ? char.MaxValue :
                        float_to_char < char.MinValue ? char.MinValue : (char)float_to_char,
                    double double_to_char => double_to_char > char.MaxValue ? char.MaxValue :
                        double_to_char < char.MinValue ? char.MinValue : (char)double_to_char,
                    string string_to_char => char.TryParse(string_to_char, out var string_convertedChar)
                        ? string_convertedChar
                        : (char)0,
                    byte[] byte_array_to_char => byte_array_to_char.Length > 0 ? (char)byte_array_to_char[0] : (char)0,
                    _ => (char)0
                };
                return charValue;

            case 6:
                short shortValue = before switch
                {
                    bool bool_to_short => bool_to_short ? (short)1 : (short)0,
                    byte byte_to_short => byte_to_short,
                    sbyte sbyte_to_short => sbyte_to_short,
                    char char_to_short => char_to_short > short.MaxValue ? short.MaxValue : (short)char_to_short,
                    short short_to_short => short_to_short,
                    ushort ushort_to_short => ushort_to_short > short.MaxValue
                        ? short.MaxValue
                        : (short)ushort_to_short,
                    int int_to_short => int_to_short > short.MaxValue ? short.MaxValue :
                        int_to_short < short.MinValue ? short.MinValue : (short)int_to_short,
                    uint uint_to_short => uint_to_short > short.MaxValue ? short.MaxValue : (short)uint_to_short,
                    long long_to_short => long_to_short > short.MaxValue ? short.MaxValue :
                        long_to_short < short.MinValue ? short.MinValue : (short)long_to_short,
                    ulong ulong_to_short => ulong_to_short > (ulong)short.MaxValue
                        ? short.MaxValue
                        : (short)ulong_to_short,
                    float float_to_short => float_to_short > short.MaxValue ? short.MaxValue :
                        float_to_short < short.MinValue ? short.MinValue : (short)float_to_short,
                    double double_to_short => double_to_short > short.MaxValue ? short.MaxValue :
                        double_to_short < short.MinValue ? short.MinValue : (byte)double_to_short,
                    string string_to_short => short.TryParse(string_to_short, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedShort)
                        ? string_convertedShort
                        : (short)0,
                    byte[] byte_array_to_short => BitConverter.ToInt16(byte_array_to_short),
                    _ => 0
                };
                return shortValue;
            case 7:
                ushort ushortValue = before switch
                {
                    bool bool_to_ushort => bool_to_ushort ? (ushort)1 : (ushort)0,
                    byte byte_to_ushort => byte_to_ushort,
                    sbyte sbyte_to_ushort => (ushort)sbyte_to_ushort,
                    char char_to_ushort => char_to_ushort,
                    short short_to_ushort => short_to_ushort < ushort.MinValue
                        ? ushort.MinValue
                        : (ushort)short_to_ushort,
                    ushort ushort_to_ushort => ushort_to_ushort,
                    int int_to_ushort => int_to_ushort > ushort.MaxValue ? ushort.MaxValue :
                        int_to_ushort < ushort.MinValue ? ushort.MinValue : (ushort)int_to_ushort,
                    uint uint_to_ushort => uint_to_ushort > ushort.MaxValue ? ushort.MaxValue : (ushort)uint_to_ushort,
                    long long_to_ushort => long_to_ushort > ushort.MaxValue ? ushort.MaxValue :
                        long_to_ushort < ushort.MinValue ? ushort.MinValue : (ushort)long_to_ushort,
                    ulong ulong_to_ushort => ulong_to_ushort > ushort.MaxValue
                        ? ushort.MaxValue
                        : (ushort)ulong_to_ushort,
                    float float_to_ushort => float_to_ushort > ushort.MaxValue ? ushort.MaxValue :
                        float_to_ushort < ushort.MinValue ? ushort.MinValue : (ushort)float_to_ushort,
                    double double_to_ushort => double_to_ushort > ushort.MaxValue ? ushort.MaxValue :
                        double_to_ushort < ushort.MinValue ? ushort.MinValue : (byte)double_to_ushort,
                    string string_to_ushort => ushort.TryParse(string_to_ushort, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedUshort)
                        ? string_convertedUshort
                        : (ushort)0,
                    byte[] byte_array_to_ushort => BitConverter.ToUInt16(byte_array_to_ushort),
                    _ => 0
                };
                return ushortValue;
            case 8:
                var intValue = before switch
                {
                    bool bool_to_int => bool_to_int ? 1 : 0,
                    byte byte_to_int => byte_to_int,
                    sbyte sbyte_to_int => sbyte_to_int,
                    char char_to_int => char_to_int,
                    short short_to_int => short_to_int,
                    ushort int_to_ushort => int_to_ushort,
                    int int_to_int => int_to_int,
                    uint uint_to_int => uint_to_int > int.MaxValue ? int.MaxValue : (int)uint_to_int,
                    long long_to_int => long_to_int > int.MaxValue ? int.MaxValue :
                        long_to_int < int.MinValue ? int.MinValue : (int)long_to_int,
                    ulong ulong_to_int => ulong_to_int > int.MaxValue
                        ? int.MaxValue
                        : (int)ulong_to_int,
                    float float_to_int => float_to_int > int.MaxValue ? int.MaxValue :
                        float_to_int < int.MinValue ? int.MinValue : (int)float_to_int,
                    double double_to_int => double_to_int > int.MaxValue ? int.MaxValue :
                        double_to_int < int.MinValue ? int.MinValue : (byte)double_to_int,
                    string string_to_int => int.TryParse(string_to_int, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedInt)
                        ? string_convertedInt
                        : 0,
                    byte[] byte_array_to_int => BitConverter.ToInt32(byte_array_to_int),
                    _ => 0
                };
                return intValue;
            case 9:
                uint uintValue = before switch
                {
                    bool bool_to_uint => bool_to_uint ? (uint)1 : 0,
                    byte byte_to_uint => byte_to_uint,
                    sbyte sbyte_to_uint => (uint)sbyte_to_uint,
                    char char_to_uint => char_to_uint,
                    short short_to_uint => (uint)short_to_uint,
                    ushort uint_to_ushort => uint_to_ushort,
                    int int_to_uint => int_to_uint < uint.MinValue ? uint.MinValue : (uint)int_to_uint,
                    uint uint_to_uint => uint_to_uint,
                    long long_to_uint => long_to_uint > uint.MaxValue ? uint.MaxValue :
                        long_to_uint < uint.MinValue ? uint.MinValue : (uint)long_to_uint,
                    ulong ulong_to_uint => ulong_to_uint > uint.MaxValue
                        ? uint.MaxValue
                        : (uint)ulong_to_uint,
                    float float_to_uint => float_to_uint > uint.MaxValue ? uint.MaxValue :
                        float_to_uint < uint.MinValue ? uint.MinValue : (uint)float_to_uint,
                    double double_to_uint => double_to_uint > uint.MaxValue ? uint.MaxValue :
                        double_to_uint < uint.MinValue ? uint.MinValue : (byte)double_to_uint,
                    string string_to_uint => uint.TryParse(string_to_uint, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedUInt)
                        ? string_convertedUInt
                        : 0,
                    byte[] byte_array_to_uint => BitConverter.ToUInt32(byte_array_to_uint),
                    _ => 0
                };
                return uintValue;
            case 10:
                var longValue = before switch
                {
                    bool bool_to_long => bool_to_long ? 1 : 0,
                    byte byte_to_long => byte_to_long,
                    sbyte sbyte_to_long => sbyte_to_long,
                    char char_to_long => char_to_long,
                    short short_to_long => short_to_long,
                    ushort long_to_ushort => long_to_ushort,
                    int int_to_long => int_to_long,
                    uint uint_to_long => uint_to_long,
                    long long_to_long => long_to_long,
                    ulong ulong_to_long => ulong_to_long > long.MaxValue
                        ? long.MaxValue
                        : (long)ulong_to_long,
                    float float_to_long => float_to_long > long.MaxValue ? long.MaxValue :
                        float_to_long < long.MinValue ? long.MinValue : (long)float_to_long,
                    double double_to_long => double_to_long > long.MaxValue ? long.MaxValue :
                        double_to_long < long.MinValue ? long.MinValue : (byte)double_to_long,
                    string string_to_long => long.TryParse(string_to_long, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedLong)
                        ? string_convertedLong
                        : 0,
                    byte[] byte_array_to_long => BitConverter.ToInt64(byte_array_to_long),
                    _ => 0
                };
                return longValue;
            case 11:
                ulong ulongValue = before switch
                {
                    bool bool_to_ulong => bool_to_ulong ? 1 : (ulong)0,
                    byte byte_to_ulong => byte_to_ulong,
                    sbyte sbyte_to_ulong => (ulong)sbyte_to_ulong,
                    char char_to_ulong => char_to_ulong,
                    short short_to_ulong => (ulong)short_to_ulong,
                    ushort ulong_to_ushort => ulong_to_ushort,
                    int int_to_ulong => (ulong)int_to_ulong,
                    uint uint_to_ulong => uint_to_ulong,
                    long long_to_ulong => long_to_ulong < (long)ulong.MinValue ? ulong.MinValue : (ulong)long_to_ulong,
                    ulong ulong_to_ulong => ulong_to_ulong,
                    float float_to_ulong => float_to_ulong > ulong.MaxValue ? ulong.MaxValue :
                        float_to_ulong < ulong.MinValue ? ulong.MinValue : (ulong)float_to_ulong,
                    double double_to_ulong => double_to_ulong > ulong.MaxValue ? ulong.MaxValue :
                        double_to_ulong < ulong.MinValue ? ulong.MinValue : (byte)double_to_ulong,
                    string string_to_ulong => ulong.TryParse(string_to_ulong, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedUlong)
                        ? string_convertedUlong
                        : 0,
                    byte[] byte_array_to_ulong => BitConverter.ToUInt64(byte_array_to_ulong),
                    _ => 0
                };
                return ulongValue;
            case 12:
                var floatValue = before switch
                {
                    bool bool_to_float => bool_to_float ? 1 : 0,
                    byte byte_to_float => byte_to_float,
                    sbyte sbyte_to_float => sbyte_to_float,
                    char char_to_float => char_to_float,
                    short short_to_float => short_to_float,
                    ushort float_to_ushort => float_to_ushort,
                    int int_to_float => int_to_float,
                    uint uint_to_float => uint_to_float,
                    long long_to_float => long_to_float,
                    ulong ulong_to_float => ulong_to_float,
                    float float_to_float => float_to_float,
                    double double_to_float => double_to_float > float.MaxValue ? float.MaxValue :
                        double_to_float < float.MinValue ? float.MinValue : (byte)double_to_float,
                    string string_to_float => float.TryParse(string_to_float, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedFloat)
                        ? string_convertedFloat
                        : 0,
                    byte[] byte_array_to_float => BitConverter.ToSingle(byte_array_to_float),
                    _ => 0
                };
                return floatValue;
            case 13:
                var doubleValue = before switch
                {
                    bool bool_to_double => bool_to_double ? 1 : 0,
                    byte byte_to_double => byte_to_double,
                    sbyte sbyte_to_double => sbyte_to_double,
                    char char_to_double => char_to_double,
                    short short_to_double => short_to_double,
                    ushort double_to_ushort => double_to_ushort,
                    int int_to_double => int_to_double,
                    uint uint_to_double => uint_to_double,
                    long long_to_double => long_to_double,
                    ulong ulong_to_double => ulong_to_double,
                    float float_to_double => float_to_double,
                    double double_to_double => double_to_double,
                    string string_to_double => double.TryParse(string_to_double, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var string_convertedDouble)
                        ? string_convertedDouble
                        : 0,
                    byte[] byte_array_to_double => BitConverter.ToDouble(byte_array_to_double),
                    _ => 0
                };
                return doubleValue;
            case 14:
                var stringValue = before switch
                {
                    bool bool_to_string => bool_to_string ? "true" : "false",
                    byte byte_to_string => "" + byte_to_string,
                    sbyte byte_to_string => "" + byte_to_string,
                    char char_to_string => "" + char_to_string,
                    short short_to_string => "" + short_to_string,
                    ushort ushort_to_string => "" + ushort_to_string,
                    int int_to_string => "" + int_to_string,
                    uint uint_to_string => "" + uint_to_string,
                    long long_to_string => "" + long_to_string,
                    ulong ulong_to_string => "" + ulong_to_string,
                    float float_to_string => "" + float_to_string,
                    double double_to_string => "" + double_to_string,
                    string string_to_string => string_to_string,
                    byte[] byte_array_to_string => BitConverter.ToString(byte_array_to_string).Replace("-", " "),
                    _ => string.Empty
                };
                return stringValue;
            case 15:
                var byteArrayValue = before switch
                {
                    bool bool_to_byteArray => BitConverter.GetBytes(bool_to_byteArray),
                    byte byte_to_byteArray => [byte_to_byteArray],
                    sbyte byte_to_byteArray => [(byte)byte_to_byteArray],
                    char char_to_byteArray => BitConverter.GetBytes(char_to_byteArray),
                    short short_to_byteArray => BitConverter.GetBytes(short_to_byteArray),
                    ushort ushort_to_byteArray => BitConverter.GetBytes(ushort_to_byteArray),
                    int int_to_byteArray => BitConverter.GetBytes(int_to_byteArray),
                    uint uint_to_byteArray => BitConverter.GetBytes(uint_to_byteArray),
                    long long_to_byteArray => BitConverter.GetBytes(long_to_byteArray),
                    ulong ulong_to_byteArray => BitConverter.GetBytes(ulong_to_byteArray),
                    float float_to_byteArray => BitConverter.GetBytes(float_to_byteArray),
                    double double_to_byteArray => BitConverter.GetBytes(double_to_byteArray),
                    string string_to_byteArray => Encoding.Default.GetBytes(string_to_byteArray),
                    byte[] byte_array_to_byteArray => byte_array_to_byteArray,
                    _ => []
                };
                return byteArrayValue;
        }

        throw new Exception();
    }

    private int GetIndex(object? o)
    {
        return o switch
        {
            null => 0,
            true => 1,
            false => 2,
            byte => 3,
            sbyte => 4,
            char => 5,
            short => 6,
            ushort => 7,
            int => 8,
            uint => 9,
            long => 10,
            ulong => 11,
            float => 12,
            double => 13,
            string => 14,
            byte[] => 15,
            _ => throw new Exception()
        };
    }

// ReSharper disable UnusedParameter.Local
    private void NewNode(object? s, RoutedEventArgs e)
    {
        if (Nodes is null) return;
        if (Nodes.SelectedItem is TreeViewItem { Tag: FluxionNode parent } item)
        {
            var node = new FluxionNode { Name = Lang.Lang.Node_NewItem, Value = null };
            parent.Add(node);
            var new_item = new TreeViewItem
                { Header = Lang.Lang.Node_NewItem, Tag = node, IsExpanded = true };
            item.Items.Add(new_item);
            item.IsExpanded = true;
            Nodes.SelectedItem = new_item;
        }
        else
        {
            var root = new TreeViewItem
            {
                Header = Lang.Lang.Node_Root, Tag = new FluxionNode { Name = Lang.Lang.Node_Root, Value = null },
                IsExpanded = true
            };
            Nodes.Items.Add(root);
            Nodes.SelectedItem = root;
        }

        Saved = false;
        SaveState();
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable once UnusedParameter.Local
    private void NodeValueTypeChanged(object? s, SelectionChangedEventArgs e)
    {
        if (Nodes is null || NodeValue is null || s is not ComboBox cb ||
            Nodes.SelectedItem is not TreeViewItem { Tag: FluxionNode node }) return;
        NodeValue.IsEnabled = false;
        node.Value = ConvertValue(cb.SelectedIndex, node.Value);
        NodeValue.Text = node.Value switch
        {
            bool bool_value => bool_value ? "true" : "false",
            byte[] byte_array => BitConverter.ToString(byte_array).Replace("-", " "),
            _ => "" + node.Value
        };
        NodeValue.IsEnabled = true;
        SaveState();
    }

    // ReSharper disable UnusedParameter.Local
    private void NodeNameChanged(object? s, TextChangedEventArgs e)
    {
        if (Nodes is null || NodeName is null || !NodeName.IsEnabled ||
            Nodes.SelectedItem is not TreeViewItem { Tag: FluxionNode node } item) return;
        item.Header = NodeName.Text ?? "";
        node.Name = NodeName.Text ?? "";
        SaveState();
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable once UnusedParameter.Local
    private void NodeValueChanged(object? s, TextChangedEventArgs e)
    {
        if (s is not Control { IsEnabled: true } || NodeValue is null || NodeValueType is null ||
            Nodes?.SelectedItem is not TreeViewItem { Tag: FluxionNode node }) return;
        node.Value = ParseValue(NodeValueType.SelectedIndex, NodeValue.Text ?? "");
        SaveState();
    }

// ReSharper disable UnusedParameter.Local
    private void NodeSelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (Nodes?.SelectedItem is not TreeViewItem { Tag: FluxionNode node }) return;
        SetAllControls(false);
        if (NodeName is not null) NodeName.Text = node.Name;
        if (NodeValue is not null) NodeValue.Text = node.Value + "";
        if (NodeValueType is not null)
            NodeValueType.SelectedIndex = GetIndex(node.Value);
        if (Attributes is not null)
        {
            Attributes.Children.Clear();
            foreach (FluxionAttribute attr in node.Attributes)
                Attributes.Children.Add(GenerateAttributePanel(node, attr));
        }

        SaveState();

        SetAllControls(true);
    }
// ReSharper restore UnusedParameter.Local

    private void SetAllControls(bool isEnabled)
    {
        if (NodeName is not null) NodeName.IsEnabled = isEnabled;
        if (NodeValue is not null) NodeValue.IsEnabled = isEnabled;
        if (NodeValueType is not null) NodeValueType.IsEnabled = isEnabled;
        if (Attributes is not null) Attributes.IsEnabled = isEnabled;
    }

    private void SaveState()
    {
        Saved = false;
        if (AutoSave?.IsChecked is true && AutoSaveEncoding?.SelectedItem is ComboBoxItem { Tag: string encoding })
            Save(encoding, (byte)(AutoSaveVersion.SelectedIndex + 1));
    }

    private static Encoding GetEncoding(string encoding)
    {
        return encoding switch
        {
            "utf8" => Encoding.UTF8,
            "utf16" => Encoding.Unicode,
            "utf32" => Encoding.UTF32,
            _ => throw new Exception()
        };
    }

    private void Save(string encoding, byte version)
    {
        Save(GetEncoding(encoding), version);
    }

    private async void Save(Encoding encoding, byte version)
    {
        if (IsSaving || loadedFile is null) return;
        IsSaving = true;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SaveStatusText.Text = Lang.Lang.Status_SavingFile;
            SavingProgressPanel.IsVisible = true;
        });
        if (Nodes.Items[0] is not TreeViewItem { Tag: FluxionNode root }) return;
        switch (loadedFileCompression)
        {
            case "none":
                root.Write(await loadedFile.OpenWriteAsync(), encoding, version);
                break;
        }

        Saved = true;
        IsSaving = false;
        await Dispatcher.UIThread.InvokeAsync(() => SavingProgressPanel.IsVisible = false);
    }


// ReSharper disable UnusedParameter.Local
    private void Init(object? sender, VisualTreeAttachmentEventArgs e)
    {
        LoadSettings(out var encoding, out var autoSave, out var version);
        if (AutoSaveEncoding is not null) AutoSaveEncoding.SelectedIndex = encoding;
        if (AutoSave is not null) AutoSave.IsChecked = autoSave;
        if (AutoSaveVersion is not null) AutoSaveVersion.SelectedIndex = version - 1;
        IsSettingsSaving = false;
    }
// ReSharper restore UnusedParameter.Local

    private void LoadSettings(out int encoding, out bool autoSave, out int ver)
    {
        encoding = 0;
        autoSave = false;
        ver = 1;
        if (!Directory.Exists(AppFolder) || !File.Exists(AppSettingsFile)) return;
        var root = Fluxion.Read(AppSettingsFile);
        foreach (FluxionAttribute attr in root.Attributes)
            switch (attr.Name.ToLowerInvariant())
            {
                case "autosave":
                    if (attr.Value is bool save)
                        autoSave = save;
                    break;
                case "encoding":
                    if (attr.Value is int e)
                        encoding = e;
                    break;

                case "version":
                    if (attr.Value is int v)
                        ver = v;
                    break;
            }
    }

    private void SaveSettings()
    {
        if (IsSettingsSaving) return;
        if (!Directory.Exists(AppFolder)) Directory.CreateDirectory(AppFolder);
        IsSettingsSaving = true;
        var root = new FluxionNode { Name = "FluxionViewer", Value = GetAppVersion() };
        if (AutoSave is not null)
            root.Attributes.Add(new FluxionAttribute { Name = "autosave", Value = AutoSave.IsChecked is true });
        if (AutoSaveEncoding is not null)
            root.Attributes.Add(new FluxionAttribute { Name = "encoding", Value = AutoSaveEncoding.SelectedIndex });
        if (AutoSaveVersion is not null)
            root.Attributes.Add(new FluxionAttribute { Name = "version", Value = AutoSaveVersion.SelectedIndex });
        using Stream fs = File.Exists(AppSettingsFile)
            ? new FileStream(AppSettingsFile, FileMode.Truncate, FileAccess.ReadWrite, FileShare.ReadWrite)
            : File.Create(AppSettingsFile);
        root.Write(fs, Encoding.UTF8, 2);

        IsSettingsSaving = false;
    }

    internal void Close()
    {
        if (SavingProgressPanel.IsVisible) return;
        if (Saved)
        {
            while (IsSettingsSaving)
            {
            } // Wait until settings are saved.

            if (Parent is not MainWindow mw) return;
            mw.AllowClose = true;
            mw.Close();
        }
        else
        {
            if (CloseApp is not Control { Tag: Control c }) return;
            DialogHost.Show(c);
        }
    }

    private async void SaveAndExit(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SaveFile(sender, e);
            if (Parent is not MainWindow mw) return;
            mw.AllowClose = true;
            mw.Close();
        });
    }

    // ReSharper disable UnusedParameter.Local
    private void JustExit(object? sender, RoutedEventArgs e)
    {
        if (Parent is not MainWindow mw) return;
        mw.AllowClose = true;
        mw.Close();
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable once UnusedParameter.Local
    private async void DialogButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Control c }) return;
        await DialogHost.Show(c);
    }

// ReSharper disable UnusedParameter.Local
    private void RemoveNode(object? sender, RoutedEventArgs e)
    {
        if (Nodes.SelectedItem is not TreeViewItem item) return;
        switch (item.Parent)
        {
            case TreeView tv:
                tv.Items.Remove(item);
                break;
            case TreeViewItem parent:
                parent.Items.Remove(item);
                break;
        }

        SaveState();
    }
    // ReSharper restore UnusedParameter.Local


// ReSharper disable UnusedParameter.Local
    private async void OpenFile(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Parent is not TopLevel topLevel) return;
            if (!topLevel.StorageProvider.CanOpen) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Lang.Lang.Open_Title,
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            });

            if (files.Count <= 0) return;
            SavingProgressPanel.IsVisible = true;
            SaveStatusText.Text = Lang.Lang.Status_LoadingFile;
            ReadFluxionNode(Fluxion.Read(await files[0].OpenReadAsync()), null);
            loadedFile = files[0];
            loadedFileCompression = "none";
            SavingProgressPanel.IsVisible = false;
        });
    }
// ReSharper restore UnusedParameter.Local

// ReSharper disable UnusedParameter.Local
    private async void LoadCompressed(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Parent is not TopLevel topLevel) return;
            if (LoadCompression.SelectedItem is not ComboBoxItem { Tag: string compression }) return;
            if (!topLevel.StorageProvider.CanOpen) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Lang.Lang.OpenCompressed_Title,
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            });

            if (files.Count <= 0) return;
            SavingProgressPanel.IsVisible = true;
            SaveStatusText.Text = Lang.Lang.Status_LoadingFile;
            loadedFile = files[0];
            loadedFileCompression = compression.ToLowerInvariant();
            await using var stream = GetStream(await files[0].OpenReadAsync(), compression.ToLowerInvariant(),
                false);
            ReadFluxionNode(Fluxion.Read(stream), null);

            SavingProgressPanel.IsVisible = false;
        });
    }
    // ReSharper restore UnusedParameter.Local

    private static Stream GetStream(Stream stream, string compression, bool mode)
    {
        return compression switch
        {
            "none" => stream,
            "gzip" => new GZipStream(stream, mode ? CompressionMode.Compress : CompressionMode.Decompress),
            "brotli" => new BrotliStream(stream, mode ? CompressionMode.Compress : CompressionMode.Decompress),
            "deflate" => new DeflateStream(stream, mode ? CompressionMode.Compress : CompressionMode.Decompress),
            "zlib" => new ZLibStream(stream, mode ? CompressionMode.Compress : CompressionMode.Decompress),
            _ => throw new Exception()
        };
    }

    private async void SaveFile(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (loadedFile is null)
                SaveAsFile(new SenderCombo { Encoding = SaveFileEncoding, Version = SaveVersionSelect }, e);
            if (SaveFileEncoding?.SelectedItem is not ComboBox { Tag: string encoding }) return;
            Save(encoding, (byte)(SaveVersionSelect.SelectedIndex + 1));
        });
    }

    private void SaveAsFileClicked(object? sender, RoutedEventArgs e)
    {
        SaveAsFile(new SenderCombo { Encoding = SaveFileAsEncoding, Version = SaveAsVersionSelect }, e);
    }

    // ReSharper disable UnusedParameter.Local
    private async void SaveAsFile(object? sender, RoutedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Parent is not TopLevel topLevel) return;
            if (SaveCompression.SelectedItem is not ComboBoxItem { Tag: string compression } ||
                Nodes.Items[0] is not TreeViewItem { Tag: FluxionNode root } || sender is not SenderCombo sc ||
                sc.Encoding is not { } encodingCB || sc.Version is not { } versionCB ||
                encodingCB.SelectedItem is not ComboBox { Tag: string encoding }) return;
            if (!topLevel.StorageProvider.CanSave) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Lang.Lang.SaveAs_Title,
                DefaultExtension = "*.flx",
                FileTypeChoices = fileTypes,
                ShowOverwritePrompt = true
            });

            if (file is null) return;
            loadedFile = file;
            var file_encoding = GetEncoding(encoding);
            await using var stream = GetStream(await file.OpenReadAsync(), compression.ToLowerInvariant(),
                true);
            IsSaving = true;
            await Dispatcher.UIThread.InvokeAsync(() => SavingProgressPanel.IsVisible = true);
            root.Write(stream, file_encoding, (byte)(versionCB.SelectedIndex + 1));
            Saved = true;
            IsSaving = false;
            await Dispatcher.UIThread.InvokeAsync(() => SavingProgressPanel.IsVisible = false);
        });
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable UnusedParameter.Local
    private void CutNode(object? sender, RoutedEventArgs e)
    {
        if (Nodes?.SelectedItem is not TreeViewItem
            {
                Tag: FluxionNode node, Parent: TreeViewItem { Tag: FluxionNode root } itemParent
            } item) return;
        itemParent.Items.Remove(item);
        root.Remove(node);
        Clipboard = item;
        SaveState();
    }
    // ReSharper restore UnusedParameter.Local

    // ReSharper disable UnusedParameter.Local
    private void CopyNode(object? sender, RoutedEventArgs e)
    {
        if (Nodes?.SelectedItem is not TreeViewItem { Tag: FluxionNode node } item) return;
        Clipboard = CloneItem(item, node);
    }
// ReSharper restore UnusedParameter.Local

    private TreeViewItem CloneItem(TreeViewItem item, FluxionNode node)
    {
        var cloned = CloneNode(node);
        var clone = new TreeViewItem { Header = item.Header, Tag = cloned };
        foreach (var sub_node in cloned.Children) ReadFluxionNode(sub_node, item);

        return clone;
    }

    private FluxionNode CloneNode(FluxionNode node)
    {
        var clone = new FluxionNode { Name = node.Name, Value = node.Value };
        foreach (FluxionAttribute attr in node.Attributes)
            clone.Attributes.Add(new FluxionAttribute { Name = attr.Name, Value = attr.Value });
        foreach (var sub_node in node.Children) clone.Add(CloneNode(sub_node));
        return clone;
    }

// ReSharper disable UnusedParameter.Local
    private void PasteNode(object? sender, RoutedEventArgs e)
    {
        if (Nodes?.SelectedItem is not TreeViewItem { Tag: FluxionNode newParent } itemParent ||
            Clipboard is not TreeViewItem { Tag: FluxionNode node } item) return;
        itemParent.Items.Add(item);
        newParent.Add(node);
        Clipboard = null;
        SaveState();
    }
    // ReSharper restore UnusedParameter.Local

    private void ReadFluxionNode(FluxionNode node, TreeViewItem? parent)
    {
        TreeViewItem item = new() { Tag = node, Header = node.Name };
        if (node.Count > 0)
            foreach (var sub_node in node.Children)
                ReadFluxionNode(sub_node, item);
        if (parent is null)
            Nodes.Items.Add(item);
        else
            parent.Items.Add(item);
    }

// ReSharper disable UnusedParameter.Local
    private void AutoSaveEncoding_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SaveSettings();
    }
    // ReSharper restore UnusedParameter.Local


// ReSharper disable UnusedParameter.Local
    private void AutoSaveVersion_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SaveSettings();
    }
    // ReSharper restore UnusedParameter.Local

    private class SenderCombo
    {
        public ComboBox? Encoding { get; init; }
        public ComboBox? Version { get; init; }
    }
}