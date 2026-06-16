using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static readonly IReadOnlyList<FilePickerFileType> selectableFiles = new FilePickerFileType[]
        {
            new("All Supported Types") { Patterns = new[] { "*.qoi", "*.png", "*.jpg", "*.jpeg" } },
            new("QOI Image File") { Patterns = new[] {"*.qoi"} },
            new("PNG Image File") { Patterns = new[] {"*.png"} },
            new("JPEG Image File") { Patterns = new[] {"*.jpg", "*.jpeg"} },
        };

        private string openFile
        {
            get;
            set
            {
                field = value;
                Title = filesInFolder.Length > 0
                    ? $"QOI Image Viewer - ({indexInFolder + 1}/{filesInFolder.Length}) {value}"
                    : "QOI Image Viewer";
            }
        } = "";

        private SKBitmap? openBitmap = null;

        private string lastOpenedFolder = "";
        private string[] filesInFolder = Array.Empty<string>();
        private int indexInFolder = 0;

        private bool zoomedSinceFit = false;

        private byte[] trailingData = Array.Empty<byte>();

        private IndexDebugWindow? openIndexWindow = null;

        private QOIDecoder.IndexHistoryItem[]?[]? indexHistory = null;

        private readonly ScaleTransform imageScaleTransform = new();

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

            imageViewScale.LayoutTransform = imageScaleTransform;
            
            // Prevent arrow keys/scrolling being captured for navigation.
            AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
            imageScroll.AddHandler(PointerWheelChangedEvent, ScrollViewer_PointerWheelChanged, RoutingStrategies.Tunnel);

            string[] args = Environment.GetCommandLineArgs();

            configDebugMode.IsChecked = args.Contains("--debug");
            args = args.Skip(1).Where(x => !x.StartsWith('-')).ToArray();

            if (args.Length == 1)
            {
                LoadImage(args[0]);
            }
            else if (args.Length > 1)
            {
                BulkConverter converter = new();
                converter.AddFiles(args);
                converter.Show(this);
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
                if (chunkExcludeItem is { IsChecked: true, Tag: ChunkType chunkType })
                {
                    excludeChunks.Add(chunkType);
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
                            DebugMode = configDebugMode.IsChecked,
                            StoreFullIndexHistory = openIndexWindow is not null
                        };
                        Stopwatch decodeStopwatch = Stopwatch.StartNew();
                        QOIImage newQOIImage = decoder.DecodeImageFile(path);
                        decodeStopwatch.Stop();
                        Stopwatch convertStopwatch = Stopwatch.StartNew();
                        ChangeImageSource(excludeChunks.Count > 0
                            ? newQOIImage.ConvertToAvaloniaBitmapImageFilterChunks(decoder.GenerateDebugPixels(
                                File.ReadAllBytes(path).AsSpan()[14..], (uint)newQOIImage.Pixels.Length), excludeChunks)
                            : newQOIImage.ConvertToAvaloniaBitmapImage());
                        openBitmap = newQOIImage.ConvertToSKBitmapImage();
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

                        indexHistory = decoder.IndexHistory;

                        if (!decoder.EndTagWasPresent)
                        {
                            new MessageBox("End tag was missing from loaded file.",
                            "End Tag Missing", MessageBoxImage.Warning).Show();
                        }

                        break;
                    case "png":
                    case "jpg":
                    case "jpeg":
                        ChangeImageSource(new Bitmap(path));
                        openBitmap = SKBitmap.Decode(path);
                        ShowEmptyStats();

                        indexHistory = null;
                        break;
                    default:
                        new MessageBox("Invalid file type, must be one of: .qoi, .png, .jpg, or .jpeg",
                            "Invalid Type", MessageBoxImage.Error).Show();
                        return;
                }
            }
            // ReSharper disable once RedundantCatchClause
            catch
            {
#if DEBUG
                throw;
#else
                new MessageBox("Failed to open image. It may be missing or corrupt.",
                "Image Read Failed", MessageBoxImage.Error).Show();
                return;
#endif
            }

            string fullPath = Path.GetFullPath(path);
            string newFolder = Path.GetDirectoryName(fullPath) ?? "";
            if (lastOpenedFolder != newFolder)
            {
                lastOpenedFolder = newFolder;
                filesInFolder = Directory.GetFiles(newFolder).Where(
                    x => Path.GetExtension(x).ToLower() is ".qoi" or ".png" or ".jpg" or ".jpeg")
                    .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToArray();
                indexInFolder = Array.IndexOf(filesInFolder, fullPath);
            }
            openFile = fullPath;
            if (!zoomedSinceFit)
            {
                FitImage();
            }
        }

        public void SaveImage(string path)
        {
            string extension = path.Split('.')[^1].ToLower();

            if (imageView.Source is not Bitmap bitmap || openBitmap is null)
            {
                return;
            }

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
                            QOIImage toSave = bitmap.ConvertToQOIImage();
                            toSave.TrailingData = trailingData;
                            encoder.SaveImageFile(path, toSave);
                            break;
                        }
                    case "png":
                        {
                            using FileStream fileStream = new(path, FileMode.Create);
                            openBitmap.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                            break;
                        }
                    case "jpg":
                    case "jpeg":
                        {
                            using FileStream fileStream = new(path, FileMode.Create);
                            openBitmap.Encode(fileStream, SKEncodedImageFormat.Jpeg, 100);
                            break;
                        }
                    default:
                        new MessageBox("Invalid file type, must be one of: .qoi, .png, .jpg, or .jpeg",
                            "Invalid Type", MessageBoxImage.Error).Show();
                        return;
                }
            }
            // ReSharper disable once RedundantCatchClause
            catch
            {
#if DEBUG
                throw;
#else
                new MessageBox("Failed to save image. You may not have permission to save to the given path.",
                "Image Save Failed", MessageBoxImage.Error).Show();
                return;
#endif
            }
        }

        public async Task LoadImageFromClipboard()
        {
            Task<Bitmap?>? imageTask = Clipboard?.TryGetBitmapAsync();
            Bitmap? image;
            if (imageTask is null || (image = await imageTask) is null)
            {
                new MessageBox("There is no image currently on the clipboard.", "No Clipboard Image",
                    MessageBoxImage.Error).Show();
                return;
            }
            ChangeImageSource(image);
            lastOpenedFolder = "";
            indexInFolder = 0;
            filesInFolder = Array.Empty<string>();
            openFile = "";
            openBitmap = null;
            ShowEmptyStats();
            if (!zoomedSinceFit)
            {
                FitImage();
            }
        }

        public void FitImage()
        {
            if (imageView.Source is null)
            {
                return;
            }

            double toFitX = imageScroll.Bounds.Width / imageView.Width;
            double toFitY = imageScroll.Bounds.Height / imageView.Height;
            double toFitBoth = Math.Min(toFitY, toFitX);
            imageScaleTransform.ScaleX = toFitBoth;
            imageScaleTransform.ScaleY = toFitBoth;

            zoomedSinceFit = false;
        }

        public void ZoomActualSize()
        {
            if (imageView.Source is null)
            {
                return;
            }
            imageScaleTransform.ScaleX = 1;
            imageScaleTransform.ScaleY = 1;
            zoomedSinceFit = true;
        }

        public async Task PromptFileOpen()
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                FileTypeFilter = selectableFiles
            });

            if (files.Count == 0)
            {
                return;
            }

            LoadImage(files[0].Path.LocalPath);
        }

        public async Task PromptFileSave()
        {
            if (imageView.Source is not Bitmap)
            {
                return;
            }

            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                DefaultExtension = ".qoi",
                FileTypeChoices = selectableFiles,
                SuggestedFileName = Path.GetFileNameWithoutExtension(openFile)
            });

            if (file is null)
            {
                return;
            }

            SaveImage(file.Path.LocalPath);
        }

        public void Reload()
        {
            if (File.Exists(openFile))
            {
                LoadImage(openFile);
            }
        }

        public void CopyImageToClipboard()
        {
            if (imageView.Source is not Bitmap bitmap || Clipboard is null)
            {
                return;
            }

            Clipboard.SetBitmapAsync(bitmap);
        }

        private void ChangeImageSource(Bitmap? source)
        {
            imageView.Source = source;
            imageView.Width = source?.PixelSize.Width ?? double.NaN;
            imageView.Height = source?.PixelSize.Height ?? double.NaN;
        }

        private void OpenIndexView()
        {
            if (openIndexWindow is null)
            {
                openIndexWindow = new IndexDebugWindow(this);
                openIndexWindow.Closed += openIndexWindow_Closed;
                openIndexWindow.IndexSelectionChanged += openIndexWindow_IndexSelectionChanged;

                openIndexWindow.Show();

                Reload();
            }
            else
            {
                openIndexWindow.Focus();
            }
        }

        private void UpdateIndexViewFromMousePosition(Point mousePos)
        {
            if (openIndexWindow is not null && indexHistory is not null)
            {
                int clickedPixelIndex = (int)((int)mousePos.Y * imageView.Width + mousePos.X);

                if (clickedPixelIndex < indexHistory.Length)
                {
                    QOIDecoder.IndexHistoryItem[]? history;
                    // The history item will be null if the pixel is in the extended part of a run.
                    // Find the last non-null history item (i.e. the item for the start of the run).
                    while ((history = indexHistory[clickedPixelIndex]) is null)
                    {
                        clickedPixelIndex--;
                        if (clickedPixelIndex < 0)
                        {
                            return;
                        }
                    }
                    openIndexWindow.SetColors(history);

                    selectedIndexPixelOutline.IsVisible = true;
                    Canvas.SetLeft(selectedIndexPixelOutline, clickedPixelIndex % imageView.Width);
                    Canvas.SetTop(selectedIndexPixelOutline, clickedPixelIndex / imageView.Width);
                }
            }
        }

        private async void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            await PromptFileOpen();
        }

        private async void saveItem_Click(object sender, RoutedEventArgs e)
        {
            await PromptFileSave();
        }

        private void ReloadOnClick(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void configNearestNeighbor_Checked(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != MenuItem.IsCheckedProperty)
            {
                return;
            }
            RenderOptions.SetBitmapInterpolationMode(imageView, configNearestNeighbor.IsChecked
                ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
        }

        private void BulkConverterItem_Click(object sender, RoutedEventArgs e)
        {
            new BulkConverter().Show();
        }

        private async void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            e.Handled = true;
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
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        await LoadImageFromClipboard();
                    }
                    break;
                case Key.F:
                    FitImage();
                    break;
                case Key.O:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        await PromptFileOpen();
                    }
                    break;
                case Key.S:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        await PromptFileSave();
                    }
                    break;
                case Key.B:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        new BulkConverter().Show();
                    }
                    break;
                case Key.N:
                    configNearestNeighbor.IsChecked = !configNearestNeighbor.IsChecked;
                    break;
                case Key.D:
                    configDebugMode.IsChecked = !configDebugMode.IsChecked;
                    Reload();
                    break;
                case Key.C:
                    if (e.KeyModifiers == KeyModifiers.Control)
                    {
                        CopyImageToClipboard();
                    }
                    break;
                case Key.A:
                    ZoomActualSize();
                    break;
                case Key.I:
                    OpenIndexView();
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }

        private async void openClipboardItem_Click(object sender, RoutedEventArgs e)
        {
            await LoadImageFromClipboard();
        }

        private async void FileMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            openClipboardItem.IsEnabled = Clipboard is not null && await Clipboard.TryGetBitmapAsync() is not null;
            saveItem.IsEnabled = imageView.Source is Bitmap;
            copyItem.IsEnabled = imageView.Source is Bitmap;
        }

        private void ScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                // Prevent zoom from also scrolling
                e.Handled = true;

                // Logarithmic zooming makes zoom look more "linear" to the eye
                double value = Math.Exp(Math.Log(imageScaleTransform.ScaleX) + (e.Delta.Y * 0.06));
                if (imageScaleTransform.ScaleX + value > 0 && imageScaleTransform.ScaleY + value > 0)
                {
                    zoomedSinceFit = true;
                    imageScaleTransform.ScaleX = value;
                    imageScaleTransform.ScaleY = value;
                }
            }
        }

        private void imageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!zoomedSinceFit)
            {
                FitImage();
            }
        }

        private void FitImageItem_Click(object sender, RoutedEventArgs e)
        {
            FitImage();
        }

        private void copyItem_Click(object sender, RoutedEventArgs e)
        {
            CopyImageToClipboard();
        }

        private void ActualSizeItem_Click(object sender, RoutedEventArgs e)
        {
            ZoomActualSize();
        }

        private void imageView_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            UpdateIndexViewFromMousePosition(e.GetPosition(imageView));
        }

        private void imageView_PointerMoved(object sender, PointerEventArgs e)
        {
            if (e.Properties.IsLeftButtonPressed)
            {
                UpdateIndexViewFromMousePosition(e.GetPosition(imageView));
            }
        }

        private void IndexViewerItem_Click(object sender, RoutedEventArgs e)
        {
            OpenIndexView();
        }

        private void openIndexWindow_Closed(object? sender, EventArgs e)
        {
            openIndexWindow = null;

            selectedIndexPixelOutline.IsVisible = false;
            lastUpdatedIndexPixelOutline.IsVisible = false;
        }

        private void openIndexWindow_IndexSelectionChanged(object? sender, EventArgs e)
        {
            int? index = openIndexWindow?.SelectedItemLastEditedByIndex;

            if (index is null)
            {
                lastUpdatedIndexPixelOutline.IsVisible = false;
                return;
            }

            lastUpdatedIndexPixelOutline.IsVisible = true;
            Canvas.SetLeft(lastUpdatedIndexPixelOutline, (int)(index.Value % imageView.Width));
            Canvas.SetTop(lastUpdatedIndexPixelOutline, (int)(index.Value / imageView.Width));
        }
    }
}
