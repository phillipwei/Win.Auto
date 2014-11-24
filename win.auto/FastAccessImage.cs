using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace win.auto
{
    public class FastAccessImage
    {
        public string Description;
        public PixelFormat PixelFormat;
        public List<Pixel> PixelPalette;
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
            this.PixelPalette = bmp.Palette.Entries.ToList().ConvertAll(color => new Pixel(color));
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

        // note: we force to argb -- the principle being that we probably always want to use argb, though, until we
        // do a write operation we won't force it.
        public FastAccessImage(FastAccessImage other, Rectangle subSection)
        {
            if (!Geometry.RectangleContainsRectangle(new Rectangle(Point.Empty, other.GetSize()), subSection))
            {
                throw new ArgumentException("Rectangle not within the image");
            }

            /*
            this.PixelFormat = other.PixelFormat;
            var bpp = Image.GetPixelFormatSize(this.PixelFormat) / 8;
            this.PixelPalette = other.PixelPalette;
             */

            this.PixelFormat = PixelFormat.Format32bppArgb;
            this.Width = subSection.Width;
            this.Height = subSection.Height;
            var thisStep = this.GetStep();
            this.Stride = this.Width * thisStep;
            this.Bytes = new byte[this.Stride * this.Height];

            var otherStep = other.GetStep();

            for (int y = 0; y < this.Height; y++)
            {
                for (int x = 0; x < this.Width; x++)
                {
                    var pixel = other.GetPixelUnsafe(subSection.X + x, subSection.Y + y, otherStep);
                    this.SetPixelUnsafe(x, y, thisStep, pixel);
                }
            }
        }

        public int GetStep()
        {
            return Image.GetPixelFormatSize(this.PixelFormat) / 8;
        }

        public Pixel GetPixel(Point p)
        {
            return GetPixel(p.X, p.Y);
        }

        public Pixel GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x > Width || y > Height)
            {
                throw new ArgumentException(String.Format(
                    "GetPixel({0},{1}) on an image of size [{2},{3}]", 
                    x, y, Width, Height
                ));
            }

            return GetPixelUnsafe(x, y, Image.GetPixelFormatSize(this.PixelFormat) / 8);
        }

        private Pixel GetPixelUnsafe(int x, int y, int step)
        {
            var baseIndex = y * Stride + x * step;

            switch (this.PixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                    return new Pixel(
                        r: Bytes[baseIndex + 2],
                        g: Bytes[baseIndex + 1],
                        b: Bytes[baseIndex],
                        a: Bytes[baseIndex + 3]
                    );
                case PixelFormat.Format24bppRgb:
                    return new Pixel(
                        r: Bytes[baseIndex + 2],
                        g: Bytes[baseIndex + 1],
                        b: Bytes[baseIndex]
                    );
                case PixelFormat.Format8bppIndexed:
                    return this.PixelPalette[Bytes[baseIndex]];
                default:
                    throw new InvalidOperationException(String.Format(
                        "{0} is not a supported pixel format.",
                        this.PixelFormat
                    ));
            }
        }

        private void SetPixelUnsafe(int x, int y, int step, Pixel pixel)
        {
            var baseIndex = y * Stride + x * step;

            switch (this.PixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                    Bytes[baseIndex + 0] = pixel.Blue;
                    Bytes[baseIndex + 1] = pixel.Green;
                    Bytes[baseIndex + 2] = pixel.Red;
                    Bytes[baseIndex + 3] = pixel.Alpha;
                    break;
                case PixelFormat.Format24bppRgb:
                    Bytes[baseIndex + 0] = pixel.Blue;
                    Bytes[baseIndex + 1] = pixel.Green;
                    Bytes[baseIndex + 2] = pixel.Red;
                    break;
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

        public override string ToString()
        {
            return Description;
        }
        
        // todo: does it make sense for this to be on the image itself - or does it go onto an algo/vision class?
        //       what abt the fact that it's mostly used for imageparsing?
        // Searches horizontally, scanning up-down, until it finds the search pixel.  Returns the x-position if it
        // was found -- otherwise, returns -1.  
        public int HorizontalSeek(Pixel searchPixel, Rectangle rectangle, int xOffset)
        {
            if (xOffset >= rectangle.Width)
            {
                return -1;
            }

            int y = 0;
            for (; xOffset < rectangle.Width; xOffset++)
            {
                for (y = 0; y < rectangle.Height; y++)
                {
                    var sample = GetPixel(rectangle.Left + xOffset, rectangle.Top + y);
                    if (sample.Equals(searchPixel))
                    {
                        break;
                    }
                }

                if (y != rectangle.Height)
                {
                    break;
                }
            }

            if ((xOffset == rectangle.Width) && (y == rectangle.Height))
            {
                return -1;
            }
            else
            {
                return xOffset;
            }
        }

        // the opposite of the above -- could rename and def refactor...
        public bool VerticalScan(Pixel searchPixel, Rectangle rectangle, int xOffset, out int yStart, out int yEnd)
        {
            yStart = yEnd = -1;

            if (xOffset >= rectangle.Width)
            {
                return false;
            }

            for (int y = 0; y < rectangle.Height; y++)
            {
                var sample = GetPixel(rectangle.Left + xOffset, rectangle.Top + y);
                if (sample.Equals(searchPixel))
                {
                    if(yStart == -1)
                    {
                        yStart = y;
                    }
                    yEnd = y;
                }
            }

            return yStart != -1;
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

        public bool Matches(FastAccessImage other)
        {
            if( other == null ||
                (this.Width != other.Width) ||
                (this.Height != other.Height))
            {
                return false;
            }

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (!this.GetPixel(x, y).Equals(other.GetPixel(x, y)))
                    {
                        return false;
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

        // todo: is this a reasonable name?
        public Bitmap GetBitmap()
        {
            // note: after subsections, the fai strides may not be the properly padded stride of a real bmp -- so
            // we can't just do a direct memory copy.  use 32 to keep things easy.
            var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var bmpStep = Image.GetPixelFormatSize(PixelFormat.Format32bppArgb) / 8;
            var bmpData = bmp.LockBits(GetRectangle(), ImageLockMode.WriteOnly, bmp.PixelFormat);
            var bmpPtr = bmpData.Scan0;

            // create a byte array that is sized right; we will memcopy this once it's filled
            var bmpBytes = new Byte[Math.Abs(bmpData.Stride) * bmp.Height];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var pixel = GetPixel(x, y);
                    var baseIndex = y * bmpData.Stride + x * bmpStep;
                    bmpBytes[baseIndex + 0] = pixel.Blue;
                    bmpBytes[baseIndex + 1] = pixel.Green;
                    bmpBytes[baseIndex + 2] = pixel.Red;
                    bmpBytes[baseIndex + 3] = pixel.Alpha;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(bmpBytes, 0, bmpPtr, bmpBytes.Length);
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
            {
                bmp.Save(path);
            }
        }

        // note: i don't think this quite works -- stride differences?
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

        // return new?
        public void Cutout(Pixel pixel)
        {
            var step = Image.GetPixelFormatSize(this.PixelFormat) / 8;
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    if(!GetPixel(x,y).Equals(pixel))
                    {
                        this.SetPixelUnsafe(x, y, step, Pixel.Empty);
                    }
                }
            }
        }

        public string ToAsciiArt()
        {
            var sb = new StringBuilder();
            var step = this.GetStep();
            for(int y=0; y<this.Height; y++)
            {
                for(int x=0; x<this.Width; x++)
                {
                    sb.Append(this.GetPixelUnsafe(x, y, step).Alpha == 0 ? " " : "#");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
