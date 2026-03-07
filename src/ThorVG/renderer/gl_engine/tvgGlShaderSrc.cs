// Ported from ThorVG/src/renderer/gl_engine/tvgGlShaderSrc.h and tvgGlShaderSrc.cpp
// Embedded GLSL shader source code as static string fields.

namespace ThorVG
{
    public static class GlShaderSrc
    {
        public static readonly string COLOR_VERT_SHADER = @"
    uniform float uDepth;
    uniform mat3 uViewMatrix;
    layout(location = 0) in vec2 aLocation;
    layout(std140) uniform SolidInfo {
        vec4 solidColor;
    } uSolidInfo;

    void main()
    {
        vec3 pos = uViewMatrix * vec3(aLocation, 1.0);
        gl_Position = vec4(pos.xy, uDepth, 1.0);
    }
";

        public static readonly string COLOR_FRAG_SHADER = @"
    layout(std140) uniform SolidInfo {
        vec4 solidColor;
    } uSolidInfo;
    out vec4 FragColor;

    void main()
    {
       vec4 uColor = uSolidInfo.solidColor;
       FragColor =  vec4(uColor.rgb * uColor.a, uColor.a);
    }
";

        public static readonly string GRADIENT_VERT_SHADER = @"
    uniform float uDepth;
    uniform mat3 uViewMatrix;
    layout(location = 0) in vec2 aLocation;
    out vec2 vPos;
    layout(std140) uniform TransformInfo {
        mat3 invTransform;
    } uTransformInfo;

    void main()
    {
        vec3 glPos = uViewMatrix * vec3(aLocation, 1.0);
        gl_Position = vec4(glPos.xy, uDepth, 1.0);
        vec3 pos =  uTransformInfo.invTransform * vec3(aLocation, 1.0);
        vPos = pos.xy;
    }
";

        public static readonly string STR_GRADIENT_FRAG_COMMON_VARIABLES = @"
    const int MAX_STOP_COUNT = 16;
    in vec2 vPos;
";

        public static readonly string STR_GRADIENT_FRAG_COMMON_FUNCTIONS = @"
    float gradientStep(float edge0, float edge1, float x)
    {
        x = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return x;
    }

    float gradientStop(int index)
    {
        if (index >= MAX_STOP_COUNT) index = MAX_STOP_COUNT - 1;
        int i = index / 4;
        int j = index % 4;
        return uGradientInfo.stopPoints[i][j];
    }

    float gradientWrap(float d)
    {
        int spread = int(uGradientInfo.nStops[2]);
        if (spread == 0) return clamp(d, 0.0, 1.0);

        if (spread == 1) {
            float n = mod(d, 2.0);
            if (n > 1.0) {
                n = 2.0 - n;
            }
            return n;
        }
        if (spread == 2) {
            float n = mod(d, 1.0);
            if (n < 0.0) {
                n += 1.0 + n;
            }
            return n;
        }
    }

    vec4 gradient(float t, float d, float l)
    {
        float dist = d * 2.0 / l;
        vec4 col = vec4(0.0);
        int i = 0;
        int count = int(uGradientInfo.nStops[0]);
        if (t <= gradientStop(0)) {
            col = uGradientInfo.stopColors[0];
        } else if (t >= gradientStop(count - 1)) {
            col = uGradientInfo.stopColors[count - 1];
            if (int(uGradientInfo.nStops[2]) == 2 && (1.0 - t) < dist) {
                float dd = (1.0 - t) / dist;
                float alpha =  dd;
                col *= alpha;
                col += uGradientInfo.stopColors[0] * (1. - alpha);
            }
        } else {
            for (i = 0; i < count - 1; ++i) {
                float stopi = gradientStop(i);
                float stopi1 = gradientStop(i + 1);
                if (t >= stopi && t <= stopi1) {
                    col = (uGradientInfo.stopColors[i] * (1. - gradientStep(stopi, stopi1, t)));
                    col += (uGradientInfo.stopColors[i + 1] * gradientStep(stopi, stopi1, t));
                    if (int(uGradientInfo.nStops[2]) == 2 && abs(d) > dist) {
                        if (i == 0 && (t - stopi) < dist) {
                            float dd = (t - stopi) / dist;
                            float alpha = dd;
                            col *= alpha;
                            vec4 nc = uGradientInfo.stopColors[0] * (1.0 - (t - stopi));
                            nc += uGradientInfo.stopColors[count - 1] * (t - stopi);
                            col += nc * (1.0 - alpha);
                        } else if (i == count - 2 && (1.0 - t) < dist) {
                            float dd = (1.0 - t) / dist;
                            float alpha =  dd;
                            col *= alpha;
                            col += (uGradientInfo.stopColors[0]) * (1.0 - alpha);
                        }
                    }
                    break;
                }
            }
        }
        return col;
    }

