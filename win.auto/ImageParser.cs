using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace win.auto
{
    public class ImageParser
    {
        public static List<string> Read(FastAccessImage image, GlyphLookup lookup, List<Rectangle> locations)
        {
            List<string> results = new List<string>();
            foreach (Rectangle location in locations)
            {
                results.Add(Read(image, lookup, location));
            }

            return results;
        }

        public static string Read(FastAccessImage image, GlyphLookup lookup, Rectangle location)
        {
            if (location.X > image.Width ||
                location.Right > image.Width ||
                location.Y > image.Height ||
                location.Bottom > image.Height)
            {
                throw new IndexOutOfRangeException("Rectangle outside of supplied image");
            }

            int x = HorizontalSeek(image, lookup, location, 0);
            if (x == -1)
            {
                return string.Empty;
            }

            StringBuilder parsedString = new StringBuilder();
            GlyphParseResult parseResult = new GlyphParseResult(true, string.Empty, x);
            do
            {
                parseResult = ParseNextGlyph(image, lookup, location, x);
                x = parseResult.X;
                parsedString.Append(parseResult.ParsedString);
            }
            while (parseResult.Continue);

            return parsedString.ToString().Trim();
        }

        public static int HorizontalSeek(FastAccessImage image, GlyphLookup lookup, Rectangle rectangle, int xStart)
        {
            if (xStart >= rectangle.Width)
            {
                return -1;
            }

            int left = rectangle.Left;
            int top = rectangle.Top;
            int width = rectangle.Width;
            int height = rectangle.Height;

            int y = 0;
            for (; xStart < width; xStart++)
            {
                for (y = 0; y < height; y++)
                {
                    Pixel pixel = image.GetPixel(left + xStart, top + y);
                    if (pixel.Equals(lookup.ReferencePixel))
                    {
                        break;
                    }
                }

                if (y != height)
                {
                    break;
                }
            }

            if ((xStart == width) && (y == height))
            {
                return -1;
            }
            else
            {
                return xStart;
            }
        }

        public static GlyphParseResult ParseNextGlyph(FastAccessImage image, GlyphLookup lookup, Rectangle rectangle, int xStart)
        {
            foreach (var kvp in lookup.ReferenceLookup.OrderBy(key => -1 * key.Value.Width))
            {
                if (CheckGlyphMatch(image, lookup, rectangle, xStart, lookup.ReferenceImage, kvp.Value))
                {
                    int xEndOfCurrent = xStart + kvp.Value.Width;
                    int xStartOfNext = HorizontalSeek(image, lookup, rectangle, xEndOfCurrent);
                    if (xStartOfNext == -1)
                    {
                        return new GlyphParseResult(false, kvp.Key, -1);
                    }
                    else if (xStartOfNext - xEndOfCurrent >= lookup.WhiteSpaceWidth)
                    {
                        return new GlyphParseResult(true, kvp.Key + " ", xStartOfNext);
                    }
                    else
                    {
                        return new GlyphParseResult(true, kvp.Key, xStartOfNext);
                    }
                }
            }

            return new GlyphParseResult(false, string.Empty, -1);
        }

        public static bool CheckGlyphMatch(FastAccessImage image, GlyphLookup lookup, Rectangle location, int xStart, FastAccessImage glyphImageReference, Rectangle glyphRectangle)
        {
            if (xStart + glyphRectangle.Width > location.Right || glyphRectangle.Height > location.Height)
            {
                return false;
            }

            for (int x = 0; x < glyphRectangle.Width; x++)
            {
                for (int y = 0; y < glyphRectangle.Height; y++)
                {
                    Pixel glyphPixel = glyphImageReference.GetPixel(glyphRectangle.Left + x, glyphRectangle.Top + y);
                    Pixel imagePixel = image.GetPixel(location.Left + xStart + x, location.Top + y);

                    if ((glyphPixel.Alpha != 0 && !glyphPixel.Equals(imagePixel)) ||
                        (glyphPixel.Alpha == 0 && imagePixel.Equals(lookup.ReferencePixel)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public struct GlyphParseResult
        {
            public bool Continue;
            public string ParsedString;
            public int X;

            public GlyphParseResult(bool parsingComplete, string parsedString, int x)
            {
                this.Continue = parsingComplete;
                this.ParsedString = parsedString;
                this.X = x;
            }
        }

        public class GlyphLookup
        {
            public FastAccessImage ReferenceImage { get; private set; }
            public Dictionary<string, Rectangle> ReferenceLookup { get; private set; }
            public Pixel ReferencePixel { get; private set; }
            public int WhiteSpaceWidth { get; private set; }

            public GlyphLookup(string imagePath, IList<string> glyphList, int whiteSpaceWidth)
            {
                this.ReferenceImage = new FastAccessImage(imagePath);
                this.WhiteSpaceWidth = whiteSpaceWidth;
                IList<Rectangle> rectangles = new List<Rectangle>();
                bool seekingStart = true;
                int startingX = 0;
                for (int x = 0; x < this.ReferenceImage.Width; x++)
                {
                    int y = 0;
                    for (; y < this.ReferenceImage.Height; y++)
                    {
                        Pixel pixel = this.ReferenceImage.GetPixel(x, y);
                        byte alpha = pixel.Alpha;

                        if (alpha != 0)
                        {
                            if (seekingStart)
                            {
                                startingX = x;
                                this.ReferencePixel = pixel;
                                seekingStart = !seekingStart;
                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    if ((y == this.ReferenceImage.Height && !seekingStart) ||
                        (x == this.ReferenceImage.Width - 1))
                    {
                        rectangles.Add(new Rectangle(startingX, 0, x - startingX, this.ReferenceImage.Height));
                        seekingStart = !seekingStart;
                    }
                }

                if (glyphList.Count != rectangles.Count)
                {
                    throw new ArgumentException("Glyphs don't match");
                }

                this.ReferenceLookup = new Dictionary<string, Rectangle>();
                for (int i = 0; i < glyphList.Count; i++)
                {
                    this.ReferenceLookup.Add(glyphList[i], rectangles[i]);
                }
            }
        }
    }
}
