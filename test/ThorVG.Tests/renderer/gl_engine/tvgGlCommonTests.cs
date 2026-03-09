using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlCommonTests
    {
        // ---- GlConstants -------------------------------------------

        [Fact]
        public void GlConstants_Values()
        {
            Assert.Equal(1.0f, GlConstants.MIN_GL_STROKE_WIDTH);
            Assert.Equal(0.25f, GlConstants.MIN_GL_STROKE_ALPHA);
            Assert.Equal(12u, GlConstants.GL_MAT3_STD140_SIZE);
            Assert.Equal(48u, GlConstants.GL_MAT3_STD140_BYTES); // 12 * 4
            Assert.Equal(16, GlConstants.MAX_GRADIENT_STOPS);
        }

        // ---- GlMatrixHelper ---------------------------------------------

        [Fact]
        public void GlMatrixHelper_GetMatrix3_Identity()
        {
            var identity = new Matrix
            {
                e11 = 1, e12 = 0, e13 = 0,
                e21 = 0, e22 = 1, e23 = 0,
                e31 = 0, e32 = 0, e33 = 1
            };

            var result = new float[9];
            GlMatrixHelper.GetMatrix3(in identity, result);

            // Column-major order
            // Col 0: (e11, e21, e31)
            Assert.Equal(1.0f, result[0]);
            Assert.Equal(0.0f, result[1]);
            Assert.Equal(0.0f, result[2]);
            // Col 1: (e12, e22, e32)
            Assert.Equal(0.0f, result[3]);
            Assert.Equal(1.0f, result[4]);
            Assert.Equal(0.0f, result[5]);
            // Col 2: (e13, e23, e33)
            Assert.Equal(0.0f, result[6]);
            Assert.Equal(0.0f, result[7]);
            Assert.Equal(1.0f, result[8]);
        }

        [Fact]
        public void GlMatrixHelper_GetMatrix3_NonIdentity()
        {
            var mat = new Matrix
            {
                e11 = 2, e12 = 3, e13 = 4,
                e21 = 5, e22 = 6, e23 = 7,
                e31 = 8, e32 = 9, e33 = 10
            };

            var result = new float[9];
            GlMatrixHelper.GetMatrix3(in mat, result);

            // Column major: col0 = (e11,e21,e31), col1 = (e12,e22,e32), col2 = (e13,e23,e33)
            Assert.Equal(2.0f, result[0]); // e11
            Assert.Equal(5.0f, result[1]); // e21
            Assert.Equal(8.0f, result[2]); // e31
            Assert.Equal(3.0f, result[3]); // e12
            Assert.Equal(6.0f, result[4]); // e22
            Assert.Equal(9.0f, result[5]); // e32
            Assert.Equal(4.0f, result[6]); // e13
            Assert.Equal(7.0f, result[7]); // e23
            Assert.Equal(10.0f, result[8]); // e33
        }

        [Fact]
        public void GlMatrixHelper_GetMatrix3Std140_Identity()
        {
            var identity = new Matrix
            {
                e11 = 1, e12 = 0, e13 = 0,
                e21 = 0, e22 = 1, e23 = 0,
                e31 = 0, e32 = 0, e33 = 1
            };

            var result = new float[12];
            GlMatrixHelper.GetMatrix3Std140(in identity, result);

            // std140 packs each column into vec4 stride (4 floats per column, last is padding)
            // Col 0: [0]=e11, [1]=e21, [2]=e31, [3]=0
            Assert.Equal(1.0f, result[0]);
            Assert.Equal(0.0f, result[1]);
            Assert.Equal(0.0f, result[2]);
            Assert.Equal(0.0f, result[3]); // padding

            // Col 1: [4]=e12, [5]=e22, [6]=e32, [7]=0
            Assert.Equal(0.0f, result[4]);
            Assert.Equal(1.0f, result[5]);
            Assert.Equal(0.0f, result[6]);
            Assert.Equal(0.0f, result[7]); // padding

            // Col 2: [8]=e13, [9]=e23, [10]=e33, [11]=0
            Assert.Equal(0.0f, result[8]);
            Assert.Equal(0.0f, result[9]);
            Assert.Equal(1.0f, result[10]);
            Assert.Equal(0.0f, result[11]); // padding
        }

        [Fact]
        public void GlMatrixHelper_GetMatrix3Std140_PaddingIsZero()
        {
            var mat = new Matrix
            {
                e11 = 99, e12 = 88, e13 = 77,
                e21 = 66, e22 = 55, e23 = 44,
                e31 = 33, e32 = 22, e33 = 11
            };

            var result = new float[12];
            GlMatrixHelper.GetMatrix3Std140(in mat, result);

            // Padding elements should always be 0
            Assert.Equal(0.0f, result[3]);
            Assert.Equal(0.0f, result[7]);
            Assert.Equal(0.0f, result[11]);
        }

        // ---- GlStencilMode enum ------------------------------------------

        [Fact]
        public void GlStencilMode_Values()
        {
            Assert.Equal(0, (int)GlStencilMode.None);
            Assert.Equal(1, (int)GlStencilMode.FillNonZero);
            Assert.Equal(2, (int)GlStencilMode.FillEvenOdd);
            Assert.Equal(3, (int)GlStencilMode.Stroke);
        }

        // ---- GlGeometryBuffer -------------------------------------------

        [Fact]
        public void GlGeometryBuffer_Clear()
        {
            var buf = new GlGeometryBuffer();
            buf.vertex.Push(1.0f);
            buf.vertex.Push(2.0f);
            buf.index.Push(0u);

            Assert.Equal(2u, buf.vertex.count);
            Assert.Equal(1u, buf.index.count);

            buf.Clear();
            Assert.Equal(0u, buf.vertex.count);
            Assert.Equal(0u, buf.index.count);

            buf.vertex.Dispose();
            buf.index.Dispose();
        }

        // ---- GlGeometry -------------------------------------------------

        [Fact]
        public void GlGeometry_SetMatrix_InverseMatrixChanges()
        {
            var geo = new GlGeometry();
            var mat = new Matrix { e11 = 2, e22 = 3, e33 = 1 };
            geo.SetMatrix(in mat);
            // After setting matrix, InverseMatrix should return valid inverse
            var inv = geo.InverseMatrix();
            Assert.NotEqual(default, inv);
        }

        [Fact]
        public void GlGeometry_InverseMatrix_CachesResult()
        {
            var geo = new GlGeometry();
            var mat = new Matrix { e11 = 1, e12 = 0, e13 = 0, e21 = 0, e22 = 1, e23 = 0, e31 = 0, e32 = 0, e33 = 1 };
            geo.SetMatrix(in mat);

            var inv1 = geo.InverseMatrix();
            var inv2 = geo.InverseMatrix();
            // Both calls should return the same result
            Assert.Equal(inv1.e11, inv2.e11);
            Assert.Equal(inv1.e22, inv2.e22);
        }

        [Fact]
        public void GlGeometry_DefaultFillRule()
        {
            var geo = new GlGeometry();
            Assert.Equal(FillRule.NonZero, geo.fillRule);
        }

        [Fact]
        public void GlGeometry_StubMethods_DoNotThrow()
        {
            var geo = new GlGeometry();
            var rshape = new RenderShape();

            // Prepare should not throw
            geo.Prepare(rshape);
            // Default fillRule is NonZero and convex is false, so stencil mode is FillNonZero
            Assert.Equal(GlStencilMode.FillNonZero, geo.GetStencilMode(RenderUpdateFlag.None));

            var bounds = geo.GetBounds();
            Assert.True(bounds.Invalid());
        }

        // ---- GlShape ----------------------------------------------------

        [Fact]
        public void GlShape_DefaultValues()
        {
            var shape = new GlShape();
            Assert.Null(shape.rshape);
            Assert.Equal(0.0f, shape.viewWd);
            Assert.Equal(0.0f, shape.viewHt);
            Assert.Equal(0u, shape.opacity);
            Assert.Equal(0u, shape.texId);
            Assert.Equal(0u, shape.texFlipY);
            Assert.Equal(ColorSpace.ABGR8888, shape.texColorSpace);
            Assert.NotNull(shape.geometry);
            Assert.True(shape.clips.IsEmpty);
            Assert.False(shape.validFill);
            Assert.False(shape.validStroke);
        }

        // ---- GlLinearGradientBlock --------------------------------------

        [Fact]
        public unsafe void GlLinearGradientBlock_CanSetValues()
        {
            var block = default(GlLinearGradientBlock);
            block.nStops[0] = 4.0f;
            block.startPos[0] = 10.0f;
            block.startPos[1] = 20.0f;
            block.stopPos[0] = 100.0f;

            Assert.Equal(4.0f, block.nStops[0]);
            Assert.Equal(10.0f, block.startPos[0]);
            Assert.Equal(20.0f, block.startPos[1]);
            Assert.Equal(100.0f, block.stopPos[0]);
        }

        [Fact]
        public unsafe void GlLinearGradientBlock_StopPointsCapacity()
        {
            var block = default(GlLinearGradientBlock);
            // Should be able to set all MAX_GRADIENT_STOPS points
            for (int i = 0; i < GlConstants.MAX_GRADIENT_STOPS; i++)
            {
                block.stopPoints[i] = i * 0.1f;
            }
            Assert.Equal(0.0f, block.stopPoints[0]);
            Assert.Equal(1.5f, block.stopPoints[15]);
        }

        [Fact]
        public unsafe void GlLinearGradientBlock_StopColorsCapacity()
        {
            var block = default(GlLinearGradientBlock);
            // 4 components per stop * MAX_GRADIENT_STOPS
            int totalColors = 4 * GlConstants.MAX_GRADIENT_STOPS;
            for (int i = 0; i < totalColors; i++)
            {
                block.stopColors[i] = (float)i;
            }
            Assert.Equal(0.0f, block.stopColors[0]);
            Assert.Equal(63.0f, block.stopColors[63]);
        }

        // ---- GlRadialGradientBlock --------------------------------------

        [Fact]
        public unsafe void GlRadialGradientBlock_CanSetValues()
        {
            var block = default(GlRadialGradientBlock);
            block.nStops[0] = 8.0f;
            block.centerPos[0] = 50.0f;
            block.centerPos[1] = 60.0f;
            block.radius[0] = 100.0f;
            block.radius[1] = 200.0f;

            Assert.Equal(8.0f, block.nStops[0]);
            Assert.Equal(50.0f, block.centerPos[0]);
            Assert.Equal(60.0f, block.centerPos[1]);
            Assert.Equal(100.0f, block.radius[0]);
            Assert.Equal(200.0f, block.radius[1]);
        }

        // ---- GlCompositor -----------------------------------------------

        [Fact]
        public void GlCompositor_ConstructorSetsValues()
        {
            var box = new RenderRegion(10, 20, 110, 120);
            var comp = new GlCompositor(in box, CompositionFlag.Opacity);

            Assert.Equal(10, comp.bbox.min.x);
            Assert.Equal(20, comp.bbox.min.y);
            Assert.Equal(110, comp.bbox.max.x);
            Assert.Equal(120, comp.bbox.max.y);
            Assert.Equal(CompositionFlag.Opacity, comp.flags);
        }

        [Fact]
        public void GlCompositor_DefaultBlendMethod()
        {
            var box = new RenderRegion(0, 0, 1, 1);
            var comp = new GlCompositor(in box, CompositionFlag.Invalid);
            Assert.Equal(BlendMethod.Normal, comp.blendMethod);
        }

        [Fact]
        public void GlCompositor_IsRenderCompositor()
        {
            var box = new RenderRegion(0, 0, 1, 1);
            var comp = new GlCompositor(in box, CompositionFlag.Invalid);
            Assert.IsAssignableFrom<RenderCompositor>(comp);
        }
    }
}
