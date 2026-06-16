using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace QOI.Viewer
{
    /// <summary>
    /// Interaction logic for IndexDebugWindow.xaml
    /// </summary>
    public partial class IndexDebugWindow : Window
    {
        public int? SelectedIndexPosition
        {
            get;
            private set
            {
                if (field is not null)
                {
                    // Reset outline of any existing selection
                    colorRectangles[field.Value].Stroke = Brushes.DimGray;
                }

                if (value is not null)
                {
                    // Mark the new selection
                    colorRectangles[value.Value].Stroke = Brushes.Red;
                }

                field = value;

                IndexSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int? SelectedItemLastEditedByIndex => SelectedIndexPosition is null
            ? null
            : colorRectangles[SelectedIndexPosition.Value].Tag as int?;

        public event EventHandler? IndexSelectionChanged;

        private readonly Rectangle[] colorRectangles = new Rectangle[64];

        public IndexDebugWindow(Window? owner)
        {
            InitializeComponent();

            Owner = owner;

            for (int i = 0; i < colorRectangles.Length; i++)
            {
                colorRectangles[i] = new Rectangle()
                {
                    Width = 32,
                    Height = 32,
                    StrokeThickness = 1,
                    Stroke = Brushes.DimGray,
                    Fill = Brushes.Black,
                    Margin = new Thickness(1)
                };
                ToolTip.SetTip(colorRectangles[i],
                    "Click a pixel in the main image window to see the index state after that pixel was decoded");

                // Create a copy of the index variable so that its value is retained in the anonymous function below
                int position = i;
                colorRectangles[i].PointerPressed += (_, e) =>
                {
                    SelectPosition(position);
                    e.Handled = true;
                };

                colorsPanel.Children.Add(colorRectangles[i]);
            }
        }
        
        public IndexDebugWindow() : this(null) { }

        /// <summary>
        /// Set the colors displayed by the window. There must be exactly 64 elements in the given enumerable.
        /// </summary>
        public void SetColors(IList<QOIDecoder.IndexHistoryItem> colors)
        {
            if (colors.Count != 64)
            {
                throw new ArgumentException("The number of colors provided must be exactly 64");
            }

            // Information about the existing selection may now be incorrect as the index position has changed,
            // so revert to having nothing selected.
            SelectedIndexPosition = null;

            for (int i = 0; i < colors.Count; i++)
            {
                QOIDecoder.IndexHistoryItem item = colors[i];

                colorRectangles[i].Fill = new SolidColorBrush(
                    Color.FromArgb(item.PixelColor.Alpha, item.PixelColor.Red, item.PixelColor.Green, item.PixelColor.Blue));
                ToolTip.SetTip(colorRectangles[i],
                    $"R: {item.PixelColor.Red}, G: {item.PixelColor.Green}, B: {item.PixelColor.Blue}, A: {item.PixelColor.Alpha}" +
                    $"\nLast updated by pixel at index {item.LastModifyingPixelIndex}");
                colorRectangles[i].Tag = item.LastModifyingPixelIndex;
            }
        }

        private void SelectPosition(int? position)
        {
            SelectedIndexPosition = position;
        }

        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            SelectedIndexPosition = null;
        }
    }
}
