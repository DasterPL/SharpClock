using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClock
{
    public class GifImage
    {
        Image image;
        DateTime lastTime;
        int lastFrame = 0;
        int delay;
        public GifImage(Image image)
        {
            lastTime = DateTime.Now;
            image.SelectActiveFrame(FrameDimension.Time, 0);
            delay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, 0) * 10;
            this.image = image;
        }

        public Image GetCurrentFrame
        {
            get {
                try
                {
                    //Console.WriteLine($"e1 lF: {lastFrame} delay:  {delay}");
                    double elapsed = (DateTime.Now - lastTime).TotalMilliseconds;
                    if (elapsed >= delay)
                    {
                        lastFrame = lastFrame < image.GetFrameCount(FrameDimension.Time)-1 ? lastFrame + 1 : 0;
                        image.SelectActiveFrame(FrameDimension.Time, lastFrame);
                        lastTime = DateTime.Now;
                    }
                    //return (Image)image.Clone();
                    return image;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                Bitmap bmp = new Bitmap(8, 8);
                using (Graphics gr = Graphics.FromImage(bmp))
                {
                    gr.Clear(Color.Black);
                }
                return bmp;
            }
        }
    }
}
