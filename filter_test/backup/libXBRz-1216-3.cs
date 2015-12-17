using System;

namespace XBRz_speed
{
    public class HS_XBRz
    {
        const double dominantDirectionThreshold = 3.6;
        const double steepDirectionThreshold = 2.2;
        const double eqColorThres = 900;

        const int Ymask = 0x00ff0000;
        const int Umask = 0x0000ff00;
        const int Vmask = 0x000000ff;

        const int BlendNone = 0;
        const int BlendNormal = 1;
        const int BlendDominant = 2;

        const int Rot0 = 0;
        const int Rot90 = 1;
        const int Rot180 = 2;
        const int Rot270 = 3;

        static int[] lTable;

        private const int _MAX_ROTS = 4; // Number of 90 degree rotations
        private const int _MAX_SCALE = 5; // Highest possible scale
        private const int _MAX_SCALE_SQUARED = _MAX_SCALE * _MAX_SCALE;
        private static readonly Tuple[] _MATRIX_ROTATION;

        static HS_XBRz()
        {

            _MATRIX_ROTATION = new Tuple[(_MAX_SCALE - 1) * _MAX_SCALE_SQUARED * _MAX_ROTS];
            for (var n = 2; n < _MAX_SCALE + 1; n++)
                for (var r = 0; r < _MAX_ROTS; r++)
                {
                    var nr = (n - 2) * (_MAX_ROTS * _MAX_SCALE_SQUARED) + r * _MAX_SCALE_SQUARED;
                    for (var i = 0; i < _MAX_SCALE; i++)
                        for (var j = 0; j < _MAX_SCALE; j++)
                            _MATRIX_ROTATION[nr + i * _MAX_SCALE + j] =
                              _BuildMatrixRotation(r, i, j, n);
                }
        }

        static Tuple _BuildMatrixRotation(int rotDeg, int i, int j, int n)
        {
            int iOld;
            int jOld;

            if (rotDeg == 0)
            {
                iOld = i;
                jOld = j;
            }
            else
            {
                var old = _BuildMatrixRotation(rotDeg - 1, i, j, n);
                iOld = n - 1 - old.Item2;
                jOld = old.Item1;
            }

            return new Tuple(iOld, jOld);
        }

        class Tuple
        {
            public int Item1 { get; private set; }
            public int Item2 { get; private set; }

            public Tuple(int i, int j)
            {
                this.Item1 = i;
                this.Item2 = j;
            }
        }

        public static unsafe void initTable()
        {
            if (lTable != null)
            {
                return;
            }
            lTable = new int[0x1000000];

            for (uint i = 0; i < lTable.Length; i++)
            {

                float r = (i & 0xff0000) >> 16;
                float g = (i & 0x00ff00) >> 8;
                float b = (i & 0x0000ff);

                lTable[i] = (byte)(.299 * r + .587 * g + .114 * b) | ((byte)((int)(-.169 * r - .331 * g + .5 * b) + 128) << 8) | ((byte)((int)(.5 * r - .419 * g - .081 * b) + 128) << 16);
            }
        }

        static int GetTopL(byte b)
        {
            return ((b) & 0x3);
        }

        static int GetTopR(byte b)
        {
            return ((b >> 2) & 0x3);
        }

        static int GetBottomR(byte b)
        {
            return ((b >> 4) & 0x3);
        }

        static int GetBottomL(byte b)
        {
            return ((b >> 6) & 0x3);
        }

        static byte SetTopL(byte b, int bt)
        {
            return (byte)(b | (byte)bt);
        }

        static byte SetTopR(byte b, int bt)
        {
            return (byte)(b | ((byte)bt << 2));
        }

        static byte SetBottomR(byte b, int bt)
        {
            return (byte)(b | ((byte)bt << 4));
        }

        static byte SetBottomL(byte b, int bt)
        {
            return (byte)(b | ((byte)bt << 6));
        }

        static byte Rotate(byte b, int rotDeg)
        {
            int l = (int)rotDeg << 1;
            int r = 8 - l;
            return (byte)(b << l | b >> r);
        }

        static byte getAlpha(uint pix) { return (byte)((pix & 0xff000000) >> 24); }
        static byte getRed(uint pix) { return (byte)((pix & 0xff0000) >> 16); }
        static byte getGreen(uint pix) { return (byte)((pix & 0xff00) >> 8); }
        static byte getBlue(uint pix) { return (byte)(pix & 0xff); }

