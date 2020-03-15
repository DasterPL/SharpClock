using System.Drawing;

namespace SharpClock
{
    public interface IPixelDraw
    {
        /// <summary>
        /// Change Screen Brightness
        /// </summary>
        byte Brightness { get; set; }
        /// <summary>
        /// Clear Screen
        /// </summary>
        void Clear();
        /// <summary>
        /// Display data on screen
        /// </summary>
        void Draw();
        /// <summary>
        /// Return current screen as image
        /// </summary>
        /// <returns></returns>
        Image GetScreen();
        /// <summary>
        /// Set image to display on screen
        /// </summary>
        /// <param name="image">Image object to use</param>
        /// <param name="x">Screen column id</param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        void SetImage(Image image, int x, int width = 8, int height = 8);
        /// <summary>
        /// Set pixel on screen
        /// </summary>
        /// <param name="number">Number of pixel</param>
        /// <param name="c">Color</param>
        void SetPixel(int number, Color c);
        /// <summary>
        /// Set pixel on screen
        /// </summary>
        /// <param name="point">X and Y of pixel</param>
        /// <param name="c">Color</param>
        void SetPixel(Point point, Color c);
        /// <summary>
        /// Set text to display on screen
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="c">Color of text</param>
        /// <param name="x">Column number</param>
        /// <param name="spaces">Size of space between letters</param>
        /// <returns>Size of text</returns>
        int SetText(string text, Color c, int x = 0, int spaces = 1);
        /// <summary>
        /// Use Roman numbers
        /// </summary>
        /// <param name="number">Number to draw on screen</param>
        /// <param name="c">Color</param>
        /// <param name="x">Column number</param>
        void SetTextRoman(int number, Color c, int x = 0);
        /// <summary>
        /// Calculate final length of text on screen
        /// </summary>
        /// <param name="text">Text</param>
        /// <param name="spaces">Size of space between letters</param>
        /// <param name="x">Init position</param>
        /// <returns></returns>
        int TextLength(string text, int spaces = 1, int x = 0);
    }
}