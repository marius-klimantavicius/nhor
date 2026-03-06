using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlShaderTests
    {
        // GlShader requires a real GL context to compile shaders.
        // We verify the type exists and implements IDisposable.

        [Fact]
        public void GlShader_Implements_IDisposable()
        {
            // Verify the type implements IDisposable at the type level
            Assert.True(typeof(System.IDisposable).IsAssignableFrom(typeof(GlShader)));
        }

        [Fact]
        public void GlShader_HasGetVertexShader()
        {
            var method = typeof(GlShader).GetMethod("GetVertexShader");
            Assert.NotNull(method);
            Assert.Equal(typeof(uint), method!.ReturnType);
        }

        [Fact]
        public void GlShader_HasGetFragmentShader()
        {
            var method = typeof(GlShader).GetMethod("GetFragmentShader");
            Assert.NotNull(method);
            Assert.Equal(typeof(uint), method!.ReturnType);
        }
    }
}
