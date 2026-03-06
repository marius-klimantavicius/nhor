// Ported from ThorVG/src/loaders/jpg/tvgJpgd.h and tvgJpgd.cpp
// Line-by-line port of the embedded jpgd baseline/progressive JPEG decoder.
// Public domain original by Rich Geldreich <richgel99@gmail.com>

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    #region Enums and Constants

    internal enum jpgd_status
    {
        JPGD_SUCCESS = 0, JPGD_FAILED = -1, JPGD_DONE = 1,
        JPGD_BAD_DHT_COUNTS = -256, JPGD_BAD_DHT_INDEX, JPGD_BAD_DHT_MARKER, JPGD_BAD_DQT_MARKER, JPGD_BAD_DQT_TABLE,
        JPGD_BAD_PRECISION, JPGD_BAD_HEIGHT, JPGD_BAD_WIDTH, JPGD_TOO_MANY_COMPONENTS,
        JPGD_BAD_SOF_LENGTH, JPGD_BAD_VARIABLE_MARKER, JPGD_BAD_DRI_LENGTH, JPGD_BAD_SOS_LENGTH,
        JPGD_BAD_SOS_COMP_ID, JPGD_W_EXTRA_BYTES_BEFORE_MARKER, JPGD_NO_ARITHMETIC_SUPPORT, JPGD_UNEXPECTED_MARKER,
        JPGD_NOT_JPEG, JPGD_UNSUPPORTED_MARKER, JPGD_BAD_DQT_LENGTH, JPGD_TOO_MANY_BLOCKS,
        JPGD_UNDEFINED_QUANT_TABLE, JPGD_UNDEFINED_HUFF_TABLE, JPGD_NOT_SINGLE_SCAN, JPGD_UNSUPPORTED_COLORSPACE,
        JPGD_UNSUPPORTED_SAMP_FACTORS, JPGD_DECODE_ERROR, JPGD_BAD_RESTART_MARKER, JPGD_ASSERTION_ERROR,
        JPGD_BAD_SOS_SPECTRAL, JPGD_BAD_SOS_SUCCESSIVE, JPGD_STREAM_READ, JPGD_NOTENOUGHMEM
    }

    internal enum JPEG_MARKER
    {
        M_SOF0  = 0xC0, M_SOF1  = 0xC1, M_SOF2  = 0xC2, M_SOF3  = 0xC3, M_SOF5  = 0xC5, M_SOF6  = 0xC6, M_SOF7  = 0xC7, M_JPG   = 0xC8,
        M_SOF9  = 0xC9, M_SOF10 = 0xCA, M_SOF11 = 0xCB, M_SOF13 = 0xCD, M_SOF14 = 0xCE, M_SOF15 = 0xCF, M_DHT   = 0xC4, M_DAC   = 0xCC,
        M_RST0  = 0xD0, M_RST1  = 0xD1, M_RST2  = 0xD2, M_RST3  = 0xD3, M_RST4  = 0xD4, M_RST5  = 0xD5, M_RST6  = 0xD6, M_RST7  = 0xD7,
        M_SOI   = 0xD8, M_EOI   = 0xD9, M_SOS   = 0xDA, M_DQT   = 0xDB, M_DNL   = 0xDC, M_DRI   = 0xDD, M_DHP   = 0xDE, M_EXP   = 0xDF,
        M_APP0  = 0xE0, M_APP15 = 0xEF, M_JPG0  = 0xF0, M_JPG13 = 0xFD, M_COM   = 0xFE, M_TEM   = 0x01, M_ERROR = 0x100, RST0   = 0xD0
    }

    internal enum JPEG_SUBSAMPLING { JPGD_GRAYSCALE = 0, JPGD_YH1V1, JPGD_YH2V1, JPGD_YH1V2, JPGD_YH2V2 }

    #endregion

    #region Stream classes

    internal abstract class jpeg_decoder_stream
    {
        public abstract int read(byte[] pBuf, int offset, int max_bytes_to_read, out bool pEOF_flag);
    }

    internal class jpeg_decoder_file_stream : jpeg_decoder_stream
    {
        private FileStream? m_pFile;
        private bool m_eof_flag;
        private bool m_error_flag;

        public bool open(string filename)
        {
            close();
            m_eof_flag = false;
            m_error_flag = false;
            try
            {
                m_pFile = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void close()
        {
            m_pFile?.Dispose();
            m_pFile = null;
            m_eof_flag = false;
            m_error_flag = false;
        }

        public override int read(byte[] pBuf, int offset, int max_bytes_to_read, out bool pEOF_flag)
        {
            pEOF_flag = false;
            if (m_pFile == null) return -1;
            if (m_eof_flag) { pEOF_flag = true; return 0; }
            if (m_error_flag) return -1;

            try
            {
                int bytes_read = m_pFile.Read(pBuf, offset, max_bytes_to_read);
                if (bytes_read < max_bytes_to_read)
                {
                    m_eof_flag = true;
                    pEOF_flag = true;
                }
                return bytes_read;
            }
            catch
            {
                m_error_flag = true;
                return -1;
            }
        }
    }

    internal class jpeg_decoder_mem_stream : jpeg_decoder_stream
    {
        private byte[]? m_pSrc_data;
        private int m_ofs;
        private int m_size;

        public jpeg_decoder_mem_stream() { }

        public jpeg_decoder_mem_stream(byte[] pSrc_data, int size)
        {
            m_pSrc_data = pSrc_data;
            m_ofs = 0;
            m_size = size;
        }

        public override int read(byte[] pBuf, int offset, int max_bytes_to_read, out bool pEOF_flag)
        {
            pEOF_flag = false;
            if (m_pSrc_data == null) return -1;

            int bytes_remaining = m_size - m_ofs;
            if (max_bytes_to_read > bytes_remaining)
            {
                max_bytes_to_read = bytes_remaining;
                pEOF_flag = true;
            }
            Array.Copy(m_pSrc_data, m_ofs, pBuf, offset, max_bytes_to_read);
            m_ofs += max_bytes_to_read;
            return max_bytes_to_read;
        }
    }

    #endregion

    #region IDCT helpers

    internal static class JpgdIdct
    {
        private const int CONST_BITS = 13;
        private const int PASS1_BITS = 2;
        private const int SCALEDONE = 1;

        private const int FIX_0_298631336 = 2446;
        private const int FIX_0_390180644 = 3196;
        private const int FIX_0_541196100 = 4433;
        private const int FIX_0_765366865 = 6270;
        private const int FIX_0_899976223 = 7373;
        private const int FIX_1_175875602 = 9633;
        private const int FIX_1_501321110 = 12299;
        private const int FIX_1_847759065 = 15137;
        private const int FIX_1_961570560 = 16069;
        private const int FIX_2_053119869 = 16819;
        private const int FIX_2_562915447 = 20995;
        private const int FIX_3_072711026 = 25172;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DESCALE(int x, int n) => (x + (SCALEDONE << (n - 1))) >> n;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DESCALE_ZEROSHIFT(int x, int n) => (x + (128 << n) + (SCALEDONE << (n - 1))) >> n;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte CLAMP(int i) => (byte)(((uint)i > 255) ? ((~i >> 31) & 0xFF) : i);

        // Row IDCT - generic version for nonzero_cols columns
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RowIdctGeneric(Span<int> pTemp, ReadOnlySpan<short> pSrc, int nonzero_cols)
        {
            int z2 = (2 < nonzero_cols) ? pSrc[2] : 0;
            int z3 = (6 < nonzero_cols) ? pSrc[6] : 0;
            int z1 = (z2 + z3) * FIX_0_541196100;
            int tmp2 = z1 + z3 * (-FIX_1_847759065);
            int tmp3 = z1 + z2 * FIX_0_765366865;

            int col0 = (0 < nonzero_cols) ? pSrc[0] : 0;
            int col4 = (4 < nonzero_cols) ? pSrc[4] : 0;
            int tmp0 = (int)((uint)(col0 + col4) << CONST_BITS);
            int tmp1 = (int)((uint)(col0 - col4) << CONST_BITS);

            int tmp10 = tmp0 + tmp3, tmp13 = tmp0 - tmp3, tmp11 = tmp1 + tmp2, tmp12 = tmp1 - tmp2;

            int atmp0 = (7 < nonzero_cols) ? pSrc[7] : 0;
            int atmp1 = (5 < nonzero_cols) ? pSrc[5] : 0;
            int atmp2 = (3 < nonzero_cols) ? pSrc[3] : 0;
            int atmp3 = (1 < nonzero_cols) ? pSrc[1] : 0;

            int bz1 = atmp0 + atmp3, bz2 = atmp1 + atmp2, bz3 = atmp0 + atmp2, bz4 = atmp1 + atmp3;
            int bz5 = (bz3 + bz4) * FIX_1_175875602;

            int az1 = bz1 * (-FIX_0_899976223);
            int az2 = bz2 * (-FIX_2_562915447);
            int az3 = bz3 * (-FIX_1_961570560) + bz5;
            int az4 = bz4 * (-FIX_0_390180644) + bz5;

            int btmp0 = atmp0 * FIX_0_298631336 + az1 + az3;
            int btmp1 = atmp1 * FIX_2_053119869 + az2 + az4;
            int btmp2 = atmp2 * FIX_3_072711026 + az2 + az3;
            int btmp3 = atmp3 * FIX_1_501321110 + az1 + az4;

            pTemp[0] = DESCALE(tmp10 + btmp3, CONST_BITS - PASS1_BITS);
            pTemp[7] = DESCALE(tmp10 - btmp3, CONST_BITS - PASS1_BITS);
            pTemp[1] = DESCALE(tmp11 + btmp2, CONST_BITS - PASS1_BITS);
            pTemp[6] = DESCALE(tmp11 - btmp2, CONST_BITS - PASS1_BITS);
            pTemp[2] = DESCALE(tmp12 + btmp1, CONST_BITS - PASS1_BITS);
            pTemp[5] = DESCALE(tmp12 - btmp1, CONST_BITS - PASS1_BITS);
            pTemp[3] = DESCALE(tmp13 + btmp0, CONST_BITS - PASS1_BITS);
            pTemp[4] = DESCALE(tmp13 - btmp0, CONST_BITS - PASS1_BITS);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RowIdct0(Span<int> pTemp, ReadOnlySpan<short> pSrc)
        {
            // Row<0>::idct - do nothing
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RowIdct1(Span<int> pTemp, ReadOnlySpan<short> pSrc)
        {
            int dcval = pSrc[0] * PASS1_BITS * 2;
            pTemp[0] = dcval; pTemp[1] = dcval; pTemp[2] = dcval; pTemp[3] = dcval;
            pTemp[4] = dcval; pTemp[5] = dcval; pTemp[6] = dcval; pTemp[7] = dcval;
        }

        // Col IDCT - generic version for nonzero_rows rows
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ColIdctGeneric(Span<byte> pDst_ptr, ReadOnlySpan<int> pTemp, int nonzero_rows)
        {
            int z2 = (2 < nonzero_rows) ? pTemp[2 * 8] : 0;
            int z3 = (6 < nonzero_rows) ? pTemp[6 * 8] : 0;
            int z1 = (z2 + z3) * FIX_0_541196100;
            int tmp2 = z1 + z3 * (-FIX_1_847759065);
            int tmp3 = z1 + z2 * FIX_0_765366865;

            int row0 = (0 < nonzero_rows) ? pTemp[0 * 8] : 0;
            int row4 = (4 < nonzero_rows) ? pTemp[4 * 8] : 0;
            int tmp0 = (int)((uint)(row0 + row4) << CONST_BITS);
            int tmp1 = (int)((uint)(row0 - row4) << CONST_BITS);

            int tmp10 = tmp0 + tmp3, tmp13 = tmp0 - tmp3, tmp11 = tmp1 + tmp2, tmp12 = tmp1 - tmp2;

            int atmp0 = (7 < nonzero_rows) ? pTemp[7 * 8] : 0;
            int atmp1 = (5 < nonzero_rows) ? pTemp[5 * 8] : 0;
            int atmp2 = (3 < nonzero_rows) ? pTemp[3 * 8] : 0;
            int atmp3 = (1 < nonzero_rows) ? pTemp[1 * 8] : 0;

            int bz1 = atmp0 + atmp3, bz2 = atmp1 + atmp2, bz3 = atmp0 + atmp2, bz4 = atmp1 + atmp3;
            int bz5 = (bz3 + bz4) * FIX_1_175875602;

            int az1 = bz1 * (-FIX_0_899976223);
            int az2 = bz2 * (-FIX_2_562915447);
            int az3 = bz3 * (-FIX_1_961570560) + bz5;
            int az4 = bz4 * (-FIX_0_390180644) + bz5;

            int btmp0 = atmp0 * FIX_0_298631336 + az1 + az3;
            int btmp1 = atmp1 * FIX_2_053119869 + az2 + az4;
            int btmp2 = atmp2 * FIX_3_072711026 + az2 + az3;
            int btmp3 = atmp3 * FIX_1_501321110 + az1 + az4;

            int i;
            i = DESCALE_ZEROSHIFT(tmp10 + btmp3, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 0] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp10 - btmp3, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 7] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp11 + btmp2, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 1] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp11 - btmp2, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 6] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp12 + btmp1, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 2] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp12 - btmp1, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 5] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp13 + btmp0, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 3] = CLAMP(i);
            i = DESCALE_ZEROSHIFT(tmp13 - btmp0, CONST_BITS + PASS1_BITS + 3); pDst_ptr[8 * 4] = CLAMP(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ColIdct1(Span<byte> pDst_ptr, ReadOnlySpan<int> pTemp)
        {
            int dcval = DESCALE_ZEROSHIFT(pTemp[0], PASS1_BITS + 3);
            byte dcval_clamped = CLAMP(dcval);
            pDst_ptr[0 * 8] = dcval_clamped; pDst_ptr[1 * 8] = dcval_clamped;
            pDst_ptr[2 * 8] = dcval_clamped; pDst_ptr[3 * 8] = dcval_clamped;
            pDst_ptr[4 * 8] = dcval_clamped; pDst_ptr[5 * 8] = dcval_clamped;
            pDst_ptr[6 * 8] = dcval_clamped; pDst_ptr[7 * 8] = dcval_clamped;
        }

        private static readonly byte[] s_idct_row_table = {
            1,0,0,0,0,0,0,0, 2,0,0,0,0,0,0,0, 2,1,0,0,0,0,0,0, 2,1,1,0,0,0,0,0, 2,2,1,0,0,0,0,0, 3,2,1,0,0,0,0,0, 4,2,1,0,0,0,0,0, 4,3,1,0,0,0,0,0,
            4,3,2,0,0,0,0,0, 4,3,2,1,0,0,0,0, 4,3,2,1,1,0,0,0, 4,3,2,2,1,0,0,0, 4,3,3,2,1,0,0,0, 4,4,3,2,1,0,0,0, 5,4,3,2,1,0,0,0, 6,4,3,2,1,0,0,0,
            6,5,3,2,1,0,0,0, 6,5,4,2,1,0,0,0, 6,5,4,3,1,0,0,0, 6,5,4,3,2,0,0,0, 6,5,4,3,2,1,0,0, 6,5,4,3,2,1,1,0, 6,5,4,3,2,2,1,0, 6,5,4,3,3,2,1,0,
            6,5,4,4,3,2,1,0, 6,5,5,4,3,2,1,0, 6,6,5,4,3,2,1,0, 7,6,5,4,3,2,1,0, 8,6,5,4,3,2,1,0, 8,7,5,4,3,2,1,0, 8,7,6,4,3,2,1,0, 8,7,6,5,3,2,1,0,
            8,7,6,5,4,2,1,0, 8,7,6,5,4,3,1,0, 8,7,6,5,4,3,2,0, 8,7,6,5,4,3,2,1, 8,7,6,5,4,3,2,2, 8,7,6,5,4,3,3,2, 8,7,6,5,4,4,3,2, 8,7,6,5,5,4,3,2,
            8,7,6,6,5,4,3,2, 8,7,7,6,5,4,3,2, 8,8,7,6,5,4,3,2, 8,8,8,6,5,4,3,2, 8,8,8,7,5,4,3,2, 8,8,8,7,6,4,3,2, 8,8,8,7,6,5,3,2, 8,8,8,7,6,5,4,2,
            8,8,8,7,6,5,4,3, 8,8,8,7,6,5,4,4, 8,8,8,7,6,5,5,4, 8,8,8,7,6,6,5,4, 8,8,8,7,7,6,5,4, 8,8,8,8,7,6,5,4, 8,8,8,8,8,6,5,4, 8,8,8,8,8,7,5,4,
            8,8,8,8,8,7,6,4, 8,8,8,8,8,7,6,5, 8,8,8,8,8,7,6,6, 8,8,8,8,8,7,7,6, 8,8,8,8,8,8,7,6, 8,8,8,8,8,8,8,6, 8,8,8,8,8,8,8,7, 8,8,8,8,8,8,8,8,
        };

        private static readonly byte[] s_idct_col_table = {
            1, 1, 2, 3, 3, 3, 3, 3, 3, 4, 5, 5, 5, 5, 5, 5,
            5, 5, 5, 5, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DispatchRowIdct(Span<int> pTemp, ReadOnlySpan<short> pSrc, int ncols)
        {
            switch (ncols)
            {
                case 0: RowIdct0(pTemp, pSrc); break;
                case 1: RowIdct1(pTemp, pSrc); break;
                default: RowIdctGeneric(pTemp, pSrc, ncols); break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DispatchColIdct(Span<byte> pDst, ReadOnlySpan<int> pTemp, int nrows)
        {
            switch (nrows)
            {
                case 1: ColIdct1(pDst, pTemp); break;
                default: ColIdctGeneric(pDst, pTemp, nrows); break;
            }
        }

        public static void idct(ReadOnlySpan<short> pSrc_ptr, Span<byte> pDst_ptr, int block_max_zag)
        {
            if (block_max_zag <= 1)
            {
                int k = ((pSrc_ptr[0] + 4) >> 3) + 128;
                k = (int)(((uint)k > 255) ? ((~k >> 31) & 0xFF) : k);
                byte kb = (byte)k;
                for (int i = 0; i < 64; i++) pDst_ptr[i] = kb;
                return;
            }

            Span<int> temp = stackalloc int[64];
            int rowTabOfs = (block_max_zag - 1) * 8;

            for (int i = 0; i < 8; i++)
            {
                DispatchRowIdct(temp.Slice(i * 8), pSrc_ptr.Slice(i * 8), s_idct_row_table[rowTabOfs + i]);
            }

            int nonzero_rows = s_idct_col_table[block_max_zag - 1];
            for (int i = 0; i < 8; i++)
            {
                // Build a column view: pTemp[0], pTemp[8], pTemp[16], ...
                // We pass the temp slice starting at column i and col dispatch reads pTemp[x*8]
                DispatchColIdct(pDst_ptr.Slice(i), temp.Slice(i), nonzero_rows);
            }
        }
    }

    #endregion

    #region jpeg_decoder

    internal delegate bool pDecode_block_func(jpeg_decoder pD, int component_id, int block_x, int block_y);

    internal class huff_tables
    {
        public bool ac_table;
        public uint[] look_up = new uint[256];
        public uint[] look_up2 = new uint[256];
        public byte[] code_size = new byte[256];
        public uint[] tree = new uint[512];
    }

    internal class coeff_buf
    {
        public short[]? pData;
        public int block_num_x, block_num_y;
        public int block_len_x, block_len_y;
        public int block_size; // in shorts
    }

    internal class jpeg_decoder
    {
        private const int JPGD_IN_BUF_SIZE = 8192;
        private const int JPGD_MAX_BLOCKS_PER_MCU = 10;
        private const int JPGD_MAX_HUFF_TABLES = 8;
        private const int JPGD_MAX_QUANT_TABLES = 4;
        private const int JPGD_MAX_COMPONENTS = 4;
        private const int JPGD_MAX_COMPS_IN_SCAN = 4;
        private const int JPGD_MAX_BLOCKS_PER_ROW = 8192;
        private const int JPGD_MAX_HEIGHT = 16384;
        private const int JPGD_MAX_WIDTH = 16384;

        private static readonly int[] g_ZAG = {
            0,1,8,16,9,2,3,10,17,24,32,25,18,11,4,5,12,19,26,33,40,48,41,34,27,20,13,6,7,14,21,28,35,42,49,56,57,50,43,36,29,22,15,23,30,37,44,51,58,59,52,45,38,31,39,46,53,60,61,54,47,55,62,63
        };

        private static readonly int[] s_extend_test = { 0, 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080, 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000 };
        private static readonly int[] s_extend_offset = new int[16];

        static jpeg_decoder()
        {
            for (int i = 0; i < 16; i++)
                s_extend_offset[i] = (int)(((~0u) << i) + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int JPGD_HUFF_EXTEND(int x, int s)
        {
            return (x < s_extend_test[s & 15]) ? (x + s_extend_offset[s & 15]) : x;
        }

        // --- member fields ---
        private int m_image_x_size;
        private int m_image_y_size;
        private jpeg_decoder_stream? m_pStream;
        private int m_progressive_flag;
        private byte[] m_huff_ac = new byte[JPGD_MAX_HUFF_TABLES];
        private byte[]?[] m_huff_num = new byte[JPGD_MAX_HUFF_TABLES][];
        private byte[]?[] m_huff_val = new byte[JPGD_MAX_HUFF_TABLES][];
        private short[]?[] m_quant = new short[JPGD_MAX_QUANT_TABLES][];
        private int m_scan_type;
        private int m_comps_in_frame;
        private int[] m_comp_h_samp = new int[JPGD_MAX_COMPONENTS];
        private int[] m_comp_v_samp = new int[JPGD_MAX_COMPONENTS];
        private int[] m_comp_quant = new int[JPGD_MAX_COMPONENTS];
        private int[] m_comp_ident = new int[JPGD_MAX_COMPONENTS];
        private int[] m_comp_h_blocks = new int[JPGD_MAX_COMPONENTS];
        private int[] m_comp_v_blocks = new int[JPGD_MAX_COMPONENTS];
        private int m_comps_in_scan;
        private int[] m_comp_list = new int[JPGD_MAX_COMPS_IN_SCAN];
        private int[] m_comp_dc_tab = new int[JPGD_MAX_COMPONENTS];
        private int[] m_comp_ac_tab = new int[JPGD_MAX_COMPONENTS];
        private int m_spectral_start;
        private int m_spectral_end;
        private int m_successive_low;
        private int m_successive_high;
        private int m_max_mcu_x_size;
        private int m_max_mcu_y_size;
        private int m_blocks_per_mcu;
        private int m_max_blocks_per_row;
        private int m_mcus_per_row, m_mcus_per_col;
        private int[] m_mcu_org = new int[JPGD_MAX_BLOCKS_PER_MCU];
        private int m_total_lines_left;
        private int m_mcu_lines_left;
        private int m_real_dest_bytes_per_scan_line;
        private int m_dest_bytes_per_scan_line;
        private int m_dest_bytes_per_pixel;
        private huff_tables?[] m_pHuff_tabs = new huff_tables[JPGD_MAX_HUFF_TABLES];
        private coeff_buf?[] m_dc_coeffs = new coeff_buf[JPGD_MAX_COMPONENTS];
        private coeff_buf?[] m_ac_coeffs = new coeff_buf[JPGD_MAX_COMPONENTS];
        private int m_eob_run;
        private int[] m_block_y_mcu = new int[JPGD_MAX_COMPONENTS];
        private int m_in_buf_ofs;
        private int m_in_buf_left;
        private int m_tem_flag;
        private bool m_eof_flag;
        private byte[] m_in_buf = new byte[JPGD_IN_BUF_SIZE + 256]; // extra padding
        private int m_bits_left;
        private uint m_bit_buf;
        private int m_restart_interval;
        private int m_restarts_left;
        private int m_next_restart_num;
        private int m_max_mcus_per_row;
        private int m_max_blocks_per_mcu;
        private int m_expanded_blocks_per_mcu;
        private int m_expanded_blocks_per_row;
        private int m_expanded_blocks_per_component;
        private int m_max_mcus_per_col;
        private uint[] m_last_dc_val = new uint[JPGD_MAX_COMPONENTS];
        private short[]? m_pMCU_coefficients;
        private int[] m_mcu_block_max_zag = new int[JPGD_MAX_BLOCKS_PER_MCU];
        private byte[]? m_pSample_buf;
        private int[] m_crr = new int[256];
        private int[] m_cbb = new int[256];
        private int[] m_crg = new int[256];
        private int[] m_cbg = new int[256];
        private byte[]? m_pScan_line_0;
        private byte[]? m_pScan_line_1;
        private jpgd_status m_error_code;
        private bool m_ready_flag;
        private int m_total_bytes_read;

        // --- public API ---
        public jpeg_decoder(jpeg_decoder_stream pStream)
        {
            decode_init(pStream);
        }

        public int begin_decoding()
        {
            if (m_ready_flag) return (int)jpgd_status.JPGD_SUCCESS;
            if (m_error_code != jpgd_status.JPGD_SUCCESS) return (int)jpgd_status.JPGD_FAILED;
            if (!decode_start()) return (int)jpgd_status.JPGD_FAILED;
            m_ready_flag = true;
            return (int)jpgd_status.JPGD_SUCCESS;
        }

        public int decode(out byte[]? pScan_line, out int scanLineOffset)
        {
            pScan_line = null;
            scanLineOffset = 0;

            if (m_error_code != jpgd_status.JPGD_SUCCESS || !m_ready_flag) return (int)jpgd_status.JPGD_FAILED;
            if (m_total_lines_left == 0) return (int)jpgd_status.JPGD_DONE;

            if (m_mcu_lines_left == 0)
            {
                if (m_progressive_flag != 0) load_next_row();
                else if (!decode_next_row()) return (int)jpgd_status.JPGD_FAILED;
                if (m_total_lines_left <= m_max_mcu_y_size) find_eoi();
                m_mcu_lines_left = m_max_mcu_y_size;
            }

            switch (m_scan_type)
            {
                case (int)JPEG_SUBSAMPLING.JPGD_YH2V2:
                    if ((m_mcu_lines_left & 1) == 0)
                    {
                        H2V2Convert();
                        pScan_line = m_pScan_line_0;
                    }
                    else pScan_line = m_pScan_line_1;
                    break;
                case (int)JPEG_SUBSAMPLING.JPGD_YH2V1:
                    H2V1Convert();
                    pScan_line = m_pScan_line_0;
                    break;
                case (int)JPEG_SUBSAMPLING.JPGD_YH1V2:
                    if ((m_mcu_lines_left & 1) == 0)
                    {
                        H1V2Convert();
                        pScan_line = m_pScan_line_0;
                    }
                    else pScan_line = m_pScan_line_1;
                    break;
                case (int)JPEG_SUBSAMPLING.JPGD_YH1V1:
                    H1V1Convert();
                    pScan_line = m_pScan_line_0;
                    break;
                case (int)JPEG_SUBSAMPLING.JPGD_GRAYSCALE:
                    gray_convert();
                    pScan_line = m_pScan_line_0;
                    break;
            }

            m_mcu_lines_left--;
            m_total_lines_left--;

            return (int)jpgd_status.JPGD_SUCCESS;
        }

        public jpgd_status get_error_code() => m_error_code;
        public int get_width() => m_image_x_size;
        public int get_height() => m_image_y_size;
        public int get_num_components() => m_comps_in_frame;
        public int get_bytes_per_pixel() => m_dest_bytes_per_pixel;
        public int get_bytes_per_scan_line() => m_image_x_size * get_bytes_per_pixel();
        public int get_total_bytes_read() => m_total_bytes_read;

        // --- inline helpers ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint get_char()
        {
            if (m_in_buf_left == 0)
            {
                if (!prep_in_buffer()) return 0xFF;
                if (m_in_buf_left == 0)
                {
                    int t = m_tem_flag;
                    m_tem_flag ^= 1;
                    return (t != 0) ? 0xD9u : 0xFFu;
                }
            }
            uint c = m_in_buf[m_in_buf_ofs++];
            m_in_buf_left--;
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint get_char(out bool pPadding_flag)
        {
            if (m_in_buf_left == 0)
            {
                if (!prep_in_buffer())
                {
                    pPadding_flag = false;
                    return 0xFF;
                }
                if (m_in_buf_left == 0)
                {
                    pPadding_flag = true;
                    int t = m_tem_flag;
                    m_tem_flag ^= 1;
                    return (t != 0) ? 0xD9u : 0xFFu;
                }
            }
            pPadding_flag = false;
            uint c = m_in_buf[m_in_buf_ofs++];
            m_in_buf_left--;
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void stuff_char(byte q)
        {
            m_in_buf[--m_in_buf_ofs] = q;
            m_in_buf_left++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte get_octet()
        {
            int c = (int)get_char(out bool padding_flag);
            if (c == 0xFF)
            {
                if (padding_flag) return 0xFF;
                c = (int)get_char(out padding_flag);
                if (padding_flag) { stuff_char(0xFF); return 0xFF; }
                if (c == 0x00) return 0xFF;
                else { stuff_char((byte)c); stuff_char(0xFF); return 0xFF; }
            }
            return (byte)c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint get_bits(int num_bits)
        {
            if (num_bits == 0) return 0;
            uint i = m_bit_buf >> (32 - num_bits);
            if ((m_bits_left -= num_bits) <= 0)
            {
                m_bit_buf <<= (num_bits += m_bits_left);
                uint c1 = get_char();
                uint c2 = get_char();
                m_bit_buf = (m_bit_buf & 0xFFFF0000) | (c1 << 8) | c2;
                m_bit_buf <<= -m_bits_left;
                m_bits_left += 16;
            }
            else m_bit_buf <<= num_bits;
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint get_bits_no_markers(int num_bits)
        {
            if (num_bits == 0) return 0;
            uint i = m_bit_buf >> (32 - num_bits);
            if ((m_bits_left -= num_bits) <= 0)
            {
                m_bit_buf <<= (num_bits += m_bits_left);
                if ((m_in_buf_left < 2) || (m_in_buf[m_in_buf_ofs] == 0xFF) || (m_in_buf[m_in_buf_ofs + 1] == 0xFF))
                {
                    uint c1 = get_octet();
                    uint c2 = get_octet();
                    m_bit_buf |= (c1 << 8) | c2;
                }
                else
                {
                    m_bit_buf |= ((uint)m_in_buf[m_in_buf_ofs] << 8) | m_in_buf[m_in_buf_ofs + 1];
                    m_in_buf_left -= 2;
                    m_in_buf_ofs += 2;
                }
                m_bit_buf <<= -m_bits_left;
                m_bits_left += 16;
            }
            else m_bit_buf <<= num_bits;
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int huff_decode(huff_tables pH)
        {
            int symbol;
            if ((symbol = (int)pH.look_up[m_bit_buf >> 24]) < 0)
            {
                int ofs = 23;
                do
                {
                    symbol = (int)pH.tree[-(int)(symbol + (int)((m_bit_buf >> ofs) & 1))];
                    ofs--;
                } while (symbol < 0);
                get_bits_no_markers(8 + (23 - ofs));
            }
            else get_bits_no_markers(pH.code_size[symbol]);
            return symbol;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int huff_decode(huff_tables pH, out int extra_bits)
        {
            int symbol;
            if ((symbol = (int)pH.look_up2[m_bit_buf >> 24]) < 0)
            {
                int ofs = 23;
                do
                {
                    symbol = (int)pH.tree[-(int)(symbol + (int)((m_bit_buf >> ofs) & 1))];
                    ofs--;
                } while (symbol < 0);
                get_bits_no_markers(8 + (23 - ofs));
                extra_bits = (int)get_bits_no_markers(symbol & 0xF);
            }
            else
            {
                if ((symbol & 0x8000) != 0)
                {
                    get_bits_no_markers((symbol >> 8) & 31);
                    extra_bits = symbol >> 16;
                }
                else
                {
                    int code_size = (symbol >> 8) & 31;
                    int num_extra_bits = symbol & 0xF;
                    int bits = code_size + num_extra_bits;
                    if (bits <= (m_bits_left + 16))
                        extra_bits = (int)get_bits_no_markers(bits) & ((1 << num_extra_bits) - 1);
                    else
                    {
                        get_bits_no_markers(code_size);
                        extra_bits = (int)get_bits_no_markers(num_extra_bits);
                    }
                }
                symbol &= 0xFF;
            }
            return symbol;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte clamp(int i)
        {
            if ((uint)i > 255) i = ((~i) >> 31) & 0xFF;
            return (byte)i;
        }

        // --- internal methods ---

        private bool stop_decoding(jpgd_status status)
        {
            m_error_code = jpgd_status.JPGD_FAILED;
            m_pStream = null;
            return false;
        }

        private static void word_clear(byte[] buf, int offset, ushort c, int n)
        {
            byte l = (byte)(c & 0xFF), h = (byte)((c >> 8) & 0xFF);
            int p = offset;
            while (n-- > 0) { buf[p++] = l; buf[p++] = h; }
        }

        private bool prep_in_buffer()
        {
            m_in_buf_left = 0;
            m_in_buf_ofs = 0;
            if (m_eof_flag) return true;

            do
            {
                var bytes_read = m_pStream!.read(m_in_buf, m_in_buf_left, JPGD_IN_BUF_SIZE - m_in_buf_left, out m_eof_flag);
                if (bytes_read == -1) return stop_decoding(jpgd_status.JPGD_STREAM_READ);
                m_in_buf_left += bytes_read;
            } while ((m_in_buf_left < JPGD_IN_BUF_SIZE) && !m_eof_flag);

            m_total_bytes_read += m_in_buf_left;

            // Pad with EOI marker
            word_clear(m_in_buf, m_in_buf_ofs + m_in_buf_left, 0xD9FF, 64);
            return true;
        }

        private bool read_dht_marker()
        {
            int i, index, count;
            byte[] huff_num = new byte[17];
            byte[] huff_val = new byte[256];
            uint num_left = get_bits(16);

            if (num_left < 2) return stop_decoding(jpgd_status.JPGD_BAD_DHT_MARKER);
            num_left -= 2;

            while (num_left > 0)
            {
                index = (int)get_bits(8);
                huff_num[0] = 0;
                count = 0;

                for (i = 1; i <= 16; i++)
                {
                    huff_num[i] = (byte)get_bits(8);
                    count += huff_num[i];
                }

                if (count > 255) return stop_decoding(jpgd_status.JPGD_BAD_DHT_COUNTS);

                for (i = 0; i < count; i++)
                    huff_val[i] = (byte)get_bits(8);

                i = 1 + 16 + count;
                if (num_left < (uint)i) return stop_decoding(jpgd_status.JPGD_BAD_DHT_MARKER);
                num_left -= (uint)i;

                if ((index & 0x10) > 0x10) return stop_decoding(jpgd_status.JPGD_BAD_DHT_INDEX);
                index = (index & 0x0F) + ((index & 0x10) >> 4) * (JPGD_MAX_HUFF_TABLES >> 1);
                if (index >= JPGD_MAX_HUFF_TABLES) return stop_decoding(jpgd_status.JPGD_BAD_DHT_INDEX);

                if (m_huff_num[index] == null) m_huff_num[index] = new byte[17];
                if (m_huff_val[index] == null) m_huff_val[index] = new byte[256];

                m_huff_ac[index] = (byte)(((index & 0x10) != 0) ? 1 : 0);
                Array.Copy(huff_num, m_huff_num[index]!, 17);
                Array.Copy(huff_val, m_huff_val[index]!, 256);
            }
            return true;
        }

        private bool read_dqt_marker()
        {
            int n, i, prec;
            uint temp;
            uint num_left = get_bits(16);
            if (num_left < 2) return stop_decoding(jpgd_status.JPGD_BAD_DQT_MARKER);
            num_left -= 2;

            while (num_left > 0)
            {
                n = (int)get_bits(8);
                prec = n >> 4;
                n &= 0x0F;
                if (n >= JPGD_MAX_QUANT_TABLES) return stop_decoding(jpgd_status.JPGD_BAD_DQT_TABLE);
                if (m_quant[n] == null) m_quant[n] = new short[64];

                for (i = 0; i < 64; i++)
                {
                    temp = get_bits(8);
                    if (prec != 0) temp = (temp << 8) + get_bits(8);
                    m_quant[n]![i] = (short)temp;
                }

                i = 64 + 1;
                if (prec != 0) i += 64;
                if (num_left < (uint)i) return stop_decoding(jpgd_status.JPGD_BAD_DQT_LENGTH);
                num_left -= (uint)i;
            }
            return true;
        }

        private bool read_sof_marker()
        {
            uint num_left = get_bits(16);
            if (get_bits(8) != 8) return stop_decoding(jpgd_status.JPGD_BAD_PRECISION);

            m_image_y_size = (int)get_bits(16);
            if (m_image_y_size < 1 || m_image_y_size > JPGD_MAX_HEIGHT) return stop_decoding(jpgd_status.JPGD_BAD_HEIGHT);

            m_image_x_size = (int)get_bits(16);
            if (m_image_x_size < 1 || m_image_x_size > JPGD_MAX_WIDTH) return stop_decoding(jpgd_status.JPGD_BAD_WIDTH);

            m_comps_in_frame = (int)get_bits(8);
            if (m_comps_in_frame > JPGD_MAX_COMPONENTS) return stop_decoding(jpgd_status.JPGD_TOO_MANY_COMPONENTS);
            if (num_left != (uint)(m_comps_in_frame * 3 + 8)) return stop_decoding(jpgd_status.JPGD_BAD_SOF_LENGTH);

            for (int i = 0; i < m_comps_in_frame; i++)
            {
                m_comp_ident[i] = (int)get_bits(8);
                m_comp_h_samp[i] = (int)get_bits(4);
                m_comp_v_samp[i] = (int)get_bits(4);
                m_comp_quant[i] = (int)get_bits(8);
            }
            return true;
        }

        private bool skip_variable_marker()
        {
            uint num_left = get_bits(16);
            if (num_left < 2) return stop_decoding(jpgd_status.JPGD_BAD_VARIABLE_MARKER);
            num_left -= 2;
            while (num_left > 0) { get_bits(8); num_left--; }
            return true;
        }

        private bool read_dri_marker()
        {
            if (get_bits(16) != 4) return stop_decoding(jpgd_status.JPGD_BAD_DRI_LENGTH);
            m_restart_interval = (int)get_bits(16);
            return true;
        }

        private bool read_sos_marker()
        {
            uint num_left = get_bits(16);
            int n = (int)get_bits(8);
            m_comps_in_scan = n;
            num_left -= 3;

            if (num_left != (uint)(n * 2 + 3) || n < 1 || n > JPGD_MAX_COMPS_IN_SCAN)
                return stop_decoding(jpgd_status.JPGD_BAD_SOS_LENGTH);

            for (int i = 0; i < n; i++)
            {
                int cc = (int)get_bits(8);
                int c = (int)get_bits(8);
                num_left -= 2;

                int ci;
                for (ci = 0; ci < m_comps_in_frame; ci++)
                    if (cc == m_comp_ident[ci]) break;

                if (ci >= m_comps_in_frame) return stop_decoding(jpgd_status.JPGD_BAD_SOS_COMP_ID);

                m_comp_list[i] = ci;
                m_comp_dc_tab[ci] = (c >> 4) & 15;
                m_comp_ac_tab[ci] = (c & 15) + (JPGD_MAX_HUFF_TABLES >> 1);
            }

            m_spectral_start = (int)get_bits(8);
            m_spectral_end = (int)get_bits(8);
            m_successive_high = (int)get_bits(4);
            m_successive_low = (int)get_bits(4);

            if (m_progressive_flag == 0)
            {
                m_spectral_start = 0;
                m_spectral_end = 63;
            }

            num_left -= 3;
            while (num_left > 0) { get_bits(8); num_left--; }
            return true;
        }

        private int next_marker()
        {
            uint c;
            do
            {
                do { c = get_bits(8); } while (c != 0xFF);
                do { c = get_bits(8); } while (c == 0xFF);
            } while (c == 0);
            return (int)c;
        }

        private int process_markers()
        {
            for (;;)
            {
                int c = next_marker();
                switch (c)
                {
                    case (int)JPEG_MARKER.M_SOF0:
                    case (int)JPEG_MARKER.M_SOF1:
                    case (int)JPEG_MARKER.M_SOF2:
                    case (int)JPEG_MARKER.M_SOF3:
                    case (int)JPEG_MARKER.M_SOF5:
                    case (int)JPEG_MARKER.M_SOF6:
                    case (int)JPEG_MARKER.M_SOF7:
                    case (int)JPEG_MARKER.M_SOF9:
                    case (int)JPEG_MARKER.M_SOF10:
                    case (int)JPEG_MARKER.M_SOF11:
                    case (int)JPEG_MARKER.M_SOF13:
                    case (int)JPEG_MARKER.M_SOF14:
                    case (int)JPEG_MARKER.M_SOF15:
                    case (int)JPEG_MARKER.M_SOI:
                    case (int)JPEG_MARKER.M_EOI:
                    case (int)JPEG_MARKER.M_SOS:
                        return c;
                    case (int)JPEG_MARKER.M_DHT:
                        if (read_dht_marker()) break;
                        else return (int)JPEG_MARKER.M_EOI;
                    case (int)JPEG_MARKER.M_DAC:
                        stop_decoding(jpgd_status.JPGD_NO_ARITHMETIC_SUPPORT);
                        return (int)JPEG_MARKER.M_EOI;
                    case (int)JPEG_MARKER.M_DQT:
                        if (read_dqt_marker()) break;
                        else return (int)JPEG_MARKER.M_EOI;
                    case (int)JPEG_MARKER.M_DRI:
                        if (read_dri_marker()) break;
                        else return (int)JPEG_MARKER.M_EOI;
                    case (int)JPEG_MARKER.M_JPG:
                    case (int)JPEG_MARKER.M_RST0:
                    case (int)JPEG_MARKER.M_RST1:
                    case (int)JPEG_MARKER.M_RST2:
                    case (int)JPEG_MARKER.M_RST3:
                    case (int)JPEG_MARKER.M_RST4:
                    case (int)JPEG_MARKER.M_RST5:
                    case (int)JPEG_MARKER.M_RST6:
                    case (int)JPEG_MARKER.M_RST7:
                    case (int)JPEG_MARKER.M_TEM:
                        stop_decoding(jpgd_status.JPGD_UNEXPECTED_MARKER);
                        return (int)JPEG_MARKER.M_EOI;
                    default:
                        if (skip_variable_marker()) break;
                        else return (int)JPEG_MARKER.M_EOI;
                }
            }
        }

        private bool locate_soi_marker()
        {
            uint lastchar = get_bits(8);
            uint thischar = get_bits(8);
            if (lastchar == 0xFF && thischar == (uint)JPEG_MARKER.M_SOI) return true;

            uint bytesleft = 4096;
            while (true)
            {
                if (--bytesleft == 0) return stop_decoding(jpgd_status.JPGD_NOT_JPEG);
                lastchar = thischar;
                thischar = get_bits(8);
                if (lastchar == 0xFF)
                {
                    if (thischar == (uint)JPEG_MARKER.M_SOI) break;
                    else if (thischar == (uint)JPEG_MARKER.M_EOI) return stop_decoding(jpgd_status.JPGD_NOT_JPEG);
                }
            }

            thischar = (m_bit_buf >> 24) & 0xFF;
            if (thischar != 0xFF) return stop_decoding(jpgd_status.JPGD_NOT_JPEG);
            return true;
        }

        private bool locate_sof_marker()
        {
            if (!locate_soi_marker()) return false;
            int c = process_markers();
            switch (c)
            {
                case (int)JPEG_MARKER.M_SOF2:
                    m_progressive_flag = 1;
                    return read_sof_marker();
                case (int)JPEG_MARKER.M_SOF0:
                case (int)JPEG_MARKER.M_SOF1:
                    return read_sof_marker();
                case (int)JPEG_MARKER.M_SOF9:
                    return stop_decoding(jpgd_status.JPGD_NO_ARITHMETIC_SUPPORT);
                default:
                    return stop_decoding(jpgd_status.JPGD_UNSUPPORTED_MARKER);
            }
        }

        private bool locate_sos_marker()
        {
            int c = process_markers();
            if (c == (int)JPEG_MARKER.M_EOI) return false;
            else if (c != (int)JPEG_MARKER.M_SOS) return stop_decoding(jpgd_status.JPGD_UNEXPECTED_MARKER);
            return read_sos_marker();
        }

        private void init(jpeg_decoder_stream pStream)
        {
            m_error_code = jpgd_status.JPGD_SUCCESS;
            m_ready_flag = false;
            m_image_x_size = 0;
            m_image_y_size = 0;
            m_pStream = pStream;
            m_progressive_flag = 0;

            Array.Clear(m_huff_ac);
            Array.Clear(m_huff_num);
            Array.Clear(m_huff_val);
            Array.Clear(m_quant);

            m_scan_type = 0;
            m_comps_in_frame = 0;
            Array.Clear(m_comp_h_samp); Array.Clear(m_comp_v_samp);
            Array.Clear(m_comp_quant); Array.Clear(m_comp_ident);
            Array.Clear(m_comp_h_blocks); Array.Clear(m_comp_v_blocks);

            m_comps_in_scan = 0;
            Array.Clear(m_comp_list);
            Array.Clear(m_comp_dc_tab); Array.Clear(m_comp_ac_tab);

            m_spectral_start = 0; m_spectral_end = 0;
            m_successive_low = 0; m_successive_high = 0;
            m_max_mcu_x_size = 0; m_max_mcu_y_size = 0;
            m_blocks_per_mcu = 0; m_max_blocks_per_row = 0;
            m_mcus_per_row = 0; m_mcus_per_col = 0;
            m_expanded_blocks_per_component = 0;
            m_expanded_blocks_per_mcu = 0;
            m_expanded_blocks_per_row = 0;

            Array.Clear(m_mcu_org);

            m_total_lines_left = 0; m_mcu_lines_left = 0;
            m_real_dest_bytes_per_scan_line = 0;
            m_dest_bytes_per_scan_line = 0;
            m_dest_bytes_per_pixel = 0;

            Array.Clear(m_pHuff_tabs);
            Array.Clear(m_dc_coeffs); Array.Clear(m_ac_coeffs);
            Array.Clear(m_block_y_mcu);

            m_eob_run = 0;

            m_in_buf_ofs = 0;
            m_in_buf_left = 0;
            m_eof_flag = false;
            m_tem_flag = 0;

            Array.Clear(m_in_buf);

            m_restart_interval = 0;
            m_restarts_left = 0;
            m_next_restart_num = 0;

            m_max_mcus_per_row = 0;
            m_max_blocks_per_mcu = 0;
            m_max_mcus_per_col = 0;

            Array.Clear(m_last_dc_val);
            m_pMCU_coefficients = null;
            m_pSample_buf = null;

            m_total_bytes_read = 0;
            m_pScan_line_0 = null;
            m_pScan_line_1 = null;

            prep_in_buffer();

            m_bits_left = 16;
            m_bit_buf = 0;
            get_bits(16);
            get_bits(16);

            for (int i = 0; i < JPGD_MAX_BLOCKS_PER_MCU; i++)
                m_mcu_block_max_zag[i] = 64;
        }

        private void create_look_ups()
        {
            const int SCALEBITS = 16;
            const int ONE_HALF = 1 << (SCALEBITS - 1);

            for (int i = 0; i <= 255; i++)
            {
                int k = i - 128;
                m_crr[i] = ((int)(1.40200f * (1 << SCALEBITS) + 0.5f) * k + ONE_HALF) >> SCALEBITS;
                m_cbb[i] = ((int)(1.77200f * (1 << SCALEBITS) + 0.5f) * k + ONE_HALF) >> SCALEBITS;
                m_crg[i] = (-(int)(0.71414f * (1 << SCALEBITS) + 0.5f)) * k;
                m_cbg[i] = (-(int)(0.34414f * (1 << SCALEBITS) + 0.5f)) * k + ONE_HALF;
            }
        }

        private void fix_in_buffer()
        {
            if (m_bits_left == 16) stuff_char((byte)(m_bit_buf & 0xFF));
            if (m_bits_left >= 8) stuff_char((byte)((m_bit_buf >> 8) & 0xFF));
            stuff_char((byte)((m_bit_buf >> 16) & 0xFF));
            stuff_char((byte)((m_bit_buf >> 24) & 0xFF));
            m_bits_left = 16;
            get_bits_no_markers(16);
            get_bits_no_markers(16);
        }

        private void transform_mcu(int mcu_row)
        {
            int srcOfs = 0;
            int dstOfs = mcu_row * m_blocks_per_mcu * 64;

            for (int mcu_block = 0; mcu_block < m_blocks_per_mcu; mcu_block++)
            {
                JpgdIdct.idct(
                    new ReadOnlySpan<short>(m_pMCU_coefficients, srcOfs, 64),
                    new Span<byte>(m_pSample_buf, dstOfs, 64),
                    m_mcu_block_max_zag[mcu_block]);
                srcOfs += 64;
                dstOfs += 64;
            }
        }

        private coeff_buf coeff_buf_open(int block_num_x, int block_num_y, int block_len_x, int block_len_y)
        {
            var cb = new coeff_buf();
            cb.block_num_x = block_num_x;
            cb.block_num_y = block_num_y;
            cb.block_len_x = block_len_x;
            cb.block_len_y = block_len_y;
            cb.block_size = block_len_x * block_len_y; // in shorts
            cb.pData = new short[cb.block_size * block_num_x * block_num_y];
            return cb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int coeff_buf_getp_offset(coeff_buf cb, int block_x, int block_y)
        {
            return block_x * cb.block_size + block_y * (cb.block_size * cb.block_num_x);
        }

        private void load_next_row()
        {
            int[] block_x_mcu = new int[JPGD_MAX_COMPONENTS];

            for (int mcu_row = 0; mcu_row < m_mcus_per_row; mcu_row++)
            {
                int block_x_mcu_ofs = 0, block_y_mcu_ofs = 0;

                for (int mcu_block = 0; mcu_block < m_blocks_per_mcu; mcu_block++)
                {
                    int component_id = m_mcu_org[mcu_block];
                    short[] q = m_quant[m_comp_quant[component_id]]!;
                    int pOfs = 64 * mcu_block;

                    int acOfs = coeff_buf_getp_offset(m_ac_coeffs[component_id]!, block_x_mcu[component_id] + block_x_mcu_ofs, m_block_y_mcu[component_id] + block_y_mcu_ofs);
                    int dcOfs = coeff_buf_getp_offset(m_dc_coeffs[component_id]!, block_x_mcu[component_id] + block_x_mcu_ofs, m_block_y_mcu[component_id] + block_y_mcu_ofs);

                    m_pMCU_coefficients![pOfs] = m_dc_coeffs[component_id]!.pData![dcOfs];
                    Array.Copy(m_ac_coeffs[component_id]!.pData!, acOfs + 1, m_pMCU_coefficients, pOfs + 1, 63);

                    int i;
                    for (i = 63; i > 0; i--)
                    {
                        if (m_pMCU_coefficients[pOfs + g_ZAG[i]] != 0) break;
                    }

                    m_mcu_block_max_zag[mcu_block] = i + 1;

                    for (; i >= 0; i--)
                    {
                        if (m_pMCU_coefficients[pOfs + g_ZAG[i]] != 0)
                        {
                            m_pMCU_coefficients[pOfs + g_ZAG[i]] = (short)(m_pMCU_coefficients[pOfs + g_ZAG[i]] * q[i]);
                        }
                    }

                    if (m_comps_in_scan == 1) block_x_mcu[component_id]++;
                    else
                    {
                        if (++block_x_mcu_ofs == m_comp_h_samp[component_id])
                        {
                            block_x_mcu_ofs = 0;
                            if (++block_y_mcu_ofs == m_comp_v_samp[component_id])
                            {
                                block_y_mcu_ofs = 0;
                                block_x_mcu[component_id] += m_comp_h_samp[component_id];
                            }
                        }
                    }
                }
                transform_mcu(mcu_row);
            }

            if (m_comps_in_scan == 1) m_block_y_mcu[m_comp_list[0]]++;
            else
            {
                for (int component_num = 0; component_num < m_comps_in_scan; component_num++)
                {
                    int component_id = m_comp_list[component_num];
                    m_block_y_mcu[component_id] += m_comp_v_samp[component_id];
                }
            }
        }

        private bool process_restart()
        {
            int c = 0;
            int i;
            for (i = 1536; i > 0; i--)
            {
                if (get_char() == 0xFF) break;
            }
            if (i == 0) return stop_decoding(jpgd_status.JPGD_BAD_RESTART_MARKER);

            for (; i > 0; i--)
            {
                if ((c = (int)get_char()) != 0xFF) break;
            }
            if (i == 0) return stop_decoding(jpgd_status.JPGD_BAD_RESTART_MARKER);
            if (c != (m_next_restart_num + (int)JPEG_MARKER.M_RST0)) return stop_decoding(jpgd_status.JPGD_BAD_RESTART_MARKER);

            for (int j = 0; j < m_comps_in_frame; j++) m_last_dc_val[j] = 0;

            m_eob_run = 0;
            m_restarts_left = m_restart_interval;
            m_next_restart_num = (m_next_restart_num + 1) & 7;

            m_bits_left = 16;
            get_bits_no_markers(16);
            get_bits_no_markers(16);

            return true;
        }

        private bool decode_next_row()
        {
            for (int mcu_row = 0; mcu_row < m_mcus_per_row; mcu_row++)
            {
                if (m_restart_interval != 0 && m_restarts_left == 0)
                {
                    if (!process_restart()) return false;
                }

                int pBase = 0;
                for (int mcu_block = 0; mcu_block < m_blocks_per_mcu; mcu_block++, pBase += 64)
                {
                    int component_id = m_mcu_org[mcu_block];
                    short[] q = m_quant[m_comp_quant[component_id]]!;

                    int r, s;
                    s = huff_decode(m_pHuff_tabs[m_comp_dc_tab[component_id]]!, out r);
                    s = JPGD_HUFF_EXTEND(r, s);
                    m_last_dc_val[component_id] = (uint)(s += (int)m_last_dc_val[component_id]);
                    m_pMCU_coefficients![pBase] = (short)(s * q[0]);

                    int prev_num_set = m_mcu_block_max_zag[mcu_block];
                    huff_tables pH = m_pHuff_tabs[m_comp_ac_tab[component_id]]!;

                    int k;
                    for (k = 1; k < 64; k++)
                    {
                        int extra_bits;
                        s = huff_decode(pH, out extra_bits);
                        r = s >> 4;
                        s &= 15;

                        if (s != 0)
                        {
                            if (r != 0)
                            {
                                if ((k + r) > 63) return stop_decoding(jpgd_status.JPGD_DECODE_ERROR);
                                if (k < prev_num_set)
                                {
                                    int n = Math.Min(r, prev_num_set - k);
                                    int kt = k;
                                    while (n-- > 0) m_pMCU_coefficients![pBase + g_ZAG[kt++]] = 0;
                                }
                                k += r;
                            }
                            s = JPGD_HUFF_EXTEND(extra_bits, s);
                            m_pMCU_coefficients![pBase + g_ZAG[k]] = (short)(s * q[k]);
                        }
                        else
                        {
                            if (r == 15)
                            {
                                if ((k + 16) > 64) return stop_decoding(jpgd_status.JPGD_DECODE_ERROR);
                                if (k < prev_num_set)
                                {
                                    int n = Math.Min(16, prev_num_set - k);
                                    int kt = k;
                                    while (n-- > 0) m_pMCU_coefficients![pBase + g_ZAG[kt++]] = 0;
                                }
                                k += 16 - 1;
                            }
                            else break;
                        }
                    }

                    if (k < prev_num_set)
                    {
                        int kt = k;
                        while (kt < prev_num_set) m_pMCU_coefficients![pBase + g_ZAG[kt++]] = 0;
                    }

                    m_mcu_block_max_zag[mcu_block] = k;
                }

                transform_mcu(mcu_row);
                m_restarts_left--;
            }
            return true;
        }

        // --- Color conversion ---

        private unsafe void H1V1Convert()
        {
            int row = m_max_mcu_y_size - m_mcu_lines_left;
            fixed (byte* d = m_pScan_line_0)
            fixed (byte* s = m_pSample_buf)
            fixed (int* crr = m_crr, crg = m_crg, cbg = m_cbg, cbb = m_cbb)
            {
                byte* sp = s + row * 8;
                byte* dp = d;

                for (int i = m_max_mcus_per_row; i > 0; i--)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        int y = sp[j];
                        int cb = sp[64 + j];
                        int cr = sp[128 + j];
                        dp[0] = clamp(y + crr[cr]);
                        dp[1] = clamp(y + ((crg[cr] + cbg[cb]) >> 16));
                        dp[2] = clamp(y + cbb[cb]);
                        dp[3] = 255;
                        dp += 4;
                    }
                    sp += 64 * 3;
                }
            }
        }

        private unsafe void H2V1Convert()
        {
            int row = m_max_mcu_y_size - m_mcu_lines_left;
            fixed (byte* d0 = m_pScan_line_0)
            fixed (byte* sb = m_pSample_buf)
            fixed (int* crr = m_crr, crg = m_crg, cbg = m_cbg, cbb = m_cbb)
            {
                int yOfs = row * 8;
                int cOfs = 2 * 64 + row * 8;
                byte* dp = d0;

                for (int i = m_max_mcus_per_row; i > 0; i--)
                {
                    for (int l = 0; l < 2; l++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            int cb = sb[cOfs];
                            int cr = sb[cOfs + 64];
                            int rc = crr[cr];
                            int gc = (crg[cr] + cbg[cb]) >> 16;
                            int bc = cbb[cb];

                            int yy = sb[yOfs + (j << 1)];
                            dp[0] = clamp(yy + rc);
                            dp[1] = clamp(yy + gc);
                            dp[2] = clamp(yy + bc);
                            dp[3] = 255;

                            yy = sb[yOfs + (j << 1) + 1];
                            dp[4] = clamp(yy + rc);
                            dp[5] = clamp(yy + gc);
                            dp[6] = clamp(yy + bc);
                            dp[7] = 255;
                            dp += 8;
                            cOfs++;
                        }
                        yOfs += 64;
                    }
                    yOfs += 64 * 4 - 64 * 2;
                    cOfs += 64 * 4 - 8;
                }
            }
        }

        private unsafe void H1V2Convert()
        {
            int row = m_max_mcu_y_size - m_mcu_lines_left;
            fixed (byte* d0p = m_pScan_line_0, d1p = m_pScan_line_1, sb = m_pSample_buf)
            fixed (int* crr = m_crr, crg = m_crg, cbg = m_cbg, cbb = m_cbb)
            {
                int yOfs;
                if (row < 8) yOfs = row * 8;
                else yOfs = 64 * 1 + (row & 7) * 8;

                int cOfs = 64 * 2 + (row >> 1) * 8;
                byte* dp0 = d0p, dp1 = d1p;

                for (int i = m_max_mcus_per_row; i > 0; i--)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        int cb = sb[cOfs + j];
                        int cr = sb[cOfs + 64 + j];
                        int rc = crr[cr];
                        int gc = (crg[cr] + cbg[cb]) >> 16;
                        int bc = cbb[cb];

                        int yy = sb[yOfs + j];
                        dp0[0] = clamp(yy + rc);
                        dp0[1] = clamp(yy + gc);
                        dp0[2] = clamp(yy + bc);
                        dp0[3] = 255;

                        yy = sb[yOfs + 8 + j];
                        dp1[0] = clamp(yy + rc);
                        dp1[1] = clamp(yy + gc);
                        dp1[2] = clamp(yy + bc);
                        dp1[3] = 255;

                        dp0 += 4;
                        dp1 += 4;
                    }
                    yOfs += 64 * 4;
                    cOfs += 64 * 4;
                }
            }
        }

        private unsafe void H2V2Convert()
        {
            int row = m_max_mcu_y_size - m_mcu_lines_left;
            fixed (byte* d0p = m_pScan_line_0, d1p = m_pScan_line_1, sb = m_pSample_buf)
            fixed (int* crr = m_crr, crg = m_crg, cbg = m_cbg, cbb = m_cbb)
            {
                int yOfs;
                if (row < 8) yOfs = row * 8;
                else yOfs = 64 * 2 + (row & 7) * 8;

                int cOfs = 64 * 4 + (row >> 1) * 8;
                byte* dp0 = d0p, dp1 = d1p;

                for (int i = m_max_mcus_per_row; i > 0; i--)
                {
                    for (int l = 0; l < 2; l++)
                    {
                        for (int j = 0; j < 8; j += 2)
                        {
                            int cb = sb[cOfs];
                            int cr = sb[cOfs + 64];
                            int rc = crr[cr];
                            int gc = (crg[cr] + cbg[cb]) >> 16;
                            int bc = cbb[cb];

                            int yy = sb[yOfs + j];
                            dp0[0] = clamp(yy + rc); dp0[1] = clamp(yy + gc);
                            dp0[2] = clamp(yy + bc); dp0[3] = 255;

                            yy = sb[yOfs + j + 1];
                            dp0[4] = clamp(yy + rc); dp0[5] = clamp(yy + gc);
                            dp0[6] = clamp(yy + bc); dp0[7] = 255;

                            yy = sb[yOfs + j + 8];
                            dp1[0] = clamp(yy + rc); dp1[1] = clamp(yy + gc);
                            dp1[2] = clamp(yy + bc); dp1[3] = 255;

                            yy = sb[yOfs + j + 8 + 1];
                            dp1[4] = clamp(yy + rc); dp1[5] = clamp(yy + gc);
                            dp1[6] = clamp(yy + bc); dp1[7] = 255;

                            dp0 += 8; dp1 += 8;
                            cOfs++;
                        }
                        yOfs += 64;
                    }
                    yOfs += 64 * 6 - 64 * 2;
                    cOfs += 64 * 6 - 8;
                }
            }
        }

        private void gray_convert()
        {
            int row = m_max_mcu_y_size - m_mcu_lines_left;
            byte[] d = m_pScan_line_0!;
            byte[] s = m_pSample_buf!;
            int sOfs = row * 8;
            int dOfs = 0;

            for (int i = m_max_mcus_per_row; i > 0; i--)
            {
                Array.Copy(s, sOfs, d, dOfs, 8);
                sOfs += 64;
                dOfs += 8;
            }
        }

        private void find_eoi()
        {
            if (m_progressive_flag == 0)
            {
                m_bits_left = 16;
                get_bits(16);
                get_bits(16);
                process_markers();
            }
            m_total_bytes_read -= m_in_buf_left;
        }

        // --- Huffman table construction ---

        private void make_huff_table(int index, huff_tables pH)
        {
            int p, i, l, si;
            byte[] huffsize = new byte[257];
            uint[] huffcode = new uint[257];
            uint code;
            uint subtree;
            int code_size;
            int lastp;
            int nextfreeentry;
            int currententry;

            pH.ac_table = m_huff_ac[index] != 0;
            p = 0;

            for (l = 1; l <= 16; l++)
            {
                for (i = 1; i <= m_huff_num[index]![l]; i++)
                    huffsize[p++] = (byte)l;
            }

            huffsize[p] = 0;
            lastp = p;
            code = 0;
            si = huffsize[0];
            p = 0;

            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si) { huffcode[p++] = code; code++; }
                code <<= 1;
                si++;
            }

            Array.Clear(pH.look_up); Array.Clear(pH.look_up2);
            Array.Clear(pH.tree); Array.Clear(pH.code_size);

            nextfreeentry = -1;
            p = 0;

            while (p < lastp)
            {
                i = m_huff_val[index]![p];
                code = huffcode[p];
                code_size = huffsize[p];
                pH.code_size[i] = (byte)code_size;

                if (code_size <= 8)
                {
                    code <<= (8 - code_size);
                    for (l = 1 << (8 - code_size); l > 0; l--)
                    {
                        pH.look_up[code] = (uint)i;
                        bool has_extrabits = false;
                        int extra_bits = 0;
                        int num_extra_bits = i & 15;
                        int bits_to_fetch = code_size;

                        if (num_extra_bits != 0)
                        {
                            int total_codesize = code_size + num_extra_bits;
                            if (total_codesize <= 8)
                            {
                                has_extrabits = true;
                                extra_bits = ((1 << num_extra_bits) - 1) & (int)(code >> (8 - total_codesize));
                                bits_to_fetch += num_extra_bits;
                            }
                        }
                        if (!has_extrabits) pH.look_up2[code] = (uint)(i | (bits_to_fetch << 8));
                        else pH.look_up2[code] = (uint)(i | 0x8000 | (extra_bits << 16) | (bits_to_fetch << 8));
                        code++;
                    }
                }
                else
                {
                    subtree = (code >> (code_size - 8)) & 0xFF;
                    currententry = (int)pH.look_up[subtree];

                    if (currententry == 0)
                    {
                        pH.look_up[subtree] = (uint)nextfreeentry;
                        pH.look_up2[subtree] = (uint)nextfreeentry;
                        currententry = nextfreeentry;
                        nextfreeentry -= 2;
                    }

                    code <<= (16 - (code_size - 8));

                    for (l = code_size; l > 9; l--)
                    {
                        if ((code & 0x8000) == 0) currententry--;
                        if (pH.tree[-currententry - 1] == 0)
                        {
                            pH.tree[-currententry - 1] = (uint)nextfreeentry;
                            currententry = nextfreeentry;
                            nextfreeentry -= 2;
                        }
                        else currententry = (int)pH.tree[-currententry - 1];
                        code <<= 1;
                    }
                    if ((code & 0x8000) == 0) currententry--;
                    pH.tree[-currententry - 1] = (uint)i;
                }
                p++;
            }
        }

        private bool check_quant_tables()
        {
            for (int i = 0; i < m_comps_in_scan; i++)
            {
                if (m_quant[m_comp_quant[m_comp_list[i]]] == null) return stop_decoding(jpgd_status.JPGD_UNDEFINED_QUANT_TABLE);
            }
            return true;
        }

        private bool check_huff_tables()
        {
            for (int i = 0; i < m_comps_in_scan; i++)
            {
                if (m_spectral_start == 0 && m_huff_num[m_comp_dc_tab[m_comp_list[i]]] == null) return stop_decoding(jpgd_status.JPGD_UNDEFINED_HUFF_TABLE);
                if (m_spectral_end > 0 && m_huff_num[m_comp_ac_tab[m_comp_list[i]]] == null) return stop_decoding(jpgd_status.JPGD_UNDEFINED_HUFF_TABLE);
            }

            for (int i = 0; i < JPGD_MAX_HUFF_TABLES; i++)
            {
                if (m_huff_num[i] != null)
                {
                    if (m_pHuff_tabs[i] == null) m_pHuff_tabs[i] = new huff_tables();
                    make_huff_table(i, m_pHuff_tabs[i]!);
                }
            }
            return true;
        }

        private void calc_mcu_block_order()
        {
            int max_h_samp = 0, max_v_samp = 0;
            for (int cid = 0; cid < m_comps_in_frame; cid++)
            {
                if (m_comp_h_samp[cid] > max_h_samp) max_h_samp = m_comp_h_samp[cid];
                if (m_comp_v_samp[cid] > max_v_samp) max_v_samp = m_comp_v_samp[cid];
            }

            for (int cid = 0; cid < m_comps_in_frame; cid++)
            {
                m_comp_h_blocks[cid] = ((((m_image_x_size * m_comp_h_samp[cid]) + (max_h_samp - 1)) / max_h_samp) + 7) / 8;
                m_comp_v_blocks[cid] = ((((m_image_y_size * m_comp_v_samp[cid]) + (max_v_samp - 1)) / max_v_samp) + 7) / 8;
            }

            if (m_comps_in_scan == 1)
            {
                m_mcus_per_row = m_comp_h_blocks[m_comp_list[0]];
                m_mcus_per_col = m_comp_v_blocks[m_comp_list[0]];
            }
            else
            {
                m_mcus_per_row = (((m_image_x_size + 7) / 8) + (max_h_samp - 1)) / max_h_samp;
                m_mcus_per_col = (((m_image_y_size + 7) / 8) + (max_v_samp - 1)) / max_v_samp;
            }

            if (m_comps_in_scan == 1)
            {
                m_mcu_org[0] = m_comp_list[0];
                m_blocks_per_mcu = 1;
            }
            else
            {
                m_blocks_per_mcu = 0;
                for (int component_num = 0; component_num < m_comps_in_scan; component_num++)
                {
                    int cid = m_comp_list[component_num];
                    int num_blocks = m_comp_h_samp[cid] * m_comp_v_samp[cid];
                    while (num_blocks-- > 0) m_mcu_org[m_blocks_per_mcu++] = cid;
                }
            }
        }

        private int init_scan()
        {
            if (!locate_sos_marker()) return 0;
            calc_mcu_block_order();
            check_huff_tables();
            if (!check_quant_tables()) return 0;

            for (int j = 0; j < m_comps_in_frame; j++) m_last_dc_val[j] = 0;
            m_eob_run = 0;

            if (m_restart_interval != 0)
            {
                m_restarts_left = m_restart_interval;
                m_next_restart_num = 0;
            }
            fix_in_buffer();
            return 1;
        }

        private bool init_frame()
        {
            if (m_comps_in_frame == 1)
            {
                if (m_comp_h_samp[0] != 1 || m_comp_v_samp[0] != 1) return stop_decoding(jpgd_status.JPGD_UNSUPPORTED_SAMP_FACTORS);
                m_scan_type = (int)JPEG_SUBSAMPLING.JPGD_GRAYSCALE;
                m_max_blocks_per_mcu = 1;
                m_max_mcu_x_size = 8;
                m_max_mcu_y_size = 8;
            }
            else if (m_comps_in_frame == 3)
            {
                if (m_comp_h_samp[1] != 1 || m_comp_v_samp[1] != 1 || m_comp_h_samp[2] != 1 || m_comp_v_samp[2] != 1)
                    return stop_decoding(jpgd_status.JPGD_UNSUPPORTED_SAMP_FACTORS);

                if (m_comp_h_samp[0] == 1 && m_comp_v_samp[0] == 1)
                {
                    m_scan_type = (int)JPEG_SUBSAMPLING.JPGD_YH1V1;
                    m_max_blocks_per_mcu = 3; m_max_mcu_x_size = 8; m_max_mcu_y_size = 8;
                }
                else if (m_comp_h_samp[0] == 2 && m_comp_v_samp[0] == 1)
                {
                    m_scan_type = (int)JPEG_SUBSAMPLING.JPGD_YH2V1;
                    m_max_blocks_per_mcu = 4; m_max_mcu_x_size = 16; m_max_mcu_y_size = 8;
                }
                else if (m_comp_h_samp[0] == 1 && m_comp_v_samp[0] == 2)
                {
                    m_scan_type = (int)JPEG_SUBSAMPLING.JPGD_YH1V2;
                    m_max_blocks_per_mcu = 4; m_max_mcu_x_size = 8; m_max_mcu_y_size = 16;
                }
                else if (m_comp_h_samp[0] == 2 && m_comp_v_samp[0] == 2)
                {
                    m_scan_type = (int)JPEG_SUBSAMPLING.JPGD_YH2V2;
                    m_max_blocks_per_mcu = 6; m_max_mcu_x_size = 16; m_max_mcu_y_size = 16;
                }
                else return stop_decoding(jpgd_status.JPGD_UNSUPPORTED_SAMP_FACTORS);
            }
            else return stop_decoding(jpgd_status.JPGD_UNSUPPORTED_COLORSPACE);

            m_max_mcus_per_row = (m_image_x_size + (m_max_mcu_x_size - 1)) / m_max_mcu_x_size;
            m_max_mcus_per_col = (m_image_y_size + (m_max_mcu_y_size - 1)) / m_max_mcu_y_size;

            if (m_scan_type == (int)JPEG_SUBSAMPLING.JPGD_GRAYSCALE) m_dest_bytes_per_pixel = 1;
            else m_dest_bytes_per_pixel = 4;

            m_dest_bytes_per_scan_line = ((m_image_x_size + 15) & 0xFFF0) * m_dest_bytes_per_pixel;
            m_real_dest_bytes_per_scan_line = m_image_x_size * m_dest_bytes_per_pixel;

            m_pScan_line_0 = new byte[m_dest_bytes_per_scan_line];
            if (m_scan_type == (int)JPEG_SUBSAMPLING.JPGD_YH1V2 || m_scan_type == (int)JPEG_SUBSAMPLING.JPGD_YH2V2)
                m_pScan_line_1 = new byte[m_dest_bytes_per_scan_line];

            m_max_blocks_per_row = m_max_mcus_per_row * m_max_blocks_per_mcu;
            if (m_max_blocks_per_row > JPGD_MAX_BLOCKS_PER_ROW) return stop_decoding(jpgd_status.JPGD_ASSERTION_ERROR);

            m_pMCU_coefficients = new short[m_max_blocks_per_mcu * 64];

            for (int i = 0; i < m_max_blocks_per_mcu; i++)
                m_mcu_block_max_zag[i] = 64;

            m_expanded_blocks_per_component = m_comp_h_samp[0] * m_comp_v_samp[0];
            m_expanded_blocks_per_mcu = m_expanded_blocks_per_component * m_comps_in_frame;
            m_expanded_blocks_per_row = m_max_mcus_per_row * m_expanded_blocks_per_mcu;

            m_pSample_buf = new byte[m_max_blocks_per_row * 64];

            m_total_lines_left = m_image_y_size;
            m_mcu_lines_left = 0;
            create_look_ups();

            return true;
        }

        // --- Progressive decode helpers ---

        private static bool decode_block_dc_first(jpeg_decoder pD, int component_id, int block_x, int block_y)
        {
            int s, r;
            int pOfs = pD.coeff_buf_getp_offset(pD.m_dc_coeffs[component_id]!, block_x, block_y);
            short[] pData = pD.m_dc_coeffs[component_id]!.pData!;

            if ((s = pD.huff_decode(pD.m_pHuff_tabs[pD.m_comp_dc_tab[component_id]]!)) != 0)
            {
                r = (int)pD.get_bits_no_markers(s);
                s = JPGD_HUFF_EXTEND(r, s);
            }
            pD.m_last_dc_val[component_id] = (uint)(s += (int)pD.m_last_dc_val[component_id]);
            pData[pOfs] = (short)((uint)s << pD.m_successive_low);
            return true;
        }

        private static bool decode_block_dc_refine(jpeg_decoder pD, int component_id, int block_x, int block_y)
        {
            if (pD.get_bits_no_markers(1) != 0)
            {
                int pOfs = pD.coeff_buf_getp_offset(pD.m_dc_coeffs[component_id]!, block_x, block_y);
                pD.m_dc_coeffs[component_id]!.pData![pOfs] |= (short)(1 << pD.m_successive_low);
            }
            return true;
        }

        private static bool decode_block_ac_first(jpeg_decoder pD, int component_id, int block_x, int block_y)
        {
            int k, s, r;

            if (pD.m_eob_run != 0)
            {
                pD.m_eob_run--;
                return true;
            }

            int pOfs = pD.coeff_buf_getp_offset(pD.m_ac_coeffs[component_id]!, block_x, block_y);
            short[] pData = pD.m_ac_coeffs[component_id]!.pData!;

            for (k = pD.m_spectral_start; k <= pD.m_spectral_end; k++)
            {
                s = pD.huff_decode(pD.m_pHuff_tabs[pD.m_comp_ac_tab[component_id]]!);
                r = s >> 4;
                s &= 15;
                if (s != 0)
                {
                    if ((k += r) > 63) return pD.stop_decoding(jpgd_status.JPGD_DECODE_ERROR);
                    r = (int)pD.get_bits_no_markers(s);
                    s = JPGD_HUFF_EXTEND(r, s);
                    pData[pOfs + g_ZAG[k]] = (short)((uint)s << pD.m_successive_low);
                }
                else
                {
                    if (r == 15)
                    {
                        if ((k += 15) > 63) return pD.stop_decoding(jpgd_status.JPGD_DECODE_ERROR);
                    }
                    else
                    {
                        pD.m_eob_run = 1 << r;
                        if (r != 0) pD.m_eob_run += (int)pD.get_bits_no_markers(r);
                        pD.m_eob_run--;
                        break;
                    }
                }
            }
            return true;
        }

        private static bool decode_block_ac_refine(jpeg_decoder pD, int component_id, int block_x, int block_y)
        {
            int s, k, r;
            int p1 = 1 << pD.m_successive_low;
            int m1 = (int)(unchecked((uint)(-1)) << pD.m_successive_low);
            int pOfs = pD.coeff_buf_getp_offset(pD.m_ac_coeffs[component_id]!, block_x, block_y);
            short[] pData = pD.m_ac_coeffs[component_id]!.pData!;

            k = pD.m_spectral_start;

            if (pD.m_eob_run == 0)
            {
                for (; k <= pD.m_spectral_end; k++)
                {
                    s = pD.huff_decode(pD.m_pHuff_tabs[pD.m_comp_ac_tab[component_id]]!);
                    r = s >> 4;
                    s &= 15;
                    if (s != 0)
                    {
                        if (s != 1) return pD.stop_decoding(jpgd_status.JPGD_DECODE_ERROR);
                        if (pD.get_bits_no_markers(1) != 0) s = p1;
                        else s = m1;
                    }
                    else
                    {
                        if (r != 15)
                        {
                            pD.m_eob_run = 1 << r;
                            if (r != 0) pD.m_eob_run += (int)pD.get_bits_no_markers(r);
                            break;
                        }
                    }

                    do
                    {
                        int idx = pOfs + g_ZAG[k & 63];
                        if (pData[idx] != 0)
                        {
                            if (pD.get_bits_no_markers(1) != 0)
                            {
                                if ((pData[idx] & p1) == 0)
                                {
                                    if (pData[idx] >= 0) pData[idx] = (short)(pData[idx] + p1);
                                    else pData[idx] = (short)(pData[idx] + m1);
                                }
                            }
                        }
                        else
                        {
                            if (--r < 0) break;
                        }
                        k++;
                    } while (k <= pD.m_spectral_end);

                    if (s != 0 && k < 64)
                    {
                        pData[pOfs + g_ZAG[k]] = (short)s;
                    }
                }
            }

            if (pD.m_eob_run > 0)
            {
                for (; k <= pD.m_spectral_end; k++)
                {
                    int idx = pOfs + g_ZAG[k & 63];
                    if (pData[idx] != 0)
                    {
                        if (pD.get_bits_no_markers(1) != 0)
                        {
                            if ((pData[idx] & p1) == 0)
                            {
                                if (pData[idx] >= 0) pData[idx] = (short)(pData[idx] + p1);
                                else pData[idx] = (short)(pData[idx] + m1);
                            }
                        }
                    }
                }
                pD.m_eob_run--;
            }
            return true;
        }

        private bool decode_scan(pDecode_block_func decode_block_func)
        {
            int[] block_x_mcu = new int[JPGD_MAX_COMPONENTS];
            int[] local_block_y_mcu = new int[JPGD_MAX_COMPONENTS];

            for (int mcu_col = 0; mcu_col < m_mcus_per_col; mcu_col++)
            {
                Array.Clear(block_x_mcu);

                for (int mcu_row = 0; mcu_row < m_mcus_per_row; mcu_row++)
                {
                    int block_x_mcu_ofs = 0, block_y_mcu_ofs = 0;

                    if (m_restart_interval != 0 && m_restarts_left == 0)
                    {
                        if (!process_restart()) return false;
                    }

                    for (int mcu_block = 0; mcu_block < m_blocks_per_mcu; mcu_block++)
                    {
                        int cid = m_mcu_org[mcu_block];
                        if (!decode_block_func(this, cid, block_x_mcu[cid] + block_x_mcu_ofs, local_block_y_mcu[cid] + block_y_mcu_ofs)) return false;

                        if (m_comps_in_scan == 1) block_x_mcu[cid]++;
                        else
                        {
                            if (++block_x_mcu_ofs == m_comp_h_samp[cid])
                            {
                                block_x_mcu_ofs = 0;
                                if (++block_y_mcu_ofs == m_comp_v_samp[cid])
                                {
                                    block_y_mcu_ofs = 0;
                                    block_x_mcu[cid] += m_comp_h_samp[cid];
                                }
                            }
                        }
                    }
                    m_restarts_left--;
                }

                if (m_comps_in_scan == 1) local_block_y_mcu[m_comp_list[0]]++;
                else
                {
                    for (int cn = 0; cn < m_comps_in_scan; cn++)
                    {
                        int cid = m_comp_list[cn];
                        local_block_y_mcu[cid] += m_comp_v_samp[cid];
                    }
                }
            }
            return true;
        }

        private bool init_progressive()
        {
            if (m_comps_in_frame == 4) return stop_decoding(jpgd_status.JPGD_UNSUPPORTED_COLORSPACE);

            for (int i = 0; i < m_comps_in_frame; i++)
            {
                m_dc_coeffs[i] = coeff_buf_open(m_max_mcus_per_row * m_comp_h_samp[i], m_max_mcus_per_col * m_comp_v_samp[i], 1, 1);
                m_ac_coeffs[i] = coeff_buf_open(m_max_mcus_per_row * m_comp_h_samp[i], m_max_mcus_per_col * m_comp_v_samp[i], 8, 8);
            }

            while (true)
            {
                if (init_scan() == 0) break;

                int dc_only_scan = (m_spectral_start == 0) ? 1 : 0;
                int refinement_scan = (m_successive_high != 0) ? 1 : 0;

                if (m_spectral_start > m_spectral_end || m_spectral_end > 63) return stop_decoding(jpgd_status.JPGD_BAD_SOS_SPECTRAL);
                if (dc_only_scan != 0)
                {
                    if (m_spectral_end != 0) return stop_decoding(jpgd_status.JPGD_BAD_SOS_SPECTRAL);
                }
                else if (m_comps_in_scan != 1) return stop_decoding(jpgd_status.JPGD_BAD_SOS_SPECTRAL);

                if (refinement_scan != 0 && m_successive_low != m_successive_high - 1) return stop_decoding(jpgd_status.JPGD_BAD_SOS_SUCCESSIVE);

                pDecode_block_func decode_block_func;
                if (dc_only_scan != 0)
                {
                    decode_block_func = refinement_scan != 0 ? decode_block_dc_refine : decode_block_dc_first;
                }
                else
                {
                    decode_block_func = refinement_scan != 0 ? decode_block_ac_refine : decode_block_ac_first;
                }

                if (!decode_scan(decode_block_func)) return false;
                m_bits_left = 16;
                get_bits(16);
                get_bits(16);
            }

            m_comps_in_scan = m_comps_in_frame;
            for (int i = 0; i < m_comps_in_frame; i++) m_comp_list[i] = i;
            calc_mcu_block_order();

            return true;
        }

        private bool init_sequential()
        {
            if (init_scan() == 0) return stop_decoding(jpgd_status.JPGD_UNEXPECTED_MARKER);
            return true;
        }

        private bool decode_start()
        {
            if (!init_frame()) return false;
            if (m_progressive_flag != 0) return init_progressive();
            return init_sequential();
        }

        private void decode_init(jpeg_decoder_stream pStream)
        {
            init(pStream);
            locate_sof_marker();
        }
    }

    #endregion

    #region Public API (JpegDecoder)

    /// <summary>
    /// JPEG decoder state. Mirrors C++ jpeg_decoder public API.
    /// Wraps the internal jpeg_decoder with a managed-friendly interface.
    /// </summary>
    public class JpegDecoder
    {
        private jpeg_decoder _decoder;
        internal int width;
        internal int height;
        internal bool valid;

        private JpegDecoder(jpeg_decoder decoder, int width, int height)
        {
            _decoder = decoder;
            this.width = width;
            this.height = height;
            valid = true;
        }

        /// <summary>
        /// Create a decoder from a file path. Returns null on failure.
        /// Mirrors C++ jpgdHeader(filename, width, height).
        /// </summary>
        public static JpegDecoder? FromFile(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                var fileData = File.ReadAllBytes(path);
                return FromData(fileData, fileData.Length, out width, out height);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create a decoder from a memory buffer. Returns null on failure.
        /// Mirrors C++ jpgdHeader(data, size, width, height).
        /// </summary>
        public static JpegDecoder? FromData(byte[] data, int size, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (data == null || size == 0) return null;

            try
            {
                var stream = new jpeg_decoder_mem_stream(data, size);
                var decoder = new jpeg_decoder(stream);
                if (decoder.get_error_code() != jpgd_status.JPGD_SUCCESS) return null;

                width = decoder.get_width();
                height = decoder.get_height();
                return new JpegDecoder(decoder, width, height);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decompress JPEG data into a pixel buffer.
        /// Returns pixel data in either ARGB8888 or ABGR8888 format depending on the requested color space.
        /// Mirrors C++ jpgdDecompress(decoder, cs).
        /// </summary>
        public uint[]? Decompress(ColorSpace cs)
        {
            if (!valid) return null;

            try
            {
                if (_decoder.begin_decoding() != (int)jpgd_status.JPGD_SUCCESS) return null;

                bool bgra = (cs == ColorSpace.ABGR8888S || cs == ColorSpace.ABGR8888);
                int channel = 4;
                int w = _decoder.get_width();
                int h = _decoder.get_height();
                int stride = w * channel;
                var ret = new byte[stride * h];
                int dstOfs = 0;

                for (int y = 0; y < h; y++)
                {
                    int status = _decoder.decode(out byte[]? src, out int srcOffset);
                    if (status != (int)jpgd_status.JPGD_SUCCESS)
                        return null;
                    if (src == null) return null;

                    if (_decoder.get_num_components() == 3)
                    {
                        if (bgra)
                        {
                            Array.Copy(src, 0, ret, dstOfs, stride);
                            dstOfs += stride;
                        }
                        else
                        {
                            int sOfs = 0;
                            for (int x = 0; x < w; x++, sOfs += 4, dstOfs += 4)
                            {
                                ret[dstOfs] = src[sOfs + 2];
                                ret[dstOfs + 1] = src[sOfs + 1];
                                ret[dstOfs + 2] = src[sOfs];
                                ret[dstOfs + 3] = 255;
                            }
                        }
                    }
                    else if (_decoder.get_num_components() == 1)
                    {
                        int sOfs = 0;
                        for (int x = 0; x < w; x++, sOfs++, dstOfs += 4)
                        {
                            ret[dstOfs] = src[sOfs];
                            ret[dstOfs + 1] = src[sOfs];
                            ret[dstOfs + 2] = src[sOfs];
                            ret[dstOfs + 3] = 255;
                        }
                    }
                }

                // Convert byte[] to uint[]
                var pixels = new uint[w * h];
                Buffer.BlockCopy(ret, 0, pixels, 0, ret.Length);
                return pixels;
            }
            catch
            {
                return null;
            }
        }
    }

    #endregion
}
