using System;

namespace Imager.Filters_fixed
{
    public class HS_XBRz
    {

        public class ScaleSize
        {
            public static readonly ScaleSize TIMES2 = new ScaleSize(_SCALER2_X);

            private ScaleSize(IScaler scaler)
            {
                this.scaler = scaler;
                this.size = scaler.Scale();
            }

            internal IScaler scaler;
            public int size;
        }

        public class ScalerCfg
        {
            // These are the default values:
            public double luminanceWeight = 1;
            public double equalColorTolerance = 30;
            public double dominantDirectionThreshold = 3.6;
            public double steepDirectionThreshold = 2.2;
        }

        private static readonly ScalerCfg _CONFIGURATION = new ScalerCfg();


        static uint makePixel(byte r, byte g, byte b, byte a)
        {
            return (uint)a << 24 | (uint)r << 16 | (uint)g << 8 | b;
        }

        public static uint Interpolate(uint pixel1, uint pixel2, int quantifier1, int quantifier2)
        {
            var total = (uint)(quantifier1 + quantifier2);
            return (makePixel(
              (byte)(((getRed(pixel1) * quantifier1 + getRed(pixel2) * quantifier2) / total) & 0xff),
              (byte)(((getGreen(pixel1) * quantifier1 + getGreen(pixel2) * quantifier2) / total) & 0xff),
              (byte)(((getBlue(pixel1) * quantifier1 + getBlue(pixel2) * quantifier2) / total) & 0xff),
              (byte)(((getAlpha(pixel1) * quantifier1 + getAlpha(pixel2) * quantifier2) / total) & 0xff)
              ));
        }



        private static void _AlphaBlend(int n, int m, ImagePointer dstPtr, uint col)
        {
            dstPtr.SetPixel(Interpolate(col, dstPtr.GetPixel(), n, m - n));
        }
        private static void _FillBlock(uint[] trg, int trgi, int pitch, uint col, int blockSize)
        {
            for (var y = 0; y < blockSize; ++y, trgi += pitch)
                for (var x = 0; x < blockSize; ++x)
                    trg[trgi + x] = col;
        }


        private static double _Square(double value)
        {
            return value * value;
        }

        static byte getByte(uint val, int N) { return (byte)((val >> (8 * N)) & 0xff); }
        static byte getAlpha(uint pix) { return getByte(pix, 3); }
        static byte getRed(uint pix) { return getByte(pix, 2); }
        static byte getGreen(uint pix) { return getByte(pix, 1); }
        static byte getBlue(uint pix) { return getByte(pix, 0); }

        private static double _DistYCbCr(uint pix1, uint pix2, double lumaWeight)
        {
            var rDiff = getRed(pix1) - getRed(pix2);
            var gDiff = getGreen(pix1) - getGreen(pix2);
            var bDiff = getBlue(pix1) - getBlue(pix2);


            const double kB = 0.0722; //ITU-R BT.709 conversion
            const double kR = 0.2126; //
            const double kG = 1 - kB - kR;

            const double scaleB = 0.5 / (1 - kB);
            const double scaleR = 0.5 / (1 - kR);

            var y = kR * rDiff + kG * gDiff + kB * bDiff; //[!], analog YCbCr!
            var cB = scaleB * (bDiff - y);
            var cR = scaleR * (rDiff - y);
            return _Square(lumaWeight * y) + _Square(cB) + _Square(cR);
        }


        private static double _ColorDist(uint pix1, uint pix2, double luminanceWeight)
        {
            return pix1 == pix2 ? 0 : _DistYCbCr(pix1, pix2, luminanceWeight);
        }

        private enum BlendType : byte
        {
            // These blend types must fit into 2 bits.
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

        private class Kernel_4X4
        {
            public uint b, c;
            public uint e, f, g, h;
            public uint i, j, k, l;
            public uint n, o;
        }


