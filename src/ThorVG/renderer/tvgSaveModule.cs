// Ported from ThorVG/src/renderer/tvgSaveModule.h

namespace ThorVG
{
    /// <summary>
    /// Abstract save module. Mirrors C++ tvg::SaveModule.
    /// GifSaver implements animation encoding and its matching close lifecycle.
    /// </summary>
    public abstract class SaveModule
    {
        public abstract bool Save(Paint paint, Paint? bg, string filename, uint quality);
        public abstract bool Save(Animation animation, Paint? bg, string filename, uint quality, uint fps);
        public abstract bool Close();
    }
}
