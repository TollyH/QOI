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
        public MainWindow()
        {
            InitializeComponent();
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

            string extension = fileDialog.FileName.Split('.')[^1].ToLower();

            switch (extension)
            {
                case "qoi":
                    QOIImage newQOIImage = QOIDecoder.DecodeImageFile(fileDialog.FileName);
                    imageView.Source = newQOIImage.ConvertToBitmapImage();
                    break;
                case "png":
                case "jpg":
                case "jpeg":
                    imageView.Source = new BitmapImage(new System.Uri(fileDialog.FileName));
                    break;
                default:
                    _ = MessageBox.Show("Invalid file type, must be one of: .qoi, .png, .jpg, or .jpeg",
                        "Invalid Type", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
            }
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
    }
}
