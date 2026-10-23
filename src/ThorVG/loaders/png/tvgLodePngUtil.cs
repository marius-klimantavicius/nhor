/*
 * Copyright (c) 2026 ThorVG project. All rights reserved.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

/*
    LodePNG Utils

    Copyright (c) 2005-2020 Lode Vandevenne

    This software is provided 'as-is', without any express or implied
    warranty. In no event will the authors be held liable for any damages
    arising from the use of this software.

    Permission is granted to anyone to use this software for any purpose,
    including commercial applications, and to alter it and redistribute it
    freely, subject to the following restrictions:

        1. The origin of this software must not be misrepresented; you must not
        claim that you wrote the original software. If you use this software
        in a product, an acknowledgment in the product documentation would be
        appreciated but is not required.

        2. Altered source versions must be plainly marked as such, and must not be
        misrepresented as being the original software.

        3. This notice may not be removed or altered from any source
        distribution.
*/

using System;

namespace ThorVG
{
    public static partial class LodePng
    {
        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        /* Parameters of a tone reproduction curve, either with a power law formula or with a lookup table. */
        private sealed class LodePNGICCCurve
        {
            public uint type; /* 0=linear, 1=lut, 2=simple gamma */
            public float[]? lut; /* for type 1 */
            public int lut_size;
            public float gamma; /* for type 2 */
        }

        /* Values parsed from ICC profile, see _parseICC for more information about this subset.*/
        private sealed class LodePNGICC
        {
            // Omit the unused ICC profile version fields from this stripped conversion path.

            /* The whitepoint of the profile connection space (PCS). Should always be D50, but parsed and used anyway.
            (to be clear, whitepoint and illuminant are synonyms in practice, but here field "illuminant" is ICC's
            "global" whitepoint that is always D50, and the field "white" below allows deriving the whitepoint of
            the particular RGB space represented here) */
            public readonly float[] illuminant = new float[3];

            /* if true, has chromatic adaptation matrix that must be used. If false, you must compute a chromatic adaptation
            matrix yourself from "illuminant" and "white". */
            public uint has_chad;
            public Matrix chad;

            /* The whitepoint of the RGB color space as stored in the ICC file. If has_chad, must be adapted with the
            chad matrix to become the one we need to go to absolute XYZ (in fact ICC implies it should then be
            exactly D50 in the file, redundantly, before this transformation with chad), else use as-is (then its
            values can actually be something else than D50, and are the ones we need). */
            public uint has_whitepoint;
            public readonly float[] white = new float[3];
            /* Chromaticities of the RGB space in XYZ color space, but given such that you must still
            whitepoint adapt them from D50 to the RGB space whitepoint to go to absolute XYZ (if has_chad,
            with chad, else with bradford adaptation matrix from illuminant to white). */
            public uint has_chromaticity;
            public Matrix rgb;

            public uint has_trc; /* TRC = tone reproduction curve (aka "gamma correction") */

            /* TRC's for the three channels */
            public readonly LodePNGICCCurve[] trc =
            {
                new LodePNGICCCurve(), new LodePNGICCCurve(), new LodePNGICCCurve()
            };
        }

        /* ICC tone response curve, nonlinear (encoded) to linear.
           Input and output in range 0-1. If color was integer 0-255, multiply with (1.0f/255)
           to get the correct floating point behavior.
           Outside of range 0-1, will not clip but either return x itself, or in cases
           where it makes sense, a value defined by the same function.
           NOTE: ICC requires clipping, but we do that only later when converting float to integer.*/
        private static float _iccForwardTRC(LodePNGICCCurve curve, float x)
        {
            if (curve.type == 0)
            {
                return x;
            }
            if (curve.type == 1) /* Lookup table */
            {
                float v0, v1, fraction;
                int index;
                if (curve.lut == null) return 0; /* error */
                if (x < 0) return x;
                index = (int)(x * (curve.lut_size - 1));
                if (index >= curve.lut_size) return x;

                /* LERP */
                v0 = curve.lut[index];
                v1 = (index + 1 < curve.lut_size) ? curve.lut[index + 1] : 1.0f;
                fraction = (x * (curve.lut_size - 1)) - index;
                return TvgMath.MultiplyAdd(v0, 1 - fraction, v1 * fraction);
            }
            if (curve.type == 2)
            {
                /* Gamma expansion */
                return (x > 0) ? MathF.Pow(x, curve.gamma) : x;
            }
            return 0;
        }

