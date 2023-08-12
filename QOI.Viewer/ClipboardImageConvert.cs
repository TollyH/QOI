using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace QOI.Viewer
{
    // Based on https://stackoverflow.com/a/25751020/8740147.
    // Needed because WPF's built-in Clipboard.GetImage() fails with images copied from some programs.
    internal static class ClipboardImageConvert
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource? GetBitmapSourceFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                IDataObject clipboardData = Clipboard.GetDataObject();
                if (clipboardData != null)
                {
                    if (clipboardData.GetDataPresent(DataFormats.Bitmap))
                    {
                        Bitmap bitmap = (Bitmap)clipboardData.GetData(DataFormats.Bitmap);
                        IntPtr hBitmap = bitmap.GetHbitmap();
                        try
                        {
                            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap,
                                IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        }
                        finally
                        {
                            _ = DeleteObject(hBitmap);
                        }
                    }
                }
            }
            return null;
        }
    }
}
