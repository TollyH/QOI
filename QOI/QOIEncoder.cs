﻿using System.Buffers.Binary;

namespace QOI
{
    public class QOIEncoder
    {
        /// <summary>
        /// If <see langword="false"/>, skip appending the QOI end marker to the end of the file.
        /// This should rarely, if ever, be changed from <see langword="true"/>.
        /// </summary>
        public bool InsertEndTag { get; set; } = true;

        /// <summary>
        /// Whether or not inserting the contents of <see cref="QOIImage.TrailingData"/> at the end of the file should be skipped.
        /// </summary>
        public bool StripTrailingData { get; set; } = false;

        public bool UseINDEXChunks { get; set; } = true;
        public bool UseDIFFChunks { get; set; } = true;
        public bool UseLUMAChunks { get; set; } = true;
        public bool UseRUNChunks { get; set; } = true;
        public bool UseRGBChunks { get; set; } = true;

        /// <summary>
        /// Encode a QOI image to a complete byte stream.
        /// </summary>
        /// <param name="image">A <see cref="QOIImage"/> instance filled with all the data to encode.</param>
        /// <returns>A fully encoded byte array.</returns>
        public byte[] Encode(QOIImage image)
        {
            // Pre-allocate enough memory for the worst case scenario
            // (14 byte header, 8 byte end marker, all pixels represented as a 5 byte QOI_OP_RGBA), all trailing data
            byte[] imageBytes = new byte[14 + 8 + (image.Width * image.Height * 5) + image.TrailingData.Length];
            Span<byte> byteSpan = imageBytes.AsSpan();

            // Header
            QOIImage.MagicBytes.CopyTo(imageBytes, 0);
            BinaryPrimitives.WriteUInt32BigEndian(byteSpan[4..8], image.Width);
            BinaryPrimitives.WriteUInt32BigEndian(byteSpan[8..12], image.Height);
            imageBytes[12] = (byte)image.Channels;
            imageBytes[13] = (byte)image.Colorspace;

            int written = EncodePixels(byteSpan[14..], image.Pixels);
            int index = written + 14;
            if (InsertEndTag)
            {
                QOIImage.EndMarker.CopyTo(imageBytes, index);
                index += QOIImage.EndMarker.Length;
            }
            if (!StripTrailingData)
            {
                image.TrailingData.CopyTo(imageBytes, index);
                index += image.TrailingData.Length;
            }
            return imageBytes[..index];
        }