        private static uint _decodeICC(byte[] data, int size, ref int pos, byte bytes)
        {
            pos += bytes;
            if (pos > size) return 0;
            uint value = 0;
            for (int i = pos - bytes; i < pos; ++i) value = (value << 8) | data[i];
            return value;
        }

        private static float _decodeICC15Fixed16(byte[] data, int size, ref int pos)
        {
            return unchecked((int)_decodeICC(data, size, ref pos, 4)) / 65536.0f;
        }

        private static void _decodeICCXYZ(out float x, out float y, out float z, byte[] data, int size, ref int offset)
        {
            x = _decodeICC15Fixed16(data, size, ref offset);
            y = _decodeICC15Fixed16(data, size, ref offset);
            z = _decodeICC15Fixed16(data, size, ref offset);
        }

        private static bool _isICCword(uint value, string word)
        {
            return value == (((uint)word[0] << 24) | ((uint)word[1] << 16) |
                             ((uint)word[2] << 8) | word[3]);
        }

        /* Parses a subset of the ICC profile, supporting the necessary mix of ICC v2
           and ICC v4 required to correctly convert the RGB color space to XYZ.
           Does not parse values not related to this specific PNG-related purpose, and
           does not support non-RGB profiles or lookup-table based chroma (but it
           supports lookup tables for TRC aka "gamma"). */
        private static uint _parseICC(LodePNGICC icc, byte[] data, int size)
        {
            int pos = 0;

            if (size < 132) return 1; /* Too small to be a valid icc profile. */

            pos = 16;
            if (!_isICCword(_decodeICC(data, size, ref pos, 4), "RGB ")) return 1;

            /* Should always be 0.9642, 1.0, 0.8249 */
            pos = 68;
            _decodeICCXYZ(out icc.illuminant[0], out icc.illuminant[1], out icc.illuminant[2], data, size, ref pos);

            pos = 128;
            uint numtags = _decodeICC(data, size, ref pos, 4);
            if (pos >= size) return 1;
            /* scan for tags we want to handle */
            for (uint i = 0; i < numtags; i++)
            {
                uint tag = _decodeICC(data, size, ref pos, 4);
                int offset = (int)_decodeICC(data, size, ref pos, 4);
                uint tagsize = _decodeICC(data, size, ref pos, 4);
                if (pos >= size || offset < 0 || offset >= size) return 1;
                if ((long)offset + tagsize > size) return 1;
                if (tagsize < 8) return 1;

                if (_isICCword(tag, "wtpt"))
                {
                    offset += 8; /* skip type and reserved */
                    _decodeICCXYZ(out icc.white[0], out icc.white[1], out icc.white[2], data, size, ref offset);
                    icc.has_whitepoint = 1;
                }
                else if (_isICCword(tag, "rXYZ"))
                {
                    offset += 8; /* skip type and reserved */
                    _decodeICCXYZ(out icc.rgb.e11, out icc.rgb.e21, out icc.rgb.e31, data, size, ref offset);
                    icc.has_chromaticity = 1;
                }
                else if (_isICCword(tag, "gXYZ"))
                {
                    offset += 8; /* skip type and reserved */
                    _decodeICCXYZ(out icc.rgb.e12, out icc.rgb.e22, out icc.rgb.e32, data, size, ref offset);
                    icc.has_chromaticity = 1;
                }
                else if (_isICCword(tag, "bXYZ"))
                {
                    offset += 8; /* skip type and reserved */
                    _decodeICCXYZ(out icc.rgb.e13, out icc.rgb.e23, out icc.rgb.e33, data, size, ref offset);
                    icc.has_chromaticity = 1;
                }
                else if (_isICCword(tag, "chad"))
                {
                    offset += 8; /* skip datatype keyword "sf32" and reserved */
                    _decodeICCXYZ(out icc.chad.e11, out icc.chad.e12, out icc.chad.e13, data, size, ref offset);
                    _decodeICCXYZ(out icc.chad.e21, out icc.chad.e22, out icc.chad.e23, data, size, ref offset);
                    _decodeICCXYZ(out icc.chad.e31, out icc.chad.e32, out icc.chad.e33, data, size, ref offset);
                    icc.has_chad = 1;
                }
                else if (_isICCword(tag, "rTRC") || _isICCword(tag, "gTRC") || _isICCword(tag, "bTRC"))
                {
                    int channel = _isICCword(tag, "bTRC") ? 2 : (_isICCword(tag, "gTRC") ? 1 : 0);
                    /* "curv": linear, gamma power or LUT */
                    if (_isICCword(_decodeICC(data, size, ref offset, 4), "curv"))
                    {
                        LodePNGICCCurve trc = icc.trc[channel];
                        icc.has_trc = 1;
                        offset += 4; /* skip reserved */
                        uint count = _decodeICC(data, size, ref offset, 4);
                        if (count == 0)
                        {
                            trc.type = 0; /* linear */
                        }
                        else if (count == 1)
                        {
                            trc.type = 2; /* gamma */
                            trc.gamma = _decodeICC(data, size, ref offset, 2) / 256.0f;
                        }
                        else
                        {
                            trc.type = 1; /* LUT */
                            if ((long)offset + count * 2L > size || count > 16777216) return 1; /* also avoid crazy count */
                            trc.lut = new float[count];
                            trc.lut_size = (int)count;
                            for (uint j = 0; j < count; j++)
                            {
                                trc.lut[j] = _decodeICC(data, size, ref offset, 2) * (1.0f / 65535.0f);
                            }
                        }
                    }
                    /* TODO: verify: does the "chrm" tag participate in computation so should be parsed? */
                }
                /* Return error if any parse went beyond the filesize. Note that the
                parsing itself was always safe since it bound-checks inside. */
                if (offset > size) return 1;
            }

            return 0;
        }