        private static void _PreProcessCorners(Kernel_4X4 kernel, BlendResult blendResult, IColorDist preProcessCornersColorDist)
        {
            blendResult.Reset();

            if ((kernel.f == kernel.g && kernel.j == kernel.k) || (kernel.f == kernel.j && kernel.g == kernel.k))
                return;

            var dist = preProcessCornersColorDist;

            var weight = 4;
            var jg = dist._(kernel.i, kernel.f) + dist._(kernel.f, kernel.c) + dist._(kernel.n, kernel.k) + dist._(kernel.k, kernel.h) + weight * dist._(kernel.j, kernel.g);
            var fk = dist._(kernel.e, kernel.j) + dist._(kernel.j, kernel.o) + dist._(kernel.b, kernel.g) + dist._(kernel.g, kernel.l) + weight * dist._(kernel.f, kernel.k);

            if (jg < fk)
            {

                var dominantGradient = _CONFIGURATION.dominantDirectionThreshold * jg < fk;

                if (kernel.f != kernel.g && kernel.f != kernel.j)
                    blendResult.f = dominantGradient ? BlendType.BlendDominant : BlendType.BlendNormal;

                if (kernel.k != kernel.j && kernel.k != kernel.g)
                    blendResult.k = dominantGradient ? BlendType.BlendDominant : BlendType.BlendNormal;

            }
            else if (fk < jg)
            {

                var dominantGradient = _CONFIGURATION.dominantDirectionThreshold * fk < jg;



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
                const int
                  a = 0,
                  b = 1,
                  c = 2,
                  d = 3,
                  e = 4,
                  f = 5,
                  g = 6,
                  h = 7,
                  i = 8;

                var deg0 = new[] {
          a, b, c,
          d, e, f,
          g, h, i
        };

                var deg90 = new[] {
          g, d, a,
          h, e, b,
          i, f, c
        };

                var deg180 = new[] {
          i, h, g,
          f, e, d,
          c, b, a
        };

                var deg270 = new[] {
          c, f, i,
          b, e, h,
          a, d, g
        };

                var rotation = new[] {
          deg0, deg90, deg180, deg270
        };

                for (var rotDeg = 0; rotDeg < 4; rotDeg++)
                    for (var x = 0; x < 9; x++)
                        _[(x << 2) + rotDeg] = rotation[rotDeg][x];
            }
        }
        private static void _ScalePixel(
          IScaler scaler,
          RotationDegree rotDeg,
          Kernel_3X3 ker,
          uint[] trg,
          int trgi,
          int trgWidth,
          byte blendInfo,//result of preprocessing all four corners of pixel "e"
          IColorEq scalePixelColorEq,
          IColorDist scalePixelColorDist,
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

            var blend = BlendInfo.Rotate(blendInfo, rotDeg);

            if (BlendInfo.GetBottomR(blend) == BlendType.BlendNone)
                return;

            var eq = scalePixelColorEq;
            var dist = scalePixelColorDist;

            bool doLineBlend;

            if (BlendInfo.GetBottomR(blend) >= BlendType.BlendDominant)
                doLineBlend = true;
            else if (BlendInfo.GetTopR(blend) != BlendType.BlendNone && !eq._(e, g))
                doLineBlend = false;
            else if (BlendInfo.GetBottomL(blend) != BlendType.BlendNone && !eq._(e, c))
                doLineBlend = false;
            else if (eq._(g, h) && eq._(h, i) && eq._(i, f) && eq._(f, c) && !eq._(e, i))
                doLineBlend = false;
            else
                doLineBlend = true;
            var px = dist._(e, f) <= dist._(e, h) ? f : h;

            var output = outputMatrix;
            output.Move(rotDeg, trgi);

            if (!doLineBlend)
            {
                scaler.BlendCorner(px, output);
                return;
            }

            var fg = dist._(f, g);
            var hc = dist._(h, c);

            var haveShallowLine = _CONFIGURATION.steepDirectionThreshold * fg <= hc && e != g && d != g;
            var haveSteepLine = _CONFIGURATION.steepDirectionThreshold * hc <= fg && e != c && b != c;

            if (haveShallowLine)
            {
                if (haveSteepLine)
                    scaler.BlendLineSteepAndShallow(px, output);
                else
                    scaler.BlendLineShallow(px, output);
            }
            else
            {
                if (haveSteepLine)
                    scaler.BlendLineSteep(px, output);
                else
                    scaler.BlendLineDiagonal(px, output);
            }
        }
        private class ColorDistA : IColorDist
        {
            public double _(uint col1, uint col2)
            {
                return _ColorDist(col1, col2, _CONFIGURATION.luminanceWeight);
            }
        }

