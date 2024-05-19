using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FluxionSharp;
using FluxionViewer.Views;

namespace FluxionViewer;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
            {
                if (desktop.Args != null)
                {
                    if (desktop.Args.Contains("--help"))
                    {
                        Console.WriteLine(
                            Lang.Lang.help_0);
                        Console.WriteLine("");
                        Console.WriteLine(Lang.Lang.help_1);
                        Console.WriteLine(Lang.Lang.help_2);
                        Console.WriteLine(Lang.Lang.help_3);
                        Console.WriteLine(
                            Lang.Lang.help_4);
                        Console.WriteLine(Lang.Lang.help_5);
                        Console.WriteLine(Lang.Lang.help_6);
                        Console.WriteLine("");
                        Console.WriteLine(Lang.Lang.help_7);
                        // ReSharper disable once LocalizableElement
                        Console.WriteLine("none deflate gzip brotli zlib");
                        Console.WriteLine("");
                        Console.WriteLine(Lang.Lang.help_8);
                        // ReSharper disable once LocalizableElement
                        Console.WriteLine("utf8 utf16 utf32");
                        desktop.Shutdown();
                    }

                    if (desktop.Args.Contains("--no-gui"))
                    {
                        if (desktop.Args.Length < 1)
                        {
                            Console.WriteLine(Lang.Lang.cli_notenoughargs);
                            desktop.Shutdown(1);
                        }

                        try
                        {
                            var file = desktop.Args[0];
                            var compression = "none";
                            var target_compression = "none";
                            var target_encoding = "utf8";
                            var target_file = "";

                            if (desktop.Args.Length >= 3)
                                compression = desktop.Args[Array.IndexOf(desktop.Args, "--compression") + 1];

                            if (desktop.Args.Length >= 6)
                            {
                                var saveIndex = Array.IndexOf(desktop.Args, "--save");
                                var argument1 = desktop.Args[saveIndex + 1];
                                var argument2 = desktop.Args[saveIndex + 2];
                                if (desktop.Args.Length >= 7)
                                {
                                    target_compression = argument1;
                                    target_encoding = argument2;
                                    target_file = desktop.Args[saveIndex + 3];
                                }
                                else
                                {
                                    target_encoding = argument1;
                                    target_file = argument2;
                                }
                            }

                            using var open_fs =
                                new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using Stream readStream = compression switch
                            {
                                "none" => open_fs,
                                "gzip" => new GZipStream(open_fs, CompressionMode.Decompress),
                                "deflate" => new DeflateStream(open_fs, CompressionMode.Decompress),
                                "brotli" => new BrotliStream(open_fs, CompressionMode.Decompress),
                                "zlib" => new ZLibStream(open_fs, CompressionMode.Decompress),
                                _ => throw new Exception(
                                    Lang.Lang.cli_unsupportedcompression.Replace("$1", compression))
                            };

                            var root = Fluxion.Read(readStream);

                            using var write_fs = File.Exists(target_file)
                                ? new FileStream(target_file, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite)
                                : File.Create(target_file);
                            using Stream writeStream = target_compression switch
                            {
                                "none" => write_fs,
                                "gzip" => new GZipStream(write_fs, CompressionMode.Compress),
                                "deflate" => new DeflateStream(write_fs, CompressionMode.Compress),
                                "brotli" => new BrotliStream(write_fs, CompressionMode.Compress),
                                "zlib" => new ZLibStream(write_fs, CompressionMode.Compress),
                                _ => throw new Exception(
                                    Lang.Lang.cli_unsupportedcompression.Replace("$1", target_compression))
                            };

                            root.Write(writeStream, target_encoding switch
                            {
                                "utf8" => Encoding.UTF8,
                                "utf16" => Encoding.Unicode,
                                "utf32" => Encoding.UTF32,
                                _ => throw new Exception(
                                    Lang.Lang.cli_unsupportedencoding.Replace("$1", target_encoding))
                            });
                            desktop.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                            desktop.Shutdown(1);
                        }
                    }
                }

                desktop.MainWindow = new MainWindow().WithArgs(desktop.Args ?? []);
                break;
            }
            case ISingleViewApplicationLifetime single:
                single.MainView = new MainView();
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}