namespace win.auto
{
    public struct Pixel
    {
        public int A, R, G, B;
        
        public Pixel(int R, int G, int B, int A=255)
        {
            this.R = R;
            this.G = G;
            this.B = B;
            this.A = A;
        }
    }
}
