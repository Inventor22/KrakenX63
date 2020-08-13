﻿// ************************************************************************************
// Kraken X63 driver
// Ported by Dustin Dobransky, from liquidctl by Tom Frey, Jonas Malaco and contributors
// Original source: https://github.com/jonasmalacofilho/liquidctl
// SPDX-License-Identifier: GPL-3.0-or-later
// ************************************************************************************

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using HidSharp;

namespace KrakenX63Driver
{
    public class KrakenX63 : IDisposable
    {
        private const int _READ_LENGTH = 64;
        private const int _WRITE_LENGTH = 64;
        private const int _MAX_READ_ATTEMPTS = 12;

        public static readonly Dictionary<string, List<byte>> _SPEED_CHANNELS_KRAKENX = new Dictionary<string, List<byte>> {
            { "pump", new List<byte>() { 0x1, 20, 100 } }
        };

        private const int _CRITICAL_TEMPERATURE = 59;

        public enum KrakenXColorChannel
        {
            External,
            Ring,
            Logo,
            Sync
        }

        public static readonly Dictionary<KrakenXColorChannel, byte> _COLOR_CHANNELS_KRAKENX = new Dictionary<KrakenXColorChannel, byte> {
            { KrakenXColorChannel.External, 0b001 },
            { KrakenXColorChannel.Ring, 0b010 },
            { KrakenXColorChannel.Logo, 0b100 },
            { KrakenXColorChannel.Sync, 0b111 },
        };

        public class ColorMode
        {
            public ColorMode(byte mval, byte variant, byte speedScale, byte minColors, byte maxColors, ColorEffect effect)
            {
                this.Mval = mval;
                this.Variant = variant;
                this.SpeedScale = speedScale;
                this.MinColors = minColors;
                this.MaxColors = maxColors;
                this.Effect = effect;
            }
            public byte Mval { get; set; }
            public byte Variant { get; set; }
            public byte SpeedScale { get; set; }
            public byte MinColors { get; set; }
            public byte MaxColors { get; set; }
            public ColorEffect Effect { get; set; }
        }

        public enum ColorEffect
        {
            Off,
            Fixed,
            Fading,
            SuperFixed,
            SpectrumWave,
            BackwardsSpectrumWave,
            Marquee3,
            Marquee4,
            Marquee5,
            Marquee6,
            BackwardsMarquee3,
            BackwardsMarquee4,
            BackwardsMarquee5,
            BackwardsMarquee6,
            CoveringMarquee,
            CoveringBackwardsMarquee,
            Alternating3,
            Alternating4,
            Alternating5,
            Alternating6,
            MovingAlternating3,
            MovingAlternating4,
            MovingAlternating5,
            MovingAlternating6,
            BackwardsMovingAlternating3,
            BackwardsMovingAlternating4,
            BackwardsMovingAlternating5,
            BackwardsMovingAlternating6,
            Pulse,
            Breathing,
            SuperBreathing,
            Candle,
            StarryNight,
            RainbowFlow,
            SuperRainbow,
            RainbowPulse,
            BackwardsRainbowFlow,
            BackwardsSuperRainbow,
            BackwardsRainbowPulse,
            Loading,
            TaiChi,
            WaterCooler,
            Wings
        }

        private static HashSet<ColorEffect> marqueeEffects = new HashSet<ColorEffect> {
            ColorEffect.Marquee3,
            ColorEffect.Marquee4,
            ColorEffect.Marquee5,
            ColorEffect.Marquee6,
            ColorEffect.BackwardsMarquee3,
            ColorEffect.BackwardsMarquee4,
            ColorEffect.BackwardsMarquee5,
            ColorEffect.BackwardsMarquee6,
            ColorEffect.CoveringMarquee,
            ColorEffect.CoveringBackwardsMarquee
        };

        private static HashSet<ColorEffect> movingAlternatingEffects = new HashSet<ColorEffect>() { 
            ColorEffect.MovingAlternating3,
            ColorEffect.MovingAlternating4,
            ColorEffect.MovingAlternating5,
            ColorEffect.MovingAlternating6,
            ColorEffect.BackwardsMovingAlternating3,
            ColorEffect.BackwardsMovingAlternating4,
            ColorEffect.BackwardsMovingAlternating5,
            ColorEffect.BackwardsMovingAlternating6,
        };