        /* Multiplies 3 vector values with 3x3 matrix */
        private static void _mulMatrix(out float x2, out float y2, out float z2, in Matrix m, float x, float y, float z)
        {
            x2 = TvgMath.MultiplyAdd(z, m.e13, TvgMath.MultiplyAdd(x, m.e11, y * m.e12));
            y2 = TvgMath.MultiplyAdd(z, m.e23, TvgMath.MultiplyAdd(x, m.e21, y * m.e22));
            z2 = TvgMath.MultiplyAdd(z, m.e33, TvgMath.MultiplyAdd(x, m.e31, y * m.e32));
        }

        /* Get the matrix to go from linear RGB to XYZ given the RGB whitepoint and chromaticities in XYZ colorspace */
        private static uint _getChrmMatrixXYZ(out Matrix m, in Matrix rgb, ReadOnlySpan<float> white)
        {
            float rs, gs, bs;
            if (!TvgMath.Inverse(rgb, out m)) return 1; /* error, not invertible */
            _mulMatrix(out rs, out gs, out bs, m, white[0], white[1], white[2]);
            m = new Matrix(
                rs * rgb.e11, gs * rgb.e12, bs * rgb.e13,
                rs * rgb.e21, gs * rgb.e22, bs * rgb.e23,
                rs * rgb.e31, gs * rgb.e32, bs * rgb.e33
            );
            return 0;
        }

        // Returns a matrix adapting source whitepoint 0 to destination whitepoint 1.
        // LodePNG offers XYZ scaling, Bradford, and Von Kries but effectively uses Bradford, so this path is fixed to it.
        private static void _getAdaptationMatrix(out Matrix m,
            float wx0, float wy0, float wz0,
            float wx1, float wy1, float wz1)
        {
            Matrix bradford = new Matrix(
                0.8951f, 0.2664f, -0.1614f,
                -0.7502f, 1.7135f, 0.0367f,
                0.0389f, -0.0685f, 1.0296f
            );
            Matrix bradfordInv = new Matrix(
                0.9869929f, -0.1470543f, 0.1599627f,
                0.4323053f, 0.5183603f, 0.0492912f,
                -0.0085287f, 0.0400428f, 0.9684867f
            );
            float rho0, gam0, bet0, rho1, gam1, bet1, rho2, gam2, bet2;
            _mulMatrix(out rho0, out gam0, out bet0, bradford, wx0, wy0, wz0);
            _mulMatrix(out rho1, out gam1, out bet1, bradford, wx1, wy1, wz1);
            rho2 = rho1 / rho0;
            gam2 = gam1 / gam0;
            bet2 = bet1 / bet0;
            Matrix scaled = new Matrix(
                rho2 * bradford.e11, rho2 * bradford.e12, rho2 * bradford.e13,
                gam2 * bradford.e21, gam2 * bradford.e22, gam2 * bradford.e23,
                bet2 * bradford.e31, bet2 * bradford.e32, bet2 * bradford.e33
            );
            m = TvgMath.Multiply(bradfordInv, scaled);
        }

