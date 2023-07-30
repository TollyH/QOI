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
        public static class State
        {
            public static readonly Brush? None = null;
            public static readonly Brush? Processing = Brushes.Orange;
            public static readonly Brush? Complete = Brushes.Green;
            public static readonly Brush? Error = Brushes.Red;
        }

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

        public static readonly DependencyProperty CurrentStateProperty =
            DependencyProperty.Register("CurrentState", typeof(Brush), typeof(FileProgress));
        public Brush? CurrentState
        {
            get => (Brush?)GetValue(CurrentStateProperty);
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
