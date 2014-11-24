using System;
using System.Collections.Generic;
using System.Drawing;
using win.auto;
using System.Linq;

namespace win.auto
{
    public class GlyphExtractor
    {
        List<FastAccessImage> images;
        List<Rectangle> rois;
        Pixel glyphColor;

        List<Rectangle> capturedBounds;
        List<FastAccessImage> glyphs;

        public GlyphExtractor(IEnumerable<FastAccessImage> images, IEnumerable<Rectangle> rois, Pixel glyphColor)
        {
            this.images = new List<FastAccessImage>(images);
            this.rois = new List<Rectangle>(rois);
            this.glyphColor = glyphColor;
        }

        public List<ExtractedGlyph> Extract()
        {
            var extractedGlyphs = new List<ExtractedGlyph>();

            // find glyph bounds
            foreach (var image in images)
            {
                foreach (var roi in rois)
                {
                    var extracting = false;
                    int glyphLeft = 0, glyphTop = 0, glyphBottom = 0;

                    for (int x = 0; x < roi.Width; x++)
                    {
                        int yStart, yEnd;
                        var found = image.VerticalScan(this.glyphColor, roi, x, out yStart, out yEnd);

                        if (!extracting && found)
                        {
                            extracting = true;
                            glyphLeft = x;
                            glyphTop = yStart;
                            glyphBottom = yEnd;
                        }
                        else if (extracting && found)
                        {
                            glyphTop = Math.Min(glyphTop, yStart);
                            glyphBottom = Math.Max(glyphBottom, yEnd);
                        }
                        else if (extracting && (!found || x == roi.Width - 1))
                        {
                            extracting = false;
                            var bounds = new Rectangle(glyphLeft,
                                                       glyphTop,
                                                       x - glyphLeft,
                                                       glyphBottom - glyphTop + 1);
                            bounds.Offset(roi.Location);
                            extractedGlyphs.Add(new ExtractedGlyph(image, roi, bounds));
                        }
                    }
                }
            }

            // find font bounds
            var glyphsByRoi = extractedGlyphs.GroupBy(g => g.RegionOfInterest).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in glyphsByRoi)
            {
                var top = kvp.Value.Min(g => g.GlyphBounds.Top);
                var bottom = kvp.Value.Max(g => g.GlyphBounds.Bottom);
                foreach (var g in kvp.Value)
                {
                    g.FontBounds = new Rectangle(g.GlyphBounds.X, top, g.GlyphBounds.Width, bottom - top + 1);
                }
            }

            return extractedGlyphs;
        }

        public class ExtractedGlyph
        {
            public FastAccessImage Image;
            public Rectangle RegionOfInterest;
            public Rectangle GlyphBounds;
            public Rectangle FontBounds;

            public ExtractedGlyph(FastAccessImage image, Rectangle roi, Rectangle bounds)
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
