﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace win.auto
{
    /// <summary>
    /// Data Structure that provides a mapping from Glyphs to Characters.  Used by ImageParsing to extract Character
    /// information from PixelImages.
    /// </summary>
    public class GlyphMapping
    {
        public PixelImage ReferenceImage { get; private set; }

        public PixelMatcher PixelMatcher { get; private set; }

        /// <summary>
        /// Maps the string to the matching area on the reference image.  Why is this a dictionary?  For conveniene?
        /// </summary>
        public Dictionary<string, Rectangle> ReferenceLookup { get; private set; }
        
        /// <summary>
        /// Whatever the color of the pixel is the reference color.
        /// </summary>
        public Pixel ReferencePixel { get; private set; }

        /// <summary>
        /// The pixel width beyond which is considered a whitespace (inclusive)
        /// </summary>
        public int WhiteSpaceWidth { get; private set; }

        public GlyphMapping(string imagePath, IList<string> glyphList, int whiteSpaceWidth)
            : this(new PixelImage(imagePath), glyphList, whiteSpaceWidth)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imagePath">Path to the Glyph image - has to have all glyphs on same row</param>
        /// <param name="glyphList">List of mappings</param>
        /// <param name="whiteSpaceWidth">The number of spaces</param>
        public GlyphMapping(PixelImage referenceImage, IList<string> glyphList, int whiteSpaceWidth)
        {
            this.ReferenceImage = referenceImage;
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

                // looking for end of character, empty vertical band => the characcter is up to (but not including)
                // this band
                if (!seekingStart && y == this.ReferenceImage.Height)
                {
                    rectangles.Add(new Rectangle(startingX, 0, x - startingX, this.ReferenceImage.Height));
                    seekingStart = !seekingStart;
                }
                // looking for end of character, not the above (empty vertical band) but at end -- i.e. glyph list ends
                // WITH character
                else if (!seekingStart && x == this.ReferenceImage.Width - 1)
                {
                    rectangles.Add(new Rectangle(startingX, 0, x - startingX + 1, this.ReferenceImage.Height));
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

            this.PixelMatcher = new SpecificPixelMatcher(this.ReferencePixel);
        }

        public void Save(string dir, string name)
        {
            var refPath = Path.Combine(dir, name + ".png");
            this.ReferenceImage.Save(refPath);

            var settings = new Settings()
            {
                Path = refPath,
                Chars = this.ReferenceLookup.OrderBy(kvp => kvp.Value.Left).Select(kvp => kvp.Key).ToArray(), 
                Spacing = this.WhiteSpaceWidth
            };

            File.WriteAllText(Path.Combine(dir, name + ".json"), JsonConvert.SerializeObject(settings));
        }

        public static GlyphMapping Load(string settingsPath)
        {
            var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Path.Combine(settingsPath)));
            return new GlyphMapping(settings.Path, settings.Chars, settings.Spacing);
        }

        private class Settings
        {
            public string Path;
            public string[] Chars;
            public int Spacing;
        }
    }
}
