using System.Runtime.CompilerServices;
using Unity.Burst;

/*
MIT License

Copyright(c) 2020 Jordan Peck (jordan.me2@gmail.com)
Copyright(c) 2020 Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

[BurstCompile]
public static class Noise//FastNoise
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FastFloor(float f) { return (f >= 0 ? (int)f : (int)f - 1); }
	
    public static float Simplex(int seed, float freq, float x, float y)
    {
	    x *= freq;
	    y *= freq;
	    
	    // 2D OpenSimplex2 case uses the same algorithm as ordinary Simplex.

	    const float SQRT3 = 1.7320508075688772935274463415059f;
	    const float G2 = (3 - SQRT3) / 6;

	    /*
	     * --- Skew moved to TransformNoiseCoordinate method ---
	     * const FNfloat F2 = 0.5f * (SQRT3 - 1);
	     * FNfloat s = (x + y) * F2;
	     * x += s; y += s;
	    */

	    int i = FastFloor(x);
	    int j = FastFloor(y);
	    float xi = x - i;
	    float yi = y - j;

	    float t = (xi + yi) * G2;
	    float x0 = xi - t;
	    float y0 = yi - t;

	    i *= PrimeX;
	    j *= PrimeY;

	    float n0, n1, n2;

	    float a = 0.5f - x0 * x0 - y0 * y0;
	    if (a <= 0) n0 = 0;
	    else
	    {
		    n0 = (a * a) * (a * a) * GradCoord2D(seed, i, j, x0, y0);
	    }

	    float c = 2 * (1 - 2 * G2) * (1 / G2 - 2) * t + (-2 * (1 - 2 * G2) * (1 - 2 * G2) + a);
	    if (c <= 0) n2 = 0;
	    else
	    {
		    float x2 = x0 + (2 * G2 - 1);
		    float y2 = y0 + (2 * G2 - 1);
		    n2 = (c * c) * (c * c) * GradCoord2D(seed, i + PrimeX, j + PrimeY, x2, y2);
	    }

	    if (y0 > x0)
	    {
		    float x1 = x0 + G2;
		    float y1 = y0 + (G2 - 1);
		    float b = 0.5f - x1 * x1 - y1 * y1;
		    if (b <= 0) n1 = 0;
		    else
		    {
			    n1 = (b * b) * (b * b) * GradCoord2D(seed, i, j + PrimeY, x1, y1);
		    }
	    }
	    else
	    {
		    float x1 = x0 + (G2 - 1);
		    float y1 = y0 + G2;
		    float b = 0.5f - x1 * x1 - y1 * y1;
		    if (b <= 0) n1 = 0;
		    else
		    {
			    n1 = (b * b) * (b * b) * GradCoord2D(seed, i + PrimeX, j, x1, y1);
		    }
	    }

	    return (n0 + n1 + n2) * 99.83685446303647f;
    }

    private const int PrimeX = 501125321;
    private const int PrimeY = 1136930381;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash(int seed, int xPrimed, int yPrimed)
    {
	    int hash = seed ^ xPrimed ^ yPrimed;

	    hash *= 0x27d4eb2d;
	    return hash;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GradCoord2D(int seed, int x, int y, float xd, float yd)
    {
	    int hash = Hash(seed, x, y);
	    hash ^= hash >> 15;
	    hash &= 127 << 1;

	    float xg = Gradients2D[hash];
	    float yg = Gradients2D[hash | 1];

	    return xd * xg + yd * yg;
    }

    private static readonly float[] Gradients2D =
    {
         0.130526192220052f,  0.99144486137381f,   0.38268343236509f,   0.923879532511287f,  0.608761429008721f,  0.793353340291235f,  0.793353340291235f,  0.608761429008721f,
         0.923879532511287f,  0.38268343236509f,   0.99144486137381f,   0.130526192220051f,  0.99144486137381f,  -0.130526192220051f,  0.923879532511287f, -0.38268343236509f,
         0.793353340291235f, -0.60876142900872f,   0.608761429008721f, -0.793353340291235f,  0.38268343236509f,  -0.923879532511287f,  0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f,  -0.38268343236509f,  -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
        -0.923879532511287f, -0.38268343236509f,  -0.99144486137381f,  -0.130526192220052f, -0.99144486137381f,   0.130526192220051f, -0.923879532511287f,  0.38268343236509f,
        -0.793353340291235f,  0.608761429008721f, -0.608761429008721f,  0.793353340291235f, -0.38268343236509f,   0.923879532511287f, -0.130526192220052f,  0.99144486137381f,
         0.130526192220052f,  0.99144486137381f,   0.38268343236509f,   0.923879532511287f,  0.608761429008721f,  0.793353340291235f,  0.793353340291235f,  0.608761429008721f,
         0.923879532511287f,  0.38268343236509f,   0.99144486137381f,   0.130526192220051f,  0.99144486137381f,  -0.130526192220051f,  0.923879532511287f, -0.38268343236509f,
         0.793353340291235f, -0.60876142900872f,   0.608761429008721f, -0.793353340291235f,  0.38268343236509f,  -0.923879532511287f,  0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f,  -0.38268343236509f,  -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
        -0.923879532511287f, -0.38268343236509f,  -0.99144486137381f,  -0.130526192220052f, -0.99144486137381f,   0.130526192220051f, -0.923879532511287f,  0.38268343236509f,
        -0.793353340291235f,  0.608761429008721f, -0.608761429008721f,  0.793353340291235f, -0.38268343236509f,   0.923879532511287f, -0.130526192220052f,  0.99144486137381f,
         0.130526192220052f,  0.99144486137381f,   0.38268343236509f,   0.923879532511287f,  0.608761429008721f,  0.793353340291235f,  0.793353340291235f,  0.608761429008721f,
         0.923879532511287f,  0.38268343236509f,   0.99144486137381f,   0.130526192220051f,  0.99144486137381f,  -0.130526192220051f,  0.923879532511287f, -0.38268343236509f,
         0.793353340291235f, -0.60876142900872f,   0.608761429008721f, -0.793353340291235f,  0.38268343236509f,  -0.923879532511287f,  0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f,  -0.38268343236509f,  -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
        -0.923879532511287f, -0.38268343236509f,  -0.99144486137381f,  -0.130526192220052f, -0.99144486137381f,   0.130526192220051f, -0.923879532511287f,  0.38268343236509f,
        -0.793353340291235f,  0.608761429008721f, -0.608761429008721f,  0.793353340291235f, -0.38268343236509f,   0.923879532511287f, -0.130526192220052f,  0.99144486137381f,
         0.130526192220052f,  0.99144486137381f,   0.38268343236509f,   0.923879532511287f,  0.608761429008721f,  0.793353340291235f,  0.793353340291235f,  0.608761429008721f,
         0.923879532511287f,  0.38268343236509f,   0.99144486137381f,   0.130526192220051f,  0.99144486137381f,  -0.130526192220051f,  0.923879532511287f, -0.38268343236509f,
         0.793353340291235f, -0.60876142900872f,   0.608761429008721f, -0.793353340291235f,  0.38268343236509f,  -0.923879532511287f,  0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f,  -0.38268343236509f,  -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
        -0.923879532511287f, -0.38268343236509f,  -0.99144486137381f,  -0.130526192220052f, -0.99144486137381f,   0.130526192220051f, -0.923879532511287f,  0.38268343236509f,
        -0.793353340291235f,  0.608761429008721f, -0.608761429008721f,  0.793353340291235f, -0.38268343236509f,   0.923879532511287f, -0.130526192220052f,  0.99144486137381f,
         0.130526192220052f,  0.99144486137381f,   0.38268343236509f,   0.923879532511287f,  0.608761429008721f,  0.793353340291235f,  0.793353340291235f,  0.608761429008721f,
         0.923879532511287f,  0.38268343236509f,   0.99144486137381f,   0.130526192220051f,  0.99144486137381f,  -0.130526192220051f,  0.923879532511287f, -0.38268343236509f,
         0.793353340291235f, -0.60876142900872f,   0.608761429008721f, -0.793353340291235f,  0.38268343236509f,  -0.923879532511287f,  0.130526192220052f, -0.99144486137381f,
        -0.130526192220052f, -0.99144486137381f,  -0.38268343236509f,  -0.923879532511287f, -0.608761429008721f, -0.793353340291235f, -0.793353340291235f, -0.608761429008721f,
        -0.923879532511287f, -0.38268343236509f,  -0.99144486137381f,  -0.130526192220052f, -0.99144486137381f,   0.130526192220051f, -0.923879532511287f,  0.38268343236509f,
        -0.793353340291235f,  0.608761429008721f, -0.608761429008721f,  0.793353340291235f, -0.38268343236509f,   0.923879532511287f, -0.130526192220052f,  0.99144486137381f,
         0.38268343236509f,   0.923879532511287f,  0.923879532511287f,  0.38268343236509f,   0.923879532511287f, -0.38268343236509f,   0.38268343236509f,  -0.923879532511287f,
        -0.38268343236509f,  -0.923879532511287f, -0.923879532511287f, -0.38268343236509f,  -0.923879532511287f,  0.38268343236509f,  -0.38268343236509f,   0.923879532511287f,
    };
}
