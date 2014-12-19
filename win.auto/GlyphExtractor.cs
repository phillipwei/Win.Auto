using System;
using System.Collections.Generic;
using System.Drawing;
using win.auto;
using System.Linq;
using System.Text;

namespace win.auto
{
    /// <summary>
    /// Extracts Glyphs from a sequence of PixelImages, prompts for what Characters those Glyphs correspond to, and
    /// returns a GlyphMapping.
    /// </summary>
    public class GlyphExtractor
    {
        public GlyphMapping ExtractGlyphMapping(
            IEnumerable<PixelImage> images,
            IEnumerable<Rectangle> textAreas,
            Func<Pixel, bool> glyphPixelMatcher
        ) {
            var glyphExtractions = CreateGlyphExtractions(images, textAreas, glyphPixelMatcher);
            var unifiedExtractions = UnifyGlyphExtractions(glyphExtractions, glyphPixelMatcher);
            return CreateGlyphMapping(unifiedExtractions);
        }

        // Initial Work.
        private List<GlyphExtraction> CreateGlyphExtractions(
            IEnumerable<PixelImage> images,
            IEnumerable<Rectangle> textAreas,
            Func<Pixel, bool> glyphPixelMatcher
        ) {
            var glyphExtractions = new List<GlyphExtraction>();

            foreach (var image in images)
            {
                foreach (var textArea in textAreas)
                {
                    GlyphExtraction prev = null;
                    var extracting = false;
                    int glyphLeft = 0, glyphTop = 0, glyphBottom = 0, glyphRight = 0, spacing = 0;

                    for (int x = 0; x < textArea.Width; x++)
                    {
                        int yStart, yEnd;
                        var found = image.VerticalScan(glyphPixelMatcher, textArea, x, out yStart, out yEnd);

                        // beginning of new glyph
                        if (!extracting && found)
                        {
                            extracting = true;
                            glyphLeft = x;
                            spacing = x - glyphRight;
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
                        else if (extracting && (!found || x == textArea.Width - 1))
                        {
                            extracting = false;
                            glyphRight = x;
                            var bounds = new Rectangle(glyphLeft,
                                                       0,
                                                       x - glyphLeft,
                                                       textArea.Height);
                            bounds.Offset(textArea.Location);

                            var glyph = image
                                .Subsection(bounds)
                                .Mask(glyphPixelMatcher)
                                .Replace(glyphPixelMatcher, Pixel.Black);

                            var glyphExtraction = new GlyphExtraction(image, textArea, bounds, glyph, prev, spacing);
                            prev = glyphExtraction;
                            glyphExtractions.Add(glyphExtraction);
                        }
                    }
                }
            }

            return glyphExtractions;
        }

        private Dictionary<PixelImage, List<GlyphExtraction>> UnifyGlyphExtractions(
            List<GlyphExtraction> glyphExtractions,
            Func<Pixel, bool> glyphPixelMatcher
        )
        {
            return glyphExtractions
                .GroupBy(glyphExtraction => glyphExtraction.Glyph, PixelImage.MatchesEqualityCompare)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        // Prompt user for what Glyphs Map to.
        private GlyphMapping CreateGlyphMapping(Dictionary<PixelImage, List<GlyphExtraction>> unifiedExtractions)
        {
            var acceptedGlyphs = new List<PixelImage>();
            var chars = new List<string>();

            Console.WriteLine("For each Glyph, type the string it represents, then hit enter.");
            Console.WriteLine("If invalid, just hit enter.");
            foreach (var kvp in unifiedExtractions)
            {
                var glyphImage = kvp.Key;

                Console.WriteLine(glyphImage.Description);
                Console.WriteLine(glyphImage.ToAsciiArt());
                Console.WriteLine(">");

                var val = Console.ReadLine();

                if (val != string.Empty)
                {
                    acceptedGlyphs.Add(glyphImage);
                    chars.Add(val);
                }
                else
                {
                    Console.WriteLine("Skipped");
                }
            }

            GlyphExtraction geMaxSpacing = null;
            for (int i = 0; i < chars.Count; i++)
            {
                var c = chars[i];
                var glyph = acceptedGlyphs[i];
                foreach(var ge in unifiedExtractions[glyph].Where(
                    ge => ge.PreviousExtraction != null && acceptedGlyphs.Contains(ge.PreviousExtraction.Glyph))
                ) {
                    if (geMaxSpacing == null || 
                        ge.SpacingFromPrevious > geMaxSpacing.SpacingFromPrevious)
                    {
                        geMaxSpacing = ge;
                    }
                }
            }

            Console.WriteLine(string.Join(",", chars.OrderBy(c => c)));
            Console.WriteLine("Max Spacing={0}", geMaxSpacing.SpacingFromPrevious);
            Console.WriteLine("Enter Character Spacing: ");
            var spacing = int.Parse(Console.ReadLine());
            var mapping = CombineGlyphsIntoMappingImage(acceptedGlyphs);

            return new GlyphMapping(mapping, chars, spacing);
        }

        private PixelImage CombineGlyphsIntoMappingImage(IEnumerable<PixelImage> glyphs)
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
            public PixelImage ReferenceImage;

            /// <summary>
            /// The TextArea the Glyph was found in.
            /// </summary>
            public Rectangle TextArea;

            /// <summary>
            /// The CharacterArea that encapsulates this Glyph.
            /// </summary>
            public Rectangle GlyphBounds;

            /// <summary>
            /// The extracted Glyph.
            /// </summary>
            public PixelImage Glyph;

            /// <summary>
            /// The previously extracted Glyph; null if this is the first Glyph in the TextArea.
            /// </summary>
            public GlyphExtraction PreviousExtraction;

            /// <summary>
            /// The spacing, in pixels, from the previously detected Glyph
            /// </summary>
            public int SpacingFromPrevious;

            public GlyphExtraction(
                PixelImage referenceImage, 
                Rectangle textArea, 
                Rectangle glyphBounds, 
                PixelImage glyph,
                GlyphExtraction prev, 
                int spacing
            ) {
                this.ReferenceImage = referenceImage;
                this.TextArea = textArea;
                this.GlyphBounds = glyphBounds;
                this.Glyph = glyph;
                this.PreviousExtraction = prev;
                this.SpacingFromPrevious = spacing;
            }

            public override string ToString()
            {
                return string.Format("{0} {1} {2}", ReferenceImage.Description, TextArea, GlyphBounds);
            }
        }
    }
}
