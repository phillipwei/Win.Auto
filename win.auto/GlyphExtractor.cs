using System;
using System.Collections.Generic;
using System.Drawing;
using win.auto;
using System.Linq;

namespace win.auto
{
    /// <summary>
    /// Helps extract unique Glyphs from a sequence of PixelImages, and then build a Glyph Mapping from those Glyphs.
    /// </summary>
    public class GlyphExtractor
    {
        public List<PixelImage> ExtractUniqueGlyphs(IEnumerable<PixelImage> images, IEnumerable<Rectangle> textAreas,
            Func<Pixel, bool> glyphPixelMatcher)
        {
            var extractedGlyphs = new List<GlyphExtraction>();

            // extract all glyphs that match the color
            foreach (var image in images)
            {
                foreach (var roi in textAreas)
                {
                    var extracting = false;
                    int glyphLeft = 0, glyphTop = 0, glyphBottom = 0;

                    for (int x = 0; x < roi.Width; x++)
                    {
                        int yStart, yEnd;
                        var found = image.VerticalScan(glyphPixelMatcher, roi, x, out yStart, out yEnd);

                        // beginning of new glyph
                        if (!extracting && found)
                        {
                            extracting = true;
                            glyphLeft = x;
                            glyphTop = yStart;
                            glyphBottom = yEnd;
                        }
                        // continuing new glyph
                        else if (extracting && found)
                        {
                            glyphTop = Math.Min(glyphTop, yStart);
                            glyphBottom = Math.Max(glyphBottom, yEnd);
                        }
                        // glyph end
                        else if (extracting && (!found || x == roi.Width - 1))
                        {
                            extracting = false;
                            var bounds = new Rectangle(glyphLeft,
                                                       0,
                                                       x - glyphLeft,
                                                       roi.Height);
                            bounds.Offset(roi.Location);

                            var glyphExtraction = new GlyphExtraction(image, roi, bounds);
                            extractedGlyphs.Add(glyphExtraction);
                        }
                    }
                }
            }

            // Winnow down to what is unique
            var uniqueGlyphs = new List<PixelImage>();
            foreach (var extractedGlyph in extractedGlyphs)
            {
                var glyph = extractedGlyph.Image.Subsection(extractedGlyph.GlyphBounds);
                glyph.Description = string.Format("{0} from {1}", extractedGlyph.GlyphBounds,
                    extractedGlyph.Image.Description);
                glyph.Mask(glyphPixelMatcher);
                glyph.Replace(glyphPixelMatcher, Pixel.Black);
                if (!uniqueGlyphs.Exists(g => g.Matches(glyph)))
                {
                    uniqueGlyphs.Add(glyph);
                }
            }

            return uniqueGlyphs;
        }

        public GlyphMapping CreateGlyphMapping(IEnumerable<PixelImage> glyphs)
        {
            var acceptedGlyphs = new List<PixelImage>();
            var chars = new List<string>();
            foreach (var glyph in glyphs)
            {
                Console.WriteLine(glyph.Description);
                Console.WriteLine(glyph.ToAsciiArt());
                Console.WriteLine(">");
                var val = Console.ReadLine();
                if (val != string.Empty)
                {
                    acceptedGlyphs.Add(glyph);
                    chars.Add(val);
                }
            }

            Console.WriteLine("Character Spacing: ");
            var spacing = int.Parse(Console.ReadLine());
            var mapping = CombineGlyphsIntoMappingImage(acceptedGlyphs);

            return new GlyphMapping(mapping, chars, spacing);
        }

        public PixelImage CombineGlyphsIntoMappingImage(IEnumerable<PixelImage> glyphs)
        {
            var width = glyphs.Sum(g => g.Width + 1) - 1;
            var height = glyphs.First().Height;
            if (!glyphs.Skip(1).All(g => g.Height == height))
            {
                throw new Exception("not same height");
            }
            var fai = new PixelImage(width, height);

            int x = 0;
            foreach (var glyph in glyphs)
            {
                glyph.Copy(fai, glyph.GetRectangle(), new Point(x, 0));
                x += glyph.Width + 1;
            }
            return fai;
        }

        /// <summary>
        /// Internal data structure to keep track of work as the Glyph is being extracted.
        /// </summary>
        private class GlyphExtraction
        {
            /// <summary>
            /// The Image the Glyph was found in.
            /// </summary>
            public PixelImage Image;

            /// <summary>
            /// The RegionOfInterest the Glyph was found in.
            /// </summary>
            public Rectangle RegionOfInterest;

            /// <summary>
            /// The minimum area that encapsulates this Glyph.  Not if the Glyph is short, this might not cover the
            /// entirely line height.
            /// </summary>
            public Rectangle GlyphBounds;

            public GlyphExtraction(PixelImage image, Rectangle roi, Rectangle bounds)
            {
                this.Image = image;
                this.RegionOfInterest = roi;
                this.GlyphBounds = bounds;
            }

            public override string ToString()
            {
                return string.Format("{0} {1} {2}", Image.Description, RegionOfInterest, GlyphBounds);
            }
        }
    }
}
