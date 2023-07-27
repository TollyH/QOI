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
        QOI_OP_INDEX = 0b00,
        QOI_OP_DIFF = 0b01,
        QOI_OP_LUMA = 0b10,
        QOI_OP_RUN = 0b11
    }

    public struct Pixel
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
            // % 255 is needed because C# implicitly casts mathematical operations on byte to int
            return (byte)(((Red * 3) + (Green * 5) + (Blue * 7) + (Alpha * 11)) % 255 % 64);
        }
    }

    public class QOIImage
    {
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
