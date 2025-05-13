using System;
using System.Collections.Generic;
using UnityEngine;

public class PerChunkQef
{
    public void InitializeFull()
    {
        dataNodes.Clear();
        pos.Clear();
        nor.Clear();
        numPoints = 0;
    }
    public void InitializeBeforeCalc()
    {
        pos.Clear();
        nor.Clear();
        numPoints = 0;
    }
    public List<Octree.Node> dataNodes = new List<Octree.Node>();
    
    double[] vector = new double[12];
    double[] matrix = new double[3 * 12];
    private QEF qef = new QEF();
    List<Vector3> pos = new List<Vector3>(12);
    List<Vector3> nor = new List<Vector3>(12);
    public int numPoints = 0;

    public Vector3 MassPoint()
    {
        if (numPoints == 0) return Vector3.zero;
        Vector3 v = Vector3.zero;
        for (int i = 0; i < pos.Count; i++)
            v += pos[i];
        v /= pos.Count;
        return v;
    }
    public Vector3 AvgNormal()
    {
        if (numPoints == 0) return Vector3.zero;
        Vector3 normal = Vector3.zero;

        foreach(Vector3 n in nor)
        {
            normal += n;
        }
        normal /= numPoints;

        if (normal == Vector3.zero)
        {
            return Vector3.up;
        }

        return normal.normalized;
    }

    public void Add(Vector3 pos, Vector3 normal)
    {      
        if (!this.pos.Contains(pos))
        {
            numPoints++;
            this.pos.Add(pos);
            this.nor.Add(normal);
        }
    }

    public Vector3 Solve()
    {
        Vector3 vert;
        Vector3 massPoint = MassPoint();

        for (int i = 0; i < numPoints; i++)
        {
            Vector3 p = pos[i] - massPoint;
            Vector3 n = nor[i];
            vector[i] = n.x * p.x + n.y * p.y + n.z * p.z;
            matrix[i * 3 + 0] = n.x;
            matrix[i * 3 + 1] = n.y;
            matrix[i * 3 + 2] = n.z;
        }
        
        Vector3 v = qef.Evaluate(matrix, vector, pos.Count);
        vert = v + massPoint;

        return vert;
    }
}

//https://www.oocities.org/tzukkers/isosurf/isosurfaces.html
public class QEF
{
    private const int Maxrows = 12;
    private const double Epsilon = 1e-5;
    private const int Ux = Maxrows;
    private const int Uy = 3;
    private const int Vx = 3;
    private const int Vy = 3;

    private readonly double[] u;
    private readonly double[] v;
    private readonly double[] u1;
    private readonly double[] v1;
    private readonly double[] d;
    private double[] tau_u, tau_v, tau_u1, tau_v1;
    private readonly double[] ptrs;
    private readonly double[] w;
    private readonly double[] result;

    public QEF()
    {
        u = new double[Ux * Uy];
        u1 = new double[Ux * Uy];

        v = new double[Ux * Vy];
        v1 = new double[Ux * Vy];

        d = new double[3];
        tau_u = d;
        tau_v = new double[2];

        tau_u1 = new double[3];
        tau_v1 = new double[2];

        ptrs = new double[Maxrows];

        w = new double[3];

        result = new double[3];
    }

    private void Reset()
    {
        for (var i = 0; i < u.Length; ++i)
        {
            u[i] = 0;
            u1[i] = 0;
        }

        for (var i = 0; i < v.Length; ++i)
        {
            v[i] = 0;
            v1[i] = 0;
        }

        for (var i = 0; i < 3; ++i)
        {
            d[i] = 0;
            tau_u1[i] = 0;
            w[i] = 0;
            result[i] = 0;
        }

        for (var i = 0; i < 2; ++i)
        {
            tau_v[i] = 0;
            tau_v1[i] = 0;
        }
    }

    //----------------------------------------------------------------------------

