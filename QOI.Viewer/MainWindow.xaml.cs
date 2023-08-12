using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _openFile = "";
        private string openFile
        {
            get => _openFile;
            set
            {
                _openFile = value;
                Title = filesInFolder.Length > 0
                    ? $"QOI Image Viewer - ({indexInFolder + 1}/{filesInFolder.Length}) {value}"
                    : "QOI Image Viewer";
            }
        }

        private string lastOpenedFolder = "";
        private string[] filesInFolder = Array.Empty<string>();
        private int indexInFolder = 0;

        private byte[] trailingData = Array.Empty<byte>();

        private static readonly Dictionary<ChunkType, string> debugModeColors = new()
        {
            { ChunkType.QOI_OP_RGB, "Red" },
            { ChunkType.QOI_OP_RGBA, "Green" },
            { ChunkType.QOI_OP_INDEX, "Blue" },
            { ChunkType.QOI_OP_DIFF, "Yellow" },
            { ChunkType.QOI_OP_LUMA, "Magenta" },
            { ChunkType.QOI_OP_RUN, "Cyan" }
        };

        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();

            configDebugMode.IsChecked = args.Contains("--debug");
            args = args.Skip(1).Where(x => !x.StartsWith("-")).ToArray();

            if (args.Length == 1)
            {
                LoadImage(args[0]);
            }
            else if (args.Length > 1)
            {
                BulkConverter converter = new();
                converter.AddFiles(args);
                _ = converter.ShowDialog();
            }
        }

        public void ShowEmptyStats()
        {
            statsLabelResolution.Text = "Resolution: Stats for QOI images only";
            statsLabelChannels.Text = "Channels: Stats for QOI images only";
            statsLabelColorspace.Text = "Colorspace: Stats for QOI images only";
            statsLabelTimeDecoding.Text = "Time to Decode: Stats for QOI images only";
            statsLabelTimeConverting.Text = "Time to Convert: Stats for QOI images only";
            statsLabelCompression.Text = "Compression: Stats for QOI images only";
            statsLabelTrailingData.Text = "Trailing Data Length: Stats for QOI images only";
            statsLabelChunkStats.Text = "Chunk Counts: Stats for QOI images only";
        }

        public void LoadImage(string path)
        {
            string extension = path.Split('.')[^1].ToLower();
            HashSet<ChunkType> excludeChunks = new();
            foreach (MenuItem chunkExcludeItem in chunkHideMenu.Items.OfType<MenuItem>())
            {
                if (chunkExcludeItem.IsChecked)
                {
                    excludeChunks.Add((ChunkType)chunkExcludeItem.Tag);
                }
            }

            try
            {
                switch (extension)
                {
                    case "qoi":
                        QOIDecoder decoder = new()
                        {
                            RequireEndTag = false,
                            DebugMode = configDebugMode.IsChecked
                        };
                        Stopwatch decodeStopwatch = Stopwatch.StartNew();
                        QOIImage newQOIImage = decoder.DecodeImageFile(path);
                        decodeStopwatch.Stop();
                        Stopwatch convertStopwatch = Stopwatch.StartNew();
                        imageView.Source = excludeChunks.Count > 0
                            ? newQOIImage.ConvertToBitmapImageFilterChunks(decoder.GenerateDebugPixels(
                                File.ReadAllBytes(path).AsSpan()[14..], (uint)newQOIImage.Pixels.Length), excludeChunks)
                            : newQOIImage.ConvertToBitmapImage();
                        convertStopwatch.Stop();
                        trailingData = newQOIImage.TrailingData;

                        int uncompressedBytes = newQOIImage.Pixels.Length * (newQOIImage.Channels == ChannelType.RGBA ? 4 : 3);
                        statsLabelResolution.Text = $"Resolution: {newQOIImage.Width} x {newQOIImage.Height} " +
                            $"({newQOIImage.Pixels.Length:n0})";
                        statsLabelChannels.Text = $"Channels: {newQOIImage.Channels}";
                        statsLabelColorspace.Text = $"Colorspace: {newQOIImage.Colorspace}";
                        statsLabelTimeDecoding.Text = $"Time to Decode: {decodeStopwatch.Elapsed.TotalSeconds:0.###}s";
                        statsLabelTimeConverting.Text = $"Time to Convert: {convertStopwatch.Elapsed.TotalSeconds:0.###}s";
                        statsLabelCompression.Text = $"Compression: {Utils.FormatBytes(decoder.PixelDataLength, 2)} " +
                            $"/ {Utils.FormatBytes(uncompressedBytes, 2)} ({(double)decoder.PixelDataLength / uncompressedBytes * 100d:0.##}%)";
                        statsLabelTrailingData.Text = $"Trailing Data Length: {newQOIImage.TrailingData.Length:n0} bytes";
                        statsLabelChunkStats.Text = "Chunk Counts:";
                        foreach ((ChunkType type, int count) in decoder.ChunksDecoded)
                        {
                            statsLabelChunkStats.Text += configDebugMode.IsChecked
                                ? $"{Environment.NewLine}{type} ({debugModeColors[type]}): {count:n0}"
                                : $"{Environment.NewLine}{type}: {count:n0}";
                        }
                        statsLabelChunkStats.Text += $"{Environment.NewLine}Total: {decoder.ChunksDecoded.Values.Sum():n0}";

                        if (!decoder.EndTagWasPresent)
                        {
                            _ = MessageBox.Show("End tag was missing from loaded file.",
                            "End Tag Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }

                        break;
                    case "png":
                    case "jpg":
                    case "jpeg":
                        imageView.Source = new BitmapImage(new Uri(path));
                        ShowEmptyStats();
                        break;
                    default:
                        _ = MessageBox.Show("Invalid file type, must be one of: .qoi, .png, .jpg, or .jpeg",
                            "Invalid Type", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }
            }
            catch
            {
#if DEBUG
                throw;
#else
                _ = MessageBox.Show("Failed to open image. It may be missing or corrupt.",
                "Image Read Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
#endif
            }

            string fullPath = Path.GetFullPath(path);
            string newFolder = Path.GetDirectoryName(fullPath) ?? "";
            if (lastOpenedFolder != newFolder)
            {
                lastOpenedFolder = newFolder;
                filesInFolder = Directory.GetFiles(newFolder).Where(
                    x => Path.GetExtension(x).ToLower() is ".qoi" or ".png" or ".jpg" or ".jpeg").ToArray();
                indexInFolder = Array.IndexOf(filesInFolder, fullPath);
            }
            openFile = fullPath;
        }

        public void SaveImage(string path)
        {
            string extension = path.Split('.')[^1].ToLower();

            try
            {
                switch (extension)
                {
                    case "qoi":
                        {
                            QOIEncoder encoder = new()
                            {
                                UseINDEXChunks = !configNoEncodeINDEX.IsChecked,
                                UseDIFFChunks = !configNoEncodeDIFF.IsChecked,
                                UseLUMAChunks = !configNoEncodeLUMA.IsChecked,
                                UseRUNChunks = !configNoEncodeRUN.IsChecked,
                                UseRGBChunks = !configNoEncodeRGB.IsChecked
                            };
                            QOIImage toSave = ((BitmapSource)imageView.Source).ConvertToQOIImage();
                            toSave.TrailingData = trailingData;
                            encoder.SaveImageFile(path, toSave);
                            break;
                        }
                    case "png":
                        {
                            PngBitmapEncoder encoder = new();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imageView.Source));
                            using FileStream fileStream = new(path, FileMode.Create);
                            encoder.Save(fileStream);
                            break;
                        }
                    case "jpg":
                    case "jpeg":
                        {
                            JpegBitmapEncoder encoder = new();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imageView.Source));
                            using FileStream fileStream = new(path, FileMode.Create);
                            encoder.Save(fileStream);
                            break;
                        }
                    default:
                        _ = MessageBox.Show("Invalid file type, must be one of: .qoi, .png, .jpg, or .jpeg",
                            "Invalid Type", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                }
            }
            catch
            {
#if DEBUG
                throw;
#else
                _ = MessageBox.Show("Failed to save image. You may not have permission to save to the given path.",
                "Image Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
#endif
            }
        }

        public void LoadImageFromClipboard()
        {
            if (!Clipboard.ContainsImage())
            {
                _ = MessageBox.Show("There is no image currently on the clipboard.", "No Clipboard Image",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            imageView.Source = ClipboardImageConvert.GetBitmapSourceFromClipboard();
            lastOpenedFolder = "";
            indexInFolder = 0;
            filesInFolder = Array.Empty<string>();
            openFile = "";
            ShowEmptyStats();
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new()
            {
                CheckFileExists = true,
                Filter = "All Supported Types|*.qoi;*.png;*.jpg;*.jpeg" +
                "|QOI Image File|*.qoi" +
                "|PNG Image File|*.png" +
                "|JPEG Image File|*.jpg;*.jpeg"
            };

            if (!fileDialog.ShowDialog(this) ?? true)
            {
                return;
            }

            LoadImage(fileDialog.FileName);
        }

        private void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            if (imageView.Source is null or not BitmapSource)
            {
                return;
            }

            SaveFileDialog fileDialog = new()
            {
                CheckPathExists = true,
                DefaultExt = ".qoi",
                AddExtension = true,
                Filter = "QOI Image File|*.qoi" +
                "|PNG Image File|*.png" +
                "|JPEG Image File|*.jpg;*.jpeg"
            };

            if (!fileDialog.ShowDialog(this) ?? true)
            {
                return;
            }

            SaveImage(fileDialog.FileName);
        }

        private void ReloadOnClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(openFile))
            {
                LoadImage(openFile);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadImage(files[0]);
            }
        }

        private void configNearestNeighbor_Click(object sender, RoutedEventArgs e)
        {
            RenderOptions.SetBitmapScalingMode(imageView, configNearestNeighbor.IsChecked
                ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        }

        private void BulkConverterItem_Click(object sender, RoutedEventArgs e)
        {
            _ = new BulkConverter().ShowDialog();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                    if (indexInFolder <= 0)
                    {
                        break;
                    }
                    LoadImage(filesInFolder[--indexInFolder]);
                    break;
                case Key.Right:
                    if (indexInFolder >= filesInFolder.Length - 1)
                    {
                        break;
                    }
                    LoadImage(filesInFolder[++indexInFolder]);
                    break;
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsImage())
                    {
                        LoadImageFromClipboard();
                    }
                    break;
            }
        }

        private void openClipboardItem_Click(object sender, RoutedEventArgs e)
        {
            LoadImageFromClipboard();
        }

        private void FileMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            openClipboardItem.IsEnabled = Clipboard.ContainsImage();
        }
    }
}
