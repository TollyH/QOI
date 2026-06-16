using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace QOI.Viewer
{
    public enum MessageBoxImage
    {
        Error,
        Warning,
        Information
    }
    
    public partial class MessageBox : Window
    {
        private static readonly Bitmap errorImage = new(AssetLoader.Open(new Uri("avares://QOI.Viewer/Resources/error.png")));
        private static readonly Bitmap warningImage = new(AssetLoader.Open(new Uri("avares://QOI.Viewer/Resources/warning.png")));
        private static readonly Bitmap informationImage = new(AssetLoader.Open(new Uri("avares://QOI.Viewer/Resources/information.png")));
        
        public MessageBox(string message, string title, MessageBoxImage image)
        {
            InitializeComponent();
            
            messageText.Text = message;
            Title = title;
            imageIcon.Source = image switch
            {
                MessageBoxImage.Error => errorImage,
                MessageBoxImage.Warning => warningImage,
                MessageBoxImage.Information => informationImage,
                _ => throw new ArgumentException("Invalid message box image", nameof(image))
            };
        }
        
        public MessageBox() : this("", "", MessageBoxImage.Information) { }

        private void Button_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