    vec3 ScreenSpaceDither(vec2 vScreenPos)
    {
        vec3 vDither = vec3(dot(vec2(171.0, 231.0), vScreenPos.xy));
        vDither.rgb = fract(vDither.rgb / vec3(103.0, 71.0, 97.0));
        return vDither.rgb / 255.0;
    }
";

        public static readonly string STR_LINEAR_GRADIENT_VARIABLES = @"
    layout(std140) uniform GradientInfo {
        vec4  nStops;
        vec2  gradStartPos;
        vec2  gradEndPos;
        vec4  stopPoints[MAX_STOP_COUNT / 4];
        vec4  stopColors[MAX_STOP_COUNT];
    } uGradientInfo;
";

        public static readonly string STR_LINEAR_GRADIENT_FUNCTIONS = @"
    vec4 linearGradientColor(vec2 pos)
    {
        vec2 st = uGradientInfo.gradStartPos;
        vec2 ed = uGradientInfo.gradEndPos;
        vec2 ba = ed - st;
        float d = dot(pos - st, ba) / dot(ba, ba);
        float t = gradientWrap(d);
        vec4 color = gradient(t, d, length(pos - st));
        return vec4(color.rgb * color.a, color.a);
    }
";

        public static readonly string STR_LINEAR_GRADIENT_MAIN = @"
    out vec4 FragColor;
    void main()
    {
        FragColor = linearGradientColor(vPos);
    }
";

        public static readonly string STR_RADIAL_GRADIENT_VARIABLES = @"
    layout(std140) uniform GradientInfo {
        vec4  nStops;
        vec4  centerPos;
        vec2  radius;
        vec4  stopPoints[MAX_STOP_COUNT / 4];
        vec4  stopColors[MAX_STOP_COUNT];
    } uGradientInfo ;
";

        public static readonly string STR_RADIAL_GRADIENT_FUNCTIONS = @"
    mat3 radial_matrix(vec2 p0, vec2 p1)
    {
        mat3 a = mat3(0.0, -1.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0);
        mat3 b = mat3(p1.y - p0.y, p0.x - p1.x, 0.0, p1.x - p0.x, p1.y - p0.y, 0.0, p0.x, p0.y, 1.0);
        return a * inverse(b);
    }

    vec2 compute_radial_t(vec2 c0, float r0, vec2 c1, float r1, vec2 pos)
    {
        const float scalar_nearly_zero = 2.44140625e-4;
        float d_center = distance(c0, c1);
        float d_radius = r1 - r0;
        bool radial = d_center < scalar_nearly_zero;
        bool strip = abs(d_radius) < scalar_nearly_zero;

        if (radial) {
            if (strip) return vec2(0.0, -1.0);
            float scale = 1.0 / d_radius;
            float scale_sign = sign(d_radius);
            float bias = r0 / d_radius;
            vec2 pt = (pos - c0) * scale;
            float t = length(pt) * scale_sign - bias;
            return vec2(t, 1.0);
        } else if (strip) {
            mat3 transform = radial_matrix(c0, c1);
            float r = r0 / d_center;
            float r_2 = r * r;
            vec2 pt = (transform * vec3(pos.xy, 1.0)).xy;
            float t = r_2 - pt.y * pt.y;
            if (t < 0.0) return vec2(0.0, -1.0);
            t = pt.x + sqrt(t);
            return vec2(t, 1.0);
        } else {
            float f = r0 / (r0 - r1);
            bool is_swapped = abs(f - 1.0) < scalar_nearly_zero;
            vec2 c0p = is_swapped ? c1 : c0;
            vec2 c1p = is_swapped ? c0 : c1;
            float fp = is_swapped ? 0.0 : f;
            vec2 cf = c0p * (1.0 - fp) + c1p * fp;
            mat3 transform = radial_matrix(cf, c1p);
            float scale_x = abs(1.0 - fp);
            float scale_y = scale_x;
            float r1n = abs(r1 - r0) / d_center;
            bool is_focal_on_circle = abs(r1n - 1.0) < scalar_nearly_zero;
            if (is_focal_on_circle) {
                scale_x *= 0.5;
                scale_y *= 0.5;
            } else {
                float denom = r1n * r1n - 1.0;
                scale_x *= r1n / denom;
                scale_y /= sqrt(abs(denom));
            }
            transform = mat3(scale_x, 0.0, 0.0, 0.0, scale_y, 0.0, 0.0, 0.0, 1.0) * transform;
            vec2 pt = (transform * vec3(pos.xy, 1.0)).xy;
            float inv_r1 = 1.0 / r1n;
            float d_radius_sign = sign(1.0 - fp);
            float x_t = -1.0;
            if (is_focal_on_circle) x_t = dot(pt, pt) / pt.x;
            else if (r1n > 1.0) x_t = length(pt) - pt.x * inv_r1;
            else {
                float discriminant = pt.x * pt.x - pt.y * pt.y;
                float root = sqrt(max(discriminant, 0.0));
                float s = (is_swapped == (d_radius_sign > 0.0)) ? 1.0 : -1.0;
                x_t = s * root - pt.x * inv_r1;
                if (discriminant < 0.0 || x_t < 0.0) return vec2(is_swapped ? 0.0 : 1.0, 1.0);
            }
            float t = fp + d_radius_sign * x_t;
            if (is_swapped) t = 1.0 - t;
            return vec2(t, 1.0);
        }
    }

