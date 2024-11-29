using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

    internal static readonly StyledProperty<bool> CheckingForUpdatesProperty =
        AvaloniaProperty.Register<MainView, bool>(nameof(CheckingForUpdates), true);

    internal static readonly StyledProperty<bool> UpToDateProperty =
        AvaloniaProperty.Register<MainView, bool>(nameof(UpToDate));

    internal static readonly StyledProperty<bool> UpdateAvailableProperty =
        AvaloniaProperty.Register<MainView, bool>(nameof(UpdateAvailable));

    private static readonly FilePickerFileType[] FileTypes =
    [
        new(Lang.Lang.FileExtName)
        {
            Patterns = "*.flx".Split('|'),
            MimeTypes = "application/fluxion".Split('|'),
            AppleUniformTypeIdentifiers = "com.haltroy.fluxion".Split('|')
        },
        FilePickerFileTypes.All
    ];

    private bool _isSaving;

    private bool _isSettingsSaving = true;

    private IStorageFile? _loadedFile;
    private string _loadedFileCompression = "none";
    private bool _saved = true;

    public MainView()
    {
        InitializeComponent();
        Version.Text =
            "v"
            + GetAppVersion();
        if (Design.IsDesignMode) ReadFluxionNode(GenerateExampleFluxionNode(), null);
        CheckForUpdates();
    }

    public bool CheckingForUpdates
    {
        get => GetValue(CheckingForUpdatesProperty);
        set => SetValue(CheckingForUpdatesProperty, value);
    }

    public bool UpToDate
    {
        get => GetValue(UpToDateProperty);
        set => SetValue(UpToDateProperty, value);
    }

    public bool UpdateAvailable
    {
        get => GetValue(UpdateAvailableProperty);
        set => SetValue(UpdateAvailableProperty, value);
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

    private static FluxionNode GenerateExampleFluxionNode()
    {
        var node = new FluxionNode { Name = "Root" };
        node.Add(new FluxionNode { Name = "TestItem", Value = "This is a test." });
        var subNode = new FluxionNode { Name = "NodeWithAttr", Value = 25 };
        subNode.Attributes.Add(new FluxionAttribute
            { Name = "TestAttr", Value = new byte[] { 0, 1, 2, 4, 8, 16, 64, 128, 255 } });
        node.Add(subNode);
        return node;
    }

    private async void CheckForUpdates()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Design.IsDesignMode)
                {
                    CheckingForUpdates = false;
                    UpToDate = true;
                    UpdateAvailable = false;
                    return;
                }

                const string updateCheckLocation =
                    "https://raw.githubusercontent.com/Haltroy/FluxionViewer/refs/heads/distribution/version";
                using var httpClient = new HttpClient();
                var responseTask = Task.Run(() => httpClient.GetAsync(updateCheckLocation));
                var response = await responseTask;
                response.EnsureSuccessStatusCode();
                var versionText = await response.Content.ReadAsStringAsync();
                var version = new Version(versionText);
                if (version.CompareTo(GetVersion()) > 0)
                {
                    CheckingForUpdates = false;
                    UpToDate = false;
                    UpdateAvailable = true;
                    return;
                }

                CheckingForUpdates = false;
                UpToDate = true;
                UpdateAvailable = false;
            });
        }
        catch (Exception)
        {
            // ignored
        }
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

    private static Version GetVersion()
    {
        return Assembly.GetExecutingAssembly() is { } ass
               && ass.GetName() is { } name
               && name.Version != null
            ? name.Version
            : new Version();
    }

    public async void LoadArgs(string[] args)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (args.Length < 1) return;
                var file = args[0];
                if (args.Length >= 3)
                    _loadedFileCompression = args[Array.IndexOf(args, "--compression") + 1].ToLowerInvariant();
                if (Parent is not TopLevel topLevel) return;
                _loadedFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(file);
                if (_loadedFile is null) return;
                SavingProgressPanel.IsVisible = true;
                SaveStatusText.Text = Lang.Lang.Status_LoadingFile;
                await using Stream stream = _loadedFileCompression.ToLowerInvariant() switch
                {
                    "gzip" => new GZipStream(await _loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                    "brotli" => new BrotliStream(await _loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                    "deflate" => new DeflateStream(await _loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                    "zlib" => new ZLibStream(await _loadedFile.OpenReadAsync(), CompressionMode.Decompress),
                    _ => throw new Exception()
                };
                ReadFluxionNode(Fluxion.Read(stream), null);
                SavingProgressPanel.IsVisible = false;
            });
        }
        catch (Exception)
        {
            // ignored
        }
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

        TextBox nameValue = new() { Margin = new Thickness(5, 0), Text = attr.Name };
        attributePanel2.Children.Add(nameValue);

        attrPanel.Children.Add(attributePanel2);

        DockPanel attributePanel3 = new() { LastChildFill = true };
        TextBlock value = new() { Text = Lang.Lang.Attr_Value, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(name, Dock.Left);
        attributePanel3.Children.Add(value);

        TextBox valueValue = new()
            { Margin = new Thickness(5, 0), Text = attr.Value + "", IsEnabled = attributeValueType.SelectedIndex > 2 };

        nameValue.TextChanged += (_, _) =>
        {
            if (Attributes is null || !Attributes.IsEnabled) return;
            attr.Name = nameValue.Text ?? string.Empty;
            SaveState();
        };
        valueValue.TextChanged += (_, _) =>
        {
            if (Attributes is null || !Attributes.IsEnabled) return;
            if (!valueValue.IsEnabled) return;
            attr.Value = ParseValue(attributeValueType.SelectedIndex, valueValue.Text ?? string.Empty);
            SaveState();
        };
        attributeValueType.SelectionChanged += (_, _) =>
        {
            if (Attributes is null || !Attributes.IsEnabled) return;
            if (attributeValueType.SelectedIndex < 3)
            {
                valueValue.IsEnabled = false;
                return;
            }

            attr.Value = ConvertValue(attributeValueType.SelectedIndex, attr.Value);
            valueValue.IsEnabled = false;
            valueValue.Text = attr.Value switch
            {
                bool boolValue => boolValue ? "true" : "false",
                byte[] byteArray => BitConverter.ToString(byteArray).Replace("-", " "),
                _ => "" + attr.Value
            };
            valueValue.IsEnabled = true;
            SaveState();
        };
        attributePanel3.Children.Add(valueValue);

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
                    bool boolToByte => boolToByte ? (byte)1 : (byte)0,
                    byte byteToByte => byteToByte,
                    sbyte sbyteToByte => (byte)sbyteToByte,
                    char charToByte => (byte)charToByte,
                    short shortToByte => shortToByte > byte.MaxValue ? byte.MaxValue :
                        shortToByte < byte.MinValue ? byte.MinValue : (byte)shortToByte,
                    ushort ushortToByte => ushortToByte > byte.MaxValue ? byte.MaxValue : (byte)ushortToByte,
                    int intToByte => intToByte > byte.MaxValue ? byte.MaxValue :
                        intToByte < byte.MinValue ? byte.MinValue : (byte)intToByte,
                    uint uintToByte => uintToByte > byte.MaxValue ? byte.MaxValue : (byte)uintToByte,
                    long longToByte => longToByte > byte.MaxValue ? byte.MaxValue :
                        longToByte < byte.MinValue ? byte.MinValue : (byte)longToByte,
                    ulong ulongToByte => ulongToByte > byte.MaxValue ? byte.MaxValue : (byte)ulongToByte,
                    float floatToByte => floatToByte > byte.MaxValue ? byte.MaxValue :
                        floatToByte < byte.MinValue ? byte.MinValue : (byte)floatToByte,
                    double doubleToByte => doubleToByte > byte.MaxValue ? byte.MaxValue :
                        doubleToByte < byte.MinValue ? byte.MinValue : (byte)doubleToByte,
                    string stringToByte => byte.TryParse(stringToByte, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedByte)
                        ? stringConvertedByte
                        : (byte)0,
                    byte[] byteArrayToByte => byteArrayToByte.Length > 0 ? byteArrayToByte[0] : (byte)0,
                    _ => 0
                };
                return byteValue;
            case 4:
                sbyte sbyteValue = before switch
                {
                    bool boolToSbyte => boolToSbyte ? (sbyte)1 : (sbyte)0,
                    byte byteToSbyte => (sbyte)byteToSbyte,
                    sbyte sbyteToSbyte => sbyteToSbyte,
                    char charToSbyte => charToSbyte > sbyte.MaxValue ? sbyte.MaxValue : (sbyte)charToSbyte,
                    short shortToSbyte => shortToSbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        shortToSbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)shortToSbyte,
                    ushort ushortToSbyte => ushortToSbyte > sbyte.MaxValue
                        ? sbyte.MaxValue
                        : (sbyte)ushortToSbyte,
                    int intToSbyte => intToSbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        intToSbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)intToSbyte,
                    uint uintToSbyte => uintToSbyte > sbyte.MaxValue ? sbyte.MaxValue : (sbyte)uintToSbyte,
                    long longToSbyte => longToSbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        longToSbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)longToSbyte,
                    ulong ulongToSbyte => ulongToSbyte > (ulong)sbyte.MaxValue
                        ? sbyte.MaxValue
                        : (sbyte)ulongToSbyte,
                    float floatToSbyte => floatToSbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        floatToSbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)floatToSbyte,
                    double doubleToSbyte => doubleToSbyte > sbyte.MaxValue ? sbyte.MaxValue :
                        doubleToSbyte < sbyte.MinValue ? sbyte.MinValue : (sbyte)doubleToSbyte,
                    string stringToSbyte => sbyte.TryParse(stringToSbyte, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedSByte)
                        ? stringConvertedSByte
                        : (sbyte)0,
                    byte[] byteArrayToSbyte => byteArrayToSbyte.Length > 0
                        ? (sbyte)byteArrayToSbyte[0]
                        : (sbyte)0,
                    _ => 0
                };
                return sbyteValue;
            case 5:
                var charValue = before switch
                {
                    bool boolToChar => boolToChar ? (char)1 : (char)0,
                    byte byteToChar => (char)byteToChar,
                    sbyte sbyteToChar => (char)sbyteToChar,
                    char charToChar => charToChar,
                    short shortToChar => shortToChar < char.MinValue ? char.MinValue : (char)shortToChar,
                    ushort ushortToChar => ushortToChar > char.MaxValue ? char.MaxValue : (char)ushortToChar,
                    int intToChar => intToChar > char.MaxValue ? char.MaxValue :
                        intToChar < char.MinValue ? char.MinValue : (char)intToChar,
                    uint uintToChar => uintToChar > char.MaxValue ? char.MaxValue : (char)uintToChar,
                    long longToChar => longToChar > char.MaxValue ? char.MaxValue :
                        longToChar < char.MinValue ? char.MinValue : (char)longToChar,
                    ulong ulongToChar => ulongToChar > char.MaxValue ? char.MaxValue : (char)ulongToChar,
                    float floatToChar => floatToChar > char.MaxValue ? char.MaxValue :
                        floatToChar < char.MinValue ? char.MinValue : (char)floatToChar,
                    double doubleToChar => doubleToChar > char.MaxValue ? char.MaxValue :
                        doubleToChar < char.MinValue ? char.MinValue : (char)doubleToChar,
                    string stringToChar => char.TryParse(stringToChar, out var stringConvertedChar)
                        ? stringConvertedChar
                        : (char)0,
                    byte[] byteArrayToChar => byteArrayToChar.Length > 0 ? (char)byteArrayToChar[0] : (char)0,
                    _ => (char)0
                };
                return charValue;

            case 6:
                short shortValue = before switch
                {
                    bool boolToShort => boolToShort ? (short)1 : (short)0,
                    byte byteToShort => byteToShort,
                    sbyte sbyteToShort => sbyteToShort,
                    char charToShort => charToShort > short.MaxValue ? short.MaxValue : (short)charToShort,
                    short shortToShort => shortToShort,
                    ushort ushortToShort => ushortToShort > short.MaxValue
                        ? short.MaxValue
                        : (short)ushortToShort,
                    int intToShort => intToShort > short.MaxValue ? short.MaxValue :
                        intToShort < short.MinValue ? short.MinValue : (short)intToShort,
                    uint uintToShort => uintToShort > short.MaxValue ? short.MaxValue : (short)uintToShort,
                    long longToShort => longToShort > short.MaxValue ? short.MaxValue :
                        longToShort < short.MinValue ? short.MinValue : (short)longToShort,
                    ulong ulongToShort => ulongToShort > (ulong)short.MaxValue
                        ? short.MaxValue
                        : (short)ulongToShort,
                    float floatToShort => floatToShort > short.MaxValue ? short.MaxValue :
                        floatToShort < short.MinValue ? short.MinValue : (short)floatToShort,
                    double doubleToShort => doubleToShort > short.MaxValue ? short.MaxValue :
                        doubleToShort < short.MinValue ? short.MinValue : (byte)doubleToShort,
                    string stringToShort => short.TryParse(stringToShort, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedShort)
                        ? stringConvertedShort
                        : (short)0,
                    byte[] byteArrayToShort => BitConverter.ToInt16(byteArrayToShort),
                    _ => 0
                };
                return shortValue;
            case 7:
                ushort ushortValue = before switch
                {
                    bool boolToUshort => boolToUshort ? (ushort)1 : (ushort)0,
                    byte byteToUshort => byteToUshort,
                    sbyte sbyteToUshort => (ushort)sbyteToUshort,
                    char charToUshort => charToUshort,
                    short shortToUshort => shortToUshort < ushort.MinValue
                        ? ushort.MinValue
                        : (ushort)shortToUshort,
                    ushort ushortToUshort => ushortToUshort,
                    int intToUshort => intToUshort > ushort.MaxValue ? ushort.MaxValue :
                        intToUshort < ushort.MinValue ? ushort.MinValue : (ushort)intToUshort,
                    uint uintToUshort => uintToUshort > ushort.MaxValue ? ushort.MaxValue : (ushort)uintToUshort,
                    long longToUshort => longToUshort > ushort.MaxValue ? ushort.MaxValue :
                        longToUshort < ushort.MinValue ? ushort.MinValue : (ushort)longToUshort,
                    ulong ulongToUshort => ulongToUshort > ushort.MaxValue
                        ? ushort.MaxValue
                        : (ushort)ulongToUshort,
                    float floatToUshort => floatToUshort > ushort.MaxValue ? ushort.MaxValue :
                        floatToUshort < ushort.MinValue ? ushort.MinValue : (ushort)floatToUshort,
                    double doubleToUshort => doubleToUshort > ushort.MaxValue ? ushort.MaxValue :
                        doubleToUshort < ushort.MinValue ? ushort.MinValue : (byte)doubleToUshort,
                    string stringToUshort => ushort.TryParse(stringToUshort, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedUshort)
                        ? stringConvertedUshort
                        : (ushort)0,
                    byte[] byteArrayToUshort => BitConverter.ToUInt16(byteArrayToUshort),
                    _ => 0
                };
                return ushortValue;
            case 8:
                var intValue = before switch
                {
                    bool boolToInt => boolToInt ? 1 : 0,
                    byte byteToInt => byteToInt,
                    sbyte sbyteToInt => sbyteToInt,
                    char charToInt => charToInt,
                    short shortToInt => shortToInt,
                    ushort intToUshort => intToUshort,
                    int intToInt => intToInt,
                    uint uintToInt => uintToInt > int.MaxValue ? int.MaxValue : (int)uintToInt,
                    long longToInt => longToInt > int.MaxValue ? int.MaxValue :
                        longToInt < int.MinValue ? int.MinValue : (int)longToInt,
                    ulong ulongToInt => ulongToInt > int.MaxValue
                        ? int.MaxValue
                        : (int)ulongToInt,
                    float floatToInt => floatToInt > int.MaxValue ? int.MaxValue :
                        floatToInt < int.MinValue ? int.MinValue : (int)floatToInt,
                    double doubleToInt => doubleToInt > int.MaxValue ? int.MaxValue :
                        doubleToInt < int.MinValue ? int.MinValue : (byte)doubleToInt,
                    string stringToInt => int.TryParse(stringToInt, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedInt)
                        ? stringConvertedInt
                        : 0,
                    byte[] byteArrayToInt => BitConverter.ToInt32(byteArrayToInt),
                    _ => 0
                };
                return intValue;
            case 9:
                uint uintValue = before switch
                {
                    bool boolToUint => boolToUint ? (uint)1 : 0,
                    byte byteToUint => byteToUint,
                    sbyte sbyteToUint => (uint)sbyteToUint,
                    char charToUint => charToUint,
                    short shortToUint => (uint)shortToUint,
                    ushort uintToUshort => uintToUshort,
                    int intToUint => intToUint < uint.MinValue ? uint.MinValue : (uint)intToUint,
                    uint uintToUint => uintToUint,
                    long longToUint => longToUint > uint.MaxValue ? uint.MaxValue :
                        longToUint < uint.MinValue ? uint.MinValue : (uint)longToUint,
                    ulong ulongToUint => ulongToUint > uint.MaxValue
                        ? uint.MaxValue
                        : (uint)ulongToUint,
                    float floatToUint => floatToUint > uint.MaxValue ? uint.MaxValue :
                        floatToUint < uint.MinValue ? uint.MinValue : (uint)floatToUint,
                    double doubleToUint => doubleToUint > uint.MaxValue ? uint.MaxValue :
                        doubleToUint < uint.MinValue ? uint.MinValue : (byte)doubleToUint,
                    string stringToUint => uint.TryParse(stringToUint, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedUInt)
                        ? stringConvertedUInt
                        : 0,
                    byte[] byteArrayToUint => BitConverter.ToUInt32(byteArrayToUint),
                    _ => 0
                };
                return uintValue;
            case 10:
                var longValue = before switch
                {
                    bool boolToLong => boolToLong ? 1 : 0,
                    byte byteToLong => byteToLong,
                    sbyte sbyteToLong => sbyteToLong,
                    char charToLong => charToLong,
                    short shortToLong => shortToLong,
                    ushort longToUshort => longToUshort,
                    int intToLong => intToLong,
                    uint uintToLong => uintToLong,
                    long longToLong => longToLong,
                    ulong ulongToLong => ulongToLong > long.MaxValue
                        ? long.MaxValue
                        : (long)ulongToLong,
                    float floatToLong => floatToLong > long.MaxValue ? long.MaxValue :
                        floatToLong < long.MinValue ? long.MinValue : (long)floatToLong,
                    double doubleToLong => doubleToLong > long.MaxValue ? long.MaxValue :
                        doubleToLong < long.MinValue ? long.MinValue : (byte)doubleToLong,
                    string stringToLong => long.TryParse(stringToLong, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedLong)
                        ? stringConvertedLong
                        : 0,
                    byte[] byteArrayToLong => BitConverter.ToInt64(byteArrayToLong),
                    _ => 0
                };
                return longValue;
            case 11:
                ulong ulongValue = before switch
                {
                    bool boolToUlong => boolToUlong ? 1 : (ulong)0,
                    byte byteToUlong => byteToUlong,
                    sbyte sbyteToUlong => (ulong)sbyteToUlong,
                    char charToUlong => charToUlong,
                    short shortToUlong => (ulong)shortToUlong,
                    ushort ulongToUshort => ulongToUshort,
                    int intToUlong => (ulong)intToUlong,
                    uint uintToUlong => uintToUlong,
                    long longToUlong => longToUlong < (long)ulong.MinValue ? ulong.MinValue : (ulong)longToUlong,
                    ulong ulongToUlong => ulongToUlong,
                    float floatToUlong => floatToUlong > ulong.MaxValue ? ulong.MaxValue :
                        floatToUlong < ulong.MinValue ? ulong.MinValue : (ulong)floatToUlong,
                    double doubleToUlong => doubleToUlong > ulong.MaxValue ? ulong.MaxValue :
                        doubleToUlong < ulong.MinValue ? ulong.MinValue : (byte)doubleToUlong,
                    string stringToUlong => ulong.TryParse(stringToUlong, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedUlong)
                        ? stringConvertedUlong
                        : 0,
                    byte[] byteArrayToUlong => BitConverter.ToUInt64(byteArrayToUlong),
                    _ => 0
                };
                return ulongValue;
            case 12:
                var floatValue = before switch
                {
                    bool boolToFloat => boolToFloat ? 1 : 0,
                    byte byteToFloat => byteToFloat,
                    sbyte sbyteToFloat => sbyteToFloat,
                    char charToFloat => charToFloat,
                    short shortToFloat => shortToFloat,
                    ushort floatToUshort => floatToUshort,
                    int intToFloat => intToFloat,
                    uint uintToFloat => uintToFloat,
                    long longToFloat => longToFloat,
                    ulong ulongToFloat => ulongToFloat,
                    float floatToFloat => floatToFloat,
                    double doubleToFloat => doubleToFloat > float.MaxValue ? float.MaxValue :
                        doubleToFloat < float.MinValue ? float.MinValue : (byte)doubleToFloat,
                    string stringToFloat => float.TryParse(stringToFloat, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedFloat)
                        ? stringConvertedFloat
                        : 0,
                    byte[] byteArrayToFloat => BitConverter.ToSingle(byteArrayToFloat),
                    _ => 0
                };
                return floatValue;
            case 13:
                var doubleValue = before switch
                {
                    bool boolToDouble => boolToDouble ? 1 : 0,
                    byte byteToDouble => byteToDouble,
                    sbyte sbyteToDouble => sbyteToDouble,
                    char charToDouble => charToDouble,
                    short shortToDouble => shortToDouble,
                    ushort doubleToUshort => doubleToUshort,
                    int intToDouble => intToDouble,
                    uint uintToDouble => uintToDouble,
                    long longToDouble => longToDouble,
                    ulong ulongToDouble => ulongToDouble,
                    float floatToDouble => floatToDouble,
                    double doubleToDouble => doubleToDouble,
                    string stringToDouble => double.TryParse(stringToDouble, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var stringConvertedDouble)
                        ? stringConvertedDouble
                        : 0,
                    byte[] byteArrayToDouble => BitConverter.ToDouble(byteArrayToDouble),
                    _ => 0
                };
                return doubleValue;
            case 14:
                var stringValue = before switch
                {
                    bool boolToString => boolToString ? "true" : "false",
                    byte byteToString => "" + byteToString,
                    sbyte byteToString => "" + byteToString,
                    char charToString => "" + charToString,
                    short shortToString => "" + shortToString,
                    ushort ushortToString => "" + ushortToString,
                    int intToString => "" + intToString,
                    uint uintToString => "" + uintToString,
                    long longToString => "" + longToString,
                    ulong ulongToString => "" + ulongToString,
                    float floatToString => "" + floatToString,
                    double doubleToString => "" + doubleToString,
                    string stringToString => stringToString,
                    byte[] byteArrayToString => BitConverter.ToString(byteArrayToString).Replace("-", " "),
                    _ => string.Empty
                };
                return stringValue;
            case 15:
                var byteArrayValue = before switch
                {
                    bool boolToByteArray => BitConverter.GetBytes(boolToByteArray),
                    byte byteToByteArray => [byteToByteArray],
                    sbyte byteToByteArray => [(byte)byteToByteArray],
                    char charToByteArray => BitConverter.GetBytes(charToByteArray),
                    short shortToByteArray => BitConverter.GetBytes(shortToByteArray),
                    ushort ushortToByteArray => BitConverter.GetBytes(ushortToByteArray),
                    int intToByteArray => BitConverter.GetBytes(intToByteArray),
                    uint uintToByteArray => BitConverter.GetBytes(uintToByteArray),
                    long longToByteArray => BitConverter.GetBytes(longToByteArray),
                    ulong ulongToByteArray => BitConverter.GetBytes(ulongToByteArray),
                    float floatToByteArray => BitConverter.GetBytes(floatToByteArray),
                    double doubleToByteArray => BitConverter.GetBytes(doubleToByteArray),
                    string stringToByteArray => Encoding.Default.GetBytes(stringToByteArray),
                    byte[] byteArrayToByteArray => byteArrayToByteArray,
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
            var newItem = new TreeViewItem
                { Header = Lang.Lang.Node_NewItem, Tag = node, IsExpanded = true };
            item.Items.Add(newItem);
            item.IsExpanded = true;
            Nodes.SelectedItem = newItem;
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

        _saved = false;
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
            bool boolValue => boolValue ? "true" : "false",
            byte[] byteArray => BitConverter.ToString(byteArray).Replace("-", " "),
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
        _saved = false;
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
        try
        {
            if (_isSaving || _loadedFile is null) return;
            _isSaving = true;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SaveStatusText.Text = Lang.Lang.Status_SavingFile;
                SavingProgressPanel.IsVisible = true;
            });
            if (Nodes.Items[0] is not TreeViewItem { Tag: FluxionNode root }) return;
            switch (_loadedFileCompression)
            {
                case "none":
                    root.Write(await _loadedFile.OpenWriteAsync(), encoding, version);
                    break;
            }

            _saved = true;
            _isSaving = false;
            await Dispatcher.UIThread.InvokeAsync(() => SavingProgressPanel.IsVisible = false);
        }
        catch (Exception)
        {
            // ignored
        }
    }