        private static HashSet<ColorEffect> backwardsEffects = new HashSet<ColorEffect>() {
            ColorEffect.BackwardsMarquee3,
            ColorEffect.BackwardsMarquee4,
            ColorEffect.BackwardsMarquee5,
            ColorEffect.BackwardsMarquee6,
            ColorEffect.BackwardsMovingAlternating3,
            ColorEffect.BackwardsMovingAlternating4,
            ColorEffect.BackwardsMovingAlternating5,
            ColorEffect.BackwardsMovingAlternating6,
            ColorEffect.BackwardsRainbowFlow,
            ColorEffect.BackwardsRainbowPulse,
            ColorEffect.BackwardsSpectrumWave,
            ColorEffect.BackwardsSuperRainbow,
            ColorEffect.CoveringBackwardsMarquee
        };

        private static readonly Dictionary<ColorEffect, ColorMode> _COLOR_MODES = new Dictionary<ColorEffect, ColorMode>()
        {
                { ColorEffect.Off,                              new ColorMode(0x00, 0x00,  0, 0, 0, ColorEffect.Off) },
                { ColorEffect.Fixed,                            new ColorMode(0x00, 0x00,  0, 1, 1, ColorEffect.Fixed) },
                { ColorEffect.Fading,                           new ColorMode(0x01, 0x00,  1, 1, 8, ColorEffect.Fading) },
                { ColorEffect.SuperFixed,                       new ColorMode(0x01, 0x01,  9, 1, 40, ColorEffect.SuperFixed) },
                { ColorEffect.SpectrumWave,                     new ColorMode(0x02, 0x00,  2, 0, 0, ColorEffect.SpectrumWave) },
                { ColorEffect.BackwardsSpectrumWave,            new ColorMode(0x02, 0x00,  2, 0, 0, ColorEffect.BackwardsSpectrumWave) },
                { ColorEffect.Marquee3,                         new ColorMode(0x03, 0x03,  2, 1, 1, ColorEffect.Marquee3) },
                { ColorEffect.Marquee4,                         new ColorMode(0x03, 0x04,  2, 1, 1, ColorEffect.Marquee4) },
                { ColorEffect.Marquee5,                         new ColorMode(0x03, 0x05,  2, 1, 1, ColorEffect.Marquee5) },
                { ColorEffect.Marquee6,                         new ColorMode(0x03, 0x06,  2, 1, 1, ColorEffect.Marquee6) },
                { ColorEffect.BackwardsMarquee3,                new ColorMode(0x03, 0x03,  2, 1, 1, ColorEffect.BackwardsMarquee3) },
                { ColorEffect.BackwardsMarquee4,                new ColorMode(0x03, 0x04,  2, 1, 1, ColorEffect.BackwardsMarquee4) },
                { ColorEffect.BackwardsMarquee5,                new ColorMode(0x03, 0x05,  2, 1, 1, ColorEffect.BackwardsMarquee5) },
                { ColorEffect.BackwardsMarquee6,                new ColorMode(0x03, 0x06,  2, 1, 1, ColorEffect.BackwardsMarquee6) },
                { ColorEffect.CoveringMarquee,                  new ColorMode(0x04, 0x00,  2, 1, 8, ColorEffect.CoveringMarquee) },
                { ColorEffect.CoveringBackwardsMarquee,         new ColorMode(0x04, 0x00,  2, 1, 8, ColorEffect.CoveringBackwardsMarquee) },
                { ColorEffect.Alternating3,                     new ColorMode(0x05, 0x03,  3, 1, 2, ColorEffect.Alternating3) },
                { ColorEffect.Alternating4,                     new ColorMode(0x05, 0x04,  3, 1, 2, ColorEffect.Alternating4) },
                { ColorEffect.Alternating5,                     new ColorMode(0x05, 0x05,  3, 1, 2, ColorEffect.Alternating5) },
                { ColorEffect.Alternating6,                     new ColorMode(0x05, 0x06,  3, 1, 2, ColorEffect.Alternating6) },
                { ColorEffect.MovingAlternating3,               new ColorMode(0x05, 0x03,  4, 1, 2, ColorEffect.MovingAlternating3) },
                { ColorEffect.MovingAlternating4,               new ColorMode(0x05, 0x04,  4, 1, 2, ColorEffect.MovingAlternating4) },
                { ColorEffect.MovingAlternating5,               new ColorMode(0x05, 0x05,  4, 1, 2, ColorEffect.MovingAlternating5) },
                { ColorEffect.MovingAlternating6,               new ColorMode(0x05, 0x06,  4, 1, 2, ColorEffect.MovingAlternating6) },
                { ColorEffect.BackwardsMovingAlternating3,      new ColorMode(0x05, 0x03,  4, 1, 2, ColorEffect.BackwardsMovingAlternating3) },
                { ColorEffect.BackwardsMovingAlternating4,      new ColorMode(0x05, 0x04,  4, 1, 2, ColorEffect.BackwardsMovingAlternating4) },
                { ColorEffect.BackwardsMovingAlternating5,      new ColorMode(0x05, 0x05,  4, 1, 2, ColorEffect.BackwardsMovingAlternating5) },
                { ColorEffect.BackwardsMovingAlternating6,      new ColorMode(0x05, 0x06,  4, 1, 2, ColorEffect.BackwardsMovingAlternating6) },
                { ColorEffect.Pulse,                            new ColorMode(0x06, 0x00,  5, 1, 8, ColorEffect.Pulse) },
                { ColorEffect.Breathing,                        new ColorMode(0x07, 0x00,  6, 1, 8, ColorEffect.Breathing) },
                { ColorEffect.SuperBreathing,                   new ColorMode(0x03, 0x00, 10, 1, 40, ColorEffect.SuperBreathing) },
                { ColorEffect.Candle,                           new ColorMode(0x08, 0x00,  0, 1, 1, ColorEffect.Candle) },
                { ColorEffect.StarryNight,                      new ColorMode(0x09, 0x00,  5, 1, 1, ColorEffect.StarryNight) },
                { ColorEffect.RainbowFlow,                      new ColorMode(0x0b, 0x00,  2, 0, 0, ColorEffect.RainbowFlow) },
                { ColorEffect.SuperRainbow,                     new ColorMode(0x0c, 0x00,  2, 0, 0, ColorEffect.SuperRainbow) },
                { ColorEffect.RainbowPulse,                     new ColorMode(0x0d, 0x00,  2, 0, 0, ColorEffect.RainbowPulse) },
                { ColorEffect.BackwardsRainbowFlow,             new ColorMode(0x0b, 0x00,  2, 0, 0, ColorEffect.BackwardsRainbowFlow) },
                { ColorEffect.BackwardsSuperRainbow,            new ColorMode(0x0c, 0x00,  2, 0, 0, ColorEffect.BackwardsSuperRainbow) },
                { ColorEffect.BackwardsRainbowPulse,            new ColorMode(0x0b, 0x00,  2, 0, 0, ColorEffect.BackwardsRainbowPulse) },
                { ColorEffect.Loading,                          new ColorMode(0x10, 0x00,  8, 1, 1, ColorEffect.Loading) },
                { ColorEffect.TaiChi,                           new ColorMode(0x0e, 0x00,  7, 1, 2, ColorEffect.TaiChi) },
                { ColorEffect.WaterCooler,                      new ColorMode(0x0f, 0x00,  6, 2, 2, ColorEffect.WaterCooler) },
                { ColorEffect.Wings,                            new ColorMode(0x0, 0x00, 11, 1, 1, ColorEffect.Wings) }, // 0x0 == None
        };

