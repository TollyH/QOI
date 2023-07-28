namespace QOI
{
    public enum ChannelType : byte
    {
        RGB = 3,
        RGBA = 4
    }

    public enum ColorspaceType : byte
    {
        sRGB = 0,
        Linear = 1
    }

    public enum ChunkType : byte
    {
        QOI_OP_RGB = 0b11111110,
        QOI_OP_RGBA = 0b11111111,
        QOI_OP_INDEX = 0b00000000,
        QOI_OP_DIFF = 0b01000000,
        QOI_OP_LUMA = 0b10000000,
        QOI_OP_RUN = 0b11000000
    }

    public struct Pixel : IEquatable<Pixel>
    {
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Alpha;

        public Pixel(byte red, byte green, byte blue, byte alpha = 255)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public readonly byte ColorHash()
        {
            return (byte)(((Red * 3) + (Green * 5) + (Blue * 7) + (Alpha * 11)) % 64);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Pixel pixel && Equals(pixel);
        }

        public readonly bool Equals(Pixel other)
        {
            return Red == other.Red &&
                   Green == other.Green &&
                   Blue == other.Blue &&
                   Alpha == other.Alpha;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Red, Green, Blue, Alpha);
        }

        public static bool operator ==(Pixel left, Pixel right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Pixel left, Pixel right)
        {
            return !(left == right);
        }
    }

    public class QOIImage
    {
        public static readonly byte[] MagicBytes = new byte[4] { 113, 111, 105, 102 };  // 'qoif'
        public static readonly byte[] EndMarker = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 1 };

        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public ChannelType Channels { get; private set; }
        public ColorspaceType Colorspace { get; private set; }

        private Pixel[] _pixels;
        public Pixel[] Pixels
        {
            get => _pixels;
            set
            {
                if (value.Length != Width * Height)
                {
                    throw new ArgumentException($"Pixels array is an invalid size. Expected {Width * Height} pixels, got {value.Length}.");
                }
                _pixels = value;
            }
        }
        public byte[] TrailingData { get; set; } = Array.Empty<byte>();

        public QOIImage(uint width, uint height, ChannelType channels, ColorspaceType colorspace)
        {
            Width = width;
            Height = height;
            Channels = channels;
            Colorspace = colorspace;

            _pixels = new Pixel[Width * Height];
        }

        public QOIImage(uint width, uint height, ChannelType channels, ColorspaceType colorspace, Pixel[] pixels) : this(width, height, channels, colorspace)
        {
            if (pixels.Length != width * height)
            {
                throw new ArgumentException($"Pixels array is an invalid size. Expected {width * height} pixels, got {pixels.Length}.");
            }

            _pixels = pixels;
        }
    }
}