    internal Vector3 Evaluate(double[] mat, double[] vec, int rows)
    {
        // Reset u, v and d to 0
        Reset();

        // perform singular value decomposition on matrix mat into u, v and d.
        // u is a matrix of rows x 3 (same as mat);
        // v is a square matrix 3 x 3 (for 3 columns in mat);
        // d is vector of 3 values representing the diagonal matrix 3 x 3 (for 3 colums in mat).
        ComputeSVD(mat, rows);

        // solve linear system given by mat and vec using the
        // singular value decomposition of mat into u, v and d.
        SolveSVD(vec, rows);

        return new Vector3((float)result[0], (float)result[1], (float)result[2]);
    }

    //----------------------------------------------------------------------------

    private void ComputeSVD(
        double[] mat, // matrix (rows x 3)
        int rows)
    {
        // copy mat into u
        for (var i = 0; i < rows; ++i)
        {
            for (var j = 0; j < 3; ++j)
            {
                u[i + Ux * j] = mat[i * 3 + j];
            }
        }

        //factorize(u, tau_u, tau_v, rows);
        Factorize(rows);

        //unpack(u, v, tau_u, tau_v, rows);
        Unpack(rows);

        //diagonalize(u, v, tau_u, tau_v, rows);
        Diagonalize(rows);

        //singularize(u, v, tau_u, rows);
        Singularize(rows);
    }

    //----------------------------------------------------------------------------

    private void Factorize(int rows)
    {
        int y;

        // bidiagonal factorization of (rows x 3) matrix into :-
        // tau_u, a vector of 1x3 (for 3 columns in the matrix)
        // tau_v, a vector of 1x2 (one less column than the matrix)
        for (var i = 0; i < 3; ++i)
        {
            // set up a vector to reference into the matrix
            // from u(i,i) to u(m,i), that is, from the
            // i'th column of the i'th row and down all the way
            // through that column
            var num_ptrs = rows - i;
            for (var q = 0; q < num_ptrs; ++q)
                ptrs[q] = u[(q + i) + Ux * i];

            // perform householder transformation on this vector
            var tau = Factorize_hh(num_ptrs);
            // original implementation is in C++ and ptrs[q] = &u[q+i][i], so when ptrs is modified, u is modified too.
            // in C#, we have to copy back modifications manually:
            for (var q = 0; q < num_ptrs; ++q)
                u[(q + i) + Ux * i] = ptrs[q];
            tau_u[i] = tau;

            // all computations below this point are performed
            // only for the first two columns:  i=0 or i=1
            if (i + 1 < 3)
            {
                // perform householder transformation on the matrix
                // u(i,i+1) to u(m,n), that is, on the sub-matrix
                // that begins in the (i+1)'th column of the i'th
                // row and extends to the end of the matrix at (m,n)
                if (tau != 0.0)
                {
                    for (var x = i + 1; x < 3; ++x)
                    {
                        var wx = u[i + Ux * x];
                        for (y = i + 1; y < rows; ++y)
                            wx += u[y + Ux * x] * ptrs[y - i];
                        var tau_wx = tau * wx;
                        u[i + Ux * x] -= tau_wx; // as x is always > i, we don't need to update ptrs
                        for (y = i + 1; y < rows; ++y)
                            u[y + Ux * x] -= tau_wx * ptrs[y - i];
                    }
                }

                // perform householder transformation on i'th row
                // (remember at this point, i is either 0 or 1)

                // set up a vector to reference into the matrix
                // from u(i,i+1) to u(i,n), that is, from the
                // (i+1)'th column of the i'th row and all the way
                // through to the end of that row
                ptrs[0] = u[i + Ux * (i + 1)];
                if (i == 0)
                {
                    ptrs[1] = u[i + Ux * (i + 2)];
                    num_ptrs = 2;
                }
                else // i == 1
                    num_ptrs = 1;

                // perform householder transformation on this vector
                tau = Factorize_hh(num_ptrs);
                // as before, we copy back the modifications because it's not C++
                u[i + Ux * (i + 1)] = ptrs[0];
                if (i == 0)
                {
                    u[i + Ux * (i + 2)] = ptrs[1];
                }

                tau_v[i] = tau;

                // perform householder transformation on the sub-matrix
                // u(i+1,i+1) to u(m,n), that is, on the sub-matrix
                // that begins in the (i+1)'th column of the (i+1)'th
                // row and extends to the end of the matrix at (m,n)
                if (tau != 0.0)
                {
                    for (y = i + 1; y < rows; ++y)
                    {
                        var wy = u[y + Ux * (i + 1)];
                        if (i == 0)
                            wy += u[y + Ux * (i + 2)] * ptrs[1]; // ok because i == 0
                        var tau_wy = tau * wy;
                        u[y + Ux * (i + 1)] -= tau_wy;
                        if (i == 0)
                            u[y + Ux * (i + 2)] -= tau_wy * ptrs[1]; // ok because i == 0 and y > i
                    }
                }
            } // if (i + 1 < 3)
        }
    }

