// Ported from pinned ThorVG/src/renderer/gpu_engine/gl/tvgGlStateCache.{h,cpp}.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    public unsafe sealed class GlStateCache
    {
        private struct Cached<T> where T : struct, IEquatable<T>
        {
            public T value;
            public bool valid;
            public bool Matches(T candidate)
            {
                if (!valid) return false;
                if (typeof(T) == typeof(double)) return Unsafe.As<T, double>(ref value) == Unsafe.As<T, double>(ref candidate);
                return value.Equals(candidate);
            }
            public void Set(T candidate) { value = candidate; valid = true; }
            public void Invalidate() => valid = false;
        }
        private struct Float4State : IEquatable<Float4State>
        {
            public float x, y, z, w;
            public Float4State(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
            public bool Equals(Float4State other) => x == other.x && y == other.y && z == other.z && w == other.w;
        }
        private sealed class Binding { public uint target; public Cached<uint> value; }
        private sealed class Capability { public uint capability; public Cached<bool> enabled; }
        private sealed class VertexAttribute
        {
            public Cached<bool> enabled;
            public Cached<(int, uint, byte, int, nuint, uint)> pointer;
            public Cached<Float4State> constant;
            public bool known, used;
        }

        private Cached<uint> program, vertexArray, elementArrayBuffer, activeTextureUnit, readFramebuffer, drawFramebuffer, depthFunction;
        private Cached<byte> depthWriteEnabled;
        private Cached<(int, int, int, int)> viewportState, scissorState;
        private Cached<(uint, uint)> blendFunction;
        private Cached<(byte, byte, byte, byte)> colorWriteMask;
        private Cached<(uint, int, uint)> frontStencilFunction, backStencilFunction;
        private Cached<(uint, uint, uint)> frontStencilOperation, backStencilOperation;
        private Cached<Float4State> clearColorState;
        private Cached<int> clearStencilValue;
        private Cached<double> clearDepthValue;
        private readonly List<Binding> buffers = new();
        private readonly List<Binding> textures = new();
        private readonly List<Capability> capabilities = new();
        private readonly List<VertexAttribute> attributes = new();

        private static Binding FindBinding(List<Binding> bindings, uint target)
        {
            foreach (var binding in bindings) if (binding.target == target) return binding;
            var result = new Binding { target = target }; bindings.Add(result); return result;
        }
        private VertexAttribute Attribute(uint index)
        {
            while (attributes.Count <= index) attributes.Add(new VertexAttribute());
            var result = attributes[(int)index]; result.known = true; return result;
        }
        private void InvalidateVertexArrayState()
        {
            elementArrayBuffer.Invalidate();
            foreach (var a in attributes) { a.enabled.Invalidate(); a.pointer.Invalidate(); a.used = false; }
        }
        public void Invalidate()
        {
            program.Invalidate(); vertexArray.Invalidate(); InvalidateVertexArrayState();
            foreach (var a in attributes) a.constant.Invalidate();
            foreach (var b in buffers) b.value.Invalidate();
            activeTextureUnit.Invalidate(); foreach (var t in textures) t.value.Invalidate();
            viewportState.Invalidate(); scissorState.Invalidate(); readFramebuffer.Invalidate(); drawFramebuffer.Invalidate();
            foreach (var c in capabilities) c.enabled.Invalidate();
            blendFunction.Invalidate(); depthFunction.Invalidate(); depthWriteEnabled.Invalidate(); colorWriteMask.Invalidate();
            frontStencilFunction.Invalidate(); backStencilFunction.Invalidate(); frontStencilOperation.Invalidate(); backStencilOperation.Invalidate();
            clearColorState.Invalidate(); clearStencilValue.Invalidate(); clearDepthValue.Invalidate();
        }
        public void UseProgram(uint value)
        {
            if (program.Matches(value)) return;
            GlProgram.Unload();
            GL.glUseProgram(value); program.Set(value);
        }
        public void BindVertexArray(uint value)
        {
            if (vertexArray.Matches(value)) return;
            GL.glBindVertexArray(value); vertexArray.Set(value); InvalidateVertexArrayState();
        }
        public void BindBuffer(uint target, uint buffer)
        {
            if (target == GL.GL_ELEMENT_ARRAY_BUFFER)
            {
                if (elementArrayBuffer.Matches(buffer)) return;
                GL.glBindBuffer(target, buffer); elementArrayBuffer.Set(buffer); return;
            }
            var binding = FindBinding(buffers, target);
            if (binding.value.Matches(buffer)) return;
            GL.glBindBuffer(target, buffer); binding.value.Set(buffer);
        }
        public void BindTexture2D(uint unit, uint texture)
        {
            if (!activeTextureUnit.Matches(unit)) { GL.glActiveTexture(unit); activeTextureUnit.Set(unit); }
            var binding = FindBinding(textures, unit);
            if (binding.value.Matches(texture)) return;
            GL.glBindTexture(GL.GL_TEXTURE_2D, texture); binding.value.Set(texture);
        }
        public void BeginVertexLayout() { foreach (var a in attributes) a.used = false; }
        private static void SetEnabled(uint index, VertexAttribute attribute, bool enabled)
        {
            if (attribute.enabled.Matches(enabled)) return;
            if (enabled) GL.glEnableVertexAttribArray(index); else GL.glDisableVertexAttribArray(index);
            attribute.enabled.Set(enabled);
        }
        public void SetVertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, nuint offset, uint sourceBuffer)
        {
            BindBuffer(GL.GL_ARRAY_BUFFER, sourceBuffer);
            var a = Attribute(index); a.used = true; a.constant.Invalidate(); SetEnabled(index, a, true);
            var pointer = (size, type, normalized, stride, offset, sourceBuffer);
            if (a.pointer.Matches(pointer)) return;
            GL.glVertexAttribPointer(index, size, type, normalized, stride, (void*)offset); a.pointer.Set(pointer);
        }
        public void SetVertexAttrib4f(uint index, float x, float y, float z, float w)
        {
            var a = Attribute(index); a.used = true; SetEnabled(index, a, false);
            var value = new Float4State(x, y, z, w);
            if (a.constant.Matches(value)) return;
            GL.glVertexAttrib4f(index, x, y, z, w); a.constant.Set(value);
        }
        public void EndVertexLayout()
        {
            for (int i = 0; i < attributes.Count; ++i)
            {
                var a = attributes[i]; if (a.known && !a.used) SetEnabled((uint)i, a, false);
            }
        }
        public void Viewport(int x, int y, int w, int h)
        {
            var value = (x, y, w, h); if (viewportState.Matches(value)) return;
            GL.glViewport(x, y, w, h); viewportState.Set(value);
        }
        public void Scissor(int x, int y, int w, int h)
        {
            var value = (x, y, w, h); if (scissorState.Matches(value)) return;
            GL.glScissor(x, y, w, h); scissorState.Set(value);
        }
        public void BindFramebuffer(uint target, uint framebuffer)
        {
            if (target == GL.GL_FRAMEBUFFER)
            {
                if (readFramebuffer.Matches(framebuffer) && drawFramebuffer.Matches(framebuffer)) return;
                GL.glBindFramebuffer(target, framebuffer); readFramebuffer.Set(framebuffer); drawFramebuffer.Set(framebuffer); return;
            }
            if (target == GL.GL_READ_FRAMEBUFFER)
            {
                if (readFramebuffer.Matches(framebuffer)) return;
                GL.glBindFramebuffer(target, framebuffer); readFramebuffer.Set(framebuffer); return;
            }
            if (target == GL.GL_DRAW_FRAMEBUFFER)
            {
                if (drawFramebuffer.Matches(framebuffer)) return;
                GL.glBindFramebuffer(target, framebuffer); drawFramebuffer.Set(framebuffer); return;
            }
            GL.glBindFramebuffer(target, framebuffer);
        }
        private void SetCapability(uint value, bool enabled)
        {
            Capability? capability = null;
            foreach (var c in capabilities) if (c.capability == value) { capability = c; break; }
            if (capability == null) { capability = new Capability { capability = value }; capabilities.Add(capability); }
            if (capability.enabled.Matches(enabled)) return;
            if (enabled) GL.glEnable(value); else GL.glDisable(value);
            capability.enabled.Set(enabled);
        }
        public void Enable(uint capability) => SetCapability(capability, true);
        public void Disable(uint capability) => SetCapability(capability, false);
        public void BlendFunc(uint source, uint destination)
        {
            var value = (source, destination); if (blendFunction.Matches(value)) return;
            GL.glBlendFunc(source, destination); blendFunction.Set(value);
        }
        public void DepthFunc(uint function)
        {
            if (depthFunction.Matches(function)) return;
            GL.glDepthFunc(function); depthFunction.Set(function);
        }
        public void DepthMask(byte enabled)
        {
            if (depthWriteEnabled.Matches(enabled)) return;
            GL.glDepthMask(enabled); depthWriteEnabled.Set(enabled);
        }
        public void ColorMask(byte r, byte g, byte b, byte a)
        {
            var value = (r, g, b, a); if (colorWriteMask.Matches(value)) return;
            GL.glColorMask(r, g, b, a); colorWriteMask.Set(value);
        }
        public void StencilFunc(uint function, int reference, uint mask)
        {
            var value = (function, reference, mask);
            if (frontStencilFunction.Matches(value) && backStencilFunction.Matches(value)) return;
            GL.glStencilFunc(function, reference, mask); frontStencilFunction.Set(value); backStencilFunction.Set(value);
        }
        public void StencilFuncSeparate(uint face, uint function, int reference, uint mask)
        {
            var value = (function, reference, mask);
            if (face == GL.GL_FRONT) { if (frontStencilFunction.Matches(value)) return; }
            else if (face == GL.GL_BACK) { if (backStencilFunction.Matches(value)) return; }
            else if (face == GL.GL_FRONT_AND_BACK && frontStencilFunction.Matches(value) && backStencilFunction.Matches(value)) return;
            GL.glStencilFuncSeparate(face, function, reference, mask);
            if (face == GL.GL_FRONT || face == GL.GL_FRONT_AND_BACK) frontStencilFunction.Set(value);
            if (face == GL.GL_BACK || face == GL.GL_FRONT_AND_BACK) backStencilFunction.Set(value);
        }
        public void StencilOp(uint fail, uint depthFail, uint pass)
        {
            var value = (fail, depthFail, pass);
            if (frontStencilOperation.Matches(value) && backStencilOperation.Matches(value)) return;
            GL.glStencilOp(fail, depthFail, pass); frontStencilOperation.Set(value); backStencilOperation.Set(value);
        }
        public void StencilOpSeparate(uint face, uint fail, uint depthFail, uint pass)
        {
            var value = (fail, depthFail, pass);
            if (face == GL.GL_FRONT) { if (frontStencilOperation.Matches(value)) return; }
            else if (face == GL.GL_BACK) { if (backStencilOperation.Matches(value)) return; }
            else if (face == GL.GL_FRONT_AND_BACK && frontStencilOperation.Matches(value) && backStencilOperation.Matches(value)) return;
            GL.glStencilOpSeparate(face, fail, depthFail, pass);
            if (face == GL.GL_FRONT || face == GL.GL_FRONT_AND_BACK) frontStencilOperation.Set(value);
            if (face == GL.GL_BACK || face == GL.GL_FRONT_AND_BACK) backStencilOperation.Set(value);
        }
        public void ClearColor(float r, float g, float b, float a)
        {
            var value = new Float4State(r, g, b, a); if (clearColorState.Matches(value)) return;
            GL.glClearColor(r, g, b, a); clearColorState.Set(value);
        }
        public void ClearStencil(int value)
        {
            if (clearStencilValue.Matches(value)) return;
            GL.glClearStencil(value); clearStencilValue.Set(value);
        }
        public void ClearDepth(double value)
        {
            if (clearDepthValue.Matches(value)) return;
#if THORVG_GL_TARGET_GLES
            GL.glClearDepthf((float)value);
#else
            GL.glClearDepth(value);
#endif
            clearDepthValue.Set(value);
        }
    }
}
