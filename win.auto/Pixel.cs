using System;
using System.Drawing;

namespace win.auto
{
    public struct Pixel
    {
        public static Pixel Black = new Pixel(0, 0, 0);
        public static Pixel White = new Pixel(255, 255, 255);
        public static Pixel Empty = new Pixel(0, 0, 0, 0);
        public byte Red, Green, Blue, Alpha;

        public Pixel(Color c)
        {
            this.Red   = c.R;
            this.Green = c.G;
            this.Blue  = c.B;
            this.Alpha = c.A;
        }

        public Pixel(byte r, byte g, byte b, byte a = 255)
        {
            this.Red   = r;
            this.Green = g;
            this.Blue  = b;
            this.Alpha = a;
        }

        public bool IsTransparent
        {
            get { return this.Alpha == 0; }
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 23) + this.Red.GetHashCode();
            hash = (hash * 23) + this.Green.GetHashCode();
            hash = (hash * 23) + this.Blue.GetHashCode();
            hash = (hash * 23) + this.Alpha.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Pixel))
            {
                return false;
            }

            Pixel other = (Pixel)obj;
            return this.Red == other.Red &&
                this.Green == other.Green &&
                this.Blue == other.Blue &&
                this.Alpha == other.Alpha;
        }

        public bool CloselyMatches(Pixel other, int channelThreshold)
        {
            return Math.Abs(this.Red - other.Red) < channelThreshold &&
                Math.Abs(this.Green - other.Green) < channelThreshold &&
                Math.Abs(this.Blue - other.Blue) < channelThreshold;
        }

        public override string ToString()
        {
            return string.Format("R:{0}, G:{1}, B:{2}, A:{3}", Red, Green, Blue, Alpha);
        }
    }
}
