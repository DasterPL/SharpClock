using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SharpClock
{
    class PixelDraw : IPixelDraw
    {
        internal static IPixelDraw Screen { get; private set; }
        ws281x.Net.Neopixel neopixel;

        void movePixels(int offset = 1)
        {
            if (offset > 0)
            {
                for (int i = 0; i < offset; i++)
                {
                    for (int j = LedCount - 1; j > 0; j--)
                    {
                        if (j % 8 == 0)
                            neopixel.SetPixelColor(PixelHw[j], Color.Black);
                        else
                            neopixel.SetPixelColor(PixelHw[j], neopixel.LedList.GetColor(PixelHw[j - 1]));
                    }
                }
            }
            else if (offset < 0)
            {
                for (int i = 0; i < -offset; i++)
                {
                    for (int j = 0; j < LedCount; j++)
                    {
                        if ((j - 7) % 8 == 0)
                            neopixel.SetPixelColor(PixelHw[j], Color.Black);
                        else
                            neopixel.SetPixelColor(PixelHw[j], neopixel.LedList.GetColor(PixelHw[j + 1]));
                    }
                }
            }
        }
        PixelFont Font = null;

        Dictionary<int, int[]> RomanFont = null;
        Dictionary<char, char> unexceptedCharacters = new Dictionary<char, char>()
        {
            { 'ą', 'a' },
            { 'ć', 'c' },
            { 'ę', 'e' },
            { 'ł', 'l' },
            { 'ń', 'n' },
            { 'ó', 'o' },
            { 'ś', 's' },
            { 'ż', 'z' },
            { 'ź', 'z' },
            { 'Ą', 'A' },
            { 'Ć', 'C' },
            { 'Ę', 'E' },
            { 'Ł', 'L' },
            { 'Ń', 'N' },
            { 'Ó', 'O' },
            { 'Ś', 'S' },
            { 'Ż', 'Z' },
            { 'Ź', 'Z' }
        };

        readonly int[] Pixels = { 0, 1, 2, 3, 4, 5, 6, 7, 15, 14, 13, 12, 11, 10, 9, 8, 16, 17, 18, 19, 20, 21, 22, 23, 31, 30, 29, 28, 27, 26, 25, 24, 32, 33, 34, 35, 36, 37, 38, 39, 47, 46, 45, 44, 43, 42, 41, 40, 48, 49, 50, 51, 52, 53, 54, 55, 63, 62, 61, 60, 59, 58, 57, 56, 64, 65, 66, 67, 68, 69, 70, 71, 79, 78, 77, 76, 75, 74, 73, 72, 80, 81, 82, 83, 84, 85, 86, 87, 95, 94, 93, 92, 91, 90, 89, 88, 96, 97, 98, 99, 100, 101, 102, 103, 111, 110, 109, 108, 107, 106, 105, 104, 112, 113, 114, 115, 116, 117, 118, 119, 127, 126, 125, 124, 123, 122, 121, 120, 128, 129, 130, 131, 132, 133, 134, 135, 143, 142, 141, 140, 139, 138, 137, 136, 144, 145, 146, 147, 148, 149, 150, 151, 159, 158, 157, 156, 155, 154, 153, 152, 160, 161, 162, 163, 164, 165, 166, 167, 175, 174, 173, 172, 171, 170, 169, 168, 176, 177, 178, 179, 180, 181, 182, 183, 191, 190, 189, 188, 187, 186, 185, 184, 192, 193, 194, 195, 196, 197, 198, 199, 207, 206, 205, 204, 203, 202, 201, 200, 208, 209, 210, 211, 212, 213, 214, 215, 223, 222, 221, 220, 219, 218, 217, 216, 224, 225, 226, 227, 228, 229, 230, 231, 239, 238, 237, 236, 235, 234, 233, 232, 240, 241, 242, 243, 244, 245, 246, 247, 255, 254, 253, 252, 251, 250, 249, 248 };
        // Reverse lookup: PixelHw[logical] = hardware index. Replaces O(256) Array.IndexOf.
        readonly int[] PixelHw;
        const int LedCount = 256;
        static readonly Color White = Color.FromArgb(255, 255, 255);
        internal PixelDraw()
        {
            if (Screen == null)
            {
                Screen = this;
            }
            else
            {
                throw new Exception("Can't create more screens");
            }
            neopixel = new ws281x.Net.Neopixel(ledCount: LedCount, pin: HardwareConfig.LedPin, stripType: rpi_ws281x.WS2811_STRIP_GRB, (uint)HardwareConfig.LedFreq, HardwareConfig.LedDma, false, 16, 0);
            neopixel.Begin();
            PixelHw = new int[LedCount];
            for (int i = 0; i < LedCount; i++)
                PixelHw[i] = Array.IndexOf(Pixels, i);
            LoadFont();
        }
        Color[] LoadImage(Image image, Point point, int width = 8, int height = 8)
        {
            var img = new List<Color>();
            var bitmap = (Bitmap)image;
            for (int x = point.X; x < point.X + width; x++)
            {
                for (int y = point.Y; y < point.Y + height; y++)
                {
                    img.Add(bitmap.GetPixel(x, y));
                }
            }
            return img.ToArray();
        }
        static readonly char[] FontChars =
            ("0123456789.,;:$#'!\"/?%()@*-=~+[]<>_^& " +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "abcdefghijklmnopqrstuvwxyz")
            .ToCharArray();

        void LoadFont(string file = "img/font")
        {
            Logger.Log(ConsoleColor.Cyan, "Loading Font img from file");
            Font = new PixelFont();
            using (var image = Image.FromFile(file + ".png"))
            using (var sizeFile = File.OpenText(file + "_size"))
            {
                foreach (char c in FontChars)
                    Font.Add(new PixelFont.Char(c, int.Parse(sizeFile.ReadLine())));

                int cursorX = 0;
                for (int i = 0; i < Font.Length; i++)
                {
                    var pixels = LoadImage(image, new Point(cursorX, 0), Font[i].Size);
                    cursorX += Font[i].Size + 1;
                    Font[i].Points = Enumerable.Range(0, pixels.Length)
                        .Where(idx => pixels[idx] == White)
                        .ToArray();
                }
            }
            Logger.Log(ConsoleColor.Cyan, "Loading Font Completed");
        }
        void LoadRomanFont(string file = "img/RomanNumbers.png")
        {
            Logger.Log(ConsoleColor.Cyan, "Loading Roman Font img from file");
            RomanFont = new Dictionary<int, int[]>();
            Image image = Image.FromFile(file);
            var chars = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            for (int i = 0; i < chars.Length; i++)
            {
                var Letter = LoadImage(image, new Point(i * 9, 0), 9);
                var points = new List<int>();
                for (int i1 = 0; i1 < Letter.Length; i1++)
                {
                    if (Letter[i1] == White)
                        points.Add(i1);
                }
                RomanFont.Add(chars[i], points.ToArray());
            }
        }
        public Image GetScreen()
        {
            var buffer = GetBuffer();
            Bitmap tmp = new Bitmap(32, 8);
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 8; y++)
                    tmp.SetPixel(x, y, buffer[8 * x + y]);
            return tmp;
        }
        public byte Brightness
        {
            get => neopixel.GetBrightness();
            set => neopixel.SetBrightness(value);
        }
        public void Clear()
        {
            for (int i = 0; i < LedCount; i++)
                neopixel.SetPixelColor(i, Color.Black);
        }
        public void SetPixel(int number, Color c)
        {
            if (number < 0 || number >= LedCount) return;
            neopixel.SetPixelColor(PixelHw[number], c);
        }
        public void SetPixel(Point point, Color c)
        {
            neopixel.SetPixelColor(PixelHw[8 * point.X + point.Y], c);
        }
        public int SetText(string text, Color c, int x = 0, int spaces = 1)
        {
            int current_x = x * 8;

            foreach (var letter in text)
            {
                var l = letter;
                if (!Font.ContainsKey(l))
                {
                    if (unexceptedCharacters.ContainsKey(l))
                        l = unexceptedCharacters[l];
                    else
                        l = '.';
                }
                var PixelLetter = Font[l].Points;
                foreach (var pixel in PixelLetter)
                {
                    SetPixel(current_x + pixel, c);
                }
                current_x += Font[l].Size * 8 + (8 * spaces);
            }
            return current_x/8;
        }
        public void SetTextRoman(int number, Color c, int x = 0)
        {
            if (RomanFont == null)
                LoadRomanFont();
            int current_x = x * 8;
            var PixelLetter = RomanFont[number];
            foreach (var pixel in PixelLetter)
            {
                SetPixel(current_x + pixel, c);
            }

        }
        public void SetImage(Image image, int x, int width = 8, int height = 8)
        {
            var imgColors = LoadImage(image, new Point(0, 0), width, height);
            int startPos = x * 8;
            for (int i = 0; i < imgColors.Length; i++)
            {
                if (imgColors[i].A == 0)
                    continue;
                SetPixel(startPos + i, imgColors[i]);
            }
        }
        public int SetTextRight(string text, Color c, int rightEdge, int spaces = 0)
        {
            int x = rightEdge - TextLength(text, spaces);
            return SetText(text, c, x, spaces);
        }

        public int SetTextCentered(string text, Color c, int spaces = 0)
        {
            int x = (32 - TextLength(text, spaces)) / 2;
            return SetText(text, c, x, spaces);
        }

        public int TextLength(string text, int spaces = 1, int x = 0)
        {
            int current_x = x * 8;

            foreach (var letter in text)
            {
                var l = letter;
                if (!Font.ContainsKey(l))
                {
                    if (unexceptedCharacters.ContainsKey(l))
                        l = unexceptedCharacters[l];
                    else
                        l = '.';
                }
                var PixelLetter = Font[l].Points;
                current_x += Font[l].Size * 8 + (8 * spaces);
            }
            return current_x / 8;
        }

        public void Draw(int offset = 0)
        {
            movePixels(offset);
            neopixel.Show();
        }

        public Color[] GetBuffer()
        {
            var buffer = new Color[LedCount];
            for (int i = 0; i < LedCount; i++)
                buffer[i] = neopixel.LedList.GetColor(PixelHw[i]);
            return buffer;
        }

        public void DrawFromBuffersX(Color[] current, Color[] next, int xOffset)
        {
            for (int x = 0; x < 32; x++)
            {
                int srcCol = x + xOffset;
                Color[] src = srcCol < 32 ? current : next;
                int srcX = srcCol < 32 ? srcCol : srcCol - 32;
                for (int y = 0; y < 8; y++)
                    neopixel.SetPixelColor(PixelHw[8 * x + y], src[8 * srcX + y]);
            }
            neopixel.Show();
        }

        public void DrawFromBuffers(Color[] current, Color[] next, int yOffset)
        {
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    Color c = y < yOffset
                        ? next[8 * x + (y + 8 - yOffset)]
                        : current[8 * x + (y - yOffset)];
                    neopixel.SetPixelColor(PixelHw[8 * x + y], c);
                }
            }
            neopixel.Show();
        }
    }
}
