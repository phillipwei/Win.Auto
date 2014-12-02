using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace win.auto
{
    public class GlyphMapping
    {
        public PixelImage ReferenceImage { get; private set; }

        /// <summary>
        /// Maps the string to the matching area on the reference image.  Why is this a dictionary?  For conveniene?
        /// </summary>
        public Dictionary<string, Rectangle> ReferenceLookup { get; private set; }
        
        /// <summary>
        /// Whatever the color of the pixel is the reference color.
        /// </summary>
        public Pixel ReferencePixel { get; private set; }

        // todo: doesn't belong here
        public int WhiteSpaceWidth { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imagePath">Path to the Glyph image - has to have all glyphs on same row</param>
        /// <param name="glyphList">List of mappings</param>
        /// <param name="whiteSpaceWidth">The number of spaces</param>
        public GlyphMapping(string imagePath, IList<string> glyphList, int whiteSpaceWidth)
        {
            this.ReferenceImage = new PixelImage(imagePath);
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
        }
    }
}
