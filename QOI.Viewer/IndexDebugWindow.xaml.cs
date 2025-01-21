using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for IndexDebugWindow.xaml
    /// </summary>
    public partial class IndexDebugWindow : Window
    {
        private readonly System.Windows.Shapes.Rectangle[] colorRectangles = new System.Windows.Shapes.Rectangle[64];

        public IndexDebugWindow(Window owner)
        {
            InitializeComponent();

            Owner = owner;

            for (int i = 0; i < colorRectangles.Length; i++)
            {
                colorRectangles[i] = new System.Windows.Shapes.Rectangle()
                {
                    Width = 32,
                    Height = 32,
                    StrokeThickness = 1,
                    Stroke = Brushes.DimGray,
                    Fill = Brushes.Black,
                    Margin = new Thickness(1),
                    ToolTip = "Click a pixel in the main image window to see the index state after that pixel was decoded"
                };
                colorsPanel.Children.Add(colorRectangles[i]);
            }
        }

        /// <summary>
        /// Set the colors displayed by the window. There must be exactly 64 elements in the given enumerable.
        /// </summary>
        public void SetColors(IList<Pixel> colors)
        {
            if (colors.Count != 64)
            {
                throw new ArgumentException("The number of colors provided must be exactly 64");
            }

            for (int i = 0; i < colors.Count; i++)
            {
                Pixel color = colors[i];
                colorRectangles[i].Fill = new SolidColorBrush(
                    Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
                colorRectangles[i].ToolTip = $"R: {color.Red}, G: {color.Green}, B: {color.Blue}, A: {color.Alpha}";
            }
        }
    }
}
