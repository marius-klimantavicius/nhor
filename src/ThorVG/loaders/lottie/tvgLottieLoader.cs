// Ported from ThorVG/src/loaders/lottie/tvgLottieLoader.h and tvgLottieLoader.cpp

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThorVG
{
    public class LottieLoader : AnimLoader
    {
        private sealed class LottieCustomSlot
        {
            public sealed class Pair
            {
                public LottieProperty prop = null!;
                public LottieSlot target = null!;
            }

            public readonly List<Pair> props = new();
            public readonly uint code;

            public LottieCustomSlot(uint code)
            {
                this.code = code;
            }

            public void Release()
            {
                foreach (var pair in props)
                {
                    if (pair.prop is LottieBitmap bitmap) bitmap.Release();
                }
                props.Clear();
            }
        }

        public string? content;
        public uint size;
        public float frameNo;
        public float frameCnt;
        public float frameRate;

        public LottieBuilder builder;
        public LottieComposition? comp;
        public uint curSlot;
        private readonly List<LottieCustomSlot> _slots = new();

        public string? dirName;
        #pragma warning disable CS0414
        private bool _copy;
        #pragma warning restore CS0414
        private bool _build = true;

        public LottieLoader() : base(FileType.Lot)
        {
            builder = new LottieBuilder();
        }

        public override bool Open(string path, LoaderOps? ops = null)
        {
            // Read file content
            if (!File.Exists(path)) return false;

            try
            {
                content = File.ReadAllText(path);
                size = (uint)content.Length;
                dirName = Path.GetDirectoryName(path);
                _copy = true;

                return Header();
            }
            catch
            {
                return false;
            }
        }

        public override bool Open(byte[] data, uint size, LoaderOps? ops, bool copy)
        {
            if (data == null || data.Length == 0 || size == 0) return false;

            // Lottie data is JSON text - decode from UTF-8 bytes
            var str = Encoding.UTF8.GetString(data, 0, (int)Math.Min(size, (uint)data.Length));
            if (string.IsNullOrEmpty(str)) return false;

            if (copy)
            {
                content = new string(str);
                _copy = true;
            }
            else
            {
                content = str;
            }
            this.size = size;
            dirName = (ops as PictureOps)?.rpath ?? ".";

            return Header();
        }

        public override bool Read()
        {
            // Parse the Lottie JSON
            if (content == null) return false;

            var parser = new LottieParser(content, dirName, builder.Expressions());
            if (!parser.Parse())
            {
                return false;
            }

            comp = parser.comp;
            if (comp == null) return false;

            // Apply default slot overrides from the "slots" section of the JSON.
            // Mirrors C++ LottieLoader::prepare() lines: gen(parser.slots) → apply → del
            if (parser.slots != null)
            {
                var slotcode = GenSlot(parser.slots, true);
                ApplySlot(slotcode, true);
                DelSlot(slotcode, true);
            }

            w = (uint)comp.w;
            h = (uint)comp.h;
            segmentEnd = frameCnt = comp.FrameCnt();
            frameRate = comp.frameRate;

            return true;
        }

        public override bool Resize(Paint paint, float w, float h)
        {
            if (paint == null) return false;

            var sx = this.w > 0 ? w / this.w : 1.0f;
            var sy = this.h > 0 ? h / this.h : 1.0f;
            var m = new Matrix { e11 = sx, e12 = 0, e13 = 0, e21 = 0, e22 = sy, e23 = 0, e31 = 0, e32 = 0, e33 = 1 };
            paint.Transform(m);

            // Apply the scale to the base clipper
            var clipper = paint.GetClipper();
            if (clipper != null) clipper.Transform(m);

            return true;
        }

        public override Paint? GetPaint()
        {
            if (comp == null) return null;

            // Build if needed
            if (_build)
            {
                builder.Build(comp);
                _build = false;
            }

            // Update to current frame
            builder.Update(comp, frameNo);

            comp.initiated = true;
            return comp.root?.scene;
        }

        /// <summary>
        /// Called by Picture.InternalLoad() on subsequent frame updates.
        /// Mirrors C++ LottieLoader::run() which is dispatched via TaskScheduler::request().
        /// </summary>
        public override void Sync()
        {
            if (comp != null)
            {
                if (_build)
                {
                    comp.Clear();
                    builder.Build(comp);
                    _build = false;
                }
                builder.Update(comp, frameNo);
            }
        }

        // Frame Controls
        public override bool Frame(float no)
        {
            if (comp == null) return false;

            no = Shorten(no);

            // Skip update if frame diff is too small (and not tweening)
            if (!builder.tween.active && MathF.Abs(this.frameNo - no) <= 0.0009f) return false;

            this.frameNo = no;

            builder.tween.Off();

            if (comp != null) comp.Clear();

            // The native loader dispatches its update task here. Run it
            // synchronously so resolver state changes are visible on return.
            Sync();

            return true;
        }

        public override float TotalFrame()
        {
            return segmentEnd - segmentBegin;
        }

        public override float CurFrame()
        {
            return frameNo - StartFrame();
        }

        public override float Duration()
        {
            if (comp == null || comp.frameRate <= 0) return 0;
            return (segmentEnd - segmentBegin) / comp.frameRate;
        }

        public override Result Segment(float begin, float end)
        {
            if (comp == null) return Result.InsufficientCondition;
            if (begin < 0.0f) begin = 0.0f;
            if (end > frameCnt) end = frameCnt;
            if (begin > end) return Result.InvalidArguments;
            segmentBegin = begin;
            segmentEnd = end;
            return Result.Success;
        }

        // Marker supports
        public uint MarkersCnt()
        {
            return comp != null ? (uint)comp.markers.Count : 0;
        }

        public string? GetMarker(uint index, out float begin, out float end)
        {
            begin = 0;
            end = 0;
            if (comp == null || index >= comp.markers.Count) return null;
            var marker = comp.markers[(int)index];
            begin = marker.time;
            end = marker.time + marker.duration;
            return marker.name;
        }

        public bool Segment(string? marker, out float begin, out float end)
        {
            begin = 0;
            end = 0;
            if (comp == null || marker == null) return false;

            foreach (var m in comp.markers)
            {
                if (m.name == marker)
                {
                    begin = m.time;
                    end = m.time + m.duration;
                    return true;
                }
            }
            return false;
        }

        public float Shorten(float frameNo)
        {
            // This ensures that the target frame number is reached.
            // C++ uses nearbyintf which rounds to nearest-even (default rounding mode)
            return MathF.Round((frameNo + StartFrame()) * 10000.0f) * 0.0001f;
        }

        public bool Tween(float from, float to, float progress)
        {
            if (comp == null) return false;

            builder.tween.Off();

            // Tweening is not necessary at boundary
            if (TvgMath.Zero(progress)) return Frame(from);
            else if (TvgMath.Equal(progress, 1.0f)) return Frame(to);

            frameNo = Shorten(from);

            progress = Shorten(progress);
            builder.tween.On(Shorten(to), progress);

            if (comp != null) comp.Clear();

            return true;
        }

        public bool TweenTo(float to)
        {
            if (comp == null) return false;
            builder.tween.On(Shorten(to));
            return true;
        }

        public bool Tween(float progress)
        {
            if (comp == null || !builder.tween.active || builder.tween.legacy) return false;
            progress = Shorten(progress);
            if (TvgMath.Equal(progress, builder.tween.progress)) return true;
            if (TvgMath.Equal(progress, 1.0f)) frameNo = builder.tween.to;
            builder.tween.progress = progress;
            comp.Clear();
            return true;
        }

        private uint GenSlot(string? slotJson, bool byDefault)
        {
            if (slotJson == null || comp == null || comp.slots.Count == 0) return 0;

            var slotParser = new LottieParser(slotJson, dirName, builder.Expressions());
            slotParser.comp = comp;
            var custom = new LottieCustomSlot((uint)TvgCompressor.Djb2Encode(slotJson));

            var idx = 0;
            string? sid;
            while ((sid = slotParser.Sid(idx == 0)) != null)
            {
                var sidHash = TvgCompressor.Djb2Encode(sid);
                var found = false;
                foreach (var slot in comp.slots)
                {
                    if (slot.sid != sidHash) continue;
                    var prop = slotParser.Parse(slot);
                    if (prop != null)
                    {
                        custom.props.Add(new LottieCustomSlot.Pair { prop = prop, target = slot });
                    }
                    found = true;
                    break;
                }
                if (!found) slotParser.Skip();
                idx++;
            }

            if (custom.props.Count == 0) return 0;
            _slots.Add(custom);
            return custom.code;
        }

        public uint GenSlot(string? slotJson)
        {
            return GenSlot(slotJson, false);
        }

        private bool ApplySlot(uint id, bool byDefault)
        {
            if (curSlot == id) return true;
            if (comp == null || comp.slots.Count == 0) return false;

            var applied = false;
            if (id == 0)
            {
                foreach (var slot in comp.slots) slot.Reset();
                applied = true;
            }
            else
            {
                foreach (var slot in _slots)
                {
                    if (slot.code != id) continue;
                    foreach (var pair in slot.props) pair.target.Apply(pair.prop, byDefault);
                    applied = true;
                    break;
                }
            }

            curSlot = id;
            if (applied) _build = true;
            return applied;
        }

        public Result ApplySlot(uint id)
        {
            return ApplySlot(id, false) ? Result.Success : Result.InvalidArguments;
        }

        private bool DelSlot(uint id, bool byDefault)
        {
            if (comp == null || comp.slots.Count == 0 || id == 0) return false;

            for (var i = 0; i < _slots.Count; ++i)
            {
                var slot = _slots[i];
                if (slot.code != id) continue;
                if (!byDefault)
                {
                    foreach (var pair in slot.props) pair.target.Reset();
                    _build = true;
                }
                slot.Release();
                _slots.RemoveAt(i);
                if (curSlot == id) curSlot = 0;
                break;
            }
            return true;
        }

        public Result DelSlot(uint id)
        {
            return DelSlot(id, false) ? Result.Success : Result.InvalidArguments;
        }

        public bool SetQuality(byte value)
        {
            if (comp == null) return false;
            if (comp.quality != value)
            {
                comp.quality = value;
                _build = true;
            }
            return true;
        }

        public void Resolver(Action<LottieAudioResolver, object?>? func, object? data)
        {
            builder.audioResolver = func;
            builder.audioResolverData = data;
        }

        private bool Header()
        {
            if (content == null) return false;

            // Quick validation that this looks like a Lottie JSON
            var trimmed = content.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '{') return false;

            // Quickly scan JSON at depth 1 for "v", "fr", "ip", "op", "w", "h"
            var startFrame = 0.0f;
            var endFrame = 0.0f;
            uint depth = 0;
            int p = 0;

            while (p < content.Length)
            {
                var c = content[p];
                if (c == '{') { ++depth; ++p; continue; }
                if (c == '}') { --depth; ++p; continue; }
                if (depth != 1) { ++p; continue; }

                // version - skip
                if (p + 4 <= content.Length && content.AsSpan(p, 4).SequenceEqual("\"v\":".AsSpan()))
                {
                    p += 4;
                    continue;
                }
                // framerate
                if (p + 5 <= content.Length && content.AsSpan(p, 5).SequenceEqual("\"fr\":".AsSpan()))
                {
                    p += 5;
                    var end = content.IndexOfAny(new[] { ',', '}' }, p);
                    if (end < 0) break;
                    float.TryParse(content.AsSpan(p, end - p), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out frameRate);
                    p = end;
                    continue;
                }
                // start frame
                if (p + 5 <= content.Length && content.AsSpan(p, 5).SequenceEqual("\"ip\":".AsSpan()))
                {
                    p += 5;
                    var end = content.IndexOfAny(new[] { ',', '}' }, p);
                    if (end < 0) break;
                    float.TryParse(content.AsSpan(p, end - p), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out startFrame);
                    p = end;
                    continue;
                }
                // end frame
                if (p + 5 <= content.Length && content.AsSpan(p, 5).SequenceEqual("\"op\":".AsSpan()))
                {
                    p += 5;
                    var end = content.IndexOfAny(new[] { ',', '}' }, p);
                    if (end < 0) break;
                    float.TryParse(content.AsSpan(p, end - p), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out endFrame);
                    p = end;
                    continue;
                }
                // width
                if (p + 4 <= content.Length && content.AsSpan(p, 4).SequenceEqual("\"w\":".AsSpan()))
                {
                    p += 4;
                    var end = content.IndexOfAny(new[] { ',', '}' }, p);
                    if (end < 0) break;
                    float fw;
                    float.TryParse(content.AsSpan(p, end - p), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fw);
                    w = fw;
                    p = end;
                    continue;
                }
                // height
                if (p + 4 <= content.Length && content.AsSpan(p, 4).SequenceEqual("\"h\":".AsSpan()))
                {
                    p += 4;
                    var end = content.IndexOfAny(new[] { ',', '}' }, p);
                    if (end < 0) break;
                    float fh;
                    float.TryParse(content.AsSpan(p, end - p), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fh);
                    h = fh;
                    p = end;
                    continue;
                }
                ++p;
            }

            if (frameRate <= 0) return false;
            segmentEnd = frameCnt = endFrame - startFrame;
            return true;
        }

        private float StartFrame()
        {
            return segmentBegin;
        }

        private void Clear()
        {
            foreach (var slot in _slots) slot.Release();
            _slots.Clear();
            curSlot = 0;
            comp = null;
            content = null;
            size = 0;
        }
    }
}