// ReSharper disable UnusedParameter.Local
    private void Init(object? sender, VisualTreeAttachmentEventArgs e)
    {
        LoadSettings(out var encoding, out var autoSave, out var version);
        if (AutoSaveEncoding is not null) AutoSaveEncoding.SelectedIndex = encoding;
        if (AutoSave is not null) AutoSave.IsChecked = autoSave;
        if (AutoSaveVersion is not null) AutoSaveVersion.SelectedIndex = version - 1;
        _isSettingsSaving = false;
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
        if (_isSettingsSaving) return;
        if (!Directory.Exists(AppFolder)) Directory.CreateDirectory(AppFolder);
        _isSettingsSaving = true;
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

        _isSettingsSaving = false;
    }

    internal void Close()
    {
        if (SavingProgressPanel.IsVisible) return;
        if (_saved)
        {
            while (_isSettingsSaving)
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
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SaveFile(sender, e);
                if (Parent is not MainWindow mw) return;
                mw.AllowClose = true;
                mw.Close();
            });
        }
        catch (Exception)
        {
            //ignored
        }
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
        try
        {
            if (sender is not Button { Tag: Control c }) return;
            await DialogHost.Show(c);
        }
        catch (Exception)
        {
            // ignored
        }
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
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Parent is not TopLevel topLevel) return;
                if (!topLevel.StorageProvider.CanOpen) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = Lang.Lang.Open_Title,
                    AllowMultiple = false,
                    FileTypeFilter = FileTypes
                });

                if (files.Count <= 0) return;
                SavingProgressPanel.IsVisible = true;
                SaveStatusText.Text = Lang.Lang.Status_LoadingFile;
                ReadFluxionNode(Fluxion.Read(await files[0].OpenReadAsync()), null);
                _loadedFile = files[0];
                _loadedFileCompression = "none";
                SavingProgressPanel.IsVisible = false;
            });
        }
        catch (Exception)
        {
            // ignored
        }
    }
