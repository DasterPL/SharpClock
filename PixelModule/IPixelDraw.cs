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
        void Draw(int offset = 0);
        /// <summary>
        /// Snapshot current screen state into a flat Color[256] buffer (logical order: 8*x + y)
        /// </summary>
        Color[] GetBuffer();
        /// <summary>
        /// Composite two buffers with vertical slide and show on screen.
        /// Current slides down, next slides in from top. yOffset: 1..8.
        /// </summary>
        void DrawFromBuffers(Color[] current, Color[] next, int yOffset);
        /// <summary>
        /// Composite two buffers with horizontal slide (right-to-left) and show on screen.
        /// Current exits left, next enters from right. xOffset: 1..32.
        /// </summary>
        void DrawFromBuffersX(Color[] current, Color[] next, int xOffset);
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
        /// <summary>
        /// Draw text right-aligned so it ends at rightEdge column.
        /// </summary>
        int SetTextRight(string text, Color c, int rightEdge, int spaces = 0);
        /// <summary>
        /// Draw text centred across the full 32-column screen.
        /// </summary>
        int SetTextCentered(string text, Color c, int spaces = 0);
    }
}