        private static Dictionary<byte, byte> _STATIC_VALUE = new Dictionary<byte, byte>
        {
            { 0b001, 40 },
            { 0b010, 8 },
            { 0b100, 1 },
            { 0b111, 40 },
        };

        private static readonly Dictionary<int, List<(byte, byte)>> _SPEED_VALUE = new Dictionary<int, List<(byte, byte)>>
        {
            { 0,  new List<(byte, byte)> { (0x32, 0x00), (0x32, 0x00), (0x32, 0x00), (0x32, 0x00), (0x32, 0x00) } },
            { 1,  new List<(byte, byte)> { (0x50, 0x00), (0x3c, 0x00), (0x28, 0x00), (0x14, 0x00), (0x0a, 0x00) } },
            { 2,  new List<(byte, byte)> { (0x5e, 0x01), (0x2c, 0x01), (0xfa, 0x00), (0x96, 0x00), (0x50, 0x00) } },
            { 3,  new List<(byte, byte)> { (0x40, 0x06), (0x14, 0x05), (0xe8, 0x03), (0x20, 0x03), (0x58, 0x02) } },
            { 4,  new List<(byte, byte)> { (0x20, 0x03), (0xbc, 0x02), (0xf4, 0x01), (0x90, 0x01), (0x2c, 0x01) } },
            { 5,  new List<(byte, byte)> { (0x19, 0x00), (0x14, 0x00), (0x0f, 0x00), (0x07, 0x00), (0x04, 0x00) } },
            { 6,  new List<(byte, byte)> { (0x28, 0x00), (0x1e, 0x00), (0x14, 0x00), (0x0a, 0x00), (0x04, 0x00) } },
            { 7,  new List<(byte, byte)> { (0x32, 0x00), (0x28, 0x00), (0x1e, 0x00), (0x14, 0x00), (0x0a, 0x00) } },
            { 8,  new List<(byte, byte)> { (0x14, 0x00), (0x14, 0x00), (0x14, 0x00), (0x14, 0x00), (0x14, 0x00) } },
            { 9,  new List<(byte, byte)> { (0x00, 0x00), (0x00, 0x00), (0x00, 0x00), (0x00, 0x00), (0x00, 0x00) } },
            { 10, new List<(byte, byte)> { (0x37, 0x00), (0x28, 0x00), (0x19, 0x00), (0x0a, 0x00), (0x00, 0x00) } },
            { 11, new List<(byte, byte)> { (0x6e, 0x00), (0x53, 0x00), (0x39, 0x00), (0x2e, 0x00), (0x20, 0x00) } },
        };

