using System.Collections.Generic;
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
            // If the image has transparency, we have to use PNG at the cost of performance,
            // as BMP doesn't support transparency in GDI+.
            bitmap.Save(stream, image.Channels == ChannelType.RGBA ? ImageFormat.Png : ImageFormat.Bmp);

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public static BitmapImage ConvertToBitmapImageFilterChunks(this QOIImage image,
            Pixel[] debugPixels, IReadOnlySet<ChunkType> excludeChunks)
        {
            int width = (int)image.Width;
            int height = (int)image.Height;
            using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Pixel continuedRun = new(255, 255, 255);

            // Copy pixel array to new Bitmap object
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)bmpData.Scan0 + (y * bmpData.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        Pixel debugPixel = debugPixels[(y * width) + x];
                        if ((debugPixel == continuedRun && excludeChunks.Contains(ChunkType.QOI_OP_RUN))
                            || (debugPixel != continuedRun
                                && excludeChunks.Contains(QOIDecoder.InvertedDebugModeColors[debugPixel])))
                        {
                            row[x * 4] = 255;
                            row[(x * 4) + 1] = 255;
                            row[(x * 4) + 2] = 255;
                            row[(x * 4) + 3] = 255;
                        }
                        else
                        {
                            Pixel pixel = image.Pixels[(y * width) + x];
                            row[x * 4] = pixel.Blue;
                            row[(x * 4) + 1] = pixel.Green;
                            row[(x * 4) + 2] = pixel.Red;
                            row[(x * 4) + 3] = pixel.Alpha;
                        }
                    }
                }
            }

            bitmap.UnlockBits(bmpData);

            // Convert Bitmap to BitmapImage for use with WPF
            using MemoryStream stream = new();
            // If the image has transparency, we have to use PNG at the cost of performance,
            // as BMP doesn't support transparency in GDI+.
            bitmap.Save(stream, image.Channels == ChannelType.RGBA ? ImageFormat.Png : ImageFormat.Bmp);

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        public static QOIImage ConvertToQOIImage(this BitmapSource image)
        {
            // Convert BitmapSource to Bitmap
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

            bool hasAlpha = false;
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
                        if (pixel.Alpha != 255)
                        {
                            hasAlpha = true;
                        }
                    }
                }
            }
            qoiImage.Channels = hasAlpha ? ChannelType.RGBA : ChannelType.RGB;

            bitmap.UnlockBits(bmpData);
            bitmap.Dispose();
            return qoiImage;
        }
    }
}