        private class ColorEqA : IColorEq
        {
            private readonly double _eqColorThres;

            public ColorEqA(double a)
            {
                this._eqColorThres = a;
            }

            public bool _(uint col1, uint col2)
            {
                return _ColorDist(col1, col2, _CONFIGURATION.luminanceWeight) < this._eqColorThres;
            }
        }

        public static void ScaleImage(ScaleSize scaleSize, uint[] src, uint[] trg, int srcWidth, int srcHeight )
        {
            var trgWidth = srcWidth * scaleSize.size;
            var preProcBuffer = new byte[srcWidth];

            var ker4 = new Kernel_4X4();
            var preProcessCornersColorDist = new ColorDistA();
            var eqColorThres = _Square(_CONFIGURATION.equalColorTolerance);
            var scalePixelColorEq = new ColorEqA(eqColorThres);
            var scalePixelColorDist = new ColorDistA();
            var outputMatrix = new OutputMatrix(scaleSize.size, trg, trgWidth);

            var ker3 = new Kernel_3X3();

            for (var y = 0; y < srcHeight ; ++y)
            {
                var trgi = scaleSize.size * y * trgWidth;

                var sM1 = srcWidth * Math.Max(y - 1, 0);
                var s0 = srcWidth * y;
                var sP1 = srcWidth * Math.Min(y + 1, srcHeight - 1);
                var sP2 = srcWidth * Math.Min(y + 2, srcHeight - 1);

                byte blendXy1 = 0;

                for (var x = 0; x < srcWidth; ++x, trgi += scaleSize.size)
                {
                    var xM1 = Math.Max(x - 1, 0);
                    var xP1 = Math.Min(x + 1, srcWidth - 1);
                    var xP2 = Math.Min(x + 2, srcWidth - 1);

                    byte blendXy;
                    {
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
                        _PreProcessCorners(ker4, blendResult, preProcessCornersColorDist); // writes to blendResult

                        blendXy = BlendInfo.SetBottomR(preProcBuffer[x], blendResult.f);

                        blendXy1 = BlendInfo.SetTopR(blendXy1, blendResult.j);

                        preProcBuffer[x] = blendXy1;

                        blendXy1 = BlendInfo.SetTopL(0, blendResult.k);

                        if (x + 1 < srcWidth)
                            preProcBuffer[x + 1] = BlendInfo.SetBottomL(preProcBuffer[x + 1], blendResult.g);
                    }


                    _FillBlock(trg, trgi, trgWidth, src[s0 + x], scaleSize.size);



                    if (blendXy == 0)
                        continue;

                    const int a = 0, b = 1, c = 2, d = 3, e = 4, f = 5, g = 6, h = 7, i = 8;

                    ker3._[a] = src[sM1 + xM1];
                    ker3._[b] = src[sM1 + x];
                    ker3._[c] = src[sM1 + xP1];

                    ker3._[d] = src[s0 + xM1];
                    ker3._[e] = src[s0 + x];
                    ker3._[f] = src[s0 + xP1];

                    ker3._[g] = src[sP1 + xM1];
                    ker3._[h] = src[sP1 + x];
                    ker3._[i] = src[sP1 + xP1];

                    _ScalePixel(scaleSize.scaler, RotationDegree.Rot0, ker3, trg, trgi, trgWidth, blendXy, scalePixelColorEq, scalePixelColorDist, outputMatrix);
                    _ScalePixel(scaleSize.scaler, RotationDegree.Rot90, ker3, trg, trgi, trgWidth, blendXy, scalePixelColorEq, scalePixelColorDist, outputMatrix);
                    _ScalePixel(scaleSize.scaler, RotationDegree.Rot180, ker3, trg, trgi, trgWidth, blendXy, scalePixelColorEq, scalePixelColorDist, outputMatrix);
                    _ScalePixel(scaleSize.scaler, RotationDegree.Rot270, ker3, trg, trgi, trgWidth, blendXy, scalePixelColorEq, scalePixelColorDist, outputMatrix);
                }
            }
        }