        public enum AnimationSpeed
        {
            Slowest,
            Slower,
            Normal,
            Faster,
            Fastest
        }

        public static readonly Dictionary<AnimationSpeed, byte> _ANIMATION_SPEEDS = new Dictionary<AnimationSpeed, byte> {
            { AnimationSpeed.Slowest, 0x0 },
            { AnimationSpeed.Slower,  0x1 },
            { AnimationSpeed.Normal,  0x2 },
            { AnimationSpeed.Faster,  0x3 },
            { AnimationSpeed.Fastest, 0x4 }
        };

        private HidStream hidStream { get; set; }

        public KrakenX63()
        {
            if (!DeviceList.Local.TryGetHidDevice(out HidDevice krakenX63, vendorID: 0x1e71, productID: 0x2007))
            {
                throw new ArgumentException("Could not find Kraken x63");
            }

            Console.WriteLine(krakenX63.DevicePath);
            Console.WriteLine(krakenX63);

            try
            {
                Console.WriteLine(string.Format("Max Lengths: Input {0}, Output {1}, Feature {2}",
                    krakenX63.GetMaxInputReportLength(),
                    krakenX63.GetMaxOutputReportLength(),
                    krakenX63.GetMaxFeatureReportLength()));
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine(e);
                throw;
            }

            if (!krakenX63.TryOpen(out HidStream stream))
            {
                throw new UnauthorizedAccessException("Could not open HidStream for Kraken X63");
            }

            this.hidStream = stream;

            this.hidStream.ReadTimeout = Timeout.Infinite;

            this.writeBytes(new List<byte> { 0x10, 0x01 }); // firmware info
            this.writeBytes(new List<byte> { 0x20, 0x03 }); // lighting info
            this.writeBytes(new List<byte> { 0x70, 0x02, 0x01, 0xb8, 0x01 }); // 0x01 = update interval
            this.writeBytes(new List<byte> { 0x70, 0x01 });

            bool firmwareReceived = false;
            bool lightingInfoReceived = false;
            while (true)
            {
                byte[] response = this.hidStream.Read();

                Console.WriteLine($"Read:  {string.Join(" ", response.Select(a => a.ToString("X2")))}");

                if (response[0] == 0x11 && response[1] == 0x01)
                {
                    string firmwareVersion = $"{response[0x11]}.{response[0x12]}.{response[0x13]}";
                    Console.WriteLine($"Firmware Version: {firmwareVersion}");
                    firmwareReceived = true;
                }

                if (response[0] == 0x21 && response[1] == 0x03)
                {
                    lightingInfoReceived = true;
                }

                if (firmwareReceived && lightingInfoReceived)
                {
                    break;
                }
            }

            stream.Flush();
        }

