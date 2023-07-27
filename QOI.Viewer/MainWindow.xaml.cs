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
                Filter = "QOI Image File|*.qoi"
            };

            if (!fileDialog.ShowDialog() ?? true)
            {
                return;
            }

            QOIImage image = QOIDecoder.DecodeImageFile(fileDialog.FileName);
            imageView.Source = image.ConvertToBitmapImage();
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