        /* validate whether the ICC profile is supported here for PNG */
        private static uint _validateICC(LodePNGICC icc)
        {
            return icc.has_chromaticity != 0 && icc.has_whitepoint != 0 && icc.has_trc != 0 ? 1u : 0u;
        }

        /* Returns chromaticity matrix for given ICC profile, adapted from ICC's
           global illuminant as necessary.
           Also returns the profile's whitepoint. */
        private static uint _getICCChrm(out Matrix m, Span<float> whitepoint, LodePNGICC icc)
        {
            /* Adaptation matrix a.
            This is an adaptation needed for ICC's file format (due to it using
            an internal global illuminant unrelated to the actual images) */
            Matrix a;
            /* If the profile has chromatic adaptation matrix "chad", use that one,
            else compute it from the illuminant and whitepoint. */
            if (icc.has_chad != 0)
            {
                if (!TvgMath.Inverse(icc.chad, out a))
                {
                    m = default;
                    return 1;
                }
            }
            else
            {
                _getAdaptationMatrix(out a, icc.illuminant[0], icc.illuminant[1], icc.illuminant[2],
                    icc.white[0], icc.white[1], icc.white[2]);
            }
            /* If the profile has a chad, then also the RGB's whitepoint must also be adapted from it (and the one
            given is normally D50). If it did not have a chad, then the whitepoint given is already the adapted one. */
            if (icc.has_chad != 0)
            {
                _mulMatrix(out whitepoint[0], out whitepoint[1], out whitepoint[2], a,
                    icc.white[0], icc.white[1], icc.white[2]);
            }
            else
            {
                whitepoint[0] = icc.white[0];
                whitepoint[1] = icc.white[1];
                whitepoint[2] = icc.white[2];
            }

            Matrix rgb = TvgMath.Multiply(a, icc.rgb);

            if (_getChrmMatrixXYZ(out m, rgb, whitepoint) != 0) return 1;
            return 0; /* success */
        }

        // RGBA8 iCCP specialization of LodePNG's convertToXYZ() setup path.
        private static uint _convertToXYZ_setup(out Matrix source, Span<float> whitepoint,
            Span<float> gammatable, LodePNGState state)
        {
            var icc = new LodePNGICC();
            byte[]? profile = state.info_png.iccp_profile;
            uint error = profile == null ? 1 : _parseICC(icc, profile, state.info_png.iccp_profile_size);
            if (error != 0 || _validateICC(icc) == 0)
            {
                source = default;
                return 1; /* corrupted ICC profile */
            }

            // Skip convertToXYZ()'s lodepng_convert(); lodepng_decode() already
            // normalized the source image to the required RGBA8 buffer.

            // Expanded from convertToXYZ()'s "Handle transfer function" block.
            for (int c = 0; c < 3; c++)
            {
                // convertToXYZ_gamma_table, n = 256 (8bit depth), use_icc path
                for (int i = 0; i < 256; i++)
                {
                    gammatable[c * 256 + i] = _iccForwardTRC(icc.trc[c], i * (1.0f / 255.0f));
                }
            }

            // see convertToXYZ_chrm -> getChrm only use getICChrm (see use_icc path)
            return _getICCChrm(out source, whitepoint, icc);
        }