        public void Dispose()
        {
            this.hidStream.Dispose();
        }

        public string GetStatus()
        {
            this.hidStream.Flush();
            byte[] response = this.hidStream.Read();
            return 
                $"Liquid temperature: {response[15] + response[16]/10} C\n" +
                $"Pump speed: {response[18] << 8 | response[17]} rpm\n" +
                $"Pump duty: {response[19]} %";
        }

        public bool SetColor(KrakenXColorChannel colorChannel, ColorEffect colorEffect, Color[] colors, AnimationSpeed speed)
        {
            byte cid = _COLOR_CHANNELS_KRAKENX[colorChannel];
            ColorMode mode = _COLOR_MODES[colorEffect];

            if (colors.Length < mode.MinColors)
            {
                throw new ArgumentException($"Not enough colors for mode '{colorEffect}', at least {mode.MinColors} required");
            }
            else if (mode.MaxColors == 0 && colors.Length > 0)
            {
                throw new ArgumentException($"Too many colors for mode '{colorEffect}', none needed");
            }
            else if (colors.Length > mode.MaxColors)
            {
                throw new ArgumentException($"Too many colors for mode '{colorEffect}', max colors: {mode.MaxColors}");
            }

            byte sval = _ANIMATION_SPEEDS[speed];

            return this.writeColors(cid, mode, colors, sval);
        }

