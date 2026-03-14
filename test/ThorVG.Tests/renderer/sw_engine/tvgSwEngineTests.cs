using System.Collections.Generic;
using Xunit;

namespace ThorVG.Tests
{
    /// <summary>
    /// Tests for the SW (software) rendering engine.
    /// Covers SwRenderer, SwCanvas integration, SwHelper blend ops,
    /// SwMath, memory pool, and full canvas pipeline rendering.
    /// </summary>
    public unsafe class tvgSwEngineTests
    {
        // =====================================================================
        //  SwHelper blend/pixel ops (public static class)
        // =====================================================================

        [Fact]
        public void SwHelper_ALPHA_BLEND_FullOpacity_ApproximatesOriginal()
        {
            uint color = 0xFF804020;
            var result = SwHelper.ALPHA_BLEND(color, 255);
            // With alpha=255, ALPHA_BLEND increments to 256 -> (c*256)>>8 = c
            Assert.True((result >> 24) >= 0xFE); // alpha channel preserved
        }

        [Fact]
        public void SwHelper_ALPHA_BLEND_ZeroOpacity_ReturnsZero()
        {
            uint color = 0xFFFFFFFF;
            var result = SwHelper.ALPHA_BLEND(color, 0);
            Assert.Equal(0u, result);
        }

        [Fact]
        public void SwHelper_IA_ReturnsInverseAlpha()
        {
            Assert.Equal(0, SwHelper.IA(0xFF000000)); // alpha=255 -> IA=0
            Assert.Equal(255, SwHelper.IA(0x00000000)); // alpha=0 -> IA=255
            Assert.Equal(127, SwHelper.IA(0x80000000)); // alpha=128 -> IA=127
        }

        [Fact]
        public void SwHelper_A_ReturnsAlphaChannel()
        {
            Assert.Equal(0xFF, SwHelper.A(0xFF000000));
            Assert.Equal(0x00, SwHelper.A(0x00FFFFFF));
            Assert.Equal(0x80, SwHelper.A(0x80123456));
        }

        [Fact]
        public void SwHelper_C1_C2_C3_ExtractChannels()
        {
            uint pixel = 0xAABBCCDD;
            Assert.Equal(0xBB, SwHelper.C1(pixel)); // bits 16-23
            Assert.Equal(0xCC, SwHelper.C2(pixel)); // bits 8-15
            Assert.Equal(0xDD, SwHelper.C3(pixel)); // bits 0-7
        }

        [Fact]
        public void SwHelper_PREMULTIPLY_WithFullAlpha_ApproximatesOriginal()
        {
            uint color = 0xFF804020;
            var result = SwHelper.PREMULTIPLY(color, 255);
            // PREMULTIPLY preserves alpha channel, but RGB channels may have slight rounding
            Assert.Equal((byte)0xFF, SwHelper.A(result)); // alpha preserved
            Assert.True(System.Math.Abs(SwHelper.C1(result) - 0x80) <= 1);
            Assert.True(System.Math.Abs(SwHelper.C2(result) - 0x40) <= 1);
            Assert.True(System.Math.Abs(SwHelper.C3(result) - 0x20) <= 1);
        }

        [Fact]
        public void SwHelper_PREMULTIPLY_WithZeroAlpha_PreservesAlpha()
        {
            // PREMULTIPLY preserves the original alpha channel, zeros out RGB
            uint color = 0xFFFFFFFF;
            var result = SwHelper.PREMULTIPLY(color, 0);
            Assert.Equal((byte)0xFF, SwHelper.A(result)); // alpha preserved
            Assert.Equal(0, SwHelper.C1(result)); // R zeroed
            Assert.Equal(0, SwHelper.C2(result)); // G zeroed
            Assert.Equal(0, SwHelper.C3(result)); // B zeroed
        }

        [Fact]
        public void SwHelper_MULTIPLY_BasicValues()
        {
            Assert.Equal(0, SwHelper.MULTIPLY(0, 255));
            Assert.Equal(0, SwHelper.MULTIPLY(255, 0));
            // 128 * 128: (128*128+255)>>8 = (16384+255)>>8 = 65
            var result = SwHelper.MULTIPLY(128, 128);
            Assert.True(result >= 63 && result <= 66);
        }

        [Fact]
        public void SwHelper_JOIN_PacksBytes()
        {
            var result = SwHelper.JOIN(0xAA, 0xBB, 0xCC, 0xDD);
            Assert.Equal(0xAABBCCDDu, result);
        }