        // Fixed sRGB specialization of LodePNG's convertFromXYZ() setup path.
        // target is convertFromXYZ_chrm()'s "XYZ to linear RGB matrix".
        private static uint _convertFromXYZ_setup(out Matrix target, ReadOnlySpan<float> whitepoint)
        {
            // From getChrm(): "the standard linear sRGB to XYZ matrix".
            Matrix srgb = new Matrix(
                0.4124564f, 0.3575761f, 0.1804375f,
                0.2126729f, 0.7151522f, 0.0721750f,
                0.0193339f, 0.1191920f, 0.9503041f
            );
            // From getChrm(): sRGB's xyY whitepoint (0.3127, 0.3290, 1) in XYZ.
            ReadOnlySpan<float> srgbWhitepoint = [0.9504559270516716f, 1.0f, 1.0890577507598784f];

            // Invert sRGB-to-XYZ into XYZ-to-linear-sRGB.
            if (!TvgMath.Inverse(srgb, out target)) return 1;

            /* for relative rendering intent (any except absolute "3"), must whitepoint adapt to the original whitepoint.
            this also preserves neutral RGB values (with absolute, they could become e.g. blue or sepia) */
            Matrix adaptation;
            /* "white" = absolute whitepoint of the new target RGB space, read from the target color profile.
               "whitepoint" is original absolute whitepoint (input as parameter of this function) of an
               RGB space the XYZ data once had before it was converted to XYZ, in other words the whitepoint that
               we want to adapt our current data to to make sure values that had equal R==G==B in the old space have
               the same property now (white and neutral tones stay neutral).
               Note: "absolute" whitepoint above means, can be used as-is, not needing further adaptation itself like icc.white does.*/
            _getAdaptationMatrix(out adaptation,
                whitepoint[0], whitepoint[1], whitepoint[2],
                srgbWhitepoint[0], srgbWhitepoint[1], srgbWhitepoint[2]);

            /* multiply the from xyz matrix with the adaptation matrix: in total,
            the resulting matrix first adapts in XYZ space, then converts to RGB*/
            target = TvgMath.Multiply(target, adaptation);
            return 0;
        }

        // RGBA8 single-pixel specialization of LodePNG's RGB model conversion.
        private static void _convertRGBPixel(byte[] outBuf, int outOffset, byte[] inBuf, int inOffset,
            in Matrix transform, ReadOnlySpan<float> gammatable)
        {
            _mulMatrix(out float r, out float g, out float b, transform,
                gammatable[inBuf[inOffset]], gammatable[256 + inBuf[inOffset + 1]], gammatable[512 + inBuf[inOffset + 2]]);
            Span<float> rgb = stackalloc float[3] { r, g, b };

            for (int c = 0; c < 3; c++)
            {
                // This expands convertFromXYZ()'s "Handle transfer function" call to
                // convertFromXYZ_gamma(); its float input uses powf() instead of a lookup table.
                float v = rgb[c];
                v = (v < 0.0031308f) ? (v * 12.92f) : TvgMath.MultiplyAdd(1.055f, MathF.Pow(v, 1 / 2.4f), -0.055f);

                outBuf[outOffset + c] = (byte)TvgMath.MultiplyAdd(255.0f, Math.Min(Math.Max(0.0f, v), 1.0f), 0.5f);
            }
            outBuf[outOffset + 3] = inBuf[inOffset + 3];
        }

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        // This customizes LodePNG's generic color conversion for ThorVG's fixed RGBA8 input and sRGB output.
        internal static uint ToSrgb(byte[] outBuf, byte[] inBuf, uint w, uint h, LodePNGState state_in)
        {
            try { return ToSrgbCore(outBuf, inBuf, w, h, state_in); }
            catch (OutOfMemoryException) { return 1; } // Native ICC LUT allocation failure.
        }

        private static uint ToSrgbCore(byte[] outBuf, byte[] inBuf, uint w, uint h, LodePNGState state_in)
        {
            // Skip the temporary sRGB output state. Upstream copies info_raw only
            // to preserve the input format; ThorVG always outputs RGBA8 sRGB, 8bit.

            // --- start of the expanded convertRGBModel() path ---

            // Expand convertRGBModel() into setup and per-pixel stages to avoid
            // its full-image buffers; lodepng_decode() already provides RGBA8.

            // Skip modelsEqual(); this function is called only for iCCP input,
            // which upstream always treats as different from the default sRGB target.

            Matrix source;
            Span<float> whitepoint = stackalloc float[3];
            Span<float> gammatable = stackalloc float[3 * 256]; // 256 = 8bit depth
            uint error = _convertToXYZ_setup(out source, whitepoint, gammatable, state_in);
            if (error != 0) return error;

            error = _convertFromXYZ_setup(out Matrix transform, whitepoint);
            if (error != 0) return error;

            transform = TvgMath.Multiply(transform, source); // combine transforms once for the pixel path

            long n = (long)w * h;
            for (long i = 0; i < n; i++)
            {
                int offset = (int)(i * 4);
                _convertRGBPixel(outBuf, offset, inBuf, offset, transform, gammatable);
            }

            return 0;
        }
    }
}
