using Microsoft.Win32;
using System.IO;
using System.Windows;
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
                        imageView.Source = new BitmapImage(new System.Uri(path));
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
                DefaultExt = ".png",
                Filter = "PNG Image File|*.png",
                AddExtension = true
            };

            if (!fileDialog.ShowDialog() ?? true)
            {
                return;
            }

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create((BitmapImage)imageView.Source));
            using FileStream fileStream = new(fileDialog.FileName, FileMode.Create);
            encoder.Save(fileStream);
        }

        private void configDebugMode_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(openFile))
            {
                LoadImage(openFile);
            }
        }
    }
}
