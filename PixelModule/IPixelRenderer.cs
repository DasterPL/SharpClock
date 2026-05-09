
namespace SharpClock
{
    public interface IPixelRenderer
    {
        PixelModule Current { get; }
        PixelModule[] GetModules { get; }
        bool IsReady { get; }
        bool IsRunning { get; }
        bool Pause { get; set; }
        bool AnimatedSwitching { get; set; }

        PixelModule GetModule(string name);
        void LoadModule(string absolutePath);
        void UnloadModule(string dllFileName);
        void NextModule();
        void PrevModule();
        void Reload();
        void Start();
        void Stop();
        bool SwitchModule(PixelModule module, bool forcePause = false);
        void UpdateConfig(PixelModule module);
    }
}