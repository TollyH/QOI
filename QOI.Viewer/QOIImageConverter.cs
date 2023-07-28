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
            bitmap.Save(stream, ImageFormat.Png);

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public static QOIImage ConvertToQOIImage(this BitmapImage image)
        {
            // Convert BitmapImage to Bitmap
            Bitmap bitmap;
            using (MemoryStream outStream = new())
            {
                BitmapEncoder enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(image));
                enc.Save(outStream);
                bitmap = new Bitmap(outStream);
            }

            int width = bitmap.Width;
            int height = bitmap.Height;
            QOIImage qoiImage = new((uint)width, (uint)height, ChannelType.RGBA, ColorspaceType.sRGB);

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            // Copy pixel array to new QOIImage object
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)bmpData.Scan0 + (y * bmpData.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        Pixel pixel = new(row[(x * 4) + 2], row[(x * 4) + 1], row[x * 4], row[(x * 4) + 3]);
                        qoiImage.Pixels[(y * width) + x] = pixel;
                    }
                }
            }

            bitmap.UnlockBits(bmpData);
            bitmap.Dispose();
            return qoiImage;
        }
    }
}
