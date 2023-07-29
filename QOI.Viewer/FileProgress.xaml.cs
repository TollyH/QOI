using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for FileProgress.xaml
    /// </summary>
    public partial class FileProgress : UserControl
    {
        public static readonly DependencyProperty FilenameProperty =
            DependencyProperty.Register("Filename", typeof(string), typeof(FileProgress));
        public string Filename
        {
            get => (string)GetValue(FilenameProperty);
            set
            {
                fileNameLabel.Text = value;
                SetValue(FilenameProperty, value);
            }
        }

        public static readonly DependencyProperty IsCompleteProperty =
            DependencyProperty.Register("IsComplete", typeof(bool), typeof(FileProgress));
        public bool IsComplete
        {
            get => (bool)GetValue(IsCompleteProperty);
            set
            {
                finishedIndicator.Fill = value ? Brushes.Green : null;
                SetValue(IsCompleteProperty, value);
            }
        }

        public FileProgress()
        {
            InitializeComponent();
        }
    }
}
