using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using win.auto;

namespace win.auto.test
{
    [TestFixture]
    public class PixelImageTest
    {
        List<FastAccessImage> rgbList;
        FastAccessImage colorGrid;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var dataPath = @"P:\Temp";
            var rgbFileNames = new List<String>() {"rgb.bmp", "rgb32.png", "rgb24.png", "rgb8.png", "rgb.gif"};
            this.rgbList = rgbFileNames.ConvertAll(s => new FastAccessImage(Path.Combine(dataPath, s)));
            this.colorGrid = new FastAccessImage(Path.Combine(dataPath, "colorgrid.png"));
        }

        [Test]
        public void Image_GetPixel_Test()
        {
            foreach(var image in rgbList)
            {
                var expected = Color.FromArgb(255, 255,0,0);
                var actual = image.GetPixel(0, 0);
                Assert.IsTrue(Color.Equals(expected, actual), 
                    string.Format("{0}: Expected {1}; Actual {2}", image.Description, expected, actual)
                );
                Console.WriteLine("{0} matched; PixelFormat = {1}", image.Description, image.PixelFormat);
            }
        }

        [Test]
        public void Image_GetPixel_OutOfRange_Test()
        {
            foreach(var image in rgbList)
            {
                Assert.Throws<ArgumentException>(() => image.GetPixel(-1, -1));
            }
        }

        [Test]
        public void Image_Subsection_Test()
        {
            var subColorGrid = colorGrid.Subsection(new Rectangle(1, 1, 2, 2));
        }
    }
}
