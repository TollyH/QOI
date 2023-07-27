using Microsoft.Win32;
using System.Windows;

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
    }
}
