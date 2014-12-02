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
    // bit locking, helper images - pixels
    public class PixelImage
    {
        public string Description;
        public PixelFormat PixelFormat;
        public List<Pixel> PixelPalette;
        public int Width;
        public int Height;
        public int Stride;
        public byte[] Bytes;

        public PixelImage(string imagePath)
            : this(new Bitmap(imagePath))
        {
            this.Description = imagePath;
        }

        public PixelImage(Bitmap bmp)
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

        public PixelImage(int width, int height)
        {
            this.PixelFormat = PixelFormat.Format32bppArgb;
            this.Width = width;
            this.Height = height;
            this.Stride = this.Width * this.Step;
            this.Bytes = new byte[this.Stride * this.Height];
        }

        // note: we force to argb -- the principle being that we probably always want to use argb, though, until we
        // do a write operation we won't force it.
        public PixelImage(PixelImage other, Rectangle subSection)
        {
            if (!Geometry.RectangleContainsRectangle(new Rectangle(Point.Empty, other.GetSize()), subSection))
            {
                throw new ArgumentException("Rectangle not within the image");
            }

            this.PixelFormat = PixelFormat.Format32bppArgb;
            this.Width = subSection.Width;
            this.Height = subSection.Height;
            var thisStep = this.Step;
            this.Stride = this.Width * thisStep;
            this.Bytes = new byte[this.Stride * this.Height];

            var otherStep = other.Step;

            for (int y = 0; y < this.Height; y++)
            {
                for (int x = 0; x < this.Width; x++)
                {
                    var pixel = other.GetPixelUnsafe(subSection.X + x, subSection.Y + y, otherStep);
                    this.SetPixelUnsafe(x, y, thisStep, pixel);
                }
            }
        }

        // todo: step, size and rectangle should be set when those undelrying values are setup... i.e. called in
        // constructors
        private int Step
        {
            get
            {
                return Image.GetPixelFormatSize(this.PixelFormat) / 8;
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

        public PixelImage Subsection(Rectangle r)
        {
            return new PixelImage(this, r);
        }

        public override string ToString()
        {
            return Description;
        }
        
        // todo: does it make sense for this to be on the image itself - or does it go onto an algo/vision class?
        //       what abt the fact that it's mostly used for imageparsing?
        // Searches horizontally, scanning up-down, until it finds the search pixel.  Returns the x-position if it
        // was found -- otherwise, returns -1.  
        public int HorizontalSeek(Func<Pixel, bool> pixelMatcher, Rectangle rectangle, int xOffset)
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
                    if (pixelMatcher(sample))
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
        public bool VerticalScan(Func<Pixel, bool> glyphMatcher, Rectangle rectangle, int xOffset, out int yStart, 
            out int yEnd)
        {
            yStart = yEnd = -1;

            if (xOffset >= rectangle.Width)
            {
                return false;
            }

            for (int y = 0; y < rectangle.Height; y++)
            {
                var sample = GetPixel(rectangle.Left + xOffset, rectangle.Top + y);
                if (glyphMatcher(sample))
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

        public bool Matches(PixelImage other)
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

        public bool Matches(PixelImage other, Rectangle rectangle, Point offset)
        {
            for (int x = 0; x < rectangle.Width; x++)
            {
                for (int y = 0; y < rectangle.Height; y++)
                {
                    if (!this.GetPixel(rectangle.X + x, rectangle.Y + y).Equals(
                        other.GetPixel(offset.X + x, offset.Y + y)))
                    {
                        return false;
                    }
                }
            }

            return true;
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

        public void Save(string path)
        {
            using (Bitmap bmp = GetBitmap())
            {
                bmp.Save(path);
            }
        }

        // return new?
        public void Mask(Func<Pixel,bool> pixelMatcher)
        {
            // todo: turn into map function
            var step = Image.GetPixelFormatSize(this.PixelFormat) / 8;
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    if (!pixelMatcher(GetPixel(x, y)))
                    {
                        this.SetPixelUnsafe(x, y, step, Pixel.Empty);
                    }
                }
            }
        }

        public void Replace(Func<Pixel, bool> pixelMatcher, Pixel pixelTo)
        {
            Map(p => pixelMatcher(p) ? pixelTo : p);
        }

        private void Map(Func<Pixel,Pixel> mapping)
        {
            var step = this.Step;
            for (int x = 0; x < Width; ++x)
            {
                for (int y = 0; y < Height; ++y)
                {
                    this.SetPixelUnsafe(x, y, step, mapping(GetPixel(x, y)));
                }
            }
        }

        public string ToAsciiArt()
        {
            var sb = new StringBuilder();
            var step = this.Step;
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

        public void Copy(PixelImage to, Rectangle rectangleFrom, Point pointTo)
        {
            // todo: bounds checking
            var thisStep = this.Step;
            var toStep = to.Step;

            for (int x = 0; x < rectangleFrom.Width; ++x)
            {
                for (int y = 0; y < rectangleFrom.Height; ++y)
                {
                    to.SetPixelUnsafe(pointTo.X + x, pointTo.Y + y, toStep,
                                      GetPixel(rectangleFrom.X + x, rectangleFrom.Y + y));
                }
            }
        }

        // note: i don't think this quite works -- stride differences?
        public override bool Equals(object obj)
        {
            PixelImage other = obj as PixelImage;
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