        static uint Interpolate(uint pixel1, uint pixel2, int quantifier1, int quantifier2)
        {
            uint total = (uint)(quantifier1 + quantifier2);

            return (uint)(
                ((((getRed(pixel1) * quantifier1 + getRed(pixel2) * quantifier2) / total) & 0xff) << 16) |
                ((((getGreen(pixel1) * quantifier1 + getGreen(pixel2) * quantifier2) / total) & 0xff) << 8) |
                (((getBlue(pixel1) * quantifier1 + getBlue(pixel2) * quantifier2) / total) & 0xff)
                );
        }

        static void _AlphaBlend(int n, int m, ImagePointer dstPtr, uint col)
        {
            dstPtr.SetPixel(Interpolate(col, dstPtr.GetPixel(), n, m - n));
        }
        static void _FillBlock(uint[] trg, int trgi, int pitch, uint col, int blockSize)
        {
            for (var y = 0; y < blockSize; ++y, trgi += pitch)
                for (var x = 0; x < blockSize; ++x)
                    trg[trgi + x] = col;
        }

        static double DistYCbCr(uint pix1, uint pix2)
        {

            if (pix1 == pix2) return 0;

            int YUV1 = lTable[pix1 & 0x00ffffff];
            int YUV2 = lTable[pix2 & 0x00ffffff];

            int y = ((YUV1 & Ymask) >> 16) - ((YUV2 & Ymask) >> 16);
            int u = ((YUV1 & Umask) >> 8) - ((YUV2 & Umask) >> 8);
            int v = (YUV1 & Vmask) - (YUV2 & Vmask);

            return y * y + u * u + v * v;
        }

        private static bool ColorEQ(uint pix1, uint pix2)
        {
            if (pix1 == pix2) return true;
            return DistYCbCr(pix1, pix2) < eqColorThres;
        }

        class OutputMatrix
        {
            private readonly ImagePointer _output;
            private int _outi;
            private readonly int _outWidth;
            private readonly int _n;
            private int _nr;

            public OutputMatrix(int scale, uint[] output, int outWidth)
            {
                this._n = (scale - 2) * (_MAX_ROTS * _MAX_SCALE_SQUARED);
                this._output = new ImagePointer(output);
                this._outWidth = outWidth;
            }

            public void Move(int rotDeg, int outi)
            {
                this._nr = this._n + (int)rotDeg * _MAX_SCALE_SQUARED;
                this._outi = outi;
            }

            public ImagePointer Reference(int i, int j)
            {
                var rot = _MATRIX_ROTATION[this._nr + i * _MAX_SCALE + j];
                this._output.Position(this._outi + rot.Item2 + rot.Item1 * this._outWidth);
                return this._output;
            }
        }


        class ImagePointer
        {
            private  uint[] _imageData;
            private int _offset;

            public ImagePointer(uint[] imageData)
            {
                this._imageData = imageData;
            }

            public void Position(int offset)
            {
                this._offset = offset;
            }

            public uint GetPixel()
            {
                return this._imageData[this._offset];
            }

            public void SetPixel(uint val)
            {
                this._imageData[this._offset] = val;
            }
        }


