using System.Buffers.Binary;
using System.Collections.Immutable;

namespace QOI
{
    public class QOIDecoder
    {
        /// <summary>
        /// <see langword="true"/> by default to throw an <see cref="ArgumentException"/> if the end tag is missing from a decoded image
        /// </summary>
        public bool RequireEndTag { get; set; } = true;

        /// <summary>
        /// If <see langword="true"/>, all pixel colors are replaced with ones representing what chunk type was used to encode each pixel.
        /// </summary>
        /// <remarks>Only affects Decode and DecodeImageFile methods, not DecodePixels</remarks>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Set whenever a decoding method is called to signify whether or not an end tag was present in the previously decoded image
        /// </summary>
        public bool EndTagWasPresent { get; private set; } = true;

        /// <summary>
        /// The number of each chunk type decoded in the last pixel decode operation.
        /// </summary>
        public Dictionary<ChunkType, int> ChunksDecoded { get; } = new();

        /// <summary>
        /// The number of bytes used to store all of the pixels in the last decoded image.
        /// </summary>
        public int PixelDataLength { get; private set; } = 0;

        /// <summary>
        /// Whether to store the full state of the index for every decoded pixel.
        /// Will impact performance if set to <see langword="true"/>.
        /// </summary>
        public bool StoreFullIndexHistory { get; set; } = false;

        /// <summary>
        /// The state history of the index for every decoded pixel in the last decoded image.
        /// Only updated if <see cref="StoreFullIndexHistory"/> is <see langword="true"/>.
        /// Will be <see langword="null"/> if no images have been decoded while this is the case.
        /// </summary>
        public Pixel[][]? IndexHistory { get; private set; } = null;

        public static readonly ImmutableDictionary<ChunkType, Pixel> DebugModeColors = new Dictionary<ChunkType, Pixel>()
        {
            { ChunkType.QOI_OP_RGB, new Pixel(255, 0, 0) },
            { ChunkType.QOI_OP_RGBA, new Pixel(0, 255, 0) },
            { ChunkType.QOI_OP_INDEX, new Pixel(0, 0, 255) },
            { ChunkType.QOI_OP_DIFF, new Pixel(255, 255, 0) },
            { ChunkType.QOI_OP_LUMA, new Pixel(255, 0, 255) },
            { ChunkType.QOI_OP_RUN, new Pixel(0, 255, 255) },
        }.ToImmutableDictionary();
        public static readonly ImmutableDictionary<Pixel, ChunkType> InvertedDebugModeColors =
            DebugModeColors.ToDictionary(kv => kv.Value, kv => kv.Key).ToImmutableDictionary();

        /// <summary>
        /// Decode a QOI image byte stream.
        /// </summary>
        /// <param name="data">A byte stream containing the entirety of a QOI file.</param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public QOIImage Decode(Span<byte> data)
        {
            if (!data[..4].SequenceEqual(QOIImage.MagicBytes))
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
                Pixels = DebugMode
                    ? GenerateDebugPixels(data[14..], width * height, out byte[] trailingData)
                    : DecodePixels(data[14..], width * height, out trailingData)
            };

            if (trailingData.Length < 8 || !trailingData[..8].SequenceEqual(QOIImage.EndMarker))
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
                trailingData = trailingData[8..];
            }
            image.TrailingData = trailingData;

            return image;
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header must not be included.
        /// </param>
        /// <param name="trailingData">
        /// A byte array of any extra data appended on to the end of the QOI data stream.
        /// Contains the end marker if present.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public Pixel[] DecodePixels(Span<byte> data, uint pixelCount, out byte[] trailingData)
        {
            ChunksDecoded.Clear();
            foreach (ChunkType type in Enum.GetValues<ChunkType>())
            {
                ChunksDecoded[type] = 0;
            }

            Pixel[] decodedPixels = new Pixel[pixelCount];
            Pixel previousPixel = new(0, 0, 0, 255);
            Pixel[] colorArray = new Pixel[64];

            if (StoreFullIndexHistory)
            {
                IndexHistory = new Pixel[pixelCount][];

                for (int i = 0; i < pixelCount; i++)
                {
                    IndexHistory[i] = new Pixel[64];
                }
            }

            int pixelIndex = 0;
            int dataIndex = 0;
            for (; dataIndex < data.Length && pixelIndex < pixelCount; dataIndex++, pixelIndex++)
            {
                byte tagByte = data[dataIndex];
                switch ((ChunkType)tagByte)
                {
                    case ChunkType.QOI_OP_RGB:
                        decodedPixels[pixelIndex] = new Pixel(data[++dataIndex], data[++dataIndex], data[++dataIndex], previousPixel.Alpha);
                        ChunksDecoded[ChunkType.QOI_OP_RGB]++;
                        break;
                    case ChunkType.QOI_OP_RGBA:
                        decodedPixels[pixelIndex] = new Pixel(data[++dataIndex], data[++dataIndex], data[++dataIndex], data[++dataIndex]);
                        ChunksDecoded[ChunkType.QOI_OP_RGBA]++;
                        break;
                    default:
                        switch ((ChunkType)(tagByte & 0b11000000))
                        {
                            case ChunkType.QOI_OP_INDEX:
                                decodedPixels[pixelIndex] = colorArray[tagByte];
                                ChunksDecoded[ChunkType.QOI_OP_INDEX]++;
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
                                    ChunksDecoded[ChunkType.QOI_OP_DIFF]++;
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
                                    ChunksDecoded[ChunkType.QOI_OP_LUMA]++;
                                    break;
                                }
                            case ChunkType.QOI_OP_RUN:
                                {
                                    int runLength = (0b00111111 & tagByte) + 1;
                                    for (int i = 0; i < runLength; i++)
                                    {
                                        if (StoreFullIndexHistory)
                                        {
                                            colorArray.CopyTo(IndexHistory![pixelIndex], 0);
                                        }

                                        decodedPixels[pixelIndex++] = previousPixel;
                                    }
                                    pixelIndex--;
                                    ChunksDecoded[ChunkType.QOI_OP_RUN]++;
                                    break;
                                }
                        }
                        break;
                }
                previousPixel = decodedPixels[pixelIndex];
                colorArray[previousPixel.ColorHash()] = previousPixel;

                if (StoreFullIndexHistory)
                {
                    colorArray.CopyTo(IndexHistory![pixelIndex], 0);
                }
            }

