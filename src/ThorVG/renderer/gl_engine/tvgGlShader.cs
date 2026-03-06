// Ported from ThorVG/src/renderer/gl_engine/tvgGlShader.h and tvgGlShader.cpp
// Shader compilation (glCreateShader, glCompileShader, etc.)

using System;
using System.Text;

namespace ThorVG
{
    public unsafe class GlShader : IDisposable
    {
        private uint mVtShader;
        private uint mFrShader;

        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        private uint CompileShader(uint type, string shaderSrc)
        {
            // Create the shader object
            uint shader = GL.glCreateShader(type);

            /**
             * [0] shader version string
             * [1] precision declaration
             * [2] shader source
             */
            string shaderVersion = "#version 330 core\n";
            string shaderPrecision = "precision highp float;\n precision highp int;\n";

            byte[] versionBytes = Encoding.UTF8.GetBytes(shaderVersion + '\0');
            byte[] precisionBytes = Encoding.UTF8.GetBytes(shaderPrecision + '\0');
            byte[] sourceBytes = Encoding.UTF8.GetBytes(shaderSrc + '\0');

            fixed (byte* pVersion = versionBytes)
            fixed (byte* pPrecision = precisionBytes)
            fixed (byte* pSource = sourceBytes)
            {
                byte*[] shaderPack = new byte*[3];
                shaderPack[0] = pVersion;
                shaderPack[1] = pPrecision;
                shaderPack[2] = pSource;

                fixed (byte** ppShaderPack = shaderPack)
                {
                    // Load the shader source
                    GL.glShaderSource(shader, 3, ppShaderPack, null);
                }
            }

            // Compile the shader
            GL.glCompileShader(shader);

            // Check the compile status
            int compiled;
            GL.glGetShaderiv(shader, GL.GL_COMPILE_STATUS, &compiled);

            if (compiled == 0)
            {
                int infoLen = 0;
                GL.glGetShaderiv(shader, GL.GL_INFO_LOG_LENGTH, &infoLen);

                if (infoLen > 0)
                {
                    byte[] infoLog = new byte[infoLen];
                    fixed (byte* pInfoLog = infoLog)
                    {
                        GL.glGetShaderInfoLog(shader, infoLen, null, pInfoLog);
                        string logStr = Encoding.UTF8.GetString(infoLog, 0, infoLen);
                        TvgCommon.TVGERR("GL_ENGINE", "Error compiling shader: {0}", logStr);
                    }
                }
                GL.glDeleteShader(shader);
            }

            return shader;
        }

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        public GlShader(string vertSrc, string fragSrc)
        {
            mVtShader = CompileShader(GL.GL_VERTEX_SHADER, vertSrc);
            mFrShader = CompileShader(GL.GL_FRAGMENT_SHADER, fragSrc);
        }

        public void Dispose()
        {
            if (mVtShader != 0)
            {
                GL.glDeleteShader(mVtShader);
                mVtShader = 0;
            }
            if (mFrShader != 0)
            {
                GL.glDeleteShader(mFrShader);
                mFrShader = 0;
            }
            GC.SuppressFinalize(this);
        }

        ~GlShader()
        {
            // Note: GL resources should be freed on the GL thread.
            // The destructor is a safety net only.
        }

        public uint GetVertexShader()
        {
            return mVtShader;
        }

        public uint GetFragmentShader()
        {
            return mFrShader;
        }
    }
}