    vec4 radialGradientColor(vec2 pos)
    {
        vec2 res = compute_radial_t(uGradientInfo.centerPos.xy,
                                    uGradientInfo.radius.x,
                                    uGradientInfo.centerPos.zw,
                                    uGradientInfo.radius.y,
                                    pos);
        if (res.y < 0.0) return vec4(0.0, 0.0, 0.0, 0.0);
        float t = gradientWrap(res.x);
        vec4 color = gradient(t, res.x, length(pos - uGradientInfo.centerPos.xy));
        return vec4(color.rgb * color.a, color.a);
    }
";

        public static readonly string STR_RADIAL_GRADIENT_MAIN = @"
    out vec4 FragColor;

    void main()
    {
        FragColor = radialGradientColor(vPos);
    }
";

        public static readonly string IMAGE_VERT_SHADER = @"
    uniform float uDepth;
    uniform mat3 uViewMatrix;
    layout (location = 0) in vec2 aLocation;
    layout (location = 1) in vec2 aUV;
    out vec2 vUV;

    void main()
    {
        vUV = aUV;
        vec3 pos = uViewMatrix * vec3(aLocation, 1.0);
        gl_Position = vec4(pos.xy, uDepth, 1.0);
    }
";

        public static readonly string IMAGE_FRAG_SHADER = @"
    layout(std140) uniform ColorInfo {
        int format;
        int flipY;
        int opacity;
        int dummy;
    } uColorInfo;
    uniform sampler2D uTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec2 uv = vUV;
        if (uColorInfo.flipY == 1) { uv.y = 1.0 - uv.y; }
        vec4 color = texture(uTexture, uv);
        vec4 result;
        if (uColorInfo.format == 0) {
            result = color;
        } else if (uColorInfo.format == 1) {
            result = color.bgra;
        } else if (uColorInfo.format == 2) {
            result = vec4(color.rgb * color.a, color.a);
        } else if (uColorInfo.format == 3) {
            result = vec4(color.bgr * color.a, color.a);
        }
        FragColor = result * float(uColorInfo.opacity) / 255.0;
   }
";

        public static readonly string MASK_VERT_SHADER = @"
    uniform float uDepth;
    layout(location = 0) in vec2 aLocation;
    layout(location = 1) in vec2 aUV;
    out vec2  vUV;

    void main()
    {
        vUV = aUV;
        gl_Position = vec4(aLocation, uDepth, 1.0);
    }
";

        public static readonly string MASK_ALPHA_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        FragColor = srcColor * maskColor.a;
    }
";

        public static readonly string MASK_INV_ALPHA_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        FragColor = srcColor *(1.0 - maskColor.a);
    }
";

        public static readonly string MASK_LUMA_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);

        if (maskColor.a > 0.000001) {
            maskColor = vec4(maskColor.rgb / maskColor.a, maskColor.a);
        }

        FragColor = srcColor * dot(maskColor.rgb, vec3(0.2125, 0.7154, 0.0721)) * maskColor.a;
    }
