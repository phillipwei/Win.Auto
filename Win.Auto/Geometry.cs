using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Win.Auto
{
    public static class Geometry
    {
        public static bool RectangleContainsRectangle(Rectangle outerRectangle, Rectangle innerRectangle)
        {
            return innerRectangle.Left >= outerRectangle.Left &&
                innerRectangle.Right <= outerRectangle.Right &&
                innerRectangle.Top >= outerRectangle.Top &&
                innerRectangle.Bottom <= outerRectangle.Bottom;
        }

        public static bool RectangleContainsPoint(Rectangle rectangle, Point p)
        {
            return RectangleContainsPoint(rectangle, p.X, p.Y);
        }

        public static bool RectangleContainsPoint(Rectangle rectangle, int pointX, int pointY)
        {
            return pointX >= rectangle.Left &&
                pointX <= rectangle.Right &&
                pointY >= rectangle.Top &&
                pointY <= rectangle.Bottom;
        }

        public static Point RectangleCenter(Rectangle rectangle)
        {
            return new Point((rectangle.Left + rectangle.Right) / 2, (rectangle.Top + rectangle.Bottom) / 2);
        }

        public static Point PointSubtract(Point point, Point subtractionOffset)
        {
            return new Point(point.X - subtractionOffset.X, point.Y - subtractionOffset.Y);
        }
    }
}