    //----------------------------------------------------------------------------

    private double Factorize_hh(int n)
    {
        var tau = 0.0;

        if (n > 1)
        {
            double xnorm;
            if (n == 2)
                xnorm = Math.Abs(ptrs[1]);
            else
            {
                var scl = 0.0;
                var ssq = 1.0;
                for (var i = 1; i < n; ++i)
                {
                    var x = Math.Abs(ptrs[i]);
                    if (x != 0.0)
                    {
                        if (scl < x)
                        {
                            ssq = 1.0 + ssq * (scl / x) * (scl / x);
                            scl = x;
                        }
                        else
                            ssq += x / scl * (x / scl);
                    }
                }

                xnorm = scl * Math.Sqrt(ssq);
            }

            if (xnorm != 0.0)
            {
                var alpha = ptrs[0];
                var beta = Math.Sqrt(alpha * alpha + xnorm * xnorm);
                if (alpha >= 0.0)
                    beta = -beta;
                tau = (beta - alpha) / beta;

                var scl = 1.0 / (alpha - beta);
                ptrs[0] = beta;
                for (var i = 1; i < n; ++i)
                    ptrs[i] *= scl;
            }
        }

        return tau;
    }

    //----------------------------------------------------------------------------

    private void Unpack(int rows)
    {
        int i, y;

        // reset v to the identity matrix
        v[0 + Vx * 0] = v[1 + Vx * 1] = v[2 + Vx * 2] = 1.0;
        v[0 + Vx * 1] = v[0 + Vx * 2] = v[1 + Vx * 0] = v[1 + Vx * 2] = v[2 + Vx * 0] = v[2 + Vx * 1] = 0.0;

        for (i = 1; i >= 0; --i)
        {
            var tau = tau_v[i];

            // perform householder transformation on the sub-matrix
            // v(i+1,i+1) to v(m,n), that is, on the sub-matrix of v
            // that begins in the (i+1)'th column of the (i+1)'th row
            // and extends to the end of the matrix at (m,n).  the
            // householder vector used to perform this is the vector
            // from u(i,i+1) to u(i,n)
            if (tau != 0.0)
            {
                for (var x = i + 1; x < 3; ++x)
                {
                    var wx = v[(i + 1) + Vx * x];
                    for (y = i + 1 + 1; y < 3; ++y)
                        wx += v[y + Vx * x] * u[i + Ux * y];
                    var tau_wx = tau * wx;
                    v[(i + 1) + Vx * x] -= tau_wx;
                    for (y = i + 1 + 1; y < 3; ++y)
                        v[y + Vx * x] -= tau_wx * u[i + Ux * y];
                }
            }
        }

        // copy superdiagonal of u into tau_v
        for (i = 0; i < 2; ++i)
            tau_v[i] = u[i + Ux * (i + 1)];

        // below, same idea for u:  householder transformations
        // and the superdiagonal copy

        for (i = 2; i >= 0; --i)
        {
            // copy superdiagonal of u into tau_u
            var tau = tau_u[i];
            tau_u[i] = u[i + Ux * i];

            // perform householder transformation on the sub-matrix
            // u(i,i) to u(m,n), that is, on the sub-matrix of u that
            // begins in the i'th column of the i'th row and extends
            // to the end of the matrix at (m,n).  the householder
            // vector used to perform this is the i'th column of u,
            // that is, u(0,i) to u(m,i)
            if (tau == 0.0)
            {
                u[i + Ux * i] = 1.0;
                if (i < 2)
                {
                    u[i + Ux * 2] = 0.0;
                    if (i < 1)
                        u[i + Ux * 1] = 0.0;
                }

                for (y = i + 1; y < rows; ++y)
                    u[y + Ux * i] = 0.0;
            }
            else
            {
                for (var x = i + 1; x < 3; ++x)
                {
                    var wx = 0.0;
                    for (y = i + 1; y < rows; ++y)
                        wx += u[y + Ux * x] * u[y + Ux * i];
                    var tau_wx = tau * wx;
                    u[i + Ux * x] = -tau_wx;
                    for (y = i + 1; y < rows; ++y)
                        u[y + Ux * x] -= tau_wx * u[y + Ux * i];
                }

                for (y = i + 1; y < rows; ++y)
                    u[y + Ux * i] = u[y + Ux * i] * -tau;
                u[i + Ux * i] = 1.0 - tau;
            }
        }
    }

