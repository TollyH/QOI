namespace QOI
{
    public enum ChannelType
    {
        RGB,
        RGBA
    }

    public enum ColorspaceType
    {
        sRGB,
        Linear
    }

    public enum ChunkType
    {
        QOI_OP_RGB,
        QOI_OP_RGBA,
        QOI_OP_INDEX,
        QOI_OP_DIFF,
        QOI_OP_LUMA,
        QOI_OP_RUN
    }

    public struct Pixel
    {
        public byte Red;
        public byte Green;
        public byte Blue;
        public byte Alpha = 255;

        public Pixel(byte red, byte green, byte blue, byte alpha = 255)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
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
