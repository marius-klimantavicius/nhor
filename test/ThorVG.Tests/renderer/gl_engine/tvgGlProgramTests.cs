using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlProgramTests
    {
        // GlProgram requires a real GL context to link shaders.
        // We verify the type exists and its API surface at the reflection level.

        [Fact]
        public void GlProgram_Implements_IDisposable()
        {
            Assert.True(typeof(System.IDisposable).IsAssignableFrom(typeof(GlProgram)));
        }

        [Fact]
        public void GlProgram_HasLoadMethod()
        {
            var method = typeof(GlProgram).GetMethod("Load");
            Assert.NotNull(method);
        }

        [Fact]
        public void GlProgram_HasUnloadMethod()
        {
            var method = typeof(GlProgram).GetMethod("Unload");
            Assert.NotNull(method);
        }

        [Fact]
        public void GlProgram_HasGetProgramId()
        {
            var method = typeof(GlProgram).GetMethod("GetProgramId");
            Assert.NotNull(method);
            Assert.Equal(typeof(uint), method!.ReturnType);
        }

        [Fact]
        public void GlProgram_HasGetAttributeLocation()
        {
            var method = typeof(GlProgram).GetMethod("GetAttributeLocation");
            Assert.NotNull(method);
            Assert.Equal(typeof(int), method!.ReturnType);
        }

        [Fact]
        public void GlProgram_HasGetUniformLocation()
        {
            var method = typeof(GlProgram).GetMethod("GetUniformLocation");
            Assert.NotNull(method);
            Assert.Equal(typeof(int), method!.ReturnType);
        }

        [Fact]
        public void GlProgram_HasGetUniformBlockIndex()
        {
            var method = typeof(GlProgram).GetMethod("GetUniformBlockIndex");
            Assert.NotNull(method);
            Assert.Equal(typeof(int), method!.ReturnType);
        }

        [Fact]
        public void GlProgram_Unload_StaticMethod_DoesNotThrow()
        {
            // Unload is a static method that just resets a static field
            GlProgram.Unload();
        }

        [Fact]
        public void GlProgram_HasSetUniformOverloads()
        {
            // Verify all SetUniform overloads exist
            var methods = typeof(GlProgram).GetMethods();
            int setUniform1Count = 0;
            int setUniform2Count = 0;
            int setUniform3Count = 0;
            int setUniform4Count = 0;

            foreach (var m in methods)
            {
                switch (m.Name)
                {
                    case "SetUniform1Value": setUniform1Count++; break;
                    case "SetUniform2Value": setUniform2Count++; break;
                    case "SetUniform3Value": setUniform3Count++; break;
                    case "SetUniform4Value": setUniform4Count++; break;
                }
            }

            // Each has int* and float* overloads = 2
            Assert.Equal(2, setUniform1Count);
            Assert.Equal(2, setUniform2Count);
            Assert.Equal(2, setUniform3Count);
            Assert.Equal(2, setUniform4Count);
        }
    }
}
