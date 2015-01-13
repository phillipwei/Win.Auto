using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace win.auto
{
    public abstract class PixelMatcher
    {
        private Func<Pixel, bool> _matcher;

        public PixelMatcher(Func<Pixel, bool> matcher)
        {
            this._matcher = matcher;
        }

        public bool IsMatch(Pixel p)
        {
            return _matcher(p);
        }
    }

    public class SpecificPixelMatcher : PixelMatcher
    {
        public Pixel PixelToMatch;

        public SpecificPixelMatcher(Pixel pixelToMatch)
            : base(p => pixelToMatch.Equals(p))
        {
            this.PixelToMatch = pixelToMatch;
        }
    }

    public class RangedPixelMatcher : PixelMatcher
    {
        public Pixel PixelRangeMin;
        public Pixel PixelRangeMax;

        public RangedPixelMatcher(Pixel pixelRangeMin, Pixel pixelRangeMax)
            : base(p => BetweenRange(p, pixelRangeMin, pixelRangeMax))
        {
            this.PixelRangeMax = pixelRangeMax;
            this.PixelRangeMin = pixelRangeMin;
        }

        private static bool BetweenRange(Pixel pixel, Pixel min, Pixel max)
        {
            return pixel.Red >= min.Red && pixel.Red <= max.Red
                && pixel.Green >= min.Green && pixel.Green <= max.Green
                && pixel.Blue >= min.Blue && pixel.Blue <= max.Blue;
        }
    }

    public class CombinedPixelMatcher : PixelMatcher
    {
        public IEnumerable<PixelMatcher> Matchers;

        public CombinedPixelMatcher(IEnumerable<PixelMatcher> pixelMatchers)
            : base(p => pixelMatchers.Any(pm => pm.IsMatch(p)))
        {
            this.Matchers = pixelMatchers;
        }
    }
}
