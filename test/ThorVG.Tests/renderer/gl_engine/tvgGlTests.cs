using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlTests
    {
        // ---- GL Constants ------------------------------------------------

        [Fact]
        public void GL_Constants_Version10_Values()
        {
            Assert.Equal(0x00000100u, GL.GL_DEPTH_BUFFER_BIT);
            Assert.Equal(0x00000400u, GL.GL_STENCIL_BUFFER_BIT);
            Assert.Equal(0x00004000u, GL.GL_COLOR_BUFFER_BIT);
            Assert.Equal((byte)0, GL.GL_FALSE);
            Assert.Equal((byte)1, GL.GL_TRUE);
            Assert.Equal(0x0004u, GL.GL_TRIANGLES);
            Assert.Equal(0x0005u, GL.GL_TRIANGLE_STRIP);
            Assert.Equal(0x0006u, GL.GL_TRIANGLE_FAN);
            Assert.Equal(0x0BE2u, GL.GL_BLEND);
            Assert.Equal(0x0B90u, GL.GL_STENCIL_TEST);
            Assert.Equal(0x0C11u, GL.GL_SCISSOR_TEST);
            Assert.Equal(0x0DE1u, GL.GL_TEXTURE_2D);
            Assert.Equal(0x1401u, GL.GL_UNSIGNED_BYTE);
            Assert.Equal(0x1405u, GL.GL_UNSIGNED_INT);
            Assert.Equal(0x1406u, GL.GL_FLOAT);
            Assert.Equal(0x1908u, GL.GL_RGBA);
        }

        [Fact]
        public void GL_Constants_Version11_Values()
        {
            Assert.Equal(0x8058u, GL.GL_RGBA8);
            Assert.Equal(0x8074u, GL.GL_VERTEX_ARRAY);
        }

        [Fact]
        public void GL_Constants_Version12_Values()
        {
            Assert.Equal(0x812Fu, GL.GL_CLAMP_TO_EDGE);
        }

        [Fact]
        public void GL_Constants_Version13_Values()
        {
            Assert.Equal(0x84C0u, GL.GL_TEXTURE0);
            Assert.Equal(0x84C1u, GL.GL_TEXTURE1);
            Assert.Equal(0x84C2u, GL.GL_TEXTURE2);
            Assert.Equal(0x84C3u, GL.GL_TEXTURE3);
        }

        [Fact]
        public void GL_Constants_Version14_Values()
        {
            Assert.Equal(0x81A5u, GL.GL_DEPTH_COMPONENT16);
            Assert.Equal(0x81A6u, GL.GL_DEPTH_COMPONENT24);
            Assert.Equal(0x8507u, GL.GL_INCR_WRAP);
            Assert.Equal(0x8508u, GL.GL_DECR_WRAP);
            Assert.Equal(0x8006u, GL.GL_FUNC_ADD);
        }

        [Fact]
        public void GL_Constants_Version15_Values()
        {
            Assert.Equal(0x8892u, GL.GL_ARRAY_BUFFER);
            Assert.Equal(0x8893u, GL.GL_ELEMENT_ARRAY_BUFFER);
            Assert.Equal(0x88E4u, GL.GL_STATIC_DRAW);
        }

        [Fact]
        public void GL_Constants_Version20_Values()
        {
            Assert.Equal(0x8B30u, GL.GL_FRAGMENT_SHADER);
            Assert.Equal(0x8B31u, GL.GL_VERTEX_SHADER);
            Assert.Equal(0x8B81u, GL.GL_COMPILE_STATUS);
            Assert.Equal(0x8B82u, GL.GL_LINK_STATUS);
            Assert.Equal(0x8B84u, GL.GL_INFO_LOG_LENGTH);
        }

        [Fact]
        public void GL_Constants_Version30_Values()
        {
            Assert.Equal(0x821Bu, GL.GL_MAJOR_VERSION);
            Assert.Equal(0x821Cu, GL.GL_MINOR_VERSION);
            Assert.Equal(0x88F0u, GL.GL_DEPTH24_STENCIL8);
            Assert.Equal(0x8CA6u, GL.GL_FRAMEBUFFER_BINDING);
            Assert.Equal(0x8CE0u, GL.GL_COLOR_ATTACHMENT0);
            Assert.Equal(0x8D40u, GL.GL_FRAMEBUFFER);
            Assert.Equal(0x8D41u, GL.GL_RENDERBUFFER);
        }

        [Fact]
        public void GL_Constants_Version31_Values()
        {
            Assert.Equal(0x8A11u, GL.GL_UNIFORM_BUFFER);
            Assert.Equal(0x8A34u, GL.GL_UNIFORM_BUFFER_OFFSET_ALIGNMENT);
            Assert.Equal(0xFFFFFFFFu, GL.GL_INVALID_INDEX);
        }

        [Fact]
        public void GL_RequiredVersion()
        {
            Assert.Equal(3, GL.TVG_REQUIRE_GL_MAJOR_VER);
            Assert.Equal(3, GL.TVG_REQUIRE_GL_MINOR_VER);
        }

        // ---- GL function delegates start null before init ----------------

        [Fact]
        public unsafe void GL_FunctionPointersAreNullBeforeInit()
        {
            // Before glInit, function pointers should be null (zero) since they
            // are unmanaged function pointer fields that default-initialize to null.
            // We cannot call glInit in unit tests (no GL context), but we can
            // verify that the fields are accessible and null before loading.
            Assert.True(GL.glClear == null);
            Assert.True(GL.glEnable == null);
            Assert.True(GL.glCreateShader == null);
            Assert.True(GL.glGenFramebuffers == null);
        }

        [Fact]
        public void GL_glTerm_DoesNotThrow()
        {
            // glTerm should not throw even if glInit was never called
            bool result = GL.glTerm();
            Assert.True(result);
        }

        // ---- Stencil / blend constants -----------------------------------

        [Fact]
        public void GL_StencilOp_Constants()
        {
            Assert.Equal(0x1E00u, GL.GL_KEEP);
            Assert.Equal(0x1E01u, GL.GL_REPLACE);
            Assert.Equal(0x1E02u, GL.GL_INCR);
            Assert.Equal(0x1E03u, GL.GL_DECR);
            Assert.Equal(0x150Au, GL.GL_INVERT);
        }

        [Fact]
        public void GL_BlendFunc_Constants()
        {
            Assert.Equal(0u, GL.GL_ZERO);
            Assert.Equal(1u, GL.GL_ONE);
            Assert.Equal(0x0302u, GL.GL_SRC_ALPHA);
            Assert.Equal(0x0303u, GL.GL_ONE_MINUS_SRC_ALPHA);
            Assert.Equal(0x0304u, GL.GL_DST_ALPHA);
            Assert.Equal(0x0305u, GL.GL_ONE_MINUS_DST_ALPHA);
        }

        [Fact]
        public void GL_Error_Constants()
        {
            Assert.Equal(0u, GL.GL_NO_ERROR);
            Assert.Equal(0x0500u, GL.GL_INVALID_ENUM);
            Assert.Equal(0x0501u, GL.GL_INVALID_VALUE);
            Assert.Equal(0x0502u, GL.GL_INVALID_OPERATION);
            Assert.Equal(0x0505u, GL.GL_OUT_OF_MEMORY);
            Assert.Equal(0x0506u, GL.GL_INVALID_FRAMEBUFFER_OPERATION);
        }

        [Fact]
        public void GL_TextureConsecutive()
        {
            // TEXTURE0 through TEXTURE3 should be consecutive
            Assert.Equal(GL.GL_TEXTURE0 + 1, GL.GL_TEXTURE1);
            Assert.Equal(GL.GL_TEXTURE0 + 2, GL.GL_TEXTURE2);
            Assert.Equal(GL.GL_TEXTURE0 + 3, GL.GL_TEXTURE3);
        }
    }
}