    //----------------------------------------------------------------------------

    private void Diagonalize(int rows)
    {
        int i, j;

        Chop(ref tau_u, ref tau_v, 3);

        // progressively reduce the matrices into diagonal form

        var b = 3 - 1;
        while (b > 0)
        {
            if (tau_v[b - 1] == 0.0)
                --b;
            else
            {
                var a = b - 1;
                while (a > 0 && tau_v[a - 1] != 0.0)
                    --a;
                var n = b - a + 1;

                for (j = a; j <= b; ++j)
                {
                    for (i = 0; i < rows; ++i)
                        u1[i + Ux * (j - a)] = u[i + Ux * j];
                    for (i = 0; i < 3; ++i)
                        v1[i + Vx * (j - a)] = v[i + Vx * j];
                }

                // copy desired part of the arrays
                for (var copyInd = 0; copyInd < 3 - a; ++copyInd)
                {
                    tau_u1[copyInd] = tau_u[a + copyInd];
                }

                for (var copyInd = 0; copyInd < 2 - a; ++copyInd)
                {
                    tau_v1[copyInd] = tau_v[a + copyInd];
                }

                QRstep(rows, n);

                for (j = a; j <= b; ++j)
                {
                    for (i = 0; i < rows; ++i)
                        u[i + Ux * j] = u1[i + Ux * (j - a)];
                    for (i = 0; i < 3; ++i)
                        v[i + Vx * j] = v1[i + Vx * (j - a)];
                }

                Chop(ref tau_u1, ref tau_v1, n);

                // copy back into tau_u and tau_v
                for (var copyInd = 0; copyInd < 3 - a; ++copyInd)
                {
                    tau_u[a + copyInd] = tau_u1[copyInd];
                }

                for (var copyInd = 0; copyInd < 2 - a; ++copyInd)
                {
                    tau_v[a + copyInd] = tau_v1[copyInd];
                }
            }
        }
    }

    //----------------------------------------------------------------------------

    private void Chop(ref double[] a, ref double[] b, int n)
    {
        var ai = a[0];
        for (var i = 0; i < n - 1; ++i)
        {
            var ai1 = a[i + 1];
            if (Math.Abs(b[i]) < Epsilon * (Math.Abs(ai) + Math.Abs(ai1)))
                b[i] = 0.0;
            ai = ai1;
        }
    }

    //----------------------------------------------------------------------------

