using System;
using System.Collections.Generic;
using System.Drawing;
using win.auto;
using System.Linq;

namespace win.auto
{
    /// <summary>
    /// Helps extract Glyphs from text-area bounds.  The text-area bounds must be well specified.
    /// </summary>
    public class GlyphExtractor
    {
        List<PixelImage> Images;
        List<Rectangle> RegionsOfInterest;
        Func<Pixel,bool> GlyphPixelMatcher;

        public List<PixelImage> Glyphs;

        public GlyphExtractor(IEnumerable<PixelImage> images, IEnumerable<Rectangle> textAreaBounds, 
            Func<Pixel, bool> glyphMatcher)
        {
            this.Images = new List<PixelImage>(images);
            this.RegionsOfInterest = new List<Rectangle>(textAreaBounds);
            this.GlyphPixelMatcher = glyphMatcher;
        }

        // finds glyphs, finds textareas
        public void ExtractGlyphs()
        {
            var extractedGlyphs = new List<GlyphExtraction>();

            // extract all glyphs that match the color
            foreach (var image in Images)
            {
                foreach (var roi in RegionsOfInterest)
                {
                    var extracting = false;
                    int glyphLeft = 0, glyphTop = 0, glyphBottom = 0;

                    for (int x = 0; x < roi.Width; x++)
                    {
                        int yStart, yEnd;
                        var found = image.VerticalScan(this.GlyphPixelMatcher, roi, x, out yStart, out yEnd);

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

            var uniqueGlyphs = new List<PixelImage>();

            foreach (var extractedGlyph in extractedGlyphs)
            {
                var glyph = extractedGlyph.Image.Subsection(extractedGlyph.GlyphBounds);
                glyph.Description = string.Format("{0} from {1}", extractedGlyph.GlyphBounds, extractedGlyph.Image.Description);
                glyph.Mask(GlyphPixelMatcher);
                glyph.Replace(GlyphPixelMatcher, Pixel.Black);
                if (!uniqueGlyphs.Exists(g => g.Matches(glyph)))
                {
                    uniqueGlyphs.Add(glyph);
                }
            }

            this.Glyphs = uniqueGlyphs;
        }

        /// <summary>
        /// Internal data structure to keep track of work as the Glyph is being extracted.
        /// </summary>
        public class GlyphExtraction
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
