using KrakenX63Driver;
using System;
using System.Drawing;
using System.Threading;

namespace KrakenX63Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            //RingSuperFixedAndLogoSuperBreathingTest();
            //WingsTest();
            //SuperFixedColorCycleTest();
            //SpectrumWaveTest();
            //TestMarquee3();
            TestTaiChi();
            TestGetStatus();
            TestLogoFixed();
        }

        public static void TestLogoFixed()
        {
            KrakenX63 x63 = new KrakenX63();

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Logo,
                KrakenX63.ColorEffect.Fixed,
                new Color[] {
                    Color.FromArgb(255, 0, 80)
                },
                KrakenX63.AnimationSpeed.Normal);
        }

        public static void TestGetStatus()
        {
            KrakenX63 x63 = new KrakenX63();
            Console.WriteLine(x63.GetStatus());
        }

        public static void TestTaiChi()
        {
            KrakenX63 x63 = new KrakenX63();
            
            Color pink = Color.FromArgb(255, 0, 80);

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Ring,
                KrakenX63.ColorEffect.TaiChi,
                new Color[] { Color.DarkCyan, pink },
                KrakenX63.AnimationSpeed.Slower);
        }

        public static void TestMarquee3()
        {
            KrakenX63 x63 = new KrakenX63();

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Ring,
                KrakenX63.ColorEffect.Marquee3,
                new Color[] { Color.FromKnownColor(KnownColor.DarkCyan) },
                KrakenX63.AnimationSpeed.Normal);
        }

        public static void SpectrumWaveTest()
        {
            KrakenX63 x63 = new KrakenX63();

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Ring,
                KrakenX63.ColorEffect.SpectrumWave,
                new Color[] { },
                KrakenX63.AnimationSpeed.Normal);
        }

        public static void RingSuperFixedAndLogoSuperBreathingTest()
        {
            KrakenX63 x63 = new KrakenX63();

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Ring,
                KrakenX63.ColorEffect.SuperFixed,
                new Color[] {
                    Color.FromArgb(0, 255, 255), // cyan
                    Color.FromArgb(255, 0, 80), // pink
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(255, 0, 80),
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(255, 0, 80),
                    Color.FromArgb(0, 255, 255),
                    Color.FromArgb(255, 0, 80)
                },
                KrakenX63.AnimationSpeed.Slower);

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Logo,
                KrakenX63.ColorEffect.SuperBreathing,
                new Color[] {
                    Color.FromArgb(255, 0, 80)
                },
                KrakenX63.AnimationSpeed.Slower);
        }

        public static void WingsTest()
        {
            KrakenX63 x63 = new KrakenX63();

            x63.SetColor(
                KrakenX63.KrakenXColorChannel.Ring,
                KrakenX63.ColorEffect.Wings,
                new Color[] { Color.FromArgb(255, 0, 0) },
                KrakenX63.AnimationSpeed.Normal);
        }

        public static void SuperFixedColorCycleTest()
        {
            KrakenX63 x63 = new KrakenX63();

            Color cyan = Color.FromArgb(0, 255, 255);
            Color pink = Color.FromArgb(255, 0, 80);

            Color[] colors1 = new Color[] { cyan, pink, cyan, pink, cyan, pink, cyan, pink };
            Color[] colors2 = new Color[] { pink, cyan, pink, cyan, pink, cyan, pink, cyan };

            int i = 0;
            while (true)
            {
                x63.SetColor(
                    KrakenX63.KrakenXColorChannel.Ring,
                    KrakenX63.ColorEffect.SuperFixed,
                    i++ % 2 == 0 ? colors1 : colors2,
                    KrakenX63.AnimationSpeed.Slower);

                Thread.Sleep(500);

                if (Console.KeyAvailable && Console.ReadKey().KeyChar == 'q')
                {
                    break;
                }
            }
        }
    }
}
