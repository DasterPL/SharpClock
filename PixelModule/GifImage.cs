using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace SharpClock
{
    public class GifImage
    {
        // GIF property 0x5100: per-frame delays in centiseconds (1/100 s)
        const int GifFrameDelayPropertyId = 0x5100;

        readonly Image image;
        readonly int[] delays;
        readonly int frameCount;
        readonly Bitmap fallback;

        DateTime lastTime;
        int lastFrame = 0;

        public GifImage(Image image)
        {
            this.image = image;
            frameCount = image.GetFrameCount(FrameDimension.Time);

            var raw = image.GetPropertyItem(GifFrameDelayPropertyId).Value;
            delays = new int[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                int offset = i * 4;
                int cs = offset + 4 <= raw.Length ? BitConverter.ToInt32(raw, offset) : 10;
                delays[i] = Math.Max(cs * 10, 20); // centiseconds → ms, minimum 20 ms
            }

            image.SelectActiveFrame(FrameDimension.Time, 0);
            lastTime = DateTime.Now;

            fallback = new Bitmap(8, 8);
            using (var gr = Graphics.FromImage(fallback))
                gr.Clear(Color.Black);
        }

        public Image CurrentFrame
        {
            get
            {
                try { image.SelectActiveFrame(FrameDimension.Time, lastFrame); return image; }
                catch { return fallback; }
            }
        }

        public Image Advance()
        {
            try
            {
                if ((DateTime.Now - lastTime).TotalMilliseconds >= delays[lastFrame])
                {
                    lastFrame = (lastFrame + 1) % frameCount;
                    image.SelectActiveFrame(FrameDimension.Time, lastFrame);
                    lastTime = DateTime.Now;
                }
                return image;
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, e.Message);
                return fallback;
            }
        }

        [Obsolete("Use Advance() to tick animation or CurrentFrame for a pure getter")]
        public Image GetCurrentFrame => Advance();
    }
}
