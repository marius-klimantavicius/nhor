using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgLottieSlotUpdateTests
    {
        [Fact]
        public void SlotResetRestoresAnimatedProperty()
        {
            var rect = new LottieRect();
            rect.radius.sid = 73;
            rect.radius.NewFrame().value = 10;
            var last = rect.radius.NewFrame();
            last.no = 20;
            last.value = 30;
            var originalFrames = rect.radius.frames;
            var slot = new LottieSlot(null, null, 73, rect, LottieProperty.PropertyType.Float);

            slot.Apply(new LottieFloat(8) { sid = 73 });
            Assert.Equal(8, rect.radius.Evaluate(10));
            Assert.Null(rect.radius.frames);
            slot.Reset();
            Assert.Equal(20, rect.radius.Evaluate(10));
            Assert.NotSame(originalFrames, rect.radius.frames);
            Assert.NotSame(originalFrames![0], rect.radius.frames![0]);
        }

        [Fact]
        public void SlotResetRestoresExpressionAndItsPropertyBinding()
        {
            var rect = new LottieRect();
            rect.radius.sid = 73;
            var originalExpression = rect.radius.exp = new LottieExpression { code = "value + 2", property = rect.radius };
            var slot = new LottieSlot(null, null, 73, rect, LottieProperty.PropertyType.Float);
            slot.Apply(new LottieFloat(8) { sid = 73 });
            Assert.Null(rect.radius.exp);
            slot.Reset();
            Assert.NotSame(originalExpression, rect.radius.exp);
            Assert.Same(rect.radius, rect.radius.exp!.property);
            Assert.Equal("value + 2", rect.radius.exp.code);
        }

        [Fact]
        public void SlotResetAndCompositionReleaseDestroyEveryRetainedBackupProperty()
        {
            var rect = new LottieRect();
            rect.radius.sid = 73;
            rect.radius.NewFrame().value = 4;
            var last = rect.radius.NewFrame();
            last.no = 10;
            last.value = 14;
            var slot = new LottieSlot(null, null, 73, rect, LottieProperty.PropertyType.Float);
            slot.Apply(new LottieFloat(8) { sid = 73 });
            var backup = Assert.IsType<LottieFloat>(slot.pairs[0].prop);
            Assert.NotNull(backup.frames);
            slot.Reset();
            Assert.Null(backup.frames);
            Assert.Equal(9, rect.radius.Evaluate(5));

            slot.Apply(new LottieFloat(12) { sid = 73 });
            backup = Assert.IsType<LottieFloat>(slot.pairs[0].prop);
            Assert.NotNull(backup.frames);
            var comp = new LottieComposition();
            comp.slots.Add(slot);
            comp.Release();
            Assert.Null(backup.frames);
            Assert.Empty(slot.pairs);
            comp.Release();
        }

        [Fact]
        public void PropertyCopiesOwnTheirKeyframesAndPathArrays()
        {
            var source = new LottieVector();
            source.NewFrame().value = new Point(1, 2);
            var next = source.NewFrame();
            next.no = 10;
            next.value = new Point(11, 12);
            next.outTangent = new Point(3, 4);
            var copy = new LottieVector();
            copy.CopyFrom(source, false);
            source.frames![1].value.x = 99;
            Assert.Equal(11, copy.frames![1].value.x);
            Assert.Equal(3, copy.frames[1].outTangent.x);

            var path = new LottiePathSet();
            path.NewFrame().value = new PathSet
            {
                pts = new[] { new Point(2, 3), new Point(4, 5) },
                cmds = new[] { PathCommand.MoveTo, PathCommand.LineTo },
                ptsCnt = 2, cmdsCnt = 2
            };
            var pathCopy = new LottiePathSet();
            pathCopy.CopyFrom(path, false);
            path.frames![0].value.pts[0].x = 90;
            path.frames[0].value.cmds[1] = PathCommand.Close;
            Assert.Equal(2, pathCopy.frames![0].value.pts[0].x);
            Assert.Equal(PathCommand.LineTo, pathCopy.frames[0].value.cmds[1]);
            var moved = new LottiePathSet();
            moved.CopyFrom(pathCopy);
            Assert.Null(pathCopy.frames);
            Assert.Equal(2, moved.frames![0].value.ptsCnt);
            moved.Release();
            Assert.Null(moved.frames);
            Assert.Equal(0, moved.value.ptsCnt);
        }

        [Fact]
        public void TextKeyframeCopiesRetainMetricsAndStaticOverridesRetainLayout()
        {
            var source = new LottieTextDoc();
            source.NewFrame().value = new TextDocument
            {
                text = "animated", name = "fixture-font", size = 24,
                height = 30, shift = 4, tracking = 12, justify = 0.5f,
                bboxPos = new Point(3, 5), bboxSize = new Point(100, 80),
                strokeWidth = 2, strokeBelow = true, caps = 2,
                color = new RGB32(30, 60, 90)
            };
            var copy = new LottieTextDoc(source);
            Assert.NotSame(source.frames![0].value, copy.frames![0].value);
            var document = copy.Evaluate(0);
            Assert.Equal(24, document.size);
            Assert.Equal(30, document.height);
            Assert.Equal(4, document.shift);
            Assert.Equal(12, document.tracking);
            Assert.Equal(0.5f, document.justify);
            Assert.Equal(100, document.bboxSize.x);
            Assert.Equal(2, document.strokeWidth);
            Assert.True(document.strokeBelow);
            Assert.Equal(2, document.caps);
            Assert.Equal(60, document.color.g);

            var text = new LottieText();
            text.doc.sid = 91;
            text.doc.value = document.Clone();
            var backup = text.Override(new LottieTextDoc { sid = 91, value = new TextDocument { text = "replacement", name = "other" } }, false);
            Assert.Equal("replacement", text.doc.value.text);
            Assert.Equal(24, text.doc.value.size);
            text.Override(backup!, true);
            Assert.Equal("animated", text.doc.value.text);
            Assert.Equal(24, text.doc.value.size);
        }

        [Fact]
        public void SlotTargetsCoverGeometryModifiersSelectorsAndStrokeWidths()
        {
            var trim = new LottieTrimpath(); Check(trim, trim.offset);
            var corner = new LottieRoundedCorner(); Check(corner, corner.radius);
            var rect = new LottieRect(); Check(rect, rect.radius);
            var star = new LottiePolyStar(); Check(star, star.innerRoundness);
            var transform = new LottieTransform(); Check(transform, transform.skewAxis);
            var solid = new LottieSolidStroke(); Check(solid, solid.stroke.width);
            var gradient = new LottieGradientStroke(); Check(gradient, gradient.stroke.width); Check(gradient, gradient.angle);
            var repeater = new LottieRepeater(); Check(repeater, repeater.copies);
            var offset = new LottieOffsetPath(); Check(offset, offset.miterLimit);
            var pucker = new LottiePuckerBloat(); Check(pucker, pucker.amount);
            var zigzag = new LottieZigZag(); Check(zigzag, zigzag.amplitude);
            var layer = new LottieLayer(); Check(layer, layer.timeRemap);
            var selector = new LottieTextRange(); Check(selector, selector.smoothness); Check(selector, selector.maxAmount);

            static void Check(LottieObject obj, LottieFloat target)
            {
                target.sid = 131;
                target.value = 17;
                var backup = obj.Override(new LottieFloat(29) { sid = 131 }, false);
                Assert.NotNull(backup);
                Assert.Equal(29, target.value);
                obj.Override(backup!, true);
                Assert.Equal(17, target.value);
                target.sid = 0;
            }
        }

        [Fact]
        public void ImageSlotsCopyEncodedAssetsAndInvalidateDecodedPictures()
        {
            Assert.Equal(Result.Success, Initializer.Init(0));
            var image = new LottieImage();
            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "icc-none.png"));
                image.asset.sid = 154;
                image.asset.b64Data = bytes;
                image.asset.size = (uint)bytes.Length;
                image.asset.mimeType = "png";
                image.asset.width = image.asset.height = 8;
                Assert.Null(image.picture);
                var picture = image.Get();
                Assert.True(image.valid);
                Assert.Equal((ushort)1, picture.RefCnt());
                picture.Ref();
                var replacement = new LottieAsset(image.asset) { sid = 154, width = 16 };
                Assert.NotSame(bytes, replacement.b64Data);
                var backup = image.Override(replacement, false);
                Assert.Null(image.picture);
                Assert.False(image.valid);
                Assert.Equal((ushort)1, picture.RefCnt());
                Assert.Equal(16, image.asset.width);
                Assert.NotSame(replacement.b64Data, image.asset.b64Data);
                Assert.NotSame(picture, image.Get());
                image.Override(backup!, true);
                Assert.Equal(8, image.asset.width);
                Assert.Equal(bytes, image.asset.b64Data);
                picture.Unref();
            }
            finally { image.Release(); image.asset.Release(); Initializer.Term(); }
        }

        [Fact]
        public void CompositionReleaseDropsPoolReferencesOnceForSharedAssets()
        {
            var shared = new LottieRect();
            var pooled = shared.renderPooler.Pooling();
            pooled.Ref();
            var root = new LottieLayer();
            root.children.Add(shared);
            var comp = new LottieComposition { root = root };
            comp.assets.Add(shared);
            comp.Release();
            Assert.Equal((ushort)1, pooled.RefCnt());
            Assert.Empty(shared.renderPooler.pooler);
            comp.Release();
            Assert.Equal((ushort)1, pooled.RefCnt());
            pooled.Unref();
        }
    }
}
