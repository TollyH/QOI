using System.Buffers.Binary;

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
            for (; pixelIndex < pixels.Count; pixelIndex++)
            {
                colorArray[previousPixel.ColorHash()] = previousPixel;
                Pixel pixel = pixels[pixelIndex];

                previousPixel = pixel;
            }

            return dataIndex;
        }
    }
}
