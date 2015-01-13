using System;
using System.Reflection;


namespace win.auto.test
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            var args = new string[] { Assembly.GetExecutingAssembly().Location, "/run" };
            NUnit.Gui.AppEntry.Main(args);
        }
    }
}
