using System;
using System.Collections.Generic;
using System.Drawing;
using win.auto;
using System.Linq;

namespace win.auto
{
    /// <summary>
    /// Helps extract Glyphs from loosely specified regions of interest.
    /// </summary>
    public class GlyphExtractor
    {
        List<PixelImage> Images;
        List<Rectangle> RegionsOfInterest;
        Func<Pixel,bool> GlyphPixelMatcher;

        public List<PixelImage> Glyphs;
        public List<Rectangle> TextAreaBounds;

        public GlyphExtractor(IEnumerable<PixelImage> images, IEnumerable<Rectangle> regionsOfInterest, 
            Func<Pixel, bool> glyphMatcher)
        {
            this.Images = new List<PixelImage>(images);
            this.RegionsOfInterest = new List<Rectangle>(regionsOfInterest);
            this.GlyphPixelMatcher = glyphMatcher;
        }

        // finds glyphs, finds textareas
        public void ExtractGlyphs(bool interactive=false)
        {
            var extractedGlyphs = new List<GlyphExtraction>();

            // find glyph bounds
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
                                                       glyphTop,
                                                       x - glyphLeft,
                                                       glyphBottom - glyphTop + 1);
                            bounds.Offset(roi.Location);

                            var glyphExtraction = new GlyphExtraction(image, roi, bounds);
                            if(interactive)
                            {
                                var glyph = image.Subsection(bounds);
                                glyph.Mask(GlyphPixelMatcher);
                                Console.WriteLine(glyph.ToAsciiArt());
                                if(Console.ReadKey().Key == ConsoleKey.N)
                                {
                                    Console.WriteLine("Skipping");
                                    continue;
                                }
                                Console.WriteLine("Adding");
                            }
                            extractedGlyphs.Add(glyphExtraction);
                        }
                    }
                }
            }

            // find font and textarea bounds
            var glyphsByRoi = extractedGlyphs.GroupBy(g => g.RegionOfInterest).ToDictionary(g => g.Key, g => g.ToList());
            this.TextAreaBounds = new List<Rectangle>();

            foreach (var kvp in glyphsByRoi)
            {
                var top = kvp.Value.Min(g => g.GlyphBounds.Top);
                var bottom = kvp.Value.Max(g => g.GlyphBounds.Bottom);
                var left = kvp.Value.Min(g => g.GlyphBounds.Left);
                var right = kvp.Value.Max(g => g.GlyphBounds.Right);
                var textAreaBounds = new Rectangle(left, top, right - left + 1, bottom - top + 1);
                this.TextAreaBounds.Add(textAreaBounds);
                foreach (var g in kvp.Value)
                {
                    g.FontBounds = new Rectangle(g.GlyphBounds.X, top, g.GlyphBounds.Width, bottom - top + 1);
                    g.TextAreaBounds = textAreaBounds;
                }
            }

            var uniqueGlyphs = new List<PixelImage>();

            foreach (var extractedGlyph in extractedGlyphs)
            {
                var glyph = extractedGlyph.Image.Subsection(extractedGlyph.FontBounds);
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

            /// <summary>
            /// The area that would properly encapsulate this Glyph as part of a font set.
            /// </summary>
            public Rectangle FontBounds;

            /// <summary>
            /// The area that is actually occupied by Text in this RegionOfInterest
            /// </summary>
            public Rectangle TextAreaBounds;

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
