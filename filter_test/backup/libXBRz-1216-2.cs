using System;

namespace Imager.Filters_fixed
{
    public class HS_XBRz
    {
        const double dominantDirectionThreshold = 3.6;
        const double steepDirectionThreshold = 2.2;
        const double eqColorThres = 900;

        const int Ymask = 0x00ff0000;
        const int Umask = 0x0000ff00;
        const int Vmask = 0x000000ff;

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

        private static Tuple _BuildMatrixRotation(int rotDeg, int i, int j, int n)
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


        enum RotationDegree
        {
            Rot0 = 0,
            Rot90 = 1,
            Rot180 = 2,
            Rot270 = 3,
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

        enum BlendType : byte
        {
            BlendNone = 0, //do not blend
            BlendNormal = 1, //a normal indication to blend
            BlendDominant = 2, //a strong indication to blend
        }

        private class BlendResult
        {
            public BlendType f;
            public BlendType g;
            public BlendType j;
            public BlendType k;

            public void Reset()
            {
                this.f = this.g = this.j = this.k = BlendType.BlendNone;
            }
        }

        private class Kernel_3X3
        {
            public readonly uint[] _ = new uint[3 * 3];
        }

        struct Kernel_4X4
        {
            public uint b, c, e, f, g, h, i, j, k, l, n, o;
        }


        private static void _PreProcessCorners(Kernel_4X4 kernel, BlendResult blendResult)
        {
            blendResult.Reset();

            if ((kernel.f == kernel.g && kernel.j == kernel.k) || (kernel.f == kernel.j && kernel.g == kernel.k))
                return;

            int weight = 4;
            double jg = DistYCbCr(kernel.i, kernel.f) + DistYCbCr(kernel.f, kernel.c) + DistYCbCr(kernel.n, kernel.k) + DistYCbCr(kernel.k, kernel.h) + weight * DistYCbCr(kernel.j, kernel.g);
            double fk = DistYCbCr(kernel.e, kernel.j) + DistYCbCr(kernel.j, kernel.o) + DistYCbCr(kernel.b, kernel.g) + DistYCbCr(kernel.g, kernel.l) + weight * DistYCbCr(kernel.f, kernel.k);

            if (jg < fk)
            {

                var dominantGradient = dominantDirectionThreshold * jg < fk;

                if (kernel.f != kernel.g && kernel.f != kernel.j)
                    blendResult.f = dominantGradient ? BlendType.BlendDominant : BlendType.BlendNormal;

                if (kernel.k != kernel.j && kernel.k != kernel.g)
                    blendResult.k = dominantGradient ? BlendType.BlendDominant : BlendType.BlendNormal;

            }
            else if (fk < jg)
            {

                var dominantGradient = dominantDirectionThreshold * fk < jg;



                if (kernel.j != kernel.f && kernel.j != kernel.k)
                    blendResult.j = dominantGradient ? BlendType.BlendDominant : BlendType.BlendNormal;

                if (kernel.g != kernel.f && kernel.g != kernel.k)
                    blendResult.g = dominantGradient ? BlendType.BlendDominant : BlendType.BlendNormal;

            }

        }

        private static class Rot
        {
            public static readonly int[] _ = new int[9 * 4];