    private void QRstep(int rows, int cols)
    {
        int i;

        if (cols == 2)
        {
            QRstep_cols2(rows);
            return;
        }

        // handle zeros on the diagonal or at its end
        for (i = 0; i < cols - 1; ++i)
            if (tau_u1[i] == 0.0)
            {
                QRstep_middle(rows, cols, i);
                return;
            }

        if (tau_u1[cols - 1] == 0.0)
        {
            QRstep_end();
            return;
        }

        // perform qr reduction on the diagonal and off-diagonal

        var mu = QRstep_eigenvalue();
        var y = tau_u1[0] * tau_u1[0] - mu;
        var z = tau_u1[0] * tau_v1[0];

        var ak = 0.0;
        var bk = 0.0;
        double zk;
        var ap = tau_u1[0];
        var bp = tau_v1[0];
        var aq = tau_u1[1];
        //double bq = tau_v1 [1];

        for (var k = 0; k < cols - 1; ++k)
        {
            double c, s;

            // perform Givens rotation on V

            ComputeGivens(y, z, out c, out s);

            for (i = 0; i < 3; ++i)
            {
                var vip = v1[i + Vx * k];
                var viq = v1[i + Vx * (k + 1)];
                v1[i + Vx * k] = vip * c - viq * s;
                v1[i + Vx * (k + 1)] = vip * s + viq * c;
            }

            // perform Givens rotation on B

            var bk1 = bk * c - z * s;
            var ap1 = ap * c - bp * s;
            var bp1 = ap * s + bp * c;
            var zp1 = aq * -s;
            var aq1 = aq * c;

            if (k > 0)
                tau_v1[k - 1] = bk1;

            ak = ap1;
            bk = bp1;
            zk = zp1;
            ap = aq1;

            if (k < cols - 2)
                bp = tau_v1[k + 1];
            else
                bp = 0.0;

            y = ak;
            z = zk;

            // perform Givens rotation on U

            ComputeGivens(y, z, out c, out s);

            for (i = 0; i < rows; ++i)
            {
                var uip = u1[i + Ux * k];
                var uiq = u1[i + Ux * (k + 1)];
                u1[i + Ux * k] = uip * c - uiq * s;
                u1[i + Ux * (k + 1)] = uip * s + uiq * c;
            }

            // perform Givens rotation on B

            var ak1 = ak * c - zk * s;
            bk1 = bk * c - ap * s;
            var zk1 = bp * -s;

            ap1 = bk * s + ap * c;
            bp1 = bp * c;

            tau_u1[k] = ak1;

            ak = ak1;
            bk = bk1;
            zk = zk1;
            ap = ap1;
            bp = bp1;

            if (k < cols - 2)
                aq = tau_u1[k + 2];
            else
                aq = 0.0;

            y = bk;
            z = zk;
        }

        tau_v1[cols - 2] = bk;
        tau_u1[cols - 1] = ap;
    }

    //----------------------------------------------------------------------------

    private void QRstep_middle(int rows, int cols, int col)
    {
        var x = tau_v1[col];
        var y = tau_u1[col + 1];
        for (var j = col; j < cols - 1; ++j)
        {
            double c, s;

            // perform Givens rotation on U

            ComputeGivens(y, -x, out c, out s);
            for (var i = 0; i < rows; ++i)
            {
                var uip = u1[i + Ux * col];
                var uiq = u1[i + Ux * (j + 1)];
                u1[i + Ux * col] = uip * c - uiq * s;
                u1[i + Ux * (j + 1)] = uip * s + uiq * c;
            }

            // perform transposed Givens rotation on B

            tau_u1[j + 1] = x * s + y * c;
            if (j == col)
                tau_v1[j] = x * c - y * s;

            if (j < cols - 2)
            {
                var z = tau_v1[j + 1];
                tau_v1[j + 1] *= c;
                x = z * -s;
                y = tau_u1[j + 2];
            }
        }
    }

    //----------------------------------------------------------------------------

    private void QRstep_end()
    {
        var x = tau_u1[1];
        var y = tau_v1[1];

        for (var k = 1; k >= 0; --k)
        {
            double c, s;

            // perform Givens rotation on V

            ComputeGivens(x, y, out c, out s);

            for (var i = 0; i < 3; ++i)
            {
                var vip = v1[i + Vx * k];
                var viq = v1[i + Vx * 2];
                v1[i + Vx * k] = vip * c - viq * s;
                v1[i + Vx * 2] = vip * s + viq * c;
            }

            // perform Givens rotation on B

            tau_u1[k] = x * c - y * s;
            if (k == 1)
                tau_v1[k] = x * s + y * c;
            if (k > 0)
            {
                var z = tau_v1[k - 1];
                tau_v1[k - 1] *= c;

                x = tau_u1[k - 1];
                y = z * s;
            }
        }
    }

