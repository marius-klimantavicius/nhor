// Instance adapter for native SvgLoader and its header/worker/cache lifetime.
using System;
using System.Text;

namespace ThorVG
{
    public class SvgLoaderInstance : ImageLoader
    {
        private byte[]? source;
        private uint size;
        private string? content;
        private string svgPath = "";
        private SvgParserContext context = new();
        private Scene? root;
        private Box vbox;
        private SvgViewFlag viewFlag;
        private AspectRatioAlign align;
        private AspectRatioMeetOrSlice meetOrSlice;
        private readonly LoaderTask task;

        public SvgLoaderInstance() : base(FileType.Svg) { task = new LoaderTask(Run); }

        public override Result OpenResult(string path, LoaderOps? ops = null)
            => Open(path, ops) ? Result.Success : Result.InvalidArguments;

        public override Result OpenResult(byte[] data, uint size, LoaderOps? ops)
            => Open(data, size, ops, ops?.owner == Ownership.Copy) ? Result.Success : Result.InvalidArguments;

        public override bool Open(string path, LoaderOps? ops = null)
        {
            if (ops != null && ops.caller != Type.Picture) return false;
            var bytes = ReadFile(path);
            if (bytes == null || bytes.Length == 0) return false;
            Clear();
            source = bytes;
            size = (uint)bytes.Length;
            owner = Ownership.Transfer;
            svgPath = path;
            context.accessible = (ops as PictureOps)?.accessible ?? false;
            return Header();
        }

        public override bool Open(byte[] data, uint size, LoaderOps? ops, bool copy)
        {
            if (ops != null && ops.caller != Type.Picture) return false;
            if (data == null || size == 0 || size > data.Length) return false;
            Clear();
            source = copy ? data.AsSpan(0, (int)size).ToArray() : data;
            this.size = size;
            owner = copy ? Ownership.Copy : ops?.owner ?? Ownership.Borrow;
            svgPath = (ops as PictureOps)?.rpath ?? "";
            context.accessible = (ops as PictureOps)?.accessible ?? false;
            return Header();
        }

        private bool Header()
        {
            content = Encoding.UTF8.GetString(source!, 0, (int)size);
            if (!SvgLoader.Header(content, context)) return false;
            (vbox, w, h, viewFlag) = SvgLoader.GetViewInfo(context.doc!);
            align = context.doc!.doc.align;
            meetOrSlice = context.doc.doc.meetOrSlice;
            if ((viewFlag & SvgViewFlag.Viewbox) == 0) Run(0);
            return true;
        }

        private void Run(uint tid)
        {
            if (context.svgParse == null) return;
            if ((viewFlag & SvgViewFlag.Viewbox) != 0 &&
                (MathF.Abs(vbox.w) <= MathConstants.FLOAT_EPSILON || MathF.Abs(vbox.h) <= MathConstants.FLOAT_EPSILON))
                root = Scene.Gen();
            else
            {
                content = Encoding.UTF8.GetString(source!, 0, (int)size);
                if (SvgLoader.ParseDocument(content, context, reuseHeader: true) != null)
                {
                    root = SvgBuilder.SvgSceneBuild(context, vbox, w, h, align, meetOrSlice, svgPath, viewFlag);
                    if ((viewFlag & SvgViewFlag.Viewbox) == 0)
                    {
                        vbox = context.doc!.doc.vbox;
                        w = context.doc.doc.w;
                        h = context.doc.doc.h;
                    }
                }
            }
            root?.Ref();
            ClearContext(false);
        }

        public override bool Read()
        {
            if (content == null || size == 0) return false;
            if (!base.Read() || root != null) return true;
            if (TaskScheduler.Threads() == 0) Run(0);
            else TaskScheduler.Request(task);
            return true;
        }

        public override bool Sync() { task.Done(); return true; }

        public override Paint? GetPaint()
        {
            task.Done();
            return root == null ? null : root.RefCnt() == 1 ? root : root.Duplicate();
        }

        public override bool Close()
        {
            if (!base.Close()) return false;
            task.Done();
            return true;
        }

        public override AccessorEntity? Access(uint id)
        {
            task.Done();
            foreach (var entity in context.access) if (entity.id == id) return entity;
            return null;
        }

        public override void Access(Func<Paint, object?, bool> callback, object? data)
        {
            task.Done();
            foreach (var entity in context.access) if (!callback(entity.paint, data)) return;
        }

        public override bool Resize(Paint paint, float w, float h)
        {
            if (paint == null) return false;
            paint.Transform(new Matrix { e11 = w / this.w, e22 = h / this.h, e33 = 1 });
            return true;
        }

        private void ClearContext(bool all)
        {
            context.svgParse = null;
            foreach (var gradient in context.gradients) gradient.Clear();
            context.gradients.Clear(); context.gradientStack.Clear();
            context.doc = context.def = context.cssStyle = context.currentGraphicsNode = null;
            context.stack.Clear(); context.nodesToStyle.Clear();
            context.cloneNodes.Free();
            if (!all) return;
            foreach (var font in context.fonts) Text.UnloadFont(font.name);
            context.fonts.Clear(); context.images.Clear(); context.access.Clear();
        }

        private void Clear()
        {
            task.Done();
            ClearContext(true);
            root?.Unref(); root = null;
            source = null; content = null; size = 0;
            owner = Ownership.Borrow;
            readied = false;
            context = new SvgParserContext();
        }

        internal override void ReleaseResources()
        {
            Clear();
            base.ReleaseResources();
        }
    }
}
