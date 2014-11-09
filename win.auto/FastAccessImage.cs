using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace win.auto
{
    /// <summary>
    /// * The standard .NET Imaging class's GetPixel() is too slow.
    /// * Description
    /// * Matching
    /// </summary>
    public class FastAccessImage
    {
        public string Description;
        public PixelFormat PixelFormat;
        public ColorPalette ColorPalette;
        public int Width;
        public int Height;
        public int Stride;
        public byte[] Bytes;

        public FastAccessImage(string bitmapPath)
            : this(new Bitmap(bitmapPath))
        {
            this.Description = bitmapPath;
        }

        public FastAccessImage(Bitmap bmp)
        {
            this.PixelFormat = bmp.PixelFormat;
            this.ColorPalette = bmp.Palette;
            this.Width = bmp.Width;
            this.Height = bmp.Height;
            
            BitmapData bmpData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height), 
                ImageLockMode.ReadOnly, 
                bmp.PixelFormat
            );

            this.Stride = bmpData.Stride;
            this.Bytes = new byte[bmpData.Stride * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, this.Bytes, 0, this.Bytes.Length);

            bmp.UnlockBits(bmpData);
        }

        public FastAccessImage(FastAccessImage other, Rectangle subSection)
        {
            if (!Geometry.RectangleContainsRectangle(new Rectangle(Point.Empty, other.GetSize()), subSection))
            {
                throw new ArgumentException("Rectangle not within the image");
            }

            this.PixelFormat = other.PixelFormat;
            var bpp = Image.GetPixelFormatSize(this.PixelFormat) / 8;
            this.ColorPalette = other.ColorPalette;
            this.Width = subSection.Width;
            this.Height = subSection.Height;
            this.Stride = subSection.Width * bpp;
            this.Bytes = new byte[this.Stride * this.Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var thisBaseIndex = y * Stride + x * bpp;
                    var otherBaseIndex = ((subSection.Y + y) * other.Stride + (subSection.X + x) * bpp);
                    for (int i = 0; i < bpp; i++)
                    {
                        this.Bytes[thisBaseIndex + i] = other.Bytes[otherBaseIndex + i];
                    }
                }
            }
        }

        public Color GetPixel(Point p)
        {
            return GetPixel(p.X, p.Y);
        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x > Width || y > Height)
            {
                throw new ArgumentException(String.Format(
                    "GetPixel({0},{1}) on an image of size [{2},{3}]", 
                    x, y, Width, Height
                ));
            }

            var baseIndex = y * Stride + x * Image.GetPixelFormatSize(this.PixelFormat) / 8;

            switch(this.PixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                    return Color.FromArgb(
                        Bytes[baseIndex + 3],   // Alpha
                        Bytes[baseIndex + 2],   // Red
                        Bytes[baseIndex + 1],   // Green
                        Bytes[baseIndex]        // Blue
                    );
                case PixelFormat.Format24bppRgb:
                    return Color.FromArgb(
                        Bytes[baseIndex + 2],   // Red
                        Bytes[baseIndex + 1],   // Green
                        Bytes[baseIndex]        // Blue
                    );
                case PixelFormat.Format8bppIndexed:
                    return this.ColorPalette.Entries[Bytes[baseIndex]];
                default:
                    throw new InvalidOperationException(String.Format(
                        "{0} is not a supported pixel format.",
                        this.PixelFormat
                    ));
            }
        }

        public FastAccessImage Subsection(Rectangle r)
        {
            return new FastAccessImage(this, r);
        }

        private bool MatchPixelUnsafe(FastAccessImage image, int x, int y)
        {
            return MatchPixelUnsafe(image, x, y, 0);
        }

        private bool MatchPixelUnsafe(FastAccessImage image, int x, int y, int channelAllowance)
        {
            int baseIndex = y * Stride + x * 4;
            return Math.Abs(Bytes[baseIndex] - image.Bytes[baseIndex]) <= channelAllowance &&
                Math.Abs(Bytes[baseIndex + 1] - image.Bytes[baseIndex + 1]) <= channelAllowance &&
                Math.Abs(Bytes[baseIndex + 2] - image.Bytes[baseIndex + 2]) <= channelAllowance;
        }

        private bool MatchPixelUnsafe(int x, int y, FastAccessImage image, int imageX, int imageY, int channelAllowance)
        {
            int baseIndex = y * Stride + x * 4;
            int imageBaseIndex = imageY * image.Stride + imageX * 4;
            return Math.Abs(Bytes[baseIndex] - image.Bytes[imageBaseIndex]) <= channelAllowance &&
                Math.Abs(Bytes[baseIndex + 1] - image.Bytes[imageBaseIndex + 1]) <= channelAllowance &&
                Math.Abs(Bytes[baseIndex + 2] - image.Bytes[imageBaseIndex + 2]) <= channelAllowance;
        }

        public bool MatchPixel(FastAccessImage image, int x, int y, int channelAllowance)
        {
            if (Width != image.Width || Height != image.Height)
            {
                throw new ArgumentException("Can only call " + System.Reflection.MethodBase.GetCurrentMethod() +
                    " on images of the same size");
            }
            return MatchPixelUnsafe(image, x, y, channelAllowance);
        }

        public bool MatchArea(Point offset, Point refOffset, List<Rectangle> rectangles,
            FastAccessImage refImage, double overallSimilarity, int channelAllowance)
        {
            int totalSize = 0;
            rectangles.ForEach(delegate(Rectangle r) { totalSize += r.Height * r.Width; });

            int matchingPixels = 0;
            int mismatchingPixels = 0;
            int neededMatchingPixels = (int)(totalSize * overallSimilarity);
            int allowedMismatchingPixels = (int)((1 - overallSimilarity) * totalSize);

            // Floating point handling
            if (neededMatchingPixels > totalSize)
            {
                neededMatchingPixels = totalSize;
            }
            if (allowedMismatchingPixels < 0)
            {
                allowedMismatchingPixels = 0;
            }

            foreach (Rectangle r in rectangles)
            {
                int xBounds = r.X + r.Width;
                int yBounds = r.Y + r.Height;
                for (int x = r.X; x < xBounds; x++)
                {
                    for (int y = r.Y; y < yBounds; y++)
                    {
                        if (MatchPixelUnsafe(x + offset.X, y + offset.Y, refImage, x + refOffset.X, y + refOffset.Y, channelAllowance))
                        {
                            ++matchingPixels;
                            if (matchingPixels > neededMatchingPixels)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            ++mismatchingPixels;
                            if (mismatchingPixels > allowedMismatchingPixels)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        public List<Point> DifferentPixels(FastAccessImage image)
        {
            if (Width != image.Width || Height != image.Height)
            {
                throw new ArgumentException("Can only call " + System.Reflection.MethodBase.GetCurrentMethod() +
                    " on images of the same size");
            }

            List<Point> returnValue = new List<Point>();
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    if (!MatchPixelUnsafe(image, x, y))
                    {
                        returnValue.Add(new Point(x, y));
                    }
                }
            }
            return returnValue;
        }

        public Bitmap GetBitmap()
        {
            Bitmap bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            int numberOfBytes = bmpData.Stride * bmp.Height;
            System.Runtime.InteropServices.Marshal.Copy(Bytes, 0, ptr, numberOfBytes);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        public void ToBlackAndWhite()
        {
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    int index = y * Stride + x * 4;
                    byte grayScale = (byte)(Bytes[index + 2] * .3 + Bytes[index + 1] * .59 + Bytes[index] * .11);
                    byte blackOrWhite = (grayScale > 128) ? (byte)255 : (byte)0;
                    Bytes[index + 2] = blackOrWhite;
                    Bytes[index + 1] = blackOrWhite;
                    Bytes[index] = blackOrWhite;

                    //byte grayScale = (byte)(Bytes[index + 2] * .3 + Bytes[index + 1] * .59 + Bytes[index] * .11);
                    //grayScale = (grayScale > 255 / 2) ? byte.MaxValue : (byte)0;
                    //Bytes[index + 2] = grayScale;
                    //Bytes[index + 1] = grayScale;
                    //Bytes[index] = grayScale;
                }
            }
        }

        public void Invert()
        {
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    int index = y * Stride + x * 4;
                    Bytes[index + 2] = (byte)(255 - Bytes[index + 2]);
                    Bytes[index + 1] = (byte)(255 - Bytes[index + 1]);
                    Bytes[index] = (byte)(255 - Bytes[index]);
                }
            }
        }

        public Size GetSize()
        {
            return new Size(Width, Height);
        }

        public Rectangle GetRectangle()
        {
            return new Rectangle(0, 0, Width, Height);
        }

        public void Save(string path)
        {
            using (Bitmap bmp = GetBitmap())
                bmp.Save(path);
        }

        public override bool Equals(object obj)
        {
            FastAccessImage other = obj as FastAccessImage;
            return obj != null &&
                (Width == other.Width) &&
                (Height == other.Height) &&
                (Stride == other.Stride) &&
                EqualityHelper.IListEquals(Bytes, other.Bytes);
        }

        public override int GetHashCode()
        {
            return Width * 29 + Height * 29 + Stride;
        }
    }
}