    //----------------------------------------------------------------------------

    private double QRstep_eigenvalue()
    {
        var ta = tau_u1[1] * tau_u1[1] + tau_v1[0] * tau_v1[0];
        var tb = tau_u1[2] * tau_u1[2] + tau_v1[1] * tau_v1[1];
        var tab = tau_u1[1] * tau_v1[1];
        var dt = (ta - tb) / 2.0;
        double mu;
        if (dt >= 0.0)
            mu = tb - tab * tab / (dt + Math.Sqrt(dt * dt + tab * tab));
        else
            mu = tb + tab * tab / (Math.Sqrt(dt * dt + tab * tab) - dt);
        return mu;
    }

    //----------------------------------------------------------------------------

    private void QRstep_cols2(int rows)
    {
        int i;
        double tmp;

        // eliminate off-diagonal element in [ 0  tau_v0 ]
        //                                   [ 0  tau_u1 ]
        // to make [ tau_u[0]  0 ]
        //         [ 0         0 ]

        if (tau_u1[0] == 0.0)
        {
            double c, s;

            // perform transposed Givens rotation on B
            // multiplied by X = [ 0 1 ]
            //                   [ 1 0 ]

            ComputeGivens(tau_v1[0], tau_u1[1], out c, out s);

            tau_u1[0] = tau_v1[0] * c - tau_u1[1] * s;
            tau_v1[0] = tau_v1[0] * s + tau_u1[1] * c;
            tau_u1[1] = 0.0;

            // perform Givens rotation on U

            for (i = 0; i < rows; ++i)
            {
                var uip = u1[i + Ux * 0];
                var uiq = u1[i + Ux * 1];
                u1[i + Ux * 0] = uip * c - uiq * s;
                u1[i + Ux * 1] = uip * s + uiq * c;
            }

            // multiply V by X, effectively swapping first two columns

            for (i = 0; i < 3; ++i)
            {
                tmp = v1[i + Vx * 0];
                v1[i + Vx * 0] = v1[i + Vx * 1];
                v1[i + Vx * 1] = tmp;
            }
        }

        // eliminate off-diagonal element in [ tau_u0  tau_v0 ]
        //                                   [ 0       0      ]

        else if (tau_u1[1] == 0.0)
        {
            double c, s;

            // perform Givens rotation on B

            ComputeGivens(tau_u1[0], tau_v1[0], out c, out s);

            tau_u1[0] = tau_u1[0] * c - tau_v1[0] * s;
            tau_v1[0] = 0.0;

            // perform Givens rotation on V

            for (i = 0; i < 3; ++i)
            {
                var vip = v1[i + Vx * 0];
                var viq = v1[i + Vx * 1];
                v1[i + Vx * 0] = vip * c - viq * s;
                v1[i + Vx * 1] = vip * s + viq * c;
            }
        }

        // make colums orthogonal,

        else
        {
            double c, s;

            // perform Schur rotation on B

            ComputeSchur(tau_u1[0], tau_v1[0], tau_u1[1], out c, out s);

            var a11 = tau_u1[0] * c - tau_v1[0] * s;
            var a21 = -tau_u1[1] * s;
            var a12 = tau_u1[0] * s + tau_v1[0] * c;
            var a22 = tau_u1[1] * c;

            // perform Schur rotation on V

            for (i = 0; i < 3; ++i)
            {
                var vip = v1[i + Vx * 0];
                var viq = v1[i + Vx * 1];
                v1[i + Vx * 0] = vip * c - viq * s;
                v1[i + Vx * 1] = vip * s + viq * c;
            }

            // eliminate off diagonal elements

            if (a11 * a11 + a21 * a21 < a12 * a12 + a22 * a22)
            {
                // multiply B by X

                tmp = a11;
                a11 = a12;
                a12 = tmp;
                tmp = a21;
                a21 = a22;
                a22 = tmp;

                // multiply V by X, effectively swapping first
                // two columns

                for (i = 0; i < 3; ++i)
                {
                    tmp = v1[i + Vx * 0];
                    v1[i + Vx * 0] = v1[i + Vx * 1];
                    v1[i + Vx * 1] = tmp;
                }
            }

            // perform transposed Givens rotation on B

            ComputeGivens(a11, a21, out c, out s);

            tau_u1[0] = a11 * c - a21 * s;
            tau_v1[0] = a12 * c - a22 * s;
            tau_u1[1] = a12 * s + a22 * c;

            // perform Givens rotation on U

            for (i = 0; i < rows; ++i)
            {
                var uip = u1[i + Ux * 0];
                var uiq = u1[i + Ux * 1];
                u1[i + Ux * 0] = uip * c - uiq * s;
                u1[i + Ux * 1] = uip * s + uiq * c;
            }
        }
    }