            PixelDataLength = dataIndex;
            trailingData = data[dataIndex..].ToArray();
            return decodedPixels;
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header must not be included, but the end marker must be included,
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

        /// <summary>
        /// Generate an image where each pixel value is based on the chunk type used to encode it, instead of it's actual color value.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header must not be included.
        /// </param>
        /// <param name="trailingData">
        /// A byte array of any extra data appended on to the end of the QOI data stream.
        /// Contains the end marker if present.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public Pixel[] GenerateDebugPixels(Span<byte> data, uint pixelCount, out byte[] trailingData)
        {
            ChunksDecoded.Clear();
            foreach (ChunkType type in Enum.GetValues<ChunkType>())
            {
                ChunksDecoded[type] = 0;
            }

            Pixel[] generatedPixels = new Pixel[pixelCount];

            int pixelIndex = 0;
            int dataIndex = 0;
            for (; dataIndex < data.Length && pixelIndex < pixelCount; dataIndex++, pixelIndex++)
            {
                byte tagByte = data[dataIndex];
                switch ((ChunkType)tagByte)
                {
                    case ChunkType.QOI_OP_RGB:
                        generatedPixels[pixelIndex] = DebugModeColors[ChunkType.QOI_OP_RGB];
                        ChunksDecoded[ChunkType.QOI_OP_RGB]++;
                        dataIndex += 3;
                        break;
                    case ChunkType.QOI_OP_RGBA:
                        generatedPixels[pixelIndex] = DebugModeColors[ChunkType.QOI_OP_RGBA];
                        ChunksDecoded[ChunkType.QOI_OP_RGBA]++;
                        dataIndex += 4;
                        break;
                    default:
                        switch ((ChunkType)(tagByte & 0b11000000))
                        {
                            case ChunkType.QOI_OP_INDEX:
                                generatedPixels[pixelIndex] = DebugModeColors[ChunkType.QOI_OP_INDEX];
                                ChunksDecoded[ChunkType.QOI_OP_INDEX]++;
                                break;
                            case ChunkType.QOI_OP_DIFF:
                                generatedPixels[pixelIndex] = DebugModeColors[ChunkType.QOI_OP_DIFF];
                                ChunksDecoded[ChunkType.QOI_OP_DIFF]++;
                                break;
                            case ChunkType.QOI_OP_LUMA:
                                generatedPixels[pixelIndex] = DebugModeColors[ChunkType.QOI_OP_LUMA];
                                ChunksDecoded[ChunkType.QOI_OP_LUMA]++;
                                dataIndex++;
                                break;
                            case ChunkType.QOI_OP_RUN:
                                int runLength = (0b00111111 & tagByte) + 1;
                                generatedPixels[pixelIndex++] = DebugModeColors[ChunkType.QOI_OP_RUN];
                                for (int i = 1; i < runLength; i++)
                                {
                                    generatedPixels[pixelIndex++] = new Pixel(255, 255, 255);
                                }
                                pixelIndex--;
                                ChunksDecoded[ChunkType.QOI_OP_RUN]++;
                                break;
                        }
                        break;
                }
            }

            PixelDataLength = dataIndex;
            trailingData = data[dataIndex..].ToArray();
            return generatedPixels;
        }

        /// <summary>
        /// Generate an image where each pixel value is based on the chunk type used to encode it, instead of it's actual color value.
        /// </summary>
        /// <param name="data">
        /// The data from the QOI file. The file header must not be included.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public Pixel[] GenerateDebugPixels(Span<byte> data, uint pixelCount)
        {
            return GenerateDebugPixels(data, pixelCount, out _);
        }
    }
}
