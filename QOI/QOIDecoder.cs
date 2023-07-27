using System.Buffers.Binary;

namespace QOI
{
    public static class QOIDecoder
    {
        public static readonly byte[] MagicBytes = new byte[4] { 113, 111, 105, 102 };  // 'qoif'
        public static readonly byte[] EndMarker = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 1 };

        /// <summary>
        /// Decode a QOI image byte stream.
        /// </summary>
        /// <param name="data">A byte stream containing the entirety of a QOI file.</param>
        /// <param name="requireEndTag">
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from the data stream.
        /// </param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public static QOIImage Decode(Span<byte> data, bool requireEndTag = true)
        {
            if (!data[..4].SequenceEqual(MagicBytes))
            {
                throw new ArgumentException("Given bytes do not start with the correct header");
            }

            uint width = BinaryPrimitives.ReadUInt32BigEndian(data[4..8]);
            uint height = BinaryPrimitives.ReadUInt32BigEndian(data[8..12]);
            byte channels = data[12];
            byte colorspace = data[13];

            if (!Enum.IsDefined(typeof(ChannelType), channels))
            {
                throw new ArgumentException($"Number of channels is invalid. Expected 3 or 4, got {channels}");
            }
            if (!Enum.IsDefined(typeof(ColorspaceType), colorspace))
            {
                throw new ArgumentException($"Colorspace ID is invalid. Expected 0 or 1, got {colorspace}");
            }

            QOIImage image = new(width, height, (ChannelType)channels, (ColorspaceType)colorspace)
            {
                Pixels = DecodePixels(data[14..], width * height, out byte[] trailingData, requireEndTag),
                TrailingData = trailingData
            };

            return image;
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header should not be included, but the end marker must be included,
        /// unless <paramref name="requireEndTag"/> is <see langword="false"/>.
        /// </param>
        /// <param name="trailingData">
        /// A byte array of any extra data appended on to the end of the QOI data stream.
        /// Will be an empty array if there is none.
        /// </param>
        /// <param name="requireEndTag">
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from the data stream.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public static Pixel[] DecodePixels(Span<byte> data, uint pixelCount, out byte[] trailingData, bool requireEndTag = true)
        {
            Pixel[] decodedPixels = new Pixel[pixelCount];
            Pixel previousPixel = new(0, 0, 0, 255);
            Pixel[] colorArray = new Pixel[64];

            int pixelIndex = 0;
            int dataIndex = 0;
            for (; dataIndex < data.Length && pixelIndex < pixelCount; dataIndex++, pixelIndex++)
            {
                colorArray[previousPixel.ColorHash()] = previousPixel;
                byte tagByte = data[dataIndex];
                switch ((ChunkType)tagByte)
                {
                    case ChunkType.QOI_OP_RGB:
                        decodedPixels[pixelIndex] = new Pixel(data[++dataIndex], data[++dataIndex], data[++dataIndex], previousPixel.Alpha);
                        break;
                    case ChunkType.QOI_OP_RGBA:
                        decodedPixels[pixelIndex] = new Pixel(data[++dataIndex], data[++dataIndex], data[++dataIndex], data[++dataIndex]);
                        break;
                    default:
                        switch ((ChunkType)(tagByte >> 6))
                        {
                            case ChunkType.QOI_OP_INDEX:
                                decodedPixels[pixelIndex] = colorArray[tagByte];
                                break;
                            case ChunkType.QOI_OP_DIFF:
                                {
                                    int redDiff = ((0b00110000 & tagByte) >> 4) - 2;
                                    int greenDiff = ((0b00001100 & tagByte) >> 2) - 2;
                                    int blueDiff = (0b00000011 & tagByte) - 2;
                                    decodedPixels[pixelIndex] = new Pixel(
                                        (byte)(previousPixel.Red + redDiff),
                                        (byte)(previousPixel.Green + greenDiff),
                                        (byte)(previousPixel.Blue + blueDiff),
                                        previousPixel.Alpha);
                                    break;
                                }
                            case ChunkType.QOI_OP_LUMA:
                                {
                                    int greenDiff = (0b00111111 & tagByte) - 32;
                                    byte nextByte = data[++dataIndex];
                                    int redDiff = ((0b11110000 & nextByte) >> 4) - 8;
                                    int blueDiff = ((0b00001111 & nextByte) >> 4) - 8;
                                    decodedPixels[pixelIndex] = new Pixel(
                                        (byte)(previousPixel.Red + redDiff - greenDiff),
                                        (byte)(previousPixel.Green + greenDiff),
                                        (byte)(previousPixel.Blue + blueDiff - greenDiff),
                                        previousPixel.Alpha);
                                    break;
                                }
                            case ChunkType.QOI_OP_RUN:
                                {
                                    int runLength = (0b00111111 & tagByte) + 1;
                                    for (int i = 0; i < runLength; i++)
                                    {
                                        decodedPixels[pixelIndex++] = previousPixel;
                                    }
                                    pixelIndex--;
                                    break;
                                }
                        }
                        break;
                }
                previousPixel = decodedPixels[pixelIndex];
            }

            if (!data[dataIndex..(dataIndex + 8)].SequenceEqual(EndMarker))
            {
                if (requireEndTag)
                {
                    throw new ArgumentException("End tag was missing from data stream.");
                }
            }
            else
            {
                dataIndex += 8;
            }

            trailingData = data[dataIndex..].ToArray();
            return decodedPixels;
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header should not be included, but the end marker must be included,
        /// unless <paramref name="requireEndTag"/> is <see langword="false"/>.
        /// </param>
        /// <param name="requireEndTag">
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from the data stream.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public static Pixel[] DecodePixels(Span<byte> data, uint pixelCount, bool requireEndTag = true)
        {
            return DecodePixels(data, pixelCount, out _, requireEndTag);
        }

        /// <summary>
        /// Decode a QOI image file.
        /// </summary>
        /// <param name="path">The path to the image file to decode.</param>
        /// <param name="requireEndTag">
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from the data stream.
        /// </param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public static QOIImage DecodeImageFile(string path, bool requireEndTag = true)
        {
            return Decode(File.ReadAllBytes(path), requireEndTag);
        }

        /// <summary>
        /// Decode a QOI image file.
        /// </summary>
        /// <param name="uri">A URI pointing to the image file to decode.</param>
        /// <param name="requireEndTag">
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from the data stream.
        /// </param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public static QOIImage DecodeImageFile(Uri uri, bool requireEndTag = true)
        {
            return Decode(File.ReadAllBytes(uri.AbsolutePath), requireEndTag);
        }

        /// <summary>
        /// Decode a QOI image file.
        /// </summary>
        /// <param name="file">The file info instance representing the image file to decode.</param>
        /// <param name="requireEndTag">
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from the data stream.
        /// </param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public static QOIImage DecodeImageFile(FileInfo file, bool requireEndTag = true)
        {
            return Decode(File.ReadAllBytes(file.FullName), requireEndTag);
        }
    }
}
