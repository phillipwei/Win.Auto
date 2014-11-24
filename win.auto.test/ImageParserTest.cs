using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using win.auto;

namespace win.auto.test
{
    [TestClass]
    public class ImageParserTest
    {
        GlyphMapping font;
        FastAccessImage helloWorld;

        [TestInitialize]
        public void Initialize()
        {
            var dataPath = @"Data";
            this.font = new GlyphMapping(
                Path.Combine(dataPath, "04b03.png"),
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".Select(c => c.ToString()).ToList(),
                2
            );
            this.helloWorld = new FastAccessImage(Path.Combine(dataPath, "helloworld.png"));
        }

        [TestMethod]
        public void ImageParser_HelloWorld_Test()
        {
            var result = ImageParser.Read(helloWorld, font, helloWorld.GetRectangle());
            Assert.AreEqual("Hello World", result);
        }
    }
}
