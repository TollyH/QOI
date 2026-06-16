using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for BulkConverter.xaml
    /// </summary>
    public partial class BulkConverter : Window, IDisposable
    {
        private readonly List<string> filesToConvert = new();
        private string destination = "";
        private bool converting = false;

        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly CancellationToken cancellationToken;

        public BulkConverter()
        {
            cancellationToken = cancellationTokenSource.Token;
            InitializeComponent();
        }

        ~BulkConverter()
        {
            Dispose();
        }

        public void Dispose()
        {
            cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }

        public void AddFiles(IEnumerable<string> files)
        {
            foreach (string file in files)
            {
                if (Path.GetExtension(file).ToLower() is not ".qoi" and not ".png"
                    and not ".jpg" and not ".jpeg")
                {
                    continue;
                }
                if (!File.Exists(file))
                {
                    continue;
                }
                filesToConvert.Add(file);
                filesPanel.Children.Add(new FileProgress()
                {
                    Filename = Path.GetFileName(file),
                    CurrentState = FileProgress.State.None
                });
                progressLabel.Text = $"0/{filesToConvert.Count}";
                conversionProgress.Maximum = filesToConvert.Count;
                conversionProgress.Value = 0;
            }
        }

        private async void selectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                AllowMultiple = true,
                FileTypeFilter = MainWindow.selectableFiles
            });

            if (files.Count == 0)
            {
                return;
            }

            AddFiles(files.Select(f => f.Path.LocalPath));
        }

        private async void setDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                AllowMultiple = false
            });

            if (folders.Count == 0)
            {
                return;
            }

            destination = folders[0].Path.LocalPath;
            outputLabel.Text = destination;
        }

        private async void convertButton_Click(object sender, RoutedEventArgs e)
        {
            if (converting)
            {
                return;
            }
            if (!Directory.Exists(destination))
            {
                new MessageBox("The destination folder does not exist.",
                    "Destination Not Found", MessageBoxImage.Error).Show();
                return;
            }

            string targetType = (formatSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "qoi";

            selectFilesButton.IsEnabled = false;
            selectFoldersButton.IsEnabled = false;
            clearFilesButton.IsEnabled = false;
            setDestinationButton.IsEnabled = false;
            convertButton.IsEnabled = false;
            formatSelector.IsEnabled = false;

            converting = true;
            int complete = 0;
            bool anyErrors = false;

            try
            {
                await Parallel.ForAsync(0, filesToConvert.Count,
                    new ParallelOptions()
                        { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
                    async (i, c) =>
                    {
                        string file = filesToConvert[i];
                        if (!File.Exists(file))
                        {
                            complete++;
                            return;
                        }

                        try
                        {
                            _ = Dispatcher.Invoke(() =>
                                ((FileProgress)filesPanel.Children[i]).CurrentState =
                                FileProgress.State.Processing);
                            string destinationFile = Path.Join(destination,
                                Path.ChangeExtension(Path.GetFileName(file), targetType));
                            SKBitmap source = Path.GetExtension(file).Equals(".qoi", StringComparison.OrdinalIgnoreCase)
                                ? new QOIDecoder().DecodeImageFile(file).ConvertToSKBitmapImage()
                                : SKBitmap.Decode(file);
                            switch (targetType)
                            {
                                case "qoi":
                                {
                                    QOIEncoder encoder = new();
                                    encoder.SaveImageFile(destinationFile, source.ConvertToQOIImage());
                                    break;
                                }
                                case "jpg":
                                {
                                    await using FileStream fileStream = new(destinationFile, FileMode.Create);
                                    source.Encode(fileStream, SKEncodedImageFormat.Jpeg, 100);
                                    break;
                                }
                                case "png":
                                {
                                    await using FileStream fileStream = new(destinationFile, FileMode.Create);
                                    source.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                                    break;
                                }
                            }
                            _ = Dispatcher.Invoke(() =>
                                ((FileProgress)filesPanel.Children[i]).CurrentState = FileProgress.State.Complete);
                        }
                        catch
                        {
                            anyErrors = true;
                            _ = Dispatcher.Invoke(() =>
                                ((FileProgress)filesPanel.Children[i]).CurrentState = FileProgress.State.Error);
#if DEBUG
                            throw;
#endif
                        }
                        finally
                        {
                            complete++;
                            Dispatcher.Invoke(() =>
                            {
                                progressLabel.Text = $"{complete}/{filesToConvert.Count}";
                                conversionProgress.Value = complete;
                            });
                        }
                    });
            }
            catch (TaskCanceledException)
            {
                return;
            }
            finally
            {
                converting = false;
                selectFilesButton.IsEnabled = true;
                selectFoldersButton.IsEnabled = true;
                clearFilesButton.IsEnabled = true;
                setDestinationButton.IsEnabled = true;
                convertButton.IsEnabled = true;
                formatSelector.IsEnabled = true;
            }

            new MessageBox(anyErrors ? "Some conversions failed" : "All conversions have completed.",
                "Complete", anyErrors ? MessageBoxImage.Warning : MessageBoxImage.Information).Show();
        }

        private void clearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            filesToConvert.Clear();
            filesPanel.Children.Clear();
            progressLabel.Text = $"0/0";
            conversionProgress.Value = 0;
            conversionProgress.Maximum = 1;
        }

        private async void selectFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                AllowMultiple = true
            });

            if (folders.Count == 0)
            {
                return;
            }

            foreach (string folder in folders.Select(f => f.Path.LocalPath))
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                AddFiles(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
            }
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }
    }
}
