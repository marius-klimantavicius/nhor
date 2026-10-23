// Ported from ThorVG/src/renderer/gl_engine/tvgGlProgram.h and tvgGlProgram.cpp
// Shader program linking and uniform management.

using System;
using System.Diagnostics;
using System.Text;

namespace ThorVG
{
    public enum GlShaderUniform : byte { Depth, ViewMatrix, SourceTexture, BlurTexture, DestinationTexture, MaskTexture, Texture, Count }
    public enum GlShaderUniformBlock : byte { Gaussian, Viewport, DropShadow, Params, BlendRegion, TransformInfo, GradientInfo, ColorInfo, Count }

    public unsafe class GlProgram : IDisposable
    {
        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        private static uint mCurrentProgram;
        private uint mProgramObj;
        private readonly int[] mUniformLocations = new int[(int)GlShaderUniform.Count];
        private readonly int[] mUniformBlockIndices = new int[(int)GlShaderUniformBlock.Count];
        private readonly uint[] mUniformBlockBindings = new uint[(int)GlShaderUniformBlock.Count];
        private readonly int[] mSamplerBindings = new int[(int)GlShaderUniform.Count];

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        public GlProgram(string vertSrc, string fragSrc)
        {
            System.Array.Fill(mUniformLocations, -2);
            System.Array.Fill(mUniformBlockIndices, -2);
            System.Array.Fill(mUniformBlockBindings, uint.MaxValue);
            System.Array.Fill(mSamplerBindings, -1);
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
                var location = GL.glGetAttribLocation(mProgramObj, pName);
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

        public int GetUniformLocation(GlShaderUniform uniform)
        {
            ref int location = ref mUniformLocations[(int)uniform];
            if (location != -2) return location;
            ReadOnlySpan<byte> name = uniform switch
            {
                GlShaderUniform.Depth => "uDepth\0"u8, GlShaderUniform.ViewMatrix => "uViewMatrix\0"u8,
                GlShaderUniform.SourceTexture => "uSrcTexture\0"u8, GlShaderUniform.BlurTexture => "uBlrTexture\0"u8,
                GlShaderUniform.DestinationTexture => "uDstTexture\0"u8, GlShaderUniform.MaskTexture => "uMaskTexture\0"u8,
                GlShaderUniform.Texture => "uTexture\0"u8, _ => throw new ArgumentOutOfRangeException(nameof(uniform))
            };
            return location = GetUniformLocation(name);
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

        public int GetUniformBlockIndex(GlShaderUniformBlock block)
        {
            ref int index = ref mUniformBlockIndices[(int)block];
            if (index != -2) return index;
            ReadOnlySpan<byte> name = block switch
            {
                GlShaderUniformBlock.Gaussian => "Gaussian\0"u8, GlShaderUniformBlock.Viewport => "Viewport\0"u8,
                GlShaderUniformBlock.DropShadow => "DropShadow\0"u8, GlShaderUniformBlock.Params => "Params\0"u8,
                GlShaderUniformBlock.BlendRegion => "BlendRegion\0"u8, GlShaderUniformBlock.TransformInfo => "TransformInfo\0"u8,
                GlShaderUniformBlock.GradientInfo => "GradientInfo\0"u8, GlShaderUniformBlock.ColorInfo => "ColorInfo\0"u8,
                _ => throw new ArgumentOutOfRangeException(nameof(block))
            };
            return index = GetUniformBlockIndex(name);
        }

        public bool SetUniformBlockBinding(GlShaderUniformBlock block, uint point)
            => SetUniformBlockBinding(GetUniformBlockIndex(block), point);

        public bool SetUniformBlockBinding(int blockIndex, uint point)
        {
            if (blockIndex < 0) return false;
            for (int i = 0; i < mUniformBlockIndices.Length; ++i)
            {
                if (mUniformBlockIndices[i] != blockIndex) continue;
                ref uint binding = ref mUniformBlockBindings[i];
                if (binding != uint.MaxValue) return binding == point;
                GL.glUniformBlockBinding(mProgramObj, (uint)blockIndex, point); binding = point; return true;
            }
            // Compatibility for custom uniform names queried by an existing caller.
            GL.glUniformBlockBinding(mProgramObj, (uint)blockIndex, point); return true;
        }

        public void SetSampler(GlShaderUniform uniform, int unit) => SetSampler(GetUniformLocation(uniform), unit);

        public void SetSampler(int location, int unit)
        {
            if (location < 0) return;
            for (int i = 0; i < mUniformLocations.Length; ++i)
            {
                if (mUniformLocations[i] != location) continue;
                if (mSamplerBindings[i] == unit) return;
                GL.glUniform1iv(location, 1, &unit); mSamplerBindings[i] = unit; return;
            }
            GL.glUniform1iv(location, 1, &unit);
        }

        public uint GetProgramId()
        {
            return mProgramObj;
        }

        public void SetUniform1Value(int location, int count, int* values)
        {
            for (int i = 0; i < mUniformLocations.Length; ++i)
                if (mUniformLocations[i] == location) mSamplerBindings[i] = -1;
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