";

        public static readonly string MASK_INV_LUMA_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        float luma = dot(maskColor.rgb, vec3(0.2125, 0.7154, 0.0721));
        FragColor = srcColor * (1.0 - luma);
    }
";

        public static readonly string MASK_ADD_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        vec4 color = srcColor + maskColor * (1.0 - srcColor.a);
        FragColor = min(color, vec4(1.0, 1.0, 1.0, 1.0)) ;
    }
";

        public static readonly string MASK_SUB_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        float a = srcColor.a - maskColor.a;

        if (a < 0.0 || srcColor.a == 0.0) {
            FragColor = vec4(0.0, 0.0, 0.0, 0.0);
        } else {
            vec3 srcRgb = srcColor.rgb / srcColor.a;
            FragColor = vec4(srcRgb * a, a);
        }
    }
";

        public static readonly string MASK_INTERSECT_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        FragColor = maskColor * srcColor.a;
    }
";

        public static readonly string MASK_DIFF_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        float da = srcColor.a - maskColor.a;
        if (da == 0.0) {
            FragColor = vec4(0.0, 0.0, 0.0, 0.0);
        } else if (da > 0.0) {
            FragColor = srcColor * da;
        } else {
            FragColor = maskColor * (-da);
        }
    }
";

        public static readonly string MASK_DARKEN_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        if (srcColor.a > 0.0) srcColor.rgb /= srcColor.a;
        float alpha = min(srcColor.a, maskColor.a);
        FragColor = vec4(srcColor.rgb * alpha, alpha);
    }
";

        public static readonly string MASK_LIGHTEN_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    uniform sampler2D uMaskTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        vec4 srcColor = texture(uSrcTexture, vUV);
        vec4 maskColor = texture(uMaskTexture, vUV);
        if (srcColor.a > 0.0) srcColor.rgb /= srcColor.a;
        float alpha = max(srcColor.a, maskColor.a);
        FragColor = vec4(srcColor.rgb * alpha, alpha);
    }
";

        public static readonly string STENCIL_VERT_SHADER = @"
    uniform float uDepth;
    uniform mat3 uViewMatrix;
    layout(location = 0) in vec2 aLocation;

    void main()
    {
        vec3 pos = uViewMatrix * vec3(aLocation, 1.0);
        gl_Position = vec4(pos.xy, uDepth, 1.0);
    }";

        public static readonly string STENCIL_FRAG_SHADER = @"
    out vec4 FragColor;

    void main()
    {
        FragColor = vec4(0.0);
    }
";

        public static readonly string BLIT_VERT_SHADER = @"
    layout(location = 0) in vec2 aLocation;
    layout(location = 1) in vec2 aUV;
    out vec2 vUV;

    void main()
    {
        vUV = aUV;
        gl_Position = vec4(aLocation, 0.0, 1.0);
    }";

        public static readonly string BLIT_FRAG_SHADER = @"
    uniform sampler2D uSrcTexture;
    in vec2 vUV;
    out vec4 FragColor;

    void main()
    {
        FragColor = texture(uSrcTexture, vUV);
    }";

        public static readonly string BLEND_SHAPE_SOLID_FRAG_HEADER = @"
layout(std140) uniform SolidInfo {
    vec4 solidColor;
} uSolidInfo;

layout(std140) uniform BlendRegion {
    vec4 region;
} uBlendRegion;

uniform sampler2D uDstTexture;

out vec4 FragColor;

vec3 One = vec3(1.0, 1.0, 1.0);
struct FragData { vec3 Sc; float Sa; float So; vec3 Dc; float Da; };
FragData d;

void getFragData() {
    vec2 uv = (gl_FragCoord.xy - uBlendRegion.region.xy) / uBlendRegion.region.zw;
    vec4 colorSrc = uSolidInfo.solidColor;
    vec4 colorDst = texture(uDstTexture, uv);
    d.Sc = colorSrc.rgb * colorSrc.a;
    d.Sa = colorSrc.a;
    d.So = 1.0;
    d.Dc = colorDst.rgb;
    d.Da = colorDst.a;
}

vec4 postProcess(vec4 R) { return R; }
";

        public static readonly string BLEND_SHAPE_LINEAR_FRAG_HEADER = @"
layout(std140) uniform BlendRegion {
    vec4 region;
} uBlendRegion;

uniform sampler2D uDstTexture;

out vec4 FragColor;

vec3 One = vec3(1.0, 1.0, 1.0);
struct FragData { vec3 Sc; float Sa; float So; vec3 Dc; float Da; };
FragData d;

