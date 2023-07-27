using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace QOI.Viewer
{
    public static class QOIImageConverter
    {
        public static BitmapImage ConvertToBitmapImage(this QOIImage image)
        {
            int width = (int)image.Width;
            int height = (int)image.Height;
            using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            // Copy pixel array to new Bitmap object
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)bmpData.Scan0 + (y * bmpData.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        Pixel pixel = image.Pixels[(y * width) + x];
                        row[x * 4] = pixel.Blue;
                        row[(x * 4) + 1] = pixel.Green;
                        row[(x * 4) + 2] = pixel.Red;
                        row[(x * 4) + 3] = pixel.Alpha;
                    }
                }
            }

            bitmap.UnlockBits(bmpData);

            // Convert Bitmap to BitmapImage for use with WPF
            using MemoryStream stream = new();
            bitmap.Save(stream, ImageFormat.Bmp);

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }
}
