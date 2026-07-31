using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace SharpClock
{
    public class GifImage
    {
        // GIF property 0x5100: per-frame delays in centiseconds (1/100 s)
        const int GifFrameDelayPropertyId = 0x5100;
        const int DefaultDelayMs = 100;

        readonly Image image;
        readonly int[] delays;
        readonly int frameCount;
        readonly Bitmap fallback;

        // Monotonic. DateTime.Now goes backwards at the end of DST and whenever NTP corrects
        // this clock (the Pi has no RTC), which used to freeze the animation for up to an hour.
        readonly Stopwatch clock = Stopwatch.StartNew();
        long lastFrameMs;
        int lastFrame = 0;

        public GifImage(Image image)
        {
            this.image = image;
            frameCount = Math.Max(1, image.GetFrameCount(FrameDimension.Time));

            delays = new int[frameCount];
            byte[] raw = null;
            try { raw = image.GetPropertyItem(GifFrameDelayPropertyId)?.Value; }
            catch { /* not every GIF carries the delay block */ }

            for (int i = 0; i < frameCount; i++)
            {
                int offset = i * 4;
                int cs = raw != null && offset + 4 <= raw.Length
                    ? BitConverter.ToInt32(raw, offset)
                    : DefaultDelayMs / 10;
                delays[i] = Math.Max(cs * 10, 20); // centiseconds → ms, minimum 20 ms
            }

            try { image.SelectActiveFrame(FrameDimension.Time, 0); } catch { }
            lastFrameMs = 0;

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
                long now = clock.ElapsedMilliseconds;
                if (now - lastFrameMs >= delays[lastFrame])
                {
                    lastFrame = (lastFrame + 1) % frameCount;
                    image.SelectActiveFrame(FrameDimension.Time, lastFrame);
                    lastFrameMs = now;
                }
                return image;
            }
            catch (Exception e)
            {
                Logger.Log(ConsoleColor.Red, e.Message);
                return fallback;
            }
        }

    }
}