void getFragData() {
    vec4 colorSrc = linearGradientColor(vPos);
    vec2 uv = (gl_FragCoord.xy - uBlendRegion.region.xy) / uBlendRegion.region.zw;
    vec4 colorDst = texture(uDstTexture, uv);

    d.Sc = colorSrc.rgb;
    d.Sa = colorSrc.a;
    d.So = 1.0;
    d.Dc = colorDst.rgb;
    d.Da = colorDst.a;
    if (d.Sa > 0.0) { d.Sc = d.Sc / d.Sa; }
    float srcOpacity = d.Sa * d.So;
    d.Sc = mix(d.Dc, d.Sc, srcOpacity);
    d.Sa = mix(d.Da, 1.0, srcOpacity);
}

vec4 postProcess(vec4 R) { return R; }
";

        public static readonly string BLEND_SHAPE_RADIAL_FRAG_HEADER = @"
layout(std140) uniform BlendRegion {
    vec4 region;
} uBlendRegion;

uniform sampler2D uDstTexture;

out vec4 FragColor;

vec3 One = vec3(1.0, 1.0, 1.0);
struct FragData { vec3 Sc; float Sa; float So; vec3 Dc; float Da; };
FragData d;

void getFragData() {
    vec4 colorSrc = radialGradientColor(vPos);
    vec2 uv = (gl_FragCoord.xy - uBlendRegion.region.xy) / uBlendRegion.region.zw;
    vec4 colorDst = texture(uDstTexture, uv);

    d.Sc = colorSrc.rgb;
    d.Sa = colorSrc.a;
    d.So = 1.0;
    d.Dc = colorDst.rgb;
    d.Da = colorDst.a;
    if (d.Sa > 0.0) { d.Sc = d.Sc / d.Sa; }
    float srcOpacity = d.Sa * d.So;
    d.Sc = mix(d.Dc, d.Sc, srcOpacity);
    d.Sa = mix(d.Da, 1.0, srcOpacity);
}

vec4 postProcess(vec4 R) { return R; }
";

        public static readonly string BLEND_IMAGE_FRAG_HEADER = @"
layout(std140) uniform BlendRegion {
    vec4 region;
} uBlendRegion;

uniform sampler2D uSrcTexture;
uniform sampler2D uDstTexture;

in vec2 vUV;
out vec4 FragColor;

vec3 One = vec3(1.0, 1.0, 1.0);
struct FragData { vec3 Sc; float Sa; float So; vec3 Dc; float Da; };
FragData d;

void getFragData() {
    vec4 colorSrc = texture(uSrcTexture, vUV);
    vec2 uvDst = (gl_FragCoord.xy - uBlendRegion.region.xy) / uBlendRegion.region.zw;
    vec4 colorDst = texture(uDstTexture, uvDst);
    d.Sc = colorSrc.rgb;
    d.Sa = colorSrc.a;
    d.So = 1.0;
    d.Dc = colorDst.rgb;
    d.Da = colorDst.a;
    if (d.Sa > 0.0) { d.Sc = d.Sc / d.Sa; }
}

vec4 postProcess(vec4 R) { return mix(vec4(d.Dc, d.Da), R, d.Sa * d.So); }
";

        public static readonly string BLEND_SCENE_FRAG_HEADER = @"
layout(std140) uniform ColorInfo {
    int format;
    int flipY;
    int opacity;
    int dummy;
} uColorInfo;

layout(std140) uniform BlendRegion {
    vec4 region;
} uBlendRegion;

uniform sampler2D uSrcTexture;
uniform sampler2D uDstTexture;

in vec2 vUV;
out vec4 FragColor;

vec3 One = vec3(1.0, 1.0, 1.0);
struct FragData { vec3 Sc; float Sa; float So; vec3 Dc; float Da; };
FragData d;

void getFragData() {
    vec4 colorSrc = texture(uSrcTexture, vUV);
    vec2 uvDst = (gl_FragCoord.xy - uBlendRegion.region.xy) / uBlendRegion.region.zw;
    vec4 colorDst = texture(uDstTexture, uvDst);
    d.Sc = colorSrc.rgb;
    d.Sa = colorSrc.a;
    d.So = float(uColorInfo.opacity) / 255.0;
    d.Dc = colorDst.rgb;
    d.Da = colorDst.a;
    if (d.Sa > 0.0) {d.Sc = d.Sc / d.Sa; }
}

