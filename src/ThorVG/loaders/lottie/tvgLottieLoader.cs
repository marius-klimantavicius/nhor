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
                    pair.prop.Release();
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
        private readonly LoaderTask task;
        private readonly Key key = new();
        private byte[]? source;
        private bool _build = true;

        public LottieLoader() : base(FileType.Lot)
        {
            builder = new LottieBuilder();
            task = new LoaderTask(Run);
        }

        public override Result OpenResult(string path, LoaderOps? ops = null)
            => Open(path, ops) ? Result.Success : Result.InvalidArguments;

        public override Result OpenResult(byte[] data, uint size, LoaderOps? ops)
            => Open(data, size, ops, ops?.owner == Ownership.Copy) ? Result.Success : Result.InvalidArguments;

        public override bool Open(string path, LoaderOps? ops = null)
        {
            task.Done();
            if (ops != null && ops.caller != Type.Picture) return false;
            var bytes = ReadFile(path);
            if (bytes == null || bytes.Length == 0) return false;
            source = bytes;
            content = TvgUtf8.Decode(bytes);
            size = (uint)bytes.Length;
            dirName = TvgStr.Dirname(path);
            owner = Ownership.Transfer;
            builder.resolver = (ops as PictureOps)?.resolver;
            return Header();
        }

        public override bool Open(byte[] data, uint size, LoaderOps? ops, bool copy)
        {
            task.Done();
            if (ops != null && ops.caller != Type.Picture) return false;
            if (data == null || size == 0 || size > data.Length) return false;
            source = copy ? data.AsSpan(0, (int)size).ToArray() : data;
            content = TvgUtf8.Decode(source.AsSpan(0, (int)size));
            this.size = size;
            owner = copy ? Ownership.Copy : ops?.owner ?? Ownership.Borrow;
            dirName = TvgStr.Duplicate((ops as PictureOps)?.rpath) ?? ".";
            builder.resolver = (ops as PictureOps)?.resolver;
            return Header();
        }

        private bool Prepare()
        {
            // Decode at parse time as well: borrowed bytes remain authoritative
            // until the task consumes them, just as native borrowed JSON does.
            if (source != null) content = TvgUtf8.Decode(source.AsSpan(0, (int)size));
            if (content == null) return false;
            var parser = new LottieParser(content, dirName, builder.Expressions());
            if (!parser.Parse() || parser.comp == null) return false;
            using (var guard = new ScopedLock(key)) comp = parser.comp;
            if (parser.slots != null)
            {
                var slotcode = GenSlot(parser.slots, true);
                ApplySlot(slotcode, true);
                DelSlot(slotcode, true);
            }
            builder.Build(comp);
            if (owner != Ownership.Borrow) { content = null; source = null; }
            return true;
        }

        private void Run(uint tid)
        {
            if (comp != null) builder.Update(comp, frameNo);
            else if (Prepare()) builder.Update(comp!, 0);
            _build = false;
        }

        private void Request()
        {
            if (TaskScheduler.Threads() == 0) Run(0);
            else TaskScheduler.Request(task);
        }

        public override bool Read()
        {
            if (!base.Read()) return true;
            if (content == null || size == 0) return false;
            Request();
            return true;
        }

        public override bool Resize(Paint paint, float w, float h)
        {
            if (paint == null) return false;

            var sx = w / this.w;
            var sy = h / this.h;
            var m = new Matrix { e11 = sx, e12 = 0, e13 = 0, e21 = 0, e22 = sy, e23 = 0, e31 = 0, e32 = 0, e33 = 1 };
            paint.Transform(m);

            // Apply the scale to the base clipper
            var clipper = paint.GetClipper();
            if (clipper != null) clipper.Transform(m);

            return true;
        }

        public override Paint? GetPaint()
        {
            Sync();
            if (comp == null) return null;
            comp.initiated = true;
            return comp.root?.scene;
        }

        public override bool Sync()
        {
            task.Done();
            if (_build)
            {
                comp?.Clear();
                Run(0);
            }
            return true;
        }

        private bool Ready()
        {
            using (var guard = new ScopedLock(key)) if (comp != null) return true;
            task.Done();
            return comp != null;
        }

        // Frame Controls
        public override bool Frame(float no)
        {
            // Compatibility for direct use of an unopened managed loader.
            if (comp == null && content == null && source == null) return false;
            no = Shorten(no);

            // Skip update if frame diff is too small (and not tweening)
            if (!builder.tween.active && MathF.Abs(this.frameNo - no) <= 0.0009f) return false;

            task.Done();
            this.frameNo = no;

            builder.tween.Off();

            if (comp != null) comp.Clear();

            Request();

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
            // Keep the managed unopened-loader query. Native segmentEnd is
            // uninitialized before a successful header, so it has no defined
            // duration there. Opened loaders use the native division directly.
            if (comp == null && content == null && source == null) return 0;
            return (segmentEnd - segmentBegin) / frameRate;
        }

        public override Result Segment(float begin, float end)
        {
            if (comp == null && content == null && source == null) return Result.InsufficientCondition;
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
            return Ready() ? (uint)comp!.markers.Count : 0;
        }

        public string? GetMarker(uint index, out float begin, out float end)
        {
            begin = 0;
            end = 0;
            if (!Ready() || index >= comp!.markers.Count) return null;
            var marker = comp.markers[(int)index];
            begin = marker.time;
            end = marker.time + marker.duration;
            return marker.name;
        }

        public bool Segment(string? marker, out float begin, out float end)
        {
            begin = 0;
            end = 0;
            if (!Ready() || marker == null) return false;

            foreach (var m in comp.markers)
            {
                if (TvgStr.Equal(m.name, marker))
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
            builder.tween.Off();

            // Tweening is not necessary at boundary
            if (TvgMath.Zero(progress)) return Frame(from);
            else if (TvgMath.Equal(progress, 1.0f)) return Frame(to);

            task.Done();
            progress = Shorten(progress);
            frameNo = Shorten(from);
            builder.tween.On(Shorten(to), progress);

            if (comp != null) comp.Clear();
            Request();
            return true;
        }

        public bool TweenTo(float to)
        {
            task.Done();
            builder.tween.On(Shorten(to));
            return true;
        }

        public bool Tween(float progress)
        {
            if (!builder.tween.active || builder.tween.legacy) return false;
            progress = Shorten(progress);
            if (TvgMath.Equal(progress, builder.tween.progress)) return true;
            task.Done();
            if (TvgMath.Equal(progress, 1.0f)) frameNo = builder.tween.to;
            builder.tween.progress = progress;
            comp?.Clear();
            Request();
            return true;
        }

        private uint GenSlot(string? slotJson, bool byDefault)
        {
            if (slotJson == null || !Ready() || comp!.slots.Count == 0) return 0;

            var slotParser = new LottieParser(slotJson, dirName, builder.Expressions());
            slotParser.comp = comp;
            var custom = new LottieCustomSlot((uint)TvgCompressor.Djb2EncodeNative(slotJson));

            var idx = 0;
            string? sid;
            while ((sid = slotParser.Sid(idx == 0)) != null)
            {
                var sidHash = TvgCompressor.Djb2EncodeNative(sid);
                if (sidHash == 0) break;
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
            if (!Ready() || comp!.slots.Count == 0) return false;

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
            if (!Ready() || comp!.slots.Count == 0 || id == 0) return false;

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
            if (!Ready()) return false;
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

        public void Volume(float value)
        {
            task.Done();
            builder.volume = value;
        }

        private bool Header()
        {
            if (content == null) return false;
            if (TaskScheduler.Threads() == 0)
            {
                base.Read();
                if (!Prepare()) return false;
                w = comp!.w; h = comp.h;
                segmentEnd = frameCnt = comp.FrameCnt();
                frameRate = comp.frameRate;
                return true;
            }

            // Preserve the native quick scan. Full syntax validation is deferred
            // to Prepare, including prefix/BOM and raw brace behavior.
            var text = TvgStr.Duplicate(content)!;
            var startFrame = 0.0f;
            var endFrame = 0.0f;
            uint depth = 0;
            int p = 0;

            while (p < text.Length)
            {
                var c = text[p];
                if (c == '{') { ++depth; ++p; continue; }
                if (c == '}') { --depth; ++p; continue; }
                if (depth != 1) { ++p; continue; }

                // version - skip
                if (text.AsSpan(p).StartsWith("\"v\":".AsSpan()))
                {
                    p += 4;
                    continue;
                }
                // framerate
                if (text.AsSpan(p).StartsWith("\"fr\":".AsSpan()))
                {
                    p += 5;
                    if (!HeaderFloat(text, ref p, ref frameRate)) return false;
                    continue;
                }
                // start frame
                if (text.AsSpan(p).StartsWith("\"ip\":".AsSpan()))
                {
                    p += 5;
                    if (!HeaderFloat(text, ref p, ref startFrame)) return false;
                    continue;
                }
                // end frame
                if (text.AsSpan(p).StartsWith("\"op\":".AsSpan()))
                {
                    p += 5;
                    if (!HeaderFloat(text, ref p, ref endFrame)) return false;
                    continue;
                }
                // width
                if (text.AsSpan(p).StartsWith("\"w\":".AsSpan()))
                {
                    p += 4;
                    if (!HeaderFloat(text, ref p, ref w)) return false;
                    continue;
                }
                // height
                if (text.AsSpan(p).StartsWith("\"h\":".AsSpan()))
                {
                    p += 4;
                    if (!HeaderFloat(text, ref p, ref h)) return false;
                    continue;
                }
                ++p;
            }

            if (frameRate < MathConstants.FLOAT_EPSILON) return false;
            segmentEnd = frameCnt = endFrame - startFrame;
            return true;
        }

        private static bool HeaderFloat(string text, ref int position, ref float value)
        {
            // Native strchr chooses a comma anywhere ahead before considering
            // a closing brace. Prefix conversion and failure mutation order
            // are distinct from float.TryParse of a delimited token.
            int end = text.IndexOf(',', position);
            if (end < 0) end = text.IndexOf('}', position);
            if (end < 0) return false;
            int cursor = position;
            value = TvgStr.ToFloat(text, ref cursor);
            position = end;
            return true;
        }

        private float StartFrame()
        {
            return segmentBegin;
        }

        internal override void ReleaseResources()
        {
            task.Done();
            Clear();
            builder.Dispose();
            dirName = null;
            base.ReleaseResources();
        }

        private void Clear()
        {
            foreach (var slot in _slots) slot.Release();
            _slots.Clear();
            curSlot = 0;
            builder.tween.Dispose();
            comp?.Release();
            comp = null;
            source = null;
            content = null;
            size = 0;
        }
    }
}
