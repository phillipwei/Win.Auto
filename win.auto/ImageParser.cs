using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace win.auto
{
    public class ImageParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="image">Image to parse</param>
        /// <param name="lookup">The GlyphMapping</param>
        /// <param name="locations">The locations to parse -- has to exactly match the height of the Glyphs</param>
        /// <returns></returns>
        public static List<string> Read(FastAccessImage image, GlyphMapping lookup, List<Rectangle> locations)
        {
            List<string> results = new List<string>();
            foreach (Rectangle location in locations)
            {
                results.Add(Read(image, lookup, location));
            }

            return results;
        }

        public static string Read(FastAccessImage image, GlyphMapping lookup, Rectangle location)
        {
            if (location.X > image.Width ||
                location.Right > image.Width ||
                location.Y > image.Height ||
                location.Bottom > image.Height)
            {
                throw new IndexOutOfRangeException("Rectangle outside of supplied image");
            }

            int x = image.HorizontalSeek(lookup.ReferencePixel, location, 0);
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

        public static GlyphParseResult ParseNextGlyph(FastAccessImage image, GlyphMapping lookup, Rectangle rectangle, int xStart)
        {
            foreach (var kvp in lookup.ReferenceLookup.OrderBy(key => -1 * key.Value.Width))
            {
                if (CheckGlyphMatch(image, lookup, rectangle, xStart, lookup.ReferenceImage, kvp.Value))
                {
                    int xEndOfCurrent = xStart + kvp.Value.Width;
                    int xStartOfNext = image.HorizontalSeek(lookup.ReferencePixel, rectangle, xEndOfCurrent);
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

        public static bool CheckGlyphMatch(FastAccessImage image, GlyphMapping lookup, Rectangle location, int xStart, FastAccessImage glyphImageReference, Rectangle glyphRectangle)
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
    }
}