vec4 postProcess(vec4 R) { return mix(vec4(d.Dc, d.Da), R, d.Sa * d.So); }
";

        public static readonly string BLEND_FRAG_HSL = @"
vec3 rgbToHsl(vec3 color) {
    float minVal = min(color.r, min(color.g, color.b));
    float maxVal = max(color.r, max(color.g, color.b));
    float delta = maxVal - minVal;
    float h = 0.0;
    if (delta > 0.0) {
             if (maxVal == color.r) { h = (color.g - color.b) / delta - trunc(h / 6.0) * 6.0; }
        else if (maxVal == color.g) { h = (color.b - color.r) / delta + 2.0;
        } else                      { h = (color.r - color.g) / delta + 4.0; }
        h = h * 60.0;
        if (h < 0.0) { h += 360.0; }
    }
    float l = (maxVal + minVal) * 0.5;
    float s = delta > 0.0 ? delta / (1.0 - abs(2.0 * l - 1.0)) : 0.0;
    return vec3(h, s, l);
}

vec3 hslToRgb(vec3 color) {
    float h = color.x;
    float s = color.y;
    float l = color.z;
    float C = (1.0 - abs(2.0 * l - 1.0)) * s;
    float h_prime = h / 60.0;
    float X = C * (1.0 - abs(h_prime - 2.0 * trunc(h_prime / 2.0) - 1.0));
    float m = l - C / 2.0;
    vec3 rgb = vec3(0.0);
         if (h_prime >= 0.0 && h_prime < 1.0) { rgb = vec3(C, X, 0.0); }
    else if (h_prime >= 1.0 && h_prime < 2.0) { rgb = vec3(X, C, 0.0); }
    else if (h_prime >= 2.0 && h_prime < 3.0) { rgb = vec3(0.0, C, X); }
    else if (h_prime >= 3.0 && h_prime < 4.0) { rgb = vec3(0.0, X, C); }
    else if (h_prime >= 4.0 && h_prime < 5.0) { rgb = vec3(X, 0.0, C); }
    else                                      { rgb = vec3(C, 0.0, X); }
    return rgb + vec3(m);
}
";

        public static readonly string NORMAL_BLEND_FRAG = @"
void main()
{
    FragColor = texture(uSrcTexture, vUV);
}
";
        public static readonly string MULTIPLY_BLEND_FRAG = @"
