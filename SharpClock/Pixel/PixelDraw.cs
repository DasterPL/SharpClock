using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClock
{
    class PixelDraw : IPixelDraw
    {
        public static IPixelDraw Screen { get; private set; }
        class Font_new
        {
            public class Char
            {
                public char Letter { get; private set; }
                public int[] Points { get; set; }
                public int Size { get; private set; }
                public Char(char Letter, int Size)
                {
                    this.Letter = Letter;
                    this.Size = Size;
                }
            }
            Dictionary<char, Char> Chars = new Dictionary<char, Char>();
            public Char this[int c]
            {
                get
                {
                    char[] keys = Chars.Keys.ToArray();
                    return Chars[keys[c]];
                }
            }
            public Char this[char c]
            {
                get => Chars[c];
            }
            public void Add(Char c)
            {
                Chars.Add(c.Letter, c);
            }
            public int Length { get => Chars.Count; }
            public bool ContainsKey(char c)
            {
                return Chars.ContainsKey(c);
            }
        }
        ws281x.Net.Neopixel neopixel;

        void movePixels(int offset = 1)
        {
            int len = neopixel.GetNumberOfPixels();
            if (offset > 0)
            {
                for (int i = 0; i < offset; i++)
                {
                    for (int j = len - 1; j > 0; j--)
                    {
                        if (j % 8 == 0)
                        {
                            neopixel.SetPixelColor(Array.IndexOf(Pixels, j), Color.Black);
                        }
                        else
                            neopixel.SetPixelColor(Array.IndexOf(Pixels, j), neopixel.LedList.GetColor(Array.IndexOf(Pixels, j - 1)));
                    }
                }
            }
            else if (offset < 0)
            {
                for (int i = 0; i < -offset; i++)
                {
                    for (int j = 0; j < len; j++)
                    {
                        if ((j - 7) % 8 == 0)
                            neopixel.SetPixelColor(Array.IndexOf(Pixels, j), Color.Black);
                        else
                            neopixel.SetPixelColor(Array.IndexOf(Pixels, j), neopixel.LedList.GetColor(Array.IndexOf(Pixels, j + 1)));
                    }

                }
            }
        }
        Font_new Font = null;

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

        int[] Pixels = { 0, 1, 2, 3, 4, 5, 6, 7, 15, 14, 13, 12, 11, 10, 9, 8, 16, 17, 18, 19, 20, 21, 22, 23, 31, 30, 29, 28, 27, 26, 25, 24, 32, 33, 34, 35, 36, 37, 38, 39, 47, 46, 45, 44, 43, 42, 41, 40, 48, 49, 50, 51, 52, 53, 54, 55, 63, 62, 61, 60, 59, 58, 57, 56, 64, 65, 66, 67, 68, 69, 70, 71, 79, 78, 77, 76, 75, 74, 73, 72, 80, 81, 82, 83, 84, 85, 86, 87, 95, 94, 93, 92, 91, 90, 89, 88, 96, 97, 98, 99, 100, 101, 102, 103, 111, 110, 109, 108, 107, 106, 105, 104, 112, 113, 114, 115, 116, 117, 118, 119, 127, 126, 125, 124, 123, 122, 121, 120, 128, 129, 130, 131, 132, 133, 134, 135, 143, 142, 141, 140, 139, 138, 137, 136, 144, 145, 146, 147, 148, 149, 150, 151, 159, 158, 157, 156, 155, 154, 153, 152, 160, 161, 162, 163, 164, 165, 166, 167, 175, 174, 173, 172, 171, 170, 169, 168, 176, 177, 178, 179, 180, 181, 182, 183, 191, 190, 189, 188, 187, 186, 185, 184, 192, 193, 194, 195, 196, 197, 198, 199, 207, 206, 205, 204, 203, 202, 201, 200, 208, 209, 210, 211, 212, 213, 214, 215, 223, 222, 221, 220, 219, 218, 217, 216, 224, 225, 226, 227, 228, 229, 230, 231, 239, 238, 237, 236, 235, 234, 233, 232, 240, 241, 242, 243, 244, 245, 246, 247, 255, 254, 253, 252, 251, 250, 249, 248 };
        public PixelDraw()
        {
            if (Screen == null)
            {
                Screen = this;
            }
            else
            {
                throw new Exception("Can't create more screens");
            }
            neopixel = new ws281x.Net.Neopixel(ledCount: 256, pin: 18, stripType: rpi_ws281x.WS2811_STRIP_GRB, 800000, 10, false, 16, 0);
            neopixel.Begin();
            LoadFont();
        }
        Color[] LoadImage(Image image, Point point, int width = 8, int height = 8)
        {
            var img = new List<Color>();
            //var img = new Color[width*height];
            var bitmap = (Bitmap)image;
            for (int x = point.X; x < point.X + width; x++)
            {
                for (int y = point.Y; y < point.Y + height; y++)
                {
                    img.Add(bitmap.GetPixel(x, y));
                    //Console.WriteLine(bitmap.GetPixel(x, y).ToString());
                    //img[x + y * height] = bitmap.GetPixel(x, y);
                }
            }
            return img.ToArray();
        }
        void LoadFont(string file = "img/font")
        {
            Logger.Log(ConsoleColor.Cyan, "Loading Font img from file");
            Image image = Image.FromFile(file + ".png");
            var size = File.OpenText(file + "_size");
            Font = new Font_new();
            Font.Add(new Font_new.Char('0', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('1', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('2', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('3', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('4', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('5', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('6', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('7', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('8', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('9', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('.', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char(',', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char(';', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char(':', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('$', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('#', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('\'', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('!', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('"', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('/', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('?', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('%', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('(', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char(')', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('@', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('*', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('-', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('=', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('~', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('+', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('[', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char(']', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('<', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('>', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('_', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('^', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('&', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char(' ', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('A', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('B', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('C', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('D', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('E', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('F', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('G', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('H', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('I', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('J', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('K', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('L', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('M', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('N', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('O', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('P', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('Q', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('R', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('S', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('T', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('U', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('V', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('W', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('X', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('Y', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('Z', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('a', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('b', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('c', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('d', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('e', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('f', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('g', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('h', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('i', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('j', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('k', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('l', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('m', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('n', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('o', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('p', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('q', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('r', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('s', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('t', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('u', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('v', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('w', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('x', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('y', int.Parse(size.ReadLine())));
            Font.Add(new Font_new.Char('z', int.Parse(size.ReadLine())));

            int cursor_pos = 0;
            for (int i = 0; i < Font.Length; i++)
            {
                //Console.WriteLine($"Loading Letter: {Font[i].Letter} pos: {cursor_pos} size: {Font[i].Size}");
                var Letter = LoadImage(image, new Point(cursor_pos, 0), Font[i].Size);
                cursor_pos += Font[i].Size + 1;
                var points = new List<int>();
                for (int i1 = 0; i1 < Letter.Length; i1++)
                {
                    if (Letter[i1] == Color.FromArgb(255, 255, 255))
                        points.Add(i1);
                }
                Font[i].Points = points.ToArray();
            }
            Logger.Log(ConsoleColor.Cyan, "Loading Font Completed");
        }
        void LoadRomanFont(string file = "img/RomanNumbers.png")
        {
            Logger.Log(ConsoleColor.Cyan, "Loading Roman Font img from file");
            RomanFont = new Dictionary<int, int[]>();
            Image image = Image.FromFile(file);
            var chars = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            //Logger.Log("Converting img to pixel array");
            for (int i = 0; i < chars.Length; i++)
            {
                var Letter = LoadImage(image, new Point(i * 9, 0), 9);
                var points = new List<int>();
                for (int i1 = 0; i1 < Letter.Length; i1++)
                {
                    if (Letter[i1] == Color.FromArgb(255, 255, 255))
                        points.Add(i1);
                }
                RomanFont.Add(chars[i], points.ToArray());
            }
        }
        public Image GetScreen()
        {
            Bitmap tmp = new Bitmap(32, 8);
            int x = 0, y = 0;
            for (int i = 0; i < neopixel.GetNumberOfPixels(); i++)
            {
                var c = neopixel.LedList.GetColor(Array.IndexOf(Pixels, i));
                tmp.SetPixel(x, y, c);
                if (++x > 32)
                {
                    x = 0;
                    y++;
                }
            }
            return tmp;
        }
        public byte Brightness
        {
            get => neopixel.GetBrightness();
            set => neopixel.SetBrightness(value);
        }
        public void Clear()
        {
            for (var i = 0; i < neopixel.GetNumberOfPixels(); i++)
            {
                neopixel.SetPixelColor(i, Color.Black);
            }
        }
        public void SetPixel(int number, Color c)
        {
            neopixel.SetPixelColor(Array.IndexOf(Pixels, number), c);
        }
        public void SetPixel(Point point, Color c)
        {
            int number = 8 * point.X + point.Y;
            neopixel.SetPixelColor(Array.IndexOf(Pixels, number), c);
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
    }
}