        private static void _ScalePixel2X(
            uint ker3_0, uint ker3_1, uint ker3_2, uint ker3_3, uint ker3_4, uint ker3_5, uint ker3_6, uint ker3_7, uint ker3_8,
            int rotDeg, uint[] trg, int trgi, int trgWidth, byte blendInfo, OutputMatrix outputMatrix)
        {

            uint b, c, d, e, f, g, h, i;

            if (rotDeg == Rot0)
            {
                b = ker3_1;
                c = ker3_2;
                d = ker3_3;
                e = ker3_4;
                f = ker3_5;
                g = ker3_6;
                h = ker3_7;
                i = ker3_8;

            }
            else if (rotDeg == Rot90)
            {
                b = ker3_3;
                c = ker3_0;
                d = ker3_7;
                e = ker3_4;
                f = ker3_1;
                g = ker3_8;
                h = ker3_5;
                i = ker3_2;
            }
            else if (rotDeg == Rot180)
            {
                b = ker3_7;
                c = ker3_6;
                d = ker3_5;
                e = ker3_4;
                f = ker3_3;
                g = ker3_2;
                h = ker3_1;
                i = ker3_0;
            }
            else
            {
                b = ker3_5;
                c = ker3_8;
                d = ker3_1;
                e = ker3_4;
                f = ker3_7;
                g = ker3_0;
                h = ker3_3;
                i = ker3_6;
            }

            byte blend = Rotate(blendInfo, rotDeg);

            if (GetBottomR(blend) == BlendNone)
                return;

            bool doLineBlend;

            if (GetBottomR(blend) >= BlendDominant)
                doLineBlend = true;
            else if (GetTopR(blend) != BlendNone && !ColorEQ(e, g))
                doLineBlend = false;
            else if (GetBottomL(blend) != BlendNone && !ColorEQ(e, c))
                doLineBlend = false;
            else if (ColorEQ(g, h) && ColorEQ(h, i) && ColorEQ(i, f) && ColorEQ(f, c) && !ColorEQ(e, i))
                doLineBlend = false;
            else
                doLineBlend = true;
            var px = DistYCbCr(e, f) <= DistYCbCr(e, h) ? f : h;

            var output = outputMatrix;
            output.Move(rotDeg, trgi);

            if (!doLineBlend)
            {
                Scaler_2X.BlendCorner(px, output);
                return;
            }

            var fg = DistYCbCr(f, g);
            var hc = DistYCbCr(h, c);

            var haveShallowLine = steepDirectionThreshold * fg <= hc && e != g && d != g;
            var haveSteepLine = steepDirectionThreshold * hc <= fg && e != c && b != c;

            if (haveShallowLine)
            {
                if (haveSteepLine)
                    Scaler_2X.BlendLineSteepAndShallow(px, output);
                else
                    Scaler_2X.BlendLineShallow(px, output);
            }
            else
            {
                if (haveSteepLine)
                    Scaler_2X.BlendLineSteep(px, output);
                else
                    Scaler_2X.BlendLineDiagonal(px, output);
            }
        }

