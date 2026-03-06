// Instance wrapper for the static SvgLoader, enabling SVG loading through LoaderMgr.
// Mirrors C++ tvg::SvgLoader (which is an instance class inheriting ImageLoader).

using System;
using System.IO;
using System.Text;

namespace ThorVG
{
    /// <summary>
    /// SVG image loader instance. Wraps the static SvgLoader to provide an
    /// ImageLoader-compatible interface for the LoaderMgr system.
    /// </summary>
    public class SvgLoaderInstance : ImageLoader
    {
        private string? content;
        private string? svgPath;
        private SvgNode? doc;
        private SvgLoaderData? loaderData;
        private Scene? builtScene;

        public SvgLoaderInstance() : base(FileType.Svg) { }

        public override bool Open(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                var text = File.ReadAllText(path);
                if (string.IsNullOrEmpty(text)) return false;

                var parsedDoc = SvgLoader.Parse(text, out var ld);
                if (parsedDoc == null) return false;

                content = text;
                svgPath = path;
                doc = parsedDoc;
                loaderData = ld;

                // Extract dimensions from the parsed SVG
                var (vbox, vw, vh, viewFlag) = SvgLoader.GetViewInfo(doc);
                w = vw;
                h = vh;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Open(byte[] data, uint size, string? rpath, bool copy)
        {
            if (data == null || size == 0) return false;

            try
            {
                var text = Encoding.UTF8.GetString(data, 0, (int)Math.Min(size, (uint)data.Length));
                if (string.IsNullOrEmpty(text)) return false;

                var parsedDoc = SvgLoader.Parse(text, out var ld);
                if (parsedDoc == null) return false;

                content = text;
                svgPath = rpath ?? "";
                doc = parsedDoc;
                loaderData = ld;

                // Extract dimensions from the parsed SVG
                var (vbox, vw, vh, viewFlag) = SvgLoader.GetViewInfo(doc);
                w = vw;
                h = vh;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Read()
        {
            if (doc == null) return false;
            if (!base.Read()) return true; // already read
            return true;
        }

        public override Paint? GetPaint()
        {
            if (doc == null || loaderData == null) return null;

            if (builtScene == null)
            {
                var (vbox, vw, vh, viewFlag) = SvgLoader.GetViewInfo(doc);
                var align = doc.doc.align;
                var meetOrSlice = doc.doc.meetOrSlice;

                builtScene = SvgSceneBuilder.SvgSceneBuild(
                    loaderData, vbox, vw, vh, align, meetOrSlice, svgPath ?? "", viewFlag);
            }

            return builtScene;
        }

        public override bool Resize(Paint paint, float w, float h)
        {
            if (paint == null) return false;
            if (this.w <= 0 || this.h <= 0) return false;

            var sx = w / this.w;
            var sy = h / this.h;
            var m = new Matrix { e11 = sx, e12 = 0, e13 = 0, e21 = 0, e22 = sy, e23 = 0, e31 = 0, e32 = 0, e33 = 1 };
            paint.Transform(m);

            // Apply the scale to the base clipper
            var clipper = paint.GetClipper();
            if (clipper != null) clipper.Transform(m);

            return true;
        }
    }
}
