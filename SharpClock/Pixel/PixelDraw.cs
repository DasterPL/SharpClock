using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
                            SetHw(PixelHw[j], Color.Black, j);
                        else
                            SetHw(PixelHw[j], _mirror[j - 1], j);
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
                            SetHw(PixelHw[j], Color.Black, j);
                        else if (j + 1 < LedCount)
                            SetHw(PixelHw[j], _mirror[j + 1], j);
                    }
                }
            }
        }
        PixelFont Font = null;

        Dictionary<int, int[]> RomanFont = null;
        static readonly Dictionary<char, char> unexceptedCharacters = new Dictionary<char, char>()
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
        byte _brightness = 10;
        readonly byte[] _gammaLut = new byte[256];

        // Managed mirror of what is currently on the strip, in logical order (8*x + y).
        // Lets GetBuffer/GetScreen answer without 256 native reads, and lets Clear() skip
        // pixels that are already black — a full clear used to cost 256 native calls a frame.
        readonly Color[] _mirror = new Color[LedCount];
        readonly bool[] _lit = new bool[LedCount]; // indexed logically, mirrors "is non-black"

        void UpdateLut()
        {
            float scale = _brightness / 255f;
            _gammaLut[0] = 0;
            for (int i = 1; i < 256; i++)
            {
                double v = 255 * Math.Pow(i / 255.0, 2.2) * scale;
                _gammaLut[i] = (byte)Math.Max(1, Math.Round(v));
            }
        }

        Color Gamma(Color c) => Color.FromArgb(_gammaLut[c.R], _gammaLut[c.G], _gammaLut[c.B]);

        /// <summary>
        /// Single funnel to the LED strip. `c` is the final on-wire colour (gamma already applied).
        /// Skips the native call when a pixel is already black.
        /// </summary>
        void SetHw(int hw, Color c, int logical)
        {
            bool black = c.R == 0 && c.G == 0 && c.B == 0;
            if (black && !_lit[logical]) return;
            neopixel.SetPixelColor(hw, c);
            _mirror[logical] = c;
            _lit[logical] = !black;
        }

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
            neopixel.SetBrightness(255);
            UpdateLut();
            PixelHw = new int[LedCount];
            for (int i = 0; i < LedCount; i++)
                PixelHw[i] = Array.IndexOf(Pixels, i);
            for (int i = 0; i < LedCount; i++)
                _mirror[i] = Color.Black;
            LoadFont();
        }

        /// <summary>
        /// Reads a rectangle out of a bitmap as a column-major Color[] (index = x*height + y).
        /// Uses LockBits rather than GetPixel: under Mono/libgdiplus GetPixel is a managed→native
        /// round trip per pixel, and this runs for every icon on every frame at 30 fps.
        /// </summary>
        Color[] LoadImage(Image image, Point point, int width = 8, int height = 8)
        {
            var bitmap = image as Bitmap;
            if (bitmap == null)
                throw new ArgumentException("LoadImage expects a Bitmap", nameof(image));

            var result = new Color[width * height];
            var rect = new Rectangle(point.X, point.Y, width, height);
            BitmapData data = null;
            try
            {
                data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = data.Stride;
                var buf = new byte[Math.Abs(stride) * height];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        int o = y * stride + x * 4; // BGRA little-endian
                        result[x * height + y] = Color.FromArgb(buf[o + 3], buf[o + 2], buf[o + 1], buf[o]);
                    }
                return result;
            }
            catch (Exception e)
            {
                // Older libgdiplus builds can refuse format-converting LockBits on some
                // indexed sources. Correctness first: fall back to the slow per-pixel path.
                if (!_lockBitsWarned)
                {
                    _lockBitsWarned = true;
                    Logger.Log(ConsoleColor.DarkYellow, $"[PixelDraw] LockBits unavailable, using GetPixel fallback: {e.Message}");
                }
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        result[x * height + y] = bitmap.GetPixel(point.X + x, point.Y + y);
                return result;
            }
            finally
            {
                if (data != null) { try { bitmap.UnlockBits(data); } catch { } }
            }
        }
        bool _lockBitsWarned;

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
                        .Where(idx => pixels[idx].ToArgb() == White.ToArgb())
                        .ToArray();
                }
            }
            Logger.Log(ConsoleColor.Cyan, "Loading Font Completed");
        }
        void LoadRomanFont(string file = "img/RomanNumbers.png")
        {
            Logger.Log(ConsoleColor.Cyan, "Loading Roman Font img from file");
            var roman = new Dictionary<int, int[]>();
            using (Image image = Image.FromFile(file))
            {
                var chars = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

                for (int i = 0; i < chars.Length; i++)
                {
                    var Letter = LoadImage(image, new Point(i * 9, 0), 9);
                    var points = new List<int>();
                    for (int i1 = 0; i1 < Letter.Length; i1++)
                        if (Letter[i1].ToArgb() == White.ToArgb())
                            points.Add(i1);
                    roman.Add(chars[i], points.ToArray());
                }
            }
            RomanFont = roman; // published only once fully built
        }
        public Image GetScreen()
        {
            var buffer = GetBuffer();
            var tmp = new Bitmap(32, 8, PixelFormat.Format32bppArgb);
            var data = tmp.LockBits(new Rectangle(0, 0, 32, 8), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var buf = new byte[Math.Abs(data.Stride) * 8];
                for (int x = 0; x < 32; x++)
                    for (int y = 0; y < 8; y++)
                    {
                        Color c = buffer[8 * x + y];
                        int o = y * data.Stride + x * 4;
                        buf[o + 0] = c.B;
                        buf[o + 1] = c.G;
                        buf[o + 2] = c.R;
                        buf[o + 3] = 255;
                    }
                Marshal.Copy(buf, 0, data.Scan0, buf.Length);
            }
            finally
            {
                tmp.UnlockBits(data);
            }
            return tmp;
        }
        public byte Brightness
        {
            get => _brightness;
            set { _brightness = value; UpdateLut(); }
        }
        public void Clear()
        {
            for (int i = 0; i < LedCount; i++)
            {
                if (!_lit[i]) continue;
                neopixel.SetPixelColor(PixelHw[i], Color.Black);
                _mirror[i] = Color.Black;
                _lit[i] = false;
            }
        }
        public void SetPixel(int number, Color c)
        {
            if (number < 0 || number >= LedCount) return;
            SetHw(PixelHw[number], Gamma(c), number);
        }
        public void SetPixel(Point point, Color c)
        {
            int number = 8 * point.X + point.Y;
            if (number < 0 || number >= LedCount) return;
            SetHw(PixelHw[number], Gamma(c), number);
        }

        /// <summary>Maps a character onto one the font actually has.</summary>
        char MapChar(char letter)
        {
            if (Font.ContainsKey(letter)) return letter;
            return unexceptedCharacters.TryGetValue(letter, out char replacement) ? replacement : '.';
        }

        public int SetText(string text, Color c, int x = 0, int spaces = 1)
        {
            int current_x = x * 8;

            foreach (var letter in text)
            {
                var glyph = Font[MapChar(letter)];
                foreach (var pixel in glyph.Points)
                    SetPixel(current_x + pixel, c);
                current_x += glyph.Size * 8 + (8 * spaces);
            }
            return current_x/8;
        }
        public void SetTextRoman(int number, Color c, int x = 0)
        {
            if (RomanFont == null)
                LoadRomanFont();
            if (!RomanFont.TryGetValue(number, out int[] PixelLetter))
                return;
            int current_x = x * 8;
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
                current_x += Font[MapChar(letter)].Size * 8 + (8 * spaces);

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
            Array.Copy(_mirror, buffer, LedCount);
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
                {
                    int logical = 8 * x + y;
                    SetHw(PixelHw[logical], src[8 * srcX + y], logical);
                }
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
                    int logical = 8 * x + y;
                    SetHw(PixelHw[logical], c, logical);
                }
            }
            neopixel.Show();
        }
    }
}