        /// <summary>
        /// Encode an array of RGBA pixels into a QOI data stream.
        /// </summary>
        /// <param name="destination">
        /// The span of bytes to write encoded bytes into.
        /// </param>
        /// <param name="pixels">
        /// The pixels to encode into bytes.
        /// </param>
        /// <returns>The number of bytes written to the destination span.</returns>
        public int EncodePixels(Span<byte> destination, IReadOnlyList<Pixel> pixels)
        {
            Pixel previousPixel = new(0, 0, 0, 255);
            Pixel[] colorArray = new Pixel[64];

            int pixelIndex = 0;
            int dataIndex = 0;
            for (; pixelIndex < pixels.Count && dataIndex < destination.Length; pixelIndex++, dataIndex++)
            {
                Pixel pixel = pixels[pixelIndex];
                // These six variables are left un-computed until needed for performance
                byte hash = 0;
                // Used for DIFF chunk
                sbyte dr = 0;
                sbyte dg = 0;
                sbyte db = 0;
                // Used for LUMA chunk
                sbyte drDg = 0;
                sbyte dbDg = 0;

                ChunkType typeToEncode;

                int runLength = 0;
                while (UseRUNChunks && runLength < 62 && pixelIndex + runLength < pixels.Count
                    && previousPixel == pixels[pixelIndex + runLength])
                {
                    runLength++;
                }

                if (runLength > 0)
                {
                    typeToEncode = ChunkType.QOI_OP_RUN;
                }
                else if (UseINDEXChunks && colorArray[hash = pixel.ColorHash()] == pixel)
                {
                    typeToEncode = ChunkType.QOI_OP_INDEX;
                }
                else if (pixel.Alpha != previousPixel.Alpha)
                {
                    // Aside from an index lookup, writing an entire RGBA chunk
                    // is the only way that the alpha value may change,
                    // so we can skip doing any other checks if it has
                    typeToEncode = ChunkType.QOI_OP_RGBA;
                }
                // dg is defined first so it can be used for the LUMA check if this fails
                // without having to re-compute
                else if ((dg = (sbyte)(pixel.Green - previousPixel.Green)) is >= -2 and <= 1
                    && UseDIFFChunks && (dr = (sbyte)(pixel.Red - previousPixel.Red)) is >= -2 and <= 1
                    && (db = (sbyte)(pixel.Blue - previousPixel.Blue)) is >= -2 and <= 1)
                {
                    typeToEncode = ChunkType.QOI_OP_DIFF;
                }
                else if (UseLUMAChunks && dg is >= -32 and <= 31
                    && (drDg = (sbyte)((pixel.Red - previousPixel.Red) - (pixel.Green - previousPixel.Green))) is >= -8 and <= 7
                    && (dbDg = (sbyte)((pixel.Blue - previousPixel.Blue) - (pixel.Green - previousPixel.Green))) is >= -8 and <= 7)
                {
                    typeToEncode = ChunkType.QOI_OP_LUMA;
                }
                else
                {
                    typeToEncode = UseRGBChunks ? ChunkType.QOI_OP_RGB : ChunkType.QOI_OP_RGBA;
                }

                destination[dataIndex] = (byte)typeToEncode;
                switch (typeToEncode)
                {
                    case ChunkType.QOI_OP_RGB:
                        destination[++dataIndex] = pixel.Red;
                        destination[++dataIndex] = pixel.Green;
                        destination[++dataIndex] = pixel.Blue;
                        break;
                    case ChunkType.QOI_OP_RGBA:
                        destination[++dataIndex] = pixel.Red;
                        destination[++dataIndex] = pixel.Green;
                        destination[++dataIndex] = pixel.Blue;
                        destination[++dataIndex] = pixel.Alpha;
                        break;
                    case ChunkType.QOI_OP_INDEX:
                        destination[dataIndex] |= hash;
                        break;
                    case ChunkType.QOI_OP_DIFF:
                        destination[dataIndex] |= (byte)((dr + 2) << 4);
                        destination[dataIndex] |= (byte)((dg + 2) << 2);
                        destination[dataIndex] |= (byte)(db + 2);
                        break;
                    case ChunkType.QOI_OP_LUMA:
                        destination[dataIndex] |= (byte)(dg + 32);
                        destination[++dataIndex] |= (byte)((drDg + 8) << 4);
                        destination[dataIndex] |= (byte)(dbDg + 8);
                        break;
                    case ChunkType.QOI_OP_RUN:
                        pixelIndex += runLength - 1;
                        destination[dataIndex] |= (byte)(runLength - 1);
                        break;
                }

                previousPixel = pixels[pixelIndex];
                colorArray[previousPixel.ColorHash()] = previousPixel;
            }

            return dataIndex;
        }

        /// <summary>
        /// Encode and save a QOI image file.
        /// </summary>
        /// <param name="path">The path to save the encoded image file.</param>
        public void SaveImageFile(string path, QOIImage image)
        {
            File.WriteAllBytes(path, Encode(image));
        }

        /// <summary>
        /// Encode and save a QOI image file.
        /// </summary>
        /// <param name="uri">A URI pointing to where to save the encoded image file.</param>
        public void SaveImageFile(Uri uri, QOIImage image)
        {
            File.WriteAllBytes(uri.AbsolutePath, Encode(image));
        }

        /// <summary>
        /// Encode and save a QOI image file.
        /// </summary>
        /// <param name="file">The file info instance representing where to store the encoded image file.</param>
        public void SaveImageFile(FileInfo file, QOIImage image)
        {
            File.WriteAllBytes(file.FullName, Encode(image));
        }
    }
}
