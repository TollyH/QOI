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

        public BulkConverter()
        {
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
                    IsComplete = false
                });
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

            if (!fileDialog.ShowDialog() ?? true)
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

            if (folderDialog.ShowDialog() != CommonFileDialogResult.Ok)
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

            bool anyErrors = false;
            converting = true;
            int complete = 0;
            int aliveThreads = 0;
            for (int i = 0; i < filesToConvert.Count; i++)
            {
                string file = filesToConvert[i];
                if (!File.Exists(file))
                {
                    complete++;
                    continue;
                }

                while (aliveThreads >= Environment.ProcessorCount)
                {
                    await Task.Delay(100);
                }
                aliveThreads++;
                string thisFile = file;
                int thisIndex = i;
                Thread thread = new(() =>
                {
                    try
                    {
                        string destinationFile = Path.Join(destination,
                            Path.ChangeExtension(Path.GetFileName(thisFile), targetType));
                        BitmapImage source = Path.GetExtension(thisFile).ToLower() == ".qoi"
                            ? new QOIDecoder().DecodeImageFile(thisFile).ConvertToBitmapImage()
                            : new(new Uri(thisFile));
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
                                    using FileStream fileStream = new(destinationFile, FileMode.Create);
                                    encoder.Save(fileStream);
                                    break;
                                }
                            case "png":
                                {
                                    PngBitmapEncoder encoder = new();
                                    encoder.Frames.Add(BitmapFrame.Create(source));
                                    using FileStream fileStream = new(destinationFile, FileMode.Create);
                                    encoder.Save(fileStream);
                                    break;
                                }
                        }
                        _ = Dispatcher.Invoke(() => ((FileProgress)filesPanel.Children[thisIndex]).IsComplete = true);
                    }
                    catch
                    {
                        _ = Dispatcher.Invoke(() => ((FileProgress)filesPanel.Children[thisIndex]).IsError = true);
                        anyErrors = true;
                    }
                    finally
                    {
                        aliveThreads--;
                        complete++;
                    }
                });
                thread.Start();
            }
            while (complete < filesToConvert.Count)
            {
                await Task.Delay(100);
            }

            _ = MessageBox.Show(anyErrors ? "Some conversions failed" : "All conversions have completed.",
                    "Complete", MessageBoxButton.OK,
                    anyErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);

            converting = false;
            selectFilesButton.IsEnabled = true;
            selectFoldersButton.IsEnabled = true;
            clearFilesButton.IsEnabled = true;
            setDestinationButton.IsEnabled = true;
            convertButton.IsEnabled = true;
            formatSelector.IsEnabled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        private void clearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            filesToConvert.Clear();
            filesPanel.Children.Clear();
        }

        private void selectFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog folderDialog = new()
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Multiselect = true
            };

            if (folderDialog.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            if(!Directory.Exists(folderDialog.FileName))
            {
                return;
            }
            AddFiles(Directory.GetFiles(folderDialog.FileName, "*", SearchOption.AllDirectories));
        }
    }
}
