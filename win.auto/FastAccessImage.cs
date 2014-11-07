using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace win.auto
{
    public class FastAccessImage
    {
        public byte[] Bytes;
        public int Stride;
        public int Width;
        public int Height;

        public FastAccessImage(string bitmapPath)
            : this(new Bitmap(bitmapPath))
        {
        }

        public FastAccessImage(Bitmap bmp)
        {
            if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new NotImplementedException("Only Format32bppArgb supported");
            }

            Width = bmp.Width;
            Height = bmp.Height;

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int numberOfBytes = bmpData.Stride * bmp.Height;
            Stride = bmpData.Stride;
            byte[] rgbValues = new byte[numberOfBytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, numberOfBytes);
            Bytes = rgbValues;

            // Unlock the bits.
            bmp.UnlockBits(bmpData);
        }

        public FastAccessImage(FastAccessImage other, Rectangle subSection)
        {
            if (!Geometry.RectangleContainsRectangle(new Rectangle(Point.Empty, other.GetSize()), subSection))
            {
                throw new ArgumentException("Rectangle not within the image");
            }

            Width = subSection.Width;
            Height = subSection.Height;
            Stride = subSection.Width * 4;
            Bytes = new byte[subSection.Width * subSection.Height * 4];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Color pixel = other.GetPixel(subSection.X + x, subSection.Y + y);
                    int baseIndex = y * Stride + x * 4;
                    Bytes[baseIndex + 3] = pixel.A;
                    Bytes[baseIndex + 2] = pixel.R;
                    Bytes[baseIndex + 1] = pixel.G;
                    Bytes[baseIndex + 0] = pixel.B;
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
                throw new ArgumentException(String.Format("GetPixel({0},{1}) on an image of size [{2},{3}]", x, y, Width, Height));
            }
            int baseIndex = y * Stride + x * 4;
            return Color.FromArgb(
                Bytes[baseIndex + 3],   // Alpha
                Bytes[baseIndex + 2],   // Red
                Bytes[baseIndex + 1],   // Green
                Bytes[baseIndex]        // Blue
            );
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