        public static void ScaleImage2X(uint[] src, uint[] trg, int srcWidth, int srcHeight)
        {
            int trgWidth = srcWidth << 1;
            byte[] preProcBuffer = new byte[srcWidth];

            uint ker4b, ker4c, ker4e, ker4f, ker4g, ker4h, ker4i, ker4j, ker4k, ker4l, ker4n, ker4o;
            uint ker3_0, ker3_1, ker3_2, ker3_3, ker3_4, ker3_5, ker3_6, ker3_7, ker3_8;

            OutputMatrix outputMatrix = new OutputMatrix(2, trg, trgWidth);


            for (int y = 0; y < srcHeight; ++y)
            {
                int trgi = 2 * y * trgWidth;

                int sM1 = srcWidth * Math.Max(y - 1, 0);
                int s0 = srcWidth * y;
                int sP1 = srcWidth * Math.Min(y + 1, srcHeight - 1);
                int sP2 = srcWidth * Math.Min(y + 2, srcHeight - 1);

                byte blendXy1 = 0;
                byte blendXy = 0;
                for (int x = 0; x < srcWidth; ++x, trgi += 2)
                {

                    int blendResult_f = 0, blendResult_g = 0, blendResult_j = 0, blendResult_k = 0;

                    int xM1 = Math.Max(x - 1, 0);
                    int xP1 = Math.Min(x + 1, srcWidth - 1);
                    int xP2 = Math.Min(x + 2, srcWidth - 1);


                    ker4b = src[sM1 + x];
                    ker4c = src[sM1 + xP1];

                    ker4e = src[s0 + xM1];
                    ker4f = src[s0 + x];
                    ker4g = src[s0 + xP1];
                    ker4h = src[s0 + xP2];

                    ker4i = src[sP1 + xM1];
                    ker4j = src[sP1 + x];
                    ker4k = src[sP1 + xP1];
                    ker4l = src[sP1 + xP2];

                    ker4n = src[sP2 + x];
                    ker4o = src[sP2 + xP1];



                    //--------------------------------------

                    if ((ker4f != ker4g || ker4j != ker4k) && (ker4f != ker4j || ker4g != ker4k))
                    {
                        double jg = DistYCbCr(ker4i, ker4f) + DistYCbCr(ker4f, ker4c) + DistYCbCr(ker4n, ker4k) + DistYCbCr(ker4k, ker4h) + DistYCbCr(ker4j, ker4g) * 4;
                        double fk = DistYCbCr(ker4e, ker4j) + DistYCbCr(ker4j, ker4o) + DistYCbCr(ker4b, ker4g) + DistYCbCr(ker4g, ker4l) + DistYCbCr(ker4f, ker4k) * 4;

                        if (jg < fk)
                        {
                            bool dominantGradient = dominantDirectionThreshold * jg < fk;
                            if (ker4f != ker4g && ker4f != ker4j)
                                blendResult_f = dominantGradient ? BlendDominant : BlendNormal;
                            if (ker4k != ker4j && ker4k != ker4g)
                                blendResult_k = dominantGradient ? BlendDominant : BlendNormal;

                        }
                        else if (fk < jg)
                        {
                            bool dominantGradient = dominantDirectionThreshold * fk < jg;
                            if (ker4j != ker4f && ker4j != ker4k)
                                blendResult_j = dominantGradient ? BlendDominant : BlendNormal;
                            if (ker4g != ker4f && ker4g != ker4k)
                                blendResult_g = dominantGradient ? BlendDominant : BlendNormal;
                        }

                    }
                    //--------------------------------------

                    blendXy = SetBottomR(preProcBuffer[x], blendResult_f);
                    blendXy1 = SetTopR(blendXy1, blendResult_j);
                    preProcBuffer[x] = blendXy1;
                    blendXy1 = SetTopL(0, blendResult_k);

                    if (x + 1 < srcWidth)
                        preProcBuffer[x + 1] = SetBottomL(preProcBuffer[x + 1], blendResult_g);

                    _FillBlock(trg, trgi, trgWidth, src[s0 + x], 2);

                    if (blendXy == 0)
                        continue;

                    ker3_0 = src[sM1 + xM1];
                    ker3_1 = src[sM1 + x];
                    ker3_2 = src[sM1 + xP1];

                    ker3_3 = src[s0 + xM1];
                    ker3_4 = src[s0 + x];
                    ker3_5 = src[s0 + xP1];

                    ker3_6 = src[sP1 + xM1];
                    ker3_7 = src[sP1 + x];
                    ker3_8 = src[sP1 + xP1];

                    _ScalePixel2X(ker3_0, ker3_1, ker3_2, ker3_3, ker3_4, ker3_5, ker3_6, ker3_7, ker3_8, Rot0, trg, trgi, trgWidth, blendXy, outputMatrix);
                    _ScalePixel2X(ker3_0, ker3_1, ker3_2, ker3_3, ker3_4, ker3_5, ker3_6, ker3_7, ker3_8, Rot90, trg, trgi, trgWidth, blendXy, outputMatrix);
                    _ScalePixel2X(ker3_0, ker3_1, ker3_2, ker3_3, ker3_4, ker3_5, ker3_6, ker3_7, ker3_8, Rot180, trg, trgi, trgWidth, blendXy, outputMatrix);
                    _ScalePixel2X(ker3_0, ker3_1, ker3_2, ker3_3, ker3_4, ker3_5, ker3_6, ker3_7, ker3_8, Rot270, trg, trgi, trgWidth, blendXy, outputMatrix);
                }
            }
        }

        #region Scaler
        private class Scaler_2X
        {
            private const int _SCALE = 2;

            public static void BlendLineShallow(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(_SCALE - 1, 0), col);
                _AlphaBlend(3, 4, output.Reference(_SCALE - 1, 1), col);
            }

            public static void BlendLineSteep(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(0, _SCALE - 1), col);
                _AlphaBlend(3, 4, output.Reference(1, _SCALE - 1), col);
            }

            public static void BlendLineSteepAndShallow(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(1, 0), col);
                _AlphaBlend(1, 4, output.Reference(0, 1), col);
                _AlphaBlend(5, 6, output.Reference(1, 1), col); //[!] fixes 7/8 used in xBR
            }

            public static void BlendLineDiagonal(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 2, output.Reference(1, 1), col);
            }

            public static void BlendCorner(uint col, OutputMatrix output)
            {
                _AlphaBlend(21, 100, output.Reference(1, 1), col); //exact: 1 - pi/4 = 0.2146018366
            }
        }
        #endregion
    }
}
