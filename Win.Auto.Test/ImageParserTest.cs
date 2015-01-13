using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Win.Auto.Test
{
    [TestClass]
    public class ImageParserTest
    {
        GlyphMapping font;
        PixelImage helloWorld;

        [TestInitialize]
        public void Initialize()
        {
            var dataPath = @"Data";
            this.font = new GlyphMapping(
                Path.Combine(dataPath, "04b03.png"),
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".Select(c => c.ToString()).ToList(),
                2
            );
            this.helloWorld = new PixelImage(Path.Combine(dataPath, "helloworld.png"));
        }

        [TestMethod]
        public void ImageParser_HelloWorld_Test()
        {
            var result = ImageParsing.Read(helloWorld, font, helloWorld.GetRectangle());
            Assert.AreEqual("Hello World", result);
        }
    }
}