        private bool writeColors(byte cid, ColorMode mode, Color[] colors, byte sval)
        {
            List<byte> cmds = new List<byte>();

            (byte timingByte1, byte timingByte2) speed_val = _SPEED_VALUE[mode.SpeedScale][sval];

            if (mode.Effect == ColorEffect.SuperFixed || mode.Effect == ColorEffect.SuperBreathing)
            {
                // Add header + colors + footer
                cmds.AddRange(new byte[] { 0x22, 0x10, cid, 0x00 });
                appendColorBytes(cmds, colors, mode.MaxColors);

                this.writeBytes(cmds);

                cmds.Clear();
                this.writeBytes(0x22, 0x11, cid, 0x00);

                // Add timing bytes
                cmds.AddRange(new byte[] { 0x22, 0xa0, cid, 0x00, mode.Mval, speed_val.timingByte1, speed_val.timingByte2 });
                cmds.AddRange(new byte[] { 0x08, 0x00, 0x00, 0x80, 0x00, 0x32, 0x00, 0x00, 0x01 });

                this.writeBytes(cmds);
            }
            else if (mode.Effect == ColorEffect.Wings)
            {
                this.writeBytes(0x22, 0x10, cid); // clear out all independent LEDs
                this.writeBytes(0x22, 0x11, cid); // clear out all independent LEDs
                
                Color c1 = colors[0];
                Color c2 = Color.FromArgb((byte)(c1.R / 2.5), (byte)(c1.G / 2.5), (byte)(c1.B / 2.5));
                Color c3 = Color.FromArgb((byte)(c2.R / 4), (byte)(c2.G / 4), (byte)(c2.B / 4));

                List<List<byte>> wings = new List<List<byte>>();
                wings.Add(new List<byte> { c1.G, c1.R, c1.B, c1.G, c1.R, c1.B });
                wings.Add(new List<byte> { c2.G, c2.R, c2.B, c2.G, c2.R, c2.B });
                wings.Add(new List<byte> { c3.G, c3.R, c3.B, c3.G, c3.R, c3.B });
                wings.Add(new List<byte> { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

                for (int i = 0; i < 8; i++)
                {
                    byte mod = (byte)((i == 3 || i == 7) ? 0x05 : 0x01);
                    byte dir1 = (byte)((i / 4 == 0) ? 0x04 : 0x84);
                    byte dir2 = (byte)((i / 4 == 0) ? 0x84 : 0x04);

                    cmds.AddRange(new byte[] { 
                        0x22, 0x20, cid, (byte)i, 0x04, speed_val.timingByte1, speed_val.timingByte2, mod,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, dir1, dir2,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00});

                    cmds.AddRange(wings[i%4]);
                    this.writeBytes(cmds);
                    cmds.Clear();
                }
                this.writeBytes(0x22, 0x03, cid, 0x08); // Enable windows mode
            }
            else
            {
                // Add headers
                cmds.AddRange(new byte[] { 
                    0x2a, 0x04, // opcode
                    cid, cid,   // address
                    mode.Mval,  // effect mode
                    speed_val.timingByte1, speed_val.timingByte2 // speed 
                });

                // Add colors
                appendColorBytes(cmds, colors, 16);

                byte backwardsByte = 0x00;
                byte modeRelatedByte = 0x00;
                byte colorCount = (byte) colors.Length;

                if (marqueeEffects.Contains(mode.Effect))
                {
                    backwardsByte = 0x04;
                }
                else if (mode.Effect == ColorEffect.StarryNight || movingAlternatingEffects.Contains(mode.Effect))
                {
                    backwardsByte = 0x01;
                }

                if (backwardsEffects.Contains(mode.Effect))
                {
                    backwardsByte += 0x02;
                }

                if (mode.Effect == ColorEffect.Fading || mode.Effect == ColorEffect.Pulse || mode.Effect == ColorEffect.Breathing)
                {
                    modeRelatedByte = 0x08;
                }
                else if (mode.Effect == ColorEffect.TaiChi)
                {
                    modeRelatedByte = 0x05;
                }
                else if (mode.Effect == ColorEffect.WaterCooler)
                {
                    modeRelatedByte = 0x05;
                    colorCount = 0x01;
                }
                else if (mode.Effect == ColorEffect.Loading)
                {
                    modeRelatedByte = 0x04;
                }

                byte staticByte = _STATIC_VALUE[cid];
                byte ledSize = (byte)((mode.Mval == 0x03 || mode.Mval == 0x05) ? mode.Variant : 0x03);

                // Add footer
                cmds.AddRange(new byte[] { backwardsByte, colorCount, modeRelatedByte, staticByte, ledSize });

                this.writeBytes(cmds.ToArray());
            }

            return true;
        }
        
        private void writeBytes(params byte[] bytes)
        {
            byte[] cmds = new byte[_WRITE_LENGTH];
            Array.Copy(bytes, cmds, bytes.Length);

            int paddingLength = _WRITE_LENGTH - bytes.Length;

            for (int i = 0; i < paddingLength; i++)
            {
                cmds[i+bytes.Length] = 0x00;
            }

            Console.WriteLine($"Write: {string.Join(" ", cmds.Select(a => a.ToString("X2")))}");

            this.hidStream.Write(cmds);
        }

        private void writeBytes(List<byte> cmds)
        {
            int paddingLength = _WRITE_LENGTH - cmds.Count;

            for (int i = 0; i < paddingLength; i++)
            {
                cmds.Add(0x00);
            }

            Console.WriteLine($"Write: {string.Join(" ", cmds.Select(a => a.ToString("X2")))}");

            this.hidStream.Write(cmds.ToArray());
        }

        private void appendColorBytes(List<byte> cmds, Color[] colors, int maxColors)
        {
            // Color bytes are in GRB order.
            foreach(Color color in colors)
            {
                cmds.Add(color.G);
                cmds.Add(color.R);
                cmds.Add(color.B);
            }

            int zeroPadding = Math.Min(_WRITE_LENGTH-cmds.Count, 3 * (maxColors - colors.Length));
            for (int i = 0; i < zeroPadding; i++)
            {
                cmds.Add(0x00);
            }
        }
    }
}