        [Fact]
        public void SwHelper_INTERPOLATE_BetweenColors()
        {
            // When a=255, result should be close to source
            var s = 0xFF000000u;
            var d = 0x00FFFFFFu;
            var result = SwHelper.INTERPOLATE(s, d, 255);
            // Should be close to s
            Assert.True(SwHelper.A(result) >= 0xFE);
        }

        [Fact]
        public void SwHelper_INTERPOLATE8_Midpoint()
        {
            // Interpolate between 0 and 255 at 50% should be near 128
            var result = SwHelper.INTERPOLATE8(255, 0, 128);
            Assert.True(result >= 126 && result <= 130);
        }

        [Fact]
        public void SwHelper_TO_SWCOORD_And_TO_FLOAT_Roundtrip()
        {
            var val = 123.5f;
            var coord = SwHelper.TO_SWCOORD(val);
            var back = SwHelper.TO_FLOAT(coord);
            Assert.True(System.MathF.Abs(back - val) < 0.1f);
        }

        [Fact]
        public void SwHelper_BLEND_PRE_FullAlpha_ReturnsSrc()
        {
            uint src = 0xFF112233;
            uint dst = 0xFF445566;
            var result = SwHelper.BLEND_PRE(src, dst, 255);
            Assert.Equal(src, result);
        }

        [Fact]
        public void SwHelper_BLEND_UPRE_Unpremultiplies()
        {
            // Fully opaque pixel should have same channels after un-premultiply
            uint premultiplied = 0xFF804020;
            var unpre = SwHelper.BLEND_UPRE(premultiplied);
            Assert.Equal(255, unpre.a);
            Assert.Equal(0x80, unpre.r);
            Assert.Equal(0x40, unpre.g);
            Assert.Equal(0x20, unpre.b);
        }

        [Fact]
        public void SwHelper_rasterRGB2HSL_White()
        {
            SwHelper.rasterRGB2HSL(255, 255, 255, out var h, out var s, out var l);
            Assert.True(TvgMath.Equal(l, 1.0f));
            Assert.True(TvgMath.Equal(s, 0.0f));
        }

        [Fact]
        public void SwHelper_rasterRGB2HSL_Black()
        {
            SwHelper.rasterRGB2HSL(0, 0, 0, out var h, out var s, out var l);
            Assert.True(TvgMath.Equal(l, 0.0f));
        }

        [Fact]
        public void SwHelper_rasterRGB2HSL_Red()
        {
            SwHelper.rasterRGB2HSL(255, 0, 0, out var h, out var s, out var l);
            Assert.True(System.MathF.Abs(h) < 1.0f || System.MathF.Abs(h - 360.0f) < 1.0f); // hue=0 or 360
            Assert.True(TvgMath.Equal(s, 1.0f));
            Assert.True(TvgMath.Equal(l, 0.5f));
        }

        // =====================================================================
        //  SwMath (fixed-point math)
        // =====================================================================

        [Fact]
        public void SwMath_mathLength_UnitVector()
        {
            // Unit vector along x-axis in 16.16 fixed point
            var p = new SwPoint(1 << 16, 0);
            var len = SwMath.mathLength(p);
            // Should be close to 1.0 in 16.16 fixed point (65536)
            Assert.True(System.Math.Abs(len - 65536) < 512);
        }

        [Fact]
        public void SwMath_mathAtan_PositiveX()
        {
            // atan of point (1, 0) should be 0 degrees (= 0 in fixed)
            var p = new SwPoint(1 << 16, 0);
            var angle = SwMath.mathAtan(p);
            Assert.True(System.Math.Abs(angle) < 2048);
        }

        // =====================================================================
        //  Memory pool
        // =====================================================================

        [Fact]
        public void SwMemPool_InitAndTerm()
        {
            SwMemPool.mpoolInit(1);
            var mpool = SwMemPool.mpoolReq();
            Assert.NotNull(mpool);
            SwMemPool.mpoolTerm();
        }

        [Fact]
        public void SwMemPool_InitMultipleThreads()
        {
            SwMemPool.mpoolInit(4);
            var mpool = SwMemPool.mpoolReq();
            Assert.NotNull(mpool);
            SwMemPool.mpoolTerm();
        }

        // =====================================================================
        //  SwRenderer creation and configuration
        // =====================================================================

        [Fact]
        public void SwRenderer_Gen_ReturnsInstance()
        {
            var renderer = SwRenderer.Gen();
            Assert.NotNull(renderer);
        }

