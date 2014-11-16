using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using win.auto;
using NUnit.Framework;

namespace win.auto.test
{
    [TestFixture]
    public class PixelImageTest
    {
        List<FastAccessImage> rgbList;
        FastAccessImage font, helloWorld;

        [TestFixtureSetUp]
        public void SetUp()
        {
            var dataPath = @"Data";
            var rgbFileNames = new List<String>() {"rgb.bmp", "rgb.gif", "rgb32.png", "rgb24.png", "rgb8.png"};
            this.rgbList = rgbFileNames.ConvertAll(s => new FastAccessImage(Path.Combine(dataPath, s)));
            this.font = new FastAccessImage(Path.Combine(dataPath, "04b03.png"));
            this.helloWorld = new FastAccessImage(Path.Combine(dataPath, "helloworld.png"));
        }

        [Test]
        public void Image_GetPixel_Test()
        {
            Dictionary<Point, Pixel> expectedColorsByCoord = new Dictionary<Point, Pixel>()
            {
                {new Point(0,0), new Pixel(255,0,0) },
                {new Point(1,0), new Pixel(0,255,0) },
                {new Point(2,0), new Pixel(0,0,255) },

                {new Point(0,1), new Pixel(0,255,255) },
                {new Point(1,1), new Pixel(255,0,255) },
                {new Point(2,1), new Pixel(255,255,0) },

                {new Point(0,2), new Pixel(0,0,0) },
                {new Point(1,2), new Pixel(255,255,255) },
            };

            // Standard Color Check
            foreach(var image in rgbList)
            {
                foreach(var pointAndColor in expectedColorsByCoord)
                {
                    var point = pointAndColor.Key;
                    var expected = pointAndColor.Value;
                    var actual = image.GetPixel(point);
                    Assert.AreEqual(expected, actual,
                        string.Format("{0} at {1} : Expected {2}; Actual {3}", image.Description, point, expected, actual)
                    );
                    Console.WriteLine("{0} matched; PixelFormat = {1}", image.Description, image.PixelFormat);
                }
            }

            // Transparency Check
            Dictionary<string, Pixel> expectedColorByName = new Dictionary<string, Pixel>()
            {
                { "rgb.bmp",   new Pixel(255,255,255,255) },
                { "rgb.gif",   new Pixel(0,0,0,0) },
                { "rgb32.png", new Pixel(0,0,0,0) },
                { "rgb24.png", new Pixel(255,255,255,255) },
                { "rgb8.png",  new Pixel(0,0,0,0) },
            };

            foreach(var image in rgbList)
            {
                var point = new Point(2, 2);
                var expected = expectedColorByName[image.Description.Split('\\').Last()];
                var actual = image.GetPixel(point);
                Assert.AreEqual(expected, actual,
                    string.Format("{0} at {1} : Expected {2}; Actual {3}", image.Description, point, expected, actual)
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
        }
    }
}
