using System;
using OpenSilver;

namespace SKAuto.Mac
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new App();
            Application.Run(app); // Uses OpenSilver's Application.Run
        }
    }
}