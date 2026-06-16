using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace QOI.Viewer
{
    public static class QOIImageConverter
    {
        public static SKBitmap ConvertToSKBitmapImage(this QOIImage image)
        {
            int width = (int)image.Width;
            int height = (int)image.Height;
            SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));

            // Copy pixel array to new Bitmap object
            IntPtr bmpData = bitmap.GetPixels();
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)bmpData + (y * bitmap.RowBytes);
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

            return bitmap;
        }
        
        public static WriteableBitmap ConvertToAvaloniaBitmapImage(this QOIImage image)
        {
            int width = (int)image.Width;
            int height = (int)image.Height;
            WriteableBitmap bitmap = new(
                new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            // Copy pixel array to new Bitmap object
            using ILockedFramebuffer buffer = bitmap.Lock();
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)buffer.Address + (y * buffer.RowBytes);
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

            return bitmap;
        }

        public static WriteableBitmap ConvertToAvaloniaBitmapImageFilterChunks(this QOIImage image,
            Pixel[] debugPixels, IReadOnlySet<ChunkType> excludeChunks)
        {
            int width = (int)image.Width;
            int height = (int)image.Height;
            WriteableBitmap bitmap = new(
                new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            Pixel continuedRun = new(255, 255, 255);

            // Copy pixel array to new Bitmap object
            using ILockedFramebuffer buffer = bitmap.Lock();
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)buffer.Address + (y * buffer.RowBytes);
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

            return bitmap;
        }

        public static QOIImage ConvertToQOIImage(this SKBitmap image)
        {
            int width = image.Width;
            int height = image.Height;
            
            // Ensure pixel format matches.
            SKBitmap convertedImage;
            if (image.ColorType != SKColorType.Bgra8888)
            {
                convertedImage = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
                image.CopyTo(convertedImage);
            }
            else
            {
                // Pixel format is already correct, no conversion necessary.
                convertedImage = image;
            }
            
            QOIImage qoiImage = new((uint)width, (uint)height, ChannelType.RGBA, ColorspaceType.sRGB);
            
            // Copy pixel array to new QOIImage object
            IntPtr bmpData = convertedImage.GetPixels();
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    byte* row = (byte*)bmpData + (y * convertedImage.RowBytes);
                    for (int x = 0; x < width; x++)
                    {
                        qoiImage.Pixels[(y * width) + x] = new Pixel(
                            row[(x * 4) + 2],
                            row[(x * 4) + 1],
                            row[x * 4],
                            row[(x * 4) + 3]);
                    }
                }
            }

            return qoiImage;
        }

        public static QOIImage ConvertToQOIImage(this Bitmap image)
        {
            int width = image.PixelSize.Width;
            int height = image.PixelSize.Height;
            
            QOIImage qoiImage = new((uint)width, (uint)height, ChannelType.RGBA, ColorspaceType.sRGB);
            
            unsafe
            {
                fixed (Pixel* pixelBuffer = qoiImage.Pixels)
                {
                    using LockedFramebuffer targetBuffer = new((IntPtr)pixelBuffer, new PixelSize(width, height),
                        width * 4, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul, null);
                    image.CopyPixels(targetBuffer);
                }
            }

            return qoiImage;
        }
    }
}
