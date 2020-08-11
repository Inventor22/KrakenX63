using KrakenX63Driver;
using System;
using System.Drawing;

namespace KrakenX63Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            KrakenX63 x63 = new KrakenX63();

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Ring,
                KrakenX63.ColorEffect.SuperFixed,
                new Color[] {
                    Color.FromArgb(0, 255, 255), // cyan
                    Color.FromArgb(255, 0, 180), // pink
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(255, 0, 180),
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(255, 0, 180),
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(255, 0, 180)
                },
                KrakenX63.AnimationSpeed.Slower);

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Logo,
                KrakenX63.ColorEffect.SuperBreathing,
                new Color[] {
                    Color.FromArgb(255, 0, 180)
                },
                KrakenX63.AnimationSpeed.Slower);

            //x63.SetColor(
            //    KrakenX63.KrakenXColorChannel.Ring, 
            //    KrakenX63.ColorEffect.Wings,
            //    new Color[] { Color.FromArgb(255, 0, 0) },
            //    KrakenX63.AnimationSpeed.Normal);
        }
    }
}
