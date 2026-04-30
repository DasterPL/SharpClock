using System.Collections.Generic;

namespace SharpClock
{
    class PixelFont
    {
        public class Char
        {
            public char Letter { get; private set; }
            public int[] Points { get; set; }
            public int Size { get; private set; }
            public Char(char letter, int size)
            {
                Letter = letter;
                Size = size;
            }
        }

        readonly Dictionary<char, Char> chars = new Dictionary<char, Char>();
        readonly List<Char> ordered = new List<Char>();

        public Char this[int i] => ordered[i];
        public Char this[char c] => chars[c];
        public int Length => chars.Count;
        public bool ContainsKey(char c) => chars.ContainsKey(c);

        public void Add(Char c)
        {
            chars.Add(c.Letter, c);
            ordered.Add(c);
        }
    }
}
