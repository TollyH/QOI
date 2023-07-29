using System;

namespace QOI.Viewer
{
    public static class Utils
    {
        public static string FormatBytes(long bytes, int decimalPlaces)
        {
            int orderOfBinaryMagnitude = 0;
            double formattedValue = bytes;
            while (formattedValue > 1024 && orderOfBinaryMagnitude < 6)
            {
                formattedValue /= 1024;
                orderOfBinaryMagnitude++;
            }

            formattedValue = Math.Round(formattedValue, decimalPlaces);

            return orderOfBinaryMagnitude switch
            {
                1 => $"{formattedValue} KB",
                2 => $"{formattedValue} MB",
                3 => $"{formattedValue} GB",
                4 => $"{formattedValue} TB",
                5 => $"{formattedValue} PB",
                6 => $"{formattedValue} EB",  // Max possible with a C# long (64-bits)
                _ => $"{formattedValue} bytes",
            };
        }
    }
}