// ReSharper restore UnusedParameter.Local

// ReSharper disable UnusedParameter.Local
    private async void LoadCompressed(object? sender, RoutedEventArgs e)
    {
        try
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
                    FileTypeFilter = FileTypes
                });

                if (files.Count <= 0) return;
                SavingProgressPanel.IsVisible = true;
                SaveStatusText.Text = Lang.Lang.Status_LoadingFile;
                _loadedFile = files[0];
                _loadedFileCompression = compression.ToLowerInvariant();
                await using var stream = GetStream(await files[0].OpenReadAsync(), compression.ToLowerInvariant(),
                    false);
                ReadFluxionNode(Fluxion.Read(stream), null);

                SavingProgressPanel.IsVisible = false;
            });
        }
        catch (Exception)
        {
            // ignored
        }
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
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_loadedFile is null)
                    SaveAsFile(new SenderCombo { Encoding = SaveFileEncoding, Version = SaveVersionSelect }, e);
                if (SaveFileEncoding?.SelectedItem is not ComboBox { Tag: string encoding }) return;
                Save(encoding, (byte)(SaveVersionSelect.SelectedIndex + 1));
            });
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void SaveAsFileClicked(object? sender, RoutedEventArgs e)
    {
        SaveAsFile(new SenderCombo { Encoding = SaveFileAsEncoding, Version = SaveAsVersionSelect }, e);
    }

    // ReSharper disable UnusedParameter.Local
    private async void SaveAsFile(object? sender, RoutedEventArgs e)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Parent is not TopLevel topLevel) return;
                if (SaveCompression.SelectedItem is not ComboBoxItem { Tag: string compression } ||
                    Nodes.Items[0] is not TreeViewItem { Tag: FluxionNode root } || sender is not SenderCombo sc ||
                    sc.Encoding is not { } encodingCb || sc.Version is not { } versionCb ||
                    encodingCb.SelectedItem is not ComboBox { Tag: string encoding }) return;
                if (!topLevel.StorageProvider.CanSave) return;

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = Lang.Lang.SaveAs_Title,
                    DefaultExtension = "*.flx",
                    FileTypeChoices = FileTypes,
                    ShowOverwritePrompt = true
                });

                if (file is null) return;
                _loadedFile = file;
                var fileEncoding = GetEncoding(encoding);
                await using var stream = GetStream(await file.OpenReadAsync(), compression.ToLowerInvariant(),
                    true);
                _isSaving = true;
                await Dispatcher.UIThread.InvokeAsync(() => SavingProgressPanel.IsVisible = true);
                root.Write(stream, fileEncoding, (byte)(versionCb.SelectedIndex + 1));
                _saved = true;
                _isSaving = false;
                await Dispatcher.UIThread.InvokeAsync(() => SavingProgressPanel.IsVisible = false);
            });
        }
        catch (Exception)
        {
            // ignored
        }
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
        foreach (var subNode in cloned.Children) ReadFluxionNode(subNode, item);

        return clone;
    }

    private FluxionNode CloneNode(FluxionNode node)
    {
        var clone = new FluxionNode { Name = node.Name, Value = node.Value };
        foreach (FluxionAttribute attr in node.Attributes)
            clone.Attributes.Add(new FluxionAttribute { Name = attr.Name, Value = attr.Value });
        foreach (var subNode in node.Children) clone.Add(CloneNode(subNode));
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
            foreach (var subNode in node.Children)
                ReadFluxionNode(subNode, item);
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

    private void UpdateApp(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://haltroy.com/en/fluxion#fluxion-viewer") { UseShellExecute = true });
    }

    private void UpdateButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (UpdateAvailable)
        {
            if (sender is not Button { Tag: Control c }) return;
            DialogHost.Show(c);
            return;
        }

        if (!UpToDate) return;
        CheckingForUpdates = true;
        UpToDate = false;
        UpdateAvailable = false;
        CheckForUpdates();
    }

    private class SenderCombo
    {
        public ComboBox? Encoding { get; init; }
        public ComboBox? Version { get; init; }
    }
}