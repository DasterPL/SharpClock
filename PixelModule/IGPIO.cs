using System.Threading.Tasks;

namespace SharpClock
{
    public enum ButtonId { Pause, Next, LongNext, User1, User2, User3};
    public delegate void ButtonEventArgs(ButtonId button);
    public interface IGPIO
    {
        event ButtonEventArgs OnButtonClick;

        void EnableBuzzer(int amount = 3, int howLong = 100, int interval = 200);
    }
}