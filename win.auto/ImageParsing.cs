using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace win.auto
{
    // note: statics ... is this smart?
    public class ImageParsing
    {
        public static List<string> Read(PixelImage image, GlyphMapping lookup, List<Rectangle> locations)
        {
            List<string> results = new List<string>();
            foreach (Rectangle location in locations)
            {
                results.Add(Read(image, lookup, location));
            }

            return results;
        }

        public static string Read(PixelImage image, GlyphMapping lookup, Rectangle location)
        {
            return Read(image, lookup, location, p => p.Equals(lookup.ReferencePixel));
        }
        
        public static string Read(PixelImage image, GlyphMapping lookup, Rectangle location, 
            Func<Pixel,bool> pixelMatcher)
        {
            if (location.X > image.Width ||
                location.Right > image.Width ||
                location.Y > image.Height ||
                location.Bottom > image.Height)
            {
                throw new IndexOutOfRangeException("Rectangle outside of supplied image");
            }

            int x = image.HorizontalSeek(pixelMatcher, location, 0);
            if (x == -1)
            {
                return string.Empty;
            }

            StringBuilder parsedString = new StringBuilder();
            GlyphParseResult parseResult = new GlyphParseResult(true, string.Empty, x);
            do
            {
                parseResult = ParseNextGlyph(image, lookup, location, pixelMatcher, x);
                x = parseResult.X;
                parsedString.Append(parseResult.ParsedString);
            }
            while (parseResult.Continue);

            return parsedString.ToString().Trim();
        }

        public static GlyphParseResult ParseNextGlyph(PixelImage image, GlyphMapping lookup, Rectangle rectangle,
            Func<Pixel, bool> pixelMatcher, int xStart)
        {
            foreach (var kvp in lookup.ReferenceLookup.OrderBy(key => -1 * key.Value.Width))
            {
                if (CheckGlyphMatch(image, lookup, rectangle, pixelMatcher, xStart, lookup.ReferenceImage, kvp.Value))
                {
                    int xEndOfCurrent = xStart + kvp.Value.Width;
                    int xStartOfNext = image.HorizontalSeek(pixelMatcher, rectangle, xEndOfCurrent);
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

        public static bool CheckGlyphMatch(PixelImage image, GlyphMapping lookup, Rectangle location, 
            Func<Pixel, bool> pixelMatcher, int xStart, PixelImage glyphImageReference, Rectangle glyphRectangle)
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

                    if ((glyphPixel.Alpha != 0 && !pixelMatcher(imagePixel)) ||
                        (glyphPixel.Alpha == 0 && pixelMatcher(imagePixel)))
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
    }
}
