// Ported from ThorVG/src/renderer/gl_engine/tvgGlProgram.h and tvgGlProgram.cpp
// Shader program linking and uniform management.

using System;
using System.Diagnostics;
using System.Text;

namespace ThorVG
{
    public unsafe class GlProgram : IDisposable
    {
        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        private static uint mCurrentProgram;
        private uint mProgramObj;

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        public GlProgram(string vertSrc, string fragSrc)
        {
            using var shader = new GlShader(vertSrc, fragSrc);

            // Create the program object
            uint progObj = GL.glCreateProgram();
            Debug.Assert(progObj != 0);

            GL.glAttachShader(progObj, shader.GetVertexShader());
            GL.glAttachShader(progObj, shader.GetFragmentShader());

            // Link the program
            GL.glLinkProgram(progObj);

            // Check the link status
            int linked;
            GL.glGetProgramiv(progObj, GL.GL_LINK_STATUS, &linked);

            if (linked == 0)
            {
                int infoLen = 0;
                GL.glGetProgramiv(progObj, GL.GL_INFO_LOG_LENGTH, &infoLen);
                if (infoLen > 0)
                {
                    byte[] infoLog = new byte[infoLen];
                    fixed (byte* pInfoLog = infoLog)
                    {
                        GL.glGetProgramInfoLog(progObj, infoLen, null, pInfoLog);
                        string logStr = Encoding.UTF8.GetString(infoLog, 0, infoLen);
                        TvgCommon.TVGERR("GL_ENGINE", "Error linking shader: {0}", logStr);
                    }
                }
                GL.glDeleteProgram(progObj);
                progObj = 0;
                Debug.Assert(false, "Shader linking failed");
            }
            mProgramObj = progObj;
        }

        public void Dispose()
        {
            if (mProgramObj != 0)
            {
                if (mCurrentProgram == mProgramObj) Unload();
                GL.glDeleteProgram(mProgramObj);
                mProgramObj = 0;
            }
            GC.SuppressFinalize(this);
        }

        ~GlProgram()
        {
            // Note: GL resources should be freed on the GL thread.
        }

        public void Load()
        {
            if (mCurrentProgram == mProgramObj) return;
            mCurrentProgram = mProgramObj;
            GL.glUseProgram(mProgramObj);
            GL.GL_CHECK();
        }

        public static void Unload()
        {
            mCurrentProgram = 0;
        }

        public int GetAttributeLocation(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var location = GL.glGetAttribLocation(mCurrentProgram, pName);
                GL.GL_CHECK();
                return location;
            }
        }

        public int GetUniformLocation(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var location = GL.glGetUniformLocation(mProgramObj, pName);
                GL.GL_CHECK();
                return location;
            }
        }

        public int GetUniformBlockIndex(ReadOnlySpan<byte> name)
        {
            fixed (byte* pName = name)
            {
                var index = (int)GL.glGetUniformBlockIndex(mProgramObj, pName);
                GL.GL_CHECK();
                return index;
            }
        }

        public uint GetProgramId()
        {
            return mProgramObj;
        }

        public void SetUniform1Value(int location, int count, int* values)
        {
            GL.glUniform1iv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform2Value(int location, int count, int* values)
        {
            GL.glUniform2iv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform3Value(int location, int count, int* values)
        {
            GL.glUniform3iv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform4Value(int location, int count, int* values)
        {
            GL.glUniform4iv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform1Value(int location, int count, float* values)
        {
            GL.glUniform1fv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform2Value(int location, int count, float* values)
        {
            GL.glUniform2fv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform3Value(int location, int count, float* values)
        {
            GL.glUniform3fv(location, count, values);
            GL.GL_CHECK();
        }

        public void SetUniform4Value(int location, int count, float* values)
        {
            GL.glUniform4fv(location, count, values);
            GL.GL_CHECK();
        }
    }
}