            static Rot()
            {
                const int a = 0, b = 1, c = 2, d = 3, e = 4, f = 5, g = 6, h = 7, i = 8;
                int[] deg0 = new[] { a, b, c, d, e, f, g, h, i };
                int[] deg90 = new[] { g, d, a, h, e, b, i, f, c };
                int[] deg180 = new[] { i, h, g, f, e, d, c, b, a };
                int[] deg270 = new[] { c, f, i, b, e, h, a, d, g };
                var rotation = new[] { deg0, deg90, deg180, deg270 };
                for (var rotDeg = 0; rotDeg < 4; rotDeg++)
                    for (var x = 0; x < 9; x++)
                        _[(x << 2) + rotDeg] = rotation[rotDeg][x];
            }
        }
        private static void _ScalePixel2X(
          RotationDegree rotDeg,
          Kernel_3X3 ker,
          uint[] trg,
          int trgi,
          int trgWidth,
          byte blendInfo,//result of preprocessing all four corners of pixel "e"
          OutputMatrix outputMatrix
          )
        {
            var b = ker._[Rot._[(1 << 2) + (int)rotDeg]];
            var c = ker._[Rot._[(2 << 2) + (int)rotDeg]];
            var d = ker._[Rot._[(3 << 2) + (int)rotDeg]];
            var e = ker._[Rot._[(4 << 2) + (int)rotDeg]];
            var f = ker._[Rot._[(5 << 2) + (int)rotDeg]];
            var g = ker._[Rot._[(6 << 2) + (int)rotDeg]];
            var h = ker._[Rot._[(7 << 2) + (int)rotDeg]];
            var i = ker._[Rot._[(8 << 2) + (int)rotDeg]];

            var blend = Rotate(blendInfo, rotDeg);

            if (GetBottomR(blend) == BlendType.BlendNone)
                return;

            bool doLineBlend;

            if (GetBottomR(blend) >= BlendType.BlendDominant)
                doLineBlend = true;
            else if (GetTopR(blend) != BlendType.BlendNone && !ColorEQ(e, g))
                doLineBlend = false;
            else if (GetBottomL(blend) != BlendType.BlendNone && !ColorEQ(e, c))
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

        public static void ScaleImage2X( uint[] src, uint[] trg, int srcWidth, int srcHeight)
        {
            int trgWidth = srcWidth * 2;
            byte[] preProcBuffer = new byte[srcWidth];

            Kernel_3X3 ker3 = new Kernel_3X3();
            Kernel_4X4 ker4 = new Kernel_4X4();

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
                    int xM1 = Math.Max(x - 1, 0);
                    int xP1 = Math.Min(x + 1, srcWidth - 1);
                    int xP2 = Math.Min(x + 2, srcWidth - 1);


                    ker4.b = src[sM1 + x];
                    ker4.c = src[sM1 + xP1];

                    ker4.e = src[s0 + xM1];
                    ker4.f = src[s0 + x];
                    ker4.g = src[s0 + xP1];
                    ker4.h = src[s0 + xP2];

                    ker4.i = src[sP1 + xM1];
                    ker4.j = src[sP1 + x];
                    ker4.k = src[sP1 + xP1];
                    ker4.l = src[sP1 + xP2];

                    ker4.n = src[sP2 + x];
                    ker4.o = src[sP2 + xP1];

                    var blendResult = new BlendResult();
                    _PreProcessCorners(ker4, blendResult); // writes to blendResult

                    blendXy = SetBottomR(preProcBuffer[x], blendResult.f);

                    blendXy1 = SetTopR(blendXy1, blendResult.j);

                    preProcBuffer[x] = blendXy1;

                    blendXy1 = SetTopL(0, blendResult.k);

                    if (x + 1 < srcWidth)
                        preProcBuffer[x + 1] = SetBottomL(preProcBuffer[x + 1], blendResult.g);



                    _FillBlock(trg, trgi, trgWidth, src[s0 + x], 2);

                    if (blendXy == 0)
                        continue;

                    ker3._[0] = src[sM1 + xM1];
                    ker3._[1] = src[sM1 + x];
                    ker3._[2] = src[sM1 + xP1];

                    ker3._[3] = src[s0 + xM1];
                    ker3._[4] = src[s0 + x];
                    ker3._[5] = src[s0 + xP1];

                    ker3._[6] = src[sP1 + xM1];
                    ker3._[7] = src[sP1 + x];
                    ker3._[8] = src[sP1 + xP1];

                    _ScalePixel2X( RotationDegree.Rot0, ker3, trg, trgi, trgWidth, blendXy, outputMatrix);
                    _ScalePixel2X( RotationDegree.Rot90, ker3, trg, trgi, trgWidth, blendXy, outputMatrix);
                    _ScalePixel2X( RotationDegree.Rot180, ker3, trg, trgi, trgWidth, blendXy, outputMatrix);
                    _ScalePixel2X( RotationDegree.Rot270, ker3, trg, trgi, trgWidth, blendXy, outputMatrix);
                }
            }
        }

        static BlendType GetTopL(byte b)
        {
            return (BlendType)((b) & 0x3);
        }

        static BlendType GetTopR(byte b)
        {
            return (BlendType)((b >> 2) & 0x3);
        }

        static BlendType GetBottomR(byte b)
        {
            return (BlendType)((b >> 4) & 0x3);
        }

        static BlendType GetBottomL(byte b)
        {
            return (BlendType)((b >> 6) & 0x3);
        }

        static byte SetTopL(byte b, BlendType bt)
        {
            return (byte)(b | (byte)bt);
        }

        static byte SetTopR(byte b, BlendType bt)
        {
            return (byte)(b | ((byte)bt << 2));
        }

        static byte SetBottomR(byte b, BlendType bt)
        {
            return (byte)(b | ((byte)bt << 4));
        }

        static byte SetBottomL(byte b, BlendType bt)
        {
            return (byte)(b | ((byte)bt << 6));
        }

        static byte Rotate(byte b, RotationDegree rotDeg)
        {
            int l = (int)rotDeg << 1;
            int r = 8 - l;
            return (byte)(b << l | b >> r);
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

            public void Move(RotationDegree rotDeg, int outi)
            {
                this._nr = this._n + (int)rotDeg * _MAX_SCALE_SQUARED;
                this._outi = outi;
            }

            public ImagePointer Reference(int i, int j)
            {
                var rot = _MATRIX_ROTATION[this._nr + i * _MAX_SCALE + j];
                this._output.Position(this._outi + rot.Item2 + rot.Item1 * this._outWidth);
                return this._output
                  ;
            }
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

        class ImagePointer
        {
            private readonly uint[] _imageData;
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

        private class Scaler_2X 
        {
            private const int _SCALE = 2;

            public static void BlendLineShallow(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(_SCALE - 1, 0), col);
                _AlphaBlend(3, 4, output.Reference(_SCALE - 1, 1), col);
            }

            public static  void BlendLineSteep(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(0, _SCALE - 1), col);
                _AlphaBlend(3, 4, output.Reference(1, _SCALE - 1), col);
            }

            public static  void BlendLineSteepAndShallow(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(1, 0), col);
                _AlphaBlend(1, 4, output.Reference(0, 1), col);
                _AlphaBlend(5, 6, output.Reference(1, 1), col); //[!] fixes 7/8 used in xBR
            }

            public static  void BlendLineDiagonal(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 2, output.Reference(1, 1), col);
            }

            public static  void BlendCorner(uint col, OutputMatrix output)
            {
                _AlphaBlend(21, 100, output.Reference(1, 1), col); //exact: 1 - pi/4 = 0.2146018366
            }
        }
    }
}