    //----------------------------------------------------------------------------

    private void ComputeGivens(double a, double b, out double c, out double s)
    {
        if (b == 0.0)
        {
            c = 1.0;
            s = 0.0;
        }
        else if (Math.Abs(b) > Math.Abs(a))
        {
            var t = -a / b;
            var s1 = 1.0 / Math.Sqrt(1 + t * t);
            s = s1;
            c = s1 * t;
        }
        else
        {
            var t = -b / a;
            var c1 = 1.0 / Math.Sqrt(1 + t * t);
            c = c1;
            s = c1 * t;
        }
    }

    //----------------------------------------------------------------------------

    private void ComputeSchur(double a1, double a2, double a3, out double c, out double s)
    {
        var apq = a1 * a2 * 2.0;

        if (apq == 0.0)
        {
            c = 1.0;
            s = 0.0;
        }
        else
        {
            double t;
            var tau = (a2 * a2 + (a3 + a1) * (a3 - a1)) / apq;
            if (tau >= 0.0)
                t = 1.0 / (tau + Math.Sqrt(1.0 + tau * tau));
            else
                t = -1.0 / (Math.Sqrt(1.0 + tau * tau) - tau);
            c = 1.0 / Math.Sqrt(1.0 + t * t);
            s = t * c;
        }
    }

    //----------------------------------------------------------------------------

    private void Singularize(int rows)
    {
        int i, j, y;

        // make singularize values positive

        for (j = 0; j < 3; ++j)
            if (d[j] < 0.0)
            {
                for (i = 0; i < 3; ++i)
                    v[i + Vx * j] = -v[i + Vx * j];
                d[j] = -d[j];
            }

        // sort singular values in decreasing order

        for (i = 0; i < 3; ++i)
        {
            var d_max = d[i];
            var i_max = i;
            for (j = i + 1; j < 3; ++j)
                if (d[j] > d_max)
                {
                    d_max = d[j];
                    i_max = j;
                }

            if (i_max != i)
            {
                // swap eigenvalues
                var tmp = d[i];
                d[i] = d[i_max];
                d[i_max] = tmp;

                // swap eigenvectors
                for (y = 0; y < rows; ++y)
                {
                    tmp = u[y + Ux * i];
                    u[y + Ux * i] = u[y + Ux * i_max];
                    u[y + Ux * i_max] = tmp;
                }

                for (y = 0; y < 3; ++y)
                {
                    tmp = v[y + Vx * i];
                    v[y + Vx * i] = v[y + Vx * i_max];
                    v[y + Vx * i_max] = tmp;
                }
            }
        }
    }

    //----------------------------------------------------------------------------

    private void SolveSVD(
        double[] b, // vector (1 x rows)
        int rows)
    {
        int i, j;

        // compute vector w = U^T * b

        for (i = 0; i < rows; ++i)
        {
            if (b[i] != 0)
                for (j = 0; j < 3; ++j)
                    w[j] += b[i] * u[i + Ux * j];
        }

        // introduce non-zero singular values in d into w

        for (i = 0; i < 3; ++i)
        {
            if (d[i] > 1)
                w[i] /= d[i];
        }

        // compute result vector result = V * w

        for (i = 0; i < 3; ++i)
        {
            var tmp = 0.0;
            for (j = 0; j < 3; ++j)
                tmp += w[j] * v[i + Vx * j];
            result[i] = tmp;
        }
    }
}