void main()
{
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        Rc = d.Sc * min(One, d.Dc / d.Da);
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string SCREEN_BLEND_FRAG = @"
void main()
{
    getFragData();
    vec3 Rc = d.Sc + d.Dc - d.Sc * d.Dc;
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string OVERLAY_BLEND_FRAG = @"
void main()
{
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        Rc.r = Dc.r < 0.5 ? min(1.0, 2.0 * d.Sc.r * Dc.r) : 1.0 - min(1.0, 2.0 * (1.0 - d.Sc.r) * (1.0 - Dc.r));
        Rc.g = Dc.g < 0.5 ? min(1.0, 2.0 * d.Sc.g * Dc.g) : 1.0 - min(1.0, 2.0 * (1.0 - d.Sc.g) * (1.0 - Dc.g));
        Rc.b = Dc.b < 0.5 ? min(1.0, 2.0 * d.Sc.b * Dc.b) : 1.0 - min(1.0, 2.0 * (1.0 - d.Sc.b) * (1.0 - Dc.b));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string DARKEN_BLEND_FRAG = @"
void main()
{
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        Rc = min(d.Sc, min(One, d.Dc / d.Da));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string LIGHTEN_BLEND_FRAG = @"
void main()
{
    getFragData();
    vec3 Rc = max(d.Sc, d.Dc);
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string COLOR_DODGE_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        Rc.r = Dc.r > 0.0 ? d.Sc.r < 1.0 ? min(1.0, Dc.r / (1.0 - d.Sc.r)) : 1.0 : 0.0;
        Rc.g = Dc.g > 0.0 ? d.Sc.g < 1.0 ? min(1.0, Dc.g / (1.0 - d.Sc.g)) : 1.0 : 0.0;
        Rc.b = Dc.b > 0.0 ? d.Sc.b < 1.0 ? min(1.0, Dc.b / (1.0 - d.Sc.b)) : 1.0 : 0.0;
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string COLOR_BURN_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        Rc.r = d.Sc.r > 0.0 ? 1.0 - min(1.0, (1.0 - Dc.r) / d.Sc.r) : Dc.r < 1.0 ? 0.0 : 1.0;
        Rc.g = d.Sc.g > 0.0 ? 1.0 - min(1.0, (1.0 - Dc.g) / d.Sc.g) : Dc.g < 1.0 ? 0.0 : 1.0;
        Rc.b = d.Sc.b > 0.0 ? 1.0 - min(1.0, (1.0 - Dc.b) / d.Sc.b) : Dc.b < 1.0 ? 0.0 : 1.0;
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string HARD_LIGHT_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        Rc.r = d.Sc.r < 0.5 ? min(1.0, 2.0 * d.Sc.r * Dc.r) : 1.0 - min(1.0, 2.0 * (1.0 - d.Sc.r) * (1.0 - Dc.r));
        Rc.g = d.Sc.g < 0.5 ? min(1.0, 2.0 * d.Sc.g * Dc.g) : 1.0 - min(1.0, 2.0 * (1.0 - d.Sc.g) * (1.0 - Dc.g));
        Rc.b = d.Sc.b < 0.5 ? min(1.0, 2.0 * d.Sc.b * Dc.b) : 1.0 - min(1.0, 2.0 * (1.0 - d.Sc.b) * (1.0 - Dc.b));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string SOFT_LIGHT_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        Rc = min(One, (One - 2.0 * d.Sc) * Dc * Dc + 2.0 * d.Sc * Dc);
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string DIFFERENCE_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = abs(d.Dc - d.Sc);
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string EXCLUSION_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc + d.Dc - 2.0 * d.Sc * d.Dc;
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string HUE_BLEND_FRAG = @"
void main()
{
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        vec3 Sc = d.Sc;
        vec3 Shsl = rgbToHsl(Sc);
        vec3 Dhsl = rgbToHsl(Dc);
        Rc = hslToRgb(vec3(Shsl.r, Dhsl.g, Dhsl.b));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string SATURATION_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        vec3 Sc = d.Sc;
        vec3 Shsl = rgbToHsl(Sc);
        vec3 Dhsl = rgbToHsl(Dc);
        Rc = hslToRgb(vec3(Dhsl.r, Shsl.g, Dhsl.b));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string COLOR_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        vec3 Sc = d.Sc;
        vec3 Shsl = rgbToHsl(Sc);
        vec3 Dhsl = rgbToHsl(Dc);
        Rc = hslToRgb(vec3(Shsl.r, Shsl.g, Dhsl.b));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string LUMINOSITY_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = d.Sc;
    if (d.Da > 0.0) {
        vec3 Dc = min(One, d.Dc / d.Da);
        vec3 Sc = d.Sc;
        vec3 Shsl = rgbToHsl(Sc);
        vec3 Dhsl = rgbToHsl(Dc);
        Rc = hslToRgb(vec3(Dhsl.r, Dhsl.g, Shsl.b));
        Rc = mix(d.Sc, Rc, d.Da);
    }
    FragColor = postProcess(vec4(Rc, 1.0));
}
";
        public static readonly string ADD_BLEND_FRAG = @"
void main() {
    getFragData();
    vec3 Rc = min(One, d.Sc + d.Dc);
    FragColor = postProcess(vec4(Rc, 1.0));
}
";

        public static readonly string EFFECT_VERTEX = @"
layout(location = 0) in vec2 aLocation;
out vec2 vUV;

void main()
{
    vUV = aLocation * 0.5 + 0.5;
    gl_Position = vec4(aLocation, 0.0, 1.0);
}
";

        public static readonly string GAUSSIAN_VERTICAL = @"
uniform sampler2D uSrcTexture;
layout(std140) uniform Gaussian {
    float sigma;
    float scale;
    float extend;
    float dummy0;
} uGaussian;

layout(std140) uniform Viewport {
    vec4 vp;
} uViewport;

in vec2 vUV;
out vec4 FragColor;

float gaussian(float x, float sigma) {
    float exponent = -x * x / (2.0 * sigma * sigma);
    return exp(exponent) / (sqrt(2.0 * 3.141592) * sigma);
}

void main()
{
    vec2 texelSize = 1.0 / vec2(textureSize(uSrcTexture, 0));
    vec4 colorSum = vec4(0.0);
    float sigma = uGaussian.sigma * uGaussian.scale;
    float weightSum = 0.0;
    int radius = int(uGaussian.extend);

    for (int y = -radius; y <= radius; ++y) {
        vec2 offset = vec2(0.0, float(y) * texelSize.y);
        vec2 coord = vUV + offset;
        float pixCoord = uViewport.vp.y - coord.y / texelSize.y;
        float weight = pixCoord < uViewport.vp.w ? gaussian(float(y), sigma) : 0.0;
        colorSum += texture(uSrcTexture, coord) * weight;
        weightSum += weight;
    }

    FragColor = colorSum / weightSum;
}
";

        public static readonly string GAUSSIAN_HORIZONTAL = @"
uniform sampler2D uSrcTexture;
layout(std140) uniform Gaussian {
    float sigma;
    float scale;
    float extend;
    float dummy0;
} uGaussian;

layout(std140) uniform Viewport {
    vec4 vp;
} uViewport;

in vec2 vUV;
out vec4 FragColor;

float gaussian(float x, float sigma) {
    float exponent = -x * x / (2.0 * sigma * sigma);
    return exp(exponent) / (sqrt(2.0 * 3.141592) * sigma);
}

void main()
{
    vec2 texelSize = 1.0 / vec2(textureSize(uSrcTexture, 0));
    vec4 colorSum = vec4(0.0);
    float sigma = uGaussian.sigma * uGaussian.scale;
    float weightSum = 0.0;
    int radius = int(uGaussian.extend);

    for (int y = -radius; y <= radius; ++y) {
        vec2 offset = vec2(float(y) * texelSize.x, 0.0);
        vec2 coord = vUV + offset;
        float pixCoord = uViewport.vp.x + coord.x / texelSize.x;
        float weight = pixCoord < uViewport.vp.z ? gaussian(float(y), sigma) : 0.0;
        colorSum += texture(uSrcTexture, coord) * weight;
        weightSum += weight;
    }

    FragColor = colorSum / weightSum;
}
";

        public static readonly string EFFECT_DROPSHADOW = @"
uniform sampler2D uSrcTexture;
uniform sampler2D uBlrTexture;
layout(std140) uniform DropShadow {
    float sigma;
    float scale;
    float extend;
    float dummy0;
    vec4 color;
    vec2 offset;
} uDropShadow;

in vec2 vUV;
out vec4 FragColor;

void main()
{
    vec2 texelSize = 1.0 / vec2(textureSize(uSrcTexture, 0));
    vec2 offset = uDropShadow.offset * texelSize;
    vec4 orig = texture(uSrcTexture, vUV);
    vec4 blur = texture(uBlrTexture, vUV + offset);
    vec4 shad = uDropShadow.color * blur.a;
    FragColor = orig + shad * (1.0 - orig.a);
}
";

        public static readonly string EFFECT_FILL = @"
uniform sampler2D uSrcTexture;
layout(std140) uniform Params {
    vec4 params[3];
} uParams;

in vec2 vUV;
out vec4 FragColor;

void main()
{
    vec4 orig = texture(uSrcTexture, vUV);
    vec4 fill = uParams.params[0];
    FragColor = fill * orig.a * fill.a;
}
";

        public static readonly string EFFECT_TINT = @"
uniform sampler2D uSrcTexture;
layout(std140) uniform Params {
    vec4 params[3];
} uParams;

in vec2 vUV;
out vec4 FragColor;

void main()
{
    vec4 orig = texture(uSrcTexture, vUV);
    float luma = dot(orig.rgb, vec3(0.2126, 0.7152, 0.0722));
    FragColor = vec4(mix(orig.rgb, mix(uParams.params[0].rgb, uParams.params[1].rgb, luma), uParams.params[2].r) * orig.a, orig.a);
}
";

        public static readonly string EFFECT_TRITONE = @"
uniform sampler2D uSrcTexture;
layout(std140) uniform Params {
    vec4 params[3];
} uParams;

in vec2 vUV;
out vec4 FragColor;

void main()
{
    vec4 orig = texture(uSrcTexture, vUV);
    float luma = dot(orig.rgb, vec3(0.2126, 0.7152, 0.0722));
    bool isBright = luma >= 0.5f;
    float t = isBright ? (luma - 0.5f) * 2.0f : luma * 2.0f;
    vec3 from = isBright ? uParams.params[1].rgb : uParams.params[0].rgb;
    vec3 to = isBright ? uParams.params[2].rgb : uParams.params[1].rgb;
    vec4 tmp = vec4(mix(from, to, t), 1.0f);

    if (uParams.params[2].a > 0.0f) tmp = mix(tmp, orig, uParams.params[2].a);
    FragColor = tmp * orig.a;
}
";
    }
}