        private interface IColorEq
        {
            bool _(uint col1, uint col2);
        }

        private interface IColorDist
        {
            double _(uint col1, uint col2);
        }

        private static class BlendInfo
        {
            public static BlendType GetTopL(byte b)
            {
                return (BlendType)((b) & 0x3);
            }

            public static BlendType GetTopR(byte b)
            {
                return (BlendType)((b >> 2) & 0x3);
            }

            public static BlendType GetBottomR(byte b)
            {
                return (BlendType)((b >> 4) & 0x3);
            }

            public static BlendType GetBottomL(byte b)
            {
                return (BlendType)((b >> 6) & 0x3);
            }

            public static byte SetTopL(byte b, BlendType bt)
            {
                return (byte)(b | (byte)bt);
            }

            public static byte SetTopR(byte b, BlendType bt)
            {
                return (byte)(b | ((byte)bt << 2));
            }

            public static byte SetBottomR(byte b, BlendType bt)
            {
                return (byte)(b | ((byte)bt << 4));
            }

            public static byte SetBottomL(byte b, BlendType bt)
            {
                return (byte)(b | ((byte)bt << 6));
            }

            public static byte Rotate(byte b, RotationDegree rotDeg)
            {
                var l = (int)rotDeg << 1;
                var r = 8 - l;

                return (byte)(b << l | b >> r);
            }
        }
        internal enum RotationDegree
        {
            Rot0 = 0,
            Rot90 = 1,
            Rot180 = 2,
            Rot270 = 3,
        }

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

        internal class OutputMatrix
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

        private class Tuple
        {
            public int Item1 { get; private set; }
            public int Item2 { get; private set; }

            public Tuple(int i, int j)
            {
                this.Item1 = i;
                this.Item2 = j;
            }
        }

        internal class ImagePointer
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

        internal interface IScaler
        {
            int Scale();
            void BlendLineSteep(uint col, OutputMatrix output);
            void BlendLineSteepAndShallow(uint col, OutputMatrix output);
            void BlendLineShallow(uint col, OutputMatrix output);
            void BlendLineDiagonal(uint col, OutputMatrix output);
            void BlendCorner(uint col, OutputMatrix output);
        }

        private class Scaler_2X : IScaler
        {
            private const int _SCALE = 2;

            public int Scale()
            {
                return _SCALE;
            }

            public void BlendLineShallow(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(_SCALE - 1, 0), col);
                _AlphaBlend(3, 4, output.Reference(_SCALE - 1, 1), col);
            }

            public void BlendLineSteep(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(0, _SCALE - 1), col);
                _AlphaBlend(3, 4, output.Reference(1, _SCALE - 1), col);
            }

            public void BlendLineSteepAndShallow(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 4, output.Reference(1, 0), col);
                _AlphaBlend(1, 4, output.Reference(0, 1), col);
                _AlphaBlend(5, 6, output.Reference(1, 1), col); //[!] fixes 7/8 used in xBR
            }

            public void BlendLineDiagonal(uint col, OutputMatrix output)
            {
                _AlphaBlend(1, 2, output.Reference(1, 1), col);
            }

            public void BlendCorner(uint col, OutputMatrix output)
            {
                _AlphaBlend(21, 100, output.Reference(1, 1), col); //exact: 1 - pi/4 = 0.2146018366
            }
        }
        private static readonly IScaler _SCALER2_X = new Scaler_2X();
    }
}
