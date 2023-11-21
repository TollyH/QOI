using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for BulkConverter.xaml
    /// </summary>
    public partial class BulkConverter : Window
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
                _ = filesPanel.Children.Add(new FileProgress()
                {
                    Filename = Path.GetFileName(file),
                    CurrentState = FileProgress.State.None
                });
                progressLabel.Text = $"0/{filesToConvert.Count}";
                conversionProgress.Maximum = filesToConvert.Count;
                conversionProgress.Value = 0;
            }
        }

        private void selectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new()
            {
                Multiselect = true,
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

            AddFiles(fileDialog.FileNames);
        }

        private void setDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog folderDialog = new()
            {
                IsFolderPicker = true,
                EnsurePathExists = true
            };

            if (folderDialog.ShowDialog(this) != CommonFileDialogResult.Ok)
            {
                return;
            }

            destination = folderDialog.FileName;
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
                _ = MessageBox.Show("The destination folder does not exist.",
                    "Destination Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            BitmapImage source = Path.GetExtension(file).ToLower() == ".qoi"
                                ? new QOIDecoder().DecodeImageFile(file).ConvertToBitmapImage()
                                : new(new Uri(file));
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
                                    JpegBitmapEncoder encoder = new();
                                    encoder.Frames.Add(BitmapFrame.Create(source));
                                    await using FileStream fileStream = new(destinationFile, FileMode.Create);
                                    encoder.Save(fileStream);
                                    break;
                                }
                                case "png":
                                {
                                    PngBitmapEncoder encoder = new();
                                    encoder.Frames.Add(BitmapFrame.Create(source));
                                    await using FileStream fileStream = new(destinationFile, FileMode.Create);
                                    encoder.Save(fileStream);
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

            _ = MessageBox.Show(anyErrors ? "Some conversions failed" : "All conversions have completed.",
                    "Complete", MessageBoxButton.OK,
                    anyErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])(e.Data.GetData(DataFormats.FileDrop) ?? Array.Empty<string>());
                AddFiles(files);
            }
        }

        private void clearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            filesToConvert.Clear();
            filesPanel.Children.Clear();
            progressLabel.Text = $"0/0";
            conversionProgress.Value = 0;
            conversionProgress.Maximum = 1;
        }

        private void selectFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog folderDialog = new()
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Multiselect = true
            };

            if (folderDialog.ShowDialog(this) != CommonFileDialogResult.Ok)
            {
                return;
            }

            foreach (string folder in folderDialog.FileNames)
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                AddFiles(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }
    }
}
