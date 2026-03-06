using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlShaderSrcTests
    {
        // ---- Color shaders -----------------------------------------------

        [Fact]
        public void ColorVertShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.COLOR_VERT_SHADER));
        }

        [Fact]
        public void ColorVertShader_ContainsMainFunction()
        {
            Assert.Contains("void main()", GlShaderSrc.COLOR_VERT_SHADER);
        }

        [Fact]
        public void ColorVertShader_ContainsViewMatrix()
        {
            Assert.Contains("uViewMatrix", GlShaderSrc.COLOR_VERT_SHADER);
        }

        [Fact]
        public void ColorFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.COLOR_FRAG_SHADER));
        }

        [Fact]
        public void ColorFragShader_ContainsMainFunction()
        {
            Assert.Contains("void main()", GlShaderSrc.COLOR_FRAG_SHADER);
        }

        [Fact]
        public void ColorFragShader_ContainsFragColor()
        {
            Assert.Contains("FragColor", GlShaderSrc.COLOR_FRAG_SHADER);
        }

        // ---- Gradient shaders --------------------------------------------

        [Fact]
        public void GradientVertShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.GRADIENT_VERT_SHADER));
        }

        [Fact]
        public void GradientVertShader_ContainsTransformInfo()
        {
            Assert.Contains("invTransform", GlShaderSrc.GRADIENT_VERT_SHADER);
        }

        [Fact]
        public void GradientCommonVariables_ContainsMaxStopCount()
        {
            Assert.Contains("MAX_STOP_COUNT", GlShaderSrc.STR_GRADIENT_FRAG_COMMON_VARIABLES);
        }

        [Fact]
        public void GradientCommonFunctions_ContainsGradientStep()
        {
            Assert.Contains("gradientStep", GlShaderSrc.STR_GRADIENT_FRAG_COMMON_FUNCTIONS);
        }

        [Fact]
        public void GradientCommonFunctions_ContainsGradientWrap()
        {
            Assert.Contains("gradientWrap", GlShaderSrc.STR_GRADIENT_FRAG_COMMON_FUNCTIONS);
        }

        [Fact]
        public void LinearGradientVariables_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.STR_LINEAR_GRADIENT_VARIABLES));
        }

        [Fact]
        public void LinearGradientFunctions_ContainsLinearGrad()
        {
            Assert.Contains("linearGrad", GlShaderSrc.STR_LINEAR_GRADIENT_FUNCTIONS);
        }

        [Fact]
        public void RadialGradientVariables_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.STR_RADIAL_GRADIENT_VARIABLES));
        }

        [Fact]
        public void RadialGradientFunctions_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.STR_RADIAL_GRADIENT_FUNCTIONS));
        }

        // ---- Image shaders -----------------------------------------------

        [Fact]
        public void ImageVertShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.IMAGE_VERT_SHADER));
        }

        [Fact]
        public void ImageVertShader_ContainsUV()
        {
            Assert.Contains("vUV", GlShaderSrc.IMAGE_VERT_SHADER);
        }

        [Fact]
        public void ImageFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.IMAGE_FRAG_SHADER));
        }

        [Fact]
        public void ImageFragShader_ContainsSampler()
        {
            Assert.Contains("sampler2D", GlShaderSrc.IMAGE_FRAG_SHADER);
        }

        // ---- Mask shaders ------------------------------------------------

        [Fact]
        public void MaskVertShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_VERT_SHADER));
        }

        [Fact]
        public void MaskAlphaFragShader_ContainsAlphaComputation()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_ALPHA_FRAG_SHADER));
            Assert.Contains("FragColor", GlShaderSrc.MASK_ALPHA_FRAG_SHADER);
        }

        [Fact]
        public void MaskInvAlphaFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_INV_ALPHA_FRAG_SHADER));
        }

        [Fact]
        public void MaskLumaFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_LUMA_FRAG_SHADER));
        }

        [Fact]
        public void MaskInvLumaFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_INV_LUMA_FRAG_SHADER));
        }

        [Fact]
        public void MaskAddFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_ADD_FRAG_SHADER));
        }

        [Fact]
        public void MaskSubFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_SUB_FRAG_SHADER));
        }

        [Fact]
        public void MaskIntersectFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_INTERSECT_FRAG_SHADER));
        }

        [Fact]
        public void MaskDiffFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_DIFF_FRAG_SHADER));
        }

        [Fact]
        public void MaskDarkenFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_DARKEN_FRAG_SHADER));
        }

        [Fact]
        public void MaskLightenFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MASK_LIGHTEN_FRAG_SHADER));
        }

        // ---- Stencil shaders ---------------------------------------------

        [Fact]
        public void StencilVertShader_ContainsGlPosition()
        {
            Assert.Contains("gl_Position", GlShaderSrc.STENCIL_VERT_SHADER);
        }

        [Fact]
        public void StencilFragShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.STENCIL_FRAG_SHADER));
        }

        // ---- Blit shaders ------------------------------------------------

        [Fact]
        public void BlitVertShader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.BLIT_VERT_SHADER));
        }

        [Fact]
        public void BlitFragShader_ContainsSampler()
        {
            Assert.Contains("sampler2D", GlShaderSrc.BLIT_FRAG_SHADER);
        }

        // ---- Blend shaders -----------------------------------------------

        [Fact]
        public void BlendShapeSolidFragHeader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.BLEND_SHAPE_SOLID_FRAG_HEADER));
        }

        [Fact]
        public void BlendShapeLinearFragHeader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.BLEND_SHAPE_LINEAR_FRAG_HEADER));
        }

        [Fact]
        public void BlendShapeRadialFragHeader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.BLEND_SHAPE_RADIAL_FRAG_HEADER));
        }

        [Fact]
        public void BlendImageFragHeader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.BLEND_IMAGE_FRAG_HEADER));
        }

        [Fact]
        public void BlendSceneFragHeader_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.BLEND_SCENE_FRAG_HEADER));
        }

        [Fact]
        public void BlendFragHSL_ContainsHslFunctions()
        {
            Assert.Contains("rgbToHsl", GlShaderSrc.BLEND_FRAG_HSL);
            Assert.Contains("hslToRgb", GlShaderSrc.BLEND_FRAG_HSL);
        }

        // ---- Blend mode fragments ----------------------------------------

        [Fact]
        public void AllBlendFragments_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.NORMAL_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.MULTIPLY_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.SCREEN_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.OVERLAY_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.DARKEN_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.LIGHTEN_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.COLOR_DODGE_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.COLOR_BURN_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.HARD_LIGHT_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.SOFT_LIGHT_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.DIFFERENCE_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.EXCLUSION_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.HUE_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.SATURATION_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.COLOR_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.LUMINOSITY_BLEND_FRAG));
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.ADD_BLEND_FRAG));
        }

        // ---- Effect shaders ----------------------------------------------

        [Fact]
        public void EffectVertex_ContainsGlPosition()
        {
            Assert.Contains("gl_Position", GlShaderSrc.EFFECT_VERTEX);
        }

        [Fact]
        public void GaussianVertical_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.GAUSSIAN_VERTICAL));
        }

        [Fact]
        public void GaussianHorizontal_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.GAUSSIAN_HORIZONTAL));
        }

        [Fact]
        public void EffectDropshadow_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.EFFECT_DROPSHADOW));
        }

        [Fact]
        public void EffectFill_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.EFFECT_FILL));
        }

        [Fact]
        public void EffectTint_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.EFFECT_TINT));
        }

        [Fact]
        public void EffectTritone_NotNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(GlShaderSrc.EFFECT_TRITONE));
        }

        // ---- Shader consistency ------------------------------------------

        [Fact]
        public void AllVertexShaders_ContainGlPosition()
        {
            // Every vertex shader must write to gl_Position
            Assert.Contains("gl_Position", GlShaderSrc.COLOR_VERT_SHADER);
            Assert.Contains("gl_Position", GlShaderSrc.GRADIENT_VERT_SHADER);
            Assert.Contains("gl_Position", GlShaderSrc.IMAGE_VERT_SHADER);
            Assert.Contains("gl_Position", GlShaderSrc.MASK_VERT_SHADER);
            Assert.Contains("gl_Position", GlShaderSrc.STENCIL_VERT_SHADER);
            Assert.Contains("gl_Position", GlShaderSrc.BLIT_VERT_SHADER);
            Assert.Contains("gl_Position", GlShaderSrc.EFFECT_VERTEX);
        }

        [Fact]
        public void AllFragmentShaders_ContainFragColor()
        {
            // Core fragment shaders should output FragColor
            Assert.Contains("FragColor", GlShaderSrc.COLOR_FRAG_SHADER);
            Assert.Contains("FragColor", GlShaderSrc.IMAGE_FRAG_SHADER);
            Assert.Contains("FragColor", GlShaderSrc.BLIT_FRAG_SHADER);
            Assert.Contains("FragColor", GlShaderSrc.MASK_ALPHA_FRAG_SHADER);
        }
    }
}
