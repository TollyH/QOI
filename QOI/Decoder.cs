using System.Buffers.Binary;

namespace QOI
{
    public static class Decoder
    {
        public static readonly byte[] MagicBytes = new byte[] { 113, 111, 105, 102 };  // 'qoif'

        /// <summary>
        /// Decode a QOI image byte stream.
        /// </summary>
        /// <param name="data">A byte stream containing the entirety of a QOI file.</param>
        /// <returns>A fully decoded <see cref="QOIImage"/> instance.</returns>
        public static QOIImage Decode(Span<byte> data)
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
                Pixels = DecodePixels(data[14..], out byte[] trailingData),
                TrailingData = trailingData
            };

            return image;
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">The data from the QOI file. The file header should not be included.</param>
        /// <param name="trailingData">
        /// A byte array of any extra data appended on to the end of the QOI data stream.
        /// Will be an empty array if there is none.
        /// </param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public static Pixel[] DecodePixels(Span<byte> data, out byte[] trailingData)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Decode a QOI image data stream into an array of RGBA pixels.
        /// </summary>
        /// <param name="data">The data from the QOI file. The file header should not be included.</param>
        /// <returns>An array of <see cref="Pixel"/> instances.</returns>
        public static Pixel[] DecodePixels(Span<byte> data)
        {
            return DecodePixels(data, out _);
        }
    }
}
