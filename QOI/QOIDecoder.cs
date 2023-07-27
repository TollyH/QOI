using System.Buffers.Binary;

namespace QOI
{
    public class QOIDecoder
    {
        public static readonly byte[] MagicBytes = new byte[4] { 113, 111, 105, 102 };  // 'qoif'
        public static readonly byte[] EndMarker = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 1 };

        /// <summary>
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from a decoded image
        /// </summary>
        public bool RequireEndTag { get; set; } = true;

        /// <summary>
        /// Set whenever a decoding method is called to signify whether or not an end tag was present in the previously decoded image
        /// </summary>
        public bool EndTagWasPresent { get; private set; } = true;

        /// <summary>
        /// Decode a QOI image byte stream.
        /// </summary>
        /// <param name="data">A byte stream containing the entirety of a QOI file.</param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public QOIImage Decode(Span<byte> data)
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
                Pixels = DecodePixels(data[14..], width * height, out byte[] trailingData),
                TrailingData = trailingData
            };

            return image;
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header should not be included, but the end marker must be included,
        /// unless <see cref="RequireEndTag"/> is <see langword="false"/>.
        /// </param>
        /// <param name="trailingData">
        /// A byte array of any extra data appended on to the end of the QOI data stream.
        /// Will be an empty array if there is none.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public Pixel[] DecodePixels(Span<byte> data, uint pixelCount, out byte[] trailingData)
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
                                    int blueDiff = (0b00001111 & nextByte) - 8;
                                    decodedPixels[pixelIndex] = new Pixel(
                                        (byte)(previousPixel.Red + redDiff + greenDiff),
                                        (byte)(previousPixel.Green + greenDiff),
                                        (byte)(previousPixel.Blue + blueDiff + greenDiff),
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

            if (dataIndex + 8 > data.Length || !data[dataIndex..(dataIndex + 8)].SequenceEqual(EndMarker))
            {
                EndTagWasPresent = false;
                if (RequireEndTag)
                {
                    throw new ArgumentException("End tag was missing from data stream.");
                }
            }
            else
            {
                EndTagWasPresent = true;
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
        /// unless <see cref="RequireEndTag"/> is <see langword="false"/>.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public Pixel[] DecodePixels(Span<byte> data, uint pixelCount)
        {
            return DecodePixels(data, pixelCount, out _);
        }

        /// <summary>
        /// Decode a QOI image file.
        /// </summary>
        /// <param name="path">The path to the image file to decode.</param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public QOIImage DecodeImageFile(string path)
        {
            return Decode(File.ReadAllBytes(path));
        }

        /// <summary>
        /// Decode a QOI image file.
        /// </summary>
        /// <param name="uri">A URI pointing to the image file to decode.</param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public QOIImage DecodeImageFile(Uri uri)
        {
            return Decode(File.ReadAllBytes(uri.AbsolutePath));
        }

        /// <summary>
        /// Decode a QOI image file.
        /// </summary>
        /// <param name="file">The file info instance representing the image file to decode.</param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public QOIImage DecodeImageFile(FileInfo file)
        {
            return Decode(File.ReadAllBytes(file.FullName));
        }
    }
}