        [Fact]
        public void SwRenderer_Target_ManagedArray()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            Assert.True(renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));
        }

        [Fact]
        public void SwRenderer_Target_InvalidArgs_Fails()
        {
            var renderer = SwRenderer.Gen();
            // null array
            Assert.False(renderer.Target((uint[])null!, 100, 100, 100, ColorSpace.ARGB8888));
            // zero stride
            var buffer = new uint[100 * 100];
            Assert.False(renderer.Target(buffer, 0, 100, 100, ColorSpace.ARGB8888));
            // zero width
            Assert.False(renderer.Target(buffer, 100, 0, 100, ColorSpace.ARGB8888));
            // zero height
            Assert.False(renderer.Target(buffer, 100, 100, 0, ColorSpace.ARGB8888));
        }

        [Fact]
        public void SwRenderer_PreUpdate_WithTarget_ReturnsTrue()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);
            Assert.True(renderer.PreUpdate());
        }

        [Fact]
        public void SwRenderer_PreUpdate_WithoutTarget_ReturnsFalse()
        {
            var renderer = SwRenderer.Gen();
            Assert.False(renderer.PreUpdate());
        }

        [Fact]
        public void SwRenderer_PostUpdate_ReturnsTrue()
        {
            var renderer = SwRenderer.Gen();
            Assert.True(renderer.PostUpdate());
        }

        [Fact]
        public void SwRenderer_Clear_WithTarget()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);
            Assert.True(renderer.Clear());
        }

        [Fact]
        public void SwRenderer_Clear_WithoutTarget_Fails()
        {
            var renderer = SwRenderer.Gen();
            Assert.False(renderer.Clear());
        }

        [Fact]
        public void SwRenderer_Sync_ReturnsTrue()
        {
            var renderer = SwRenderer.Gen();
            Assert.True(renderer.Sync());
        }

        [Fact]
        public void SwRenderer_ColorSpace_AfterTarget()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);
            Assert.Equal(ColorSpace.ARGB8888, renderer.ColorSpaceValue());
        }

        [Fact]
        public void SwRenderer_MainSurface_AfterTarget_NotNull()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);
            Assert.NotNull(renderer.MainSurface());
        }

        [Fact]
        public void SwRenderer_MainSurface_BeforeTarget_IsNull()
        {
            var renderer = SwRenderer.Gen();
            Assert.Null(renderer.MainSurface());
        }

        [Fact]
        public void SwRenderer_Viewport_SetAndGet()
        {
            var renderer = SwRenderer.Gen();
            var vp = new RenderRegion(10, 20, 100, 200);
            renderer.Viewport(vp);
            Assert.Equal(vp, renderer.Viewport());
        }

        [Fact]
        public void SwRenderer_Blend_Normal()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);
            Assert.True(renderer.Blend(BlendMethod.Normal));
        }

        [Fact]
        public void SwRenderer_RefCounting()
        {
            var renderer = SwRenderer.Gen();
            Assert.Equal(1u, renderer.Ref());
            Assert.Equal(2u, renderer.Ref());
            Assert.Equal(1u, renderer.Unref());
            Assert.Equal(0u, renderer.Unref());
        }

        // =====================================================================
        //  SwRenderer prepare (shape)
        // =====================================================================

        [Fact]
        public void SwRenderer_Prepare_Shape_ReturnsRenderData()
        {
            var renderer = SwRenderer.Gen();
            var buffer = new uint[100 * 100];
            renderer.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);
            renderer.Viewport(new RenderRegion(0, 0, 100, 100));

            var rshape = new RenderShape();
            rshape.path.MoveTo(new Point(0, 0));
            rshape.path.LineTo(new Point(100, 0));
            rshape.path.LineTo(new Point(100, 100));
            rshape.path.LineTo(new Point(0, 100));
            rshape.path.Close();
            rshape.color = new RGBA(255, 0, 0, 255);

            var clips = new ValueList<object?>();
            var transform = TvgMath.Identity();

            renderer.PreUpdate();
            var rd = renderer.Prepare(rshape, null, transform, ref clips, 255, RenderUpdateFlag.All, false);
            renderer.PostUpdate();

            Assert.NotNull(rd);
        }

        // =====================================================================
        //  SwCanvas full pipeline
        // =====================================================================

        [Fact]
        public void SwCanvas_FullPipeline_DrawRedRect()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888));

            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 100, 100, 0, 0);
            shape.SetFill(255, 0, 0, 255);

            Assert.Equal(Result.Success, canvas.Add(shape));
            Assert.Equal(Result.Success, canvas.Update());
            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_FullPipeline_DrawWithClear()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[50 * 50];
            canvas.Target(buffer, 50, 50, 50, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 50, 50, 0, 0);
            shape.SetFill(0, 255, 0, 128);

            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_MultipleShapes()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[200 * 200];
            canvas.Target(buffer, 200, 200, 200, ColorSpace.ARGB8888);

            var shape1 = Shape.Gen();
            shape1.AppendRect(0, 0, 100, 100, 0, 0);
            shape1.SetFill(255, 0, 0, 255);
            canvas.Add(shape1);

            var shape2 = Shape.Gen();
            shape2.AppendRect(50, 50, 100, 100, 0, 0);
            shape2.SetFill(0, 0, 255, 128);
            canvas.Add(shape2);

            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_EmptyCanvas_DrawSucceeds()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            Assert.Equal(Result.Success, canvas.Draw());
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_UpdateTwice_SecondIsNoop()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendRect(10, 10, 80, 80, 0, 0);
            shape.SetFill(100, 100, 100, 255);
            canvas.Add(shape);

            Assert.Equal(Result.Success, canvas.Update());
            Assert.Equal(Result.Success, canvas.Update());
        }

        [Fact]
        public void SwCanvas_Target_ABGR8888()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            Assert.Equal(Result.Success, canvas.Target(buffer, 100, 100, 100, ColorSpace.ABGR8888));
        }

        [Fact]
        public void SwCanvas_Target_Grayscale_NotSupported()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            Assert.Equal(Result.NonSupport, canvas.Target(buffer, 100, 100, 100, ColorSpace.Grayscale8));
        }

        [Fact]
        public void SwCanvas_Target_Unknown_InvalidArgs()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            Assert.Equal(Result.InvalidArguments, canvas.Target(buffer, 100, 100, 100, ColorSpace.Unknown));
        }

        // =====================================================================
        //  Shape with stroke rendering
        // =====================================================================

        [Fact]
        public void SwCanvas_ShapeWithStroke()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendRect(10, 10, 80, 80, 0, 0);
            shape.SetFill(0, 0, 0, 0);
            shape.StrokeWidth(3.0f);
            shape.StrokeFill(255, 255, 0, 255);
            canvas.Add(shape);

            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_ShapeWithLinearGradient()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 100, 100, 0, 0);

            var grad = LinearGradient.Gen();
            grad.Linear(0, 0, 100, 100);
            grad.SetColorStops(new Fill.ColorStop[]
            {
                new Fill.ColorStop { offset = 0.0f, r = 255, g = 0, b = 0, a = 255 },
                new Fill.ColorStop { offset = 1.0f, r = 0, g = 0, b = 255, a = 255 }
            }, 2);
            shape.SetFill(grad);

            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_ShapeWithRadialGradient()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendCircle(50, 50, 40, 40);

            var grad = RadialGradient.Gen();
            grad.Radial(50, 50, 40, 50, 50, 0);
            grad.SetColorStops(new Fill.ColorStop[]
            {
                new Fill.ColorStop { offset = 0.0f, r = 255, g = 255, b = 0, a = 255 },
                new Fill.ColorStop { offset = 1.0f, r = 0, g = 0, b = 255, a = 255 }
            }, 2);
            shape.SetFill(grad);

            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_TranslucentShape()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 100, 100, 0, 0);
            shape.SetFill(255, 0, 0, 128);
            shape.Opacity(128);

            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_ShapeWithPathCommands()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.MoveTo(10, 10);
            shape.LineTo(90, 10);
            shape.LineTo(90, 90);
            shape.LineTo(10, 90);
            shape.Close();
            shape.SetFill(128, 128, 128, 255);

            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        // =====================================================================
        //  Scene rendering
        // =====================================================================

        [Fact]
        public void SwCanvas_SceneWithShapes()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var scene = Scene.Gen();
            var shape1 = Shape.Gen();
            shape1.AppendRect(0, 0, 50, 50, 0, 0);
            shape1.SetFill(255, 0, 0, 255);
            scene.Add(shape1);

            var shape2 = Shape.Gen();
            shape2.AppendRect(50, 50, 50, 50, 0, 0);
            shape2.SetFill(0, 255, 0, 255);
            scene.Add(shape2);

            canvas.Add(scene);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        [Fact]
        public void SwCanvas_NestedScenes()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var outerScene = Scene.Gen();
            var innerScene = Scene.Gen();

            var shape = Shape.Gen();
            shape.AppendRect(10, 10, 30, 30, 0, 0);
            shape.SetFill(255, 128, 0, 255);
            innerScene.Add(shape);

            outerScene.Add(innerScene);
            canvas.Add(outerScene);

            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        // =====================================================================
        //  Viewport
        // =====================================================================

        [Fact]
        public void SwCanvas_ViewportClipping()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            // After target, viewport should be intersected with surface
            Assert.Equal(Result.Success, canvas.Sync());
            Assert.Equal(Result.Success, canvas.Viewport(0, 0, 200, 200));
        }

        // =====================================================================
        //  Redraw cycle
        // =====================================================================

        [Fact]
        public void SwCanvas_RedrawCycle()
        {
            var canvas = SwCanvas.Gen();
            var buffer = new uint[100 * 100];
            canvas.Target(buffer, 100, 100, 100, ColorSpace.ARGB8888);

            var shape = Shape.Gen();
            shape.AppendRect(0, 0, 100, 100, 0, 0);
            shape.SetFill(255, 0, 0, 255);
            canvas.Add(shape);

            // First cycle
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());

            // After Sync, canvas is in Synced state.
            // To redraw, call Update() (which needs Painting or Damaged status)
            // or add/remove a shape to transition to Painting.
            // Remove and re-add the shape to trigger the painting state:
            canvas.Remove(shape);
            shape.SetFill(0, 255, 0, 255);
            canvas.Add(shape);
            Assert.Equal(Result.Success, canvas.Draw(clear: true));
            Assert.Equal(Result.Success, canvas.Sync());
        }

        // =====================================================================
        //  RenderColor
        // =====================================================================

        [Fact]
        public void RenderColor_Constructor()
        {
            var c = new RenderColor(10, 20, 30, 40);
            Assert.Equal(10, c.r);
            Assert.Equal(20, c.g);
            Assert.Equal(30, c.b);
            Assert.Equal(40, c.a);
        }

        // =====================================================================
        //  SwConstants
        // =====================================================================

        [Fact]
        public void SwConstants_Values()
        {
            Assert.Equal(0, SwConstants.SW_CURVE_TYPE_POINT);
            Assert.Equal(1, SwConstants.SW_CURVE_TYPE_CUBIC);
            Assert.Equal(180L << 16, SwConstants.SW_ANGLE_PI);
            Assert.Equal(360L << 16, SwConstants.SW_ANGLE_2PI);
            Assert.Equal(90L << 16, SwConstants.SW_ANGLE_PI2);
            Assert.Equal(1024, SwConstants.SW_COLOR_TABLE);
        }

        // =====================================================================
        //  RenderHelper (utility functions)
        // =====================================================================

        [Fact]
        public void RenderHelper_ChannelSize()
        {
            Assert.Equal(4, RenderHelper.ChannelSize(ColorSpace.ARGB8888));
            Assert.Equal(4, RenderHelper.ChannelSize(ColorSpace.ABGR8888));
            Assert.Equal(4, RenderHelper.ChannelSize(ColorSpace.ARGB8888S));
            Assert.Equal(4, RenderHelper.ChannelSize(ColorSpace.ABGR8888S));
            Assert.Equal(1, RenderHelper.ChannelSize(ColorSpace.Grayscale8));
            Assert.Equal(0, RenderHelper.ChannelSize(ColorSpace.Unknown));
        }

        [Fact]
        public void RenderHelper_MaskRegionMerging()
        {
            Assert.False(RenderHelper.MaskRegionMerging(MaskMethod.Alpha));
            Assert.False(RenderHelper.MaskRegionMerging(MaskMethod.InvAlpha));
            Assert.False(RenderHelper.MaskRegionMerging(MaskMethod.Subtract));
            Assert.False(RenderHelper.MaskRegionMerging(MaskMethod.Intersect));
            Assert.True(RenderHelper.MaskRegionMerging(MaskMethod.Add));
            Assert.True(RenderHelper.MaskRegionMerging(MaskMethod.Difference));
            Assert.True(RenderHelper.MaskRegionMerging(MaskMethod.Lighten));
            Assert.True(RenderHelper.MaskRegionMerging(MaskMethod.Darken));
        }

        // =====================================================================
        //  SwPoint
        // =====================================================================

        [Fact]
        public void SwPoint_Addition()
        {
            var a = new SwPoint(100, 200);
            var b = new SwPoint(10, 20);
            var c = a + b;
            Assert.Equal(110, c.x);
            Assert.Equal(220, c.y);
        }

        [Fact]
        public void SwPoint_Subtraction()
        {
            var a = new SwPoint(100, 200);
            var b = new SwPoint(10, 20);
            var c = a - b;
            Assert.Equal(90, c.x);
            Assert.Equal(180, c.y);
        }

        [Fact]
        public void SwPoint_Zero()
        {
            var p = new SwPoint(0, 0);
            Assert.True(p.Zero());

            var p2 = new SwPoint(100000, 100000);
            Assert.False(p2.Zero());
        }
    }
}
