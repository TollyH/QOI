using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string openFile = "";

        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();

            configDebugMode.IsChecked = args.Contains("--debug");

            if (args.Length > 1)
            {
                LoadImage(args[1]);
            }
        }

        public void LoadImage(string path)
        {
            string extension = path.Split('.')[^1].ToLower();

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
                        QOIImage newQOIImage = decoder.DecodeImageFile(path);
                        imageView.Source = newQOIImage.ConvertToBitmapImage();

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

            openFile = path;
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
                            QOIEncoder encoder = new();
                            encoder.SaveImageFile(path, ((BitmapImage)imageView.Source).ConvertToQOIImage());
                            break;
                        }
                    case "png":
                        {
                            PngBitmapEncoder encoder = new();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapImage)imageView.Source));
                            using FileStream fileStream = new(path, FileMode.Create);
                            encoder.Save(fileStream);
                            break;
                        }
                    case "jpg":
                    case "jpeg":
                        {
                            JpegBitmapEncoder encoder = new();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapImage)imageView.Source));
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

            if (!fileDialog.ShowDialog() ?? true)
            {
                return;
            }

            LoadImage(fileDialog.FileName);
        }

        private void SaveItem_Click(object sender, RoutedEventArgs e)
        {
            if (imageView.Source is null or not BitmapImage)
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

            if (!fileDialog.ShowDialog() ?? true)
            {
                return;
            }

            SaveImage(fileDialog.FileName);
        }

        private void configDebugMode_Click(object sender, RoutedEventArgs e)
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

        private void imageView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DataObject dObj = new(DataFormats.FileDrop, new string[1] { openFile });
                _ = DragDrop.DoDragDrop(imageView, dObj, DragDropEffects.Copy);
            }
        }
    }
}
