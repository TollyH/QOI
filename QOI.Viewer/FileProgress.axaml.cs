using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for FileProgress.xaml
    /// </summary>
    public partial class FileProgress : UserControl
    {
        public static class State
        {
            public static readonly IBrush? None = null;
            public static readonly IBrush? Processing = Brushes.Orange;
            public static readonly IBrush? Complete = Brushes.Green;
            public static readonly IBrush? Error = Brushes.Red;
        }

        public static readonly StyledProperty<string> FilenameProperty =
            AvaloniaProperty.Register<FileProgress, string>("Filename");
        public string Filename
        {
            get => GetValue(FilenameProperty);
            set
            {
                fileNameLabel.Text = value;
                SetValue(FilenameProperty, value);
            }
        }

        public static readonly StyledProperty<IBrush?> CurrentStateProperty =
            AvaloniaProperty.Register<FileProgress, IBrush?>("CurrentState");
        public IBrush? CurrentState
        {
            get => GetValue(CurrentStateProperty);
            set
            {
                finishedIndicator.Fill = value;
                SetValue(CurrentStateProperty, value);
            }
        }

        public FileProgress()
        {
            InitializeComponent();
        }
    }
}
