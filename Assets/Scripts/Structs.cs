using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public struct NodeID : IEquatable<NodeID>
{
    public static NodeID Invalid => new NodeID(0, 0, 0, -100);
    

    public NodeID(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
    
    public float x, y, z, w;

    public bool Equals(NodeID v)
    {
        return Mathf.Approximately(x, v.x) && Mathf.Approximately(y, v.y) && Mathf.Approximately(z, v.z) &&
               Mathf.Approximately(w, v.w);    
    }
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Mathf.FloorToInt(x * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(y * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(z * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(w * 100f);
            return hash;
        }
    }
    
    public static NodeID operator +(NodeID v1, NodeID v2)
    {
        return new NodeID(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w + v2.w);
    }
    public static NodeID operator +(NodeID v1, Vector3 v2)
    {
        return new NodeID(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w);
    }

    public static implicit operator Vector3(NodeID v)
    {
        return new Vector3(v.x, v.y, v.z);
    }
    public static implicit operator Vector3Int(NodeID v)
    {
        return new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
    }
    public static implicit operator Vector4(NodeID v)
    {
        return new Vector4(v.x, v.y, v.z, v.w);
    }
    public static implicit operator NodeBound(NodeID v)
    {
        float w = OctreeParam.ChunkSize * Mathf.Pow(2, v.w);
        float x = v.x * w;
        float y = v.y * w;
        float z = v.z * w;
        return new NodeBound(x, y, z, w);
    }

    public override string ToString()
    {
        return ((Vector4)this).ToString();
    }
}

public struct NodeBound : IEquatable<NodeBound>
{
    public NodeBound(Vector3 vec, float size)
    {
        x = vec.x;
        y = vec.y;
        z = vec.z;
        w = size;
    }

    public NodeBound(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public NodeBound(Work.NodeBound n)
    {
        x = n.x;
        y = n.y;
        z = n.z;
        w = n.w;
    }
    
    public float x, y, z, w;

    public Vector3 MinPos => new Vector3(x, y, z);
    public Vector3 MaxPos => new Vector3(x, y, z) + Vector3.one * Size;
    public float Size => w;

    public bool Equals(NodeBound v)
    {
        return Mathf.Approximately(x, v.x) && Mathf.Approximately(y, v.y) && Mathf.Approximately(z, v.z) &&
               Mathf.Approximately(w, v.w);
    }
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Mathf.FloorToInt(x * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(y * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(z * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(w * 100f);
            return hash;
        }
    }
    
    public static NodeBound operator +(NodeBound v1, NodeBound v2)
    {
        return new NodeBound(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w + v2.w);
    }


    public static implicit operator Vector3(NodeBound v)
    {
        return new Vector3(v.x, v.y, v.z);
    }
    public static implicit operator Vector4(NodeBound v)
    {
        return new Vector4(v.x, v.y, v.z, v.w);
    }
    public static implicit operator Work.NodeBound(NodeBound v)
    {
        return new Work.NodeBound(new float3(v.x, v.y, v.z), v.w);
    }
    public static implicit operator NodeID(NodeBound v)
    {
        v.x += 0.001f;
        v.y += 0.001f;
        v.z += 0.001f;
        float x = Mathf.Floor(v.x / v.w);
        float y = Mathf.Floor(v.y / v.w);
        float z = Mathf.Floor(v.z / v.w);
        float w = Mathf.Log(v.w / OctreeParam.ChunkSize, 2);
        
        return new NodeID(x, y, z, w);
    }

    public override string ToString()
    {
        return ((Vector4)this).ToString();
    }
}

public struct NodeBoundInt : IEquatable<NodeBoundInt>
{
    public int x, y, z, w;

    public NodeBoundInt(Vector3Int vec, int size)
    {
        x = vec.x;
        y = vec.y;
        z = vec.z;
        w = size;
    }

    public NodeBoundInt(int x, int y, int z, int w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public Vector3Int MinPos => new Vector3Int(x, y, z);
    public Vector3Int MaxPos => new Vector3Int(x + w, y + w, z + w);
    public int Size => w;

    public bool Equals(NodeBoundInt other)
    {
        return x == other.x && y == other.y && z == other.z && w == other.w;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ w * (int)2654435761;
            return hash;
        }
    }

    public static NodeBoundInt operator +(NodeBoundInt a, NodeBoundInt b)
    {
        return new NodeBoundInt(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
    }

    public static implicit operator Vector3Int(NodeBoundInt v)
    {
        return new Vector3Int(v.x, v.y, v.z);
    }

    public static implicit operator Vector4Int(NodeBoundInt v)
    {
        return new Vector4Int(v.x, v.y, v.z, v.w);
    }
    

    public override string ToString()
    {
        return $"({x}, {y}, {z}, {w})";
    }
}

public struct Vector4Int : IEquatable<Vector4Int>
{
    public Vector4Int(Vector3Int vec, int size)
    {
        x = vec.x;
        y = vec.y;
        z = vec.z;
        w = size;
    }

    public Vector4Int(int x, int y, int z, int w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
    
    public int x, y, z, w;
    

    public bool Equals(Vector4Int v)
    {
        return x == v.x && y == v.y && z == v.z && w == v.w;
    }
    public override bool Equals(object obj)
    {
        return obj is Vector4Int other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ w * (int)2654435761;
        }
    }
    
    public static Vector4Int operator +(Vector4Int v1, Vector4Int v2)
    {
        return new Vector4Int(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w + v2.w);
    }
    public static Vector3Int operator +(Vector4Int v1, Vector3Int v2)
    {
        return new Vector3Int(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
    }
    public static Vector3Int operator +(Vector3Int v1, Vector4Int v2)
    {
        return new Vector3Int(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
    }

    public static implicit operator Vector3Int(Vector4Int v)
    {
        return new Vector3Int(v.x, v.y, v.z);
    }
    public static implicit operator Vector3(Vector4Int v)
    {
        return new Vector3(v.x, v.y, v.z);
    }
    public static implicit operator Vector4(Vector4Int v)
    {
        return new Vector4(v.x, v.y, v.z, v.w);
    }
    

    public override string ToString()
    {
        return ((Vector4)this).ToString();
    }
}

public struct EdgeID : IEquatable<EdgeID>
{
    public EdgeID(Vector3 vec, char dir, bool flag1 = false)
    {
        x = vec.x;
        y = vec.y;
        z = vec.z;
        w = dir;
        this.flag1 = flag1;
    }

    public EdgeID(float x, float y, float z, char w, bool flag1 = false)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
        this.flag1 = flag1;
    }

    public float x, y, z;
    public char w;

    public bool flag1;

    public bool Equals(EdgeID v)
    {
        return Mathf.Approximately(v.x, x) && Mathf.Approximately(v.y, y) && Mathf.Approximately(v.z, z) && v.w == w;
    }


    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Mathf.FloorToInt(x * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(y * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(z * 100f);
            hash = (hash * 397) ^ Mathf.FloorToInt(w * 100f);
            return hash;
        }
    }

    public static implicit operator Vector3Int(EdgeID v)
    {
        Debug.Assert(Mathf.Approximately(v.x % 1, 0) && Mathf.Approximately(v.y % 1, 0) && Mathf.Approximately(v.z % 1, 0));
        return new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
    }
    public static implicit operator Vector3(EdgeID v)
    {
        return new Vector3(v.x, v.y, v.z);
    }
}

public struct EdgeData
{
    public byte sdfType;
    public Vector3 surfacePos, surfaceNor;
    public char dir;
    public EdgeData(Vector3 surfacePos, Vector3 surfaceNor, char dir, byte sdfType)
    {
        this.surfacePos = surfacePos;
        this.surfaceNor = surfaceNor;
        this.dir = dir;
        this.sdfType = sdfType;
    }
}

[BurstCompile]
public struct SphereSDF
{
    public SphereSDF(float3 center, float radius, uint order, byte mat)
    {
        this.center = center;
        this.radius = radius;
        this.order = order;
        this.mat = mat;
    }
    
    public float3 center;
    public float radius;
    public uint order;
    public byte mat;
    
    public float3 minPos => center - radius * new float3(1);
    public float3 maxPos => center + radius * new float3(1);
    
    public float Value(float3 point)
    {
        float distance = math.distance(point, center);
        return distance - radius;
    }

    public float3 Normal(float3 surfacePoint)
    {
        return math.normalize(surfacePoint - center);
    }
    
}

[BurstCompile]
public struct SDFInfo
{
    public static SDFInfo Terrain(byte mat)
    {
        return new SDFInfo(0, mat, 0);
    }
    public static SDFInfo Invalid()
    {
        return new SDFInfo(255, 255, 0);
    }
    
    public SDFInfo(byte type, byte mat, uint order)
    {
        this.type = type;
        this.mat = mat;
        this.order = order;
        this.sphere = new SphereSDF();
        if (mat == 255) type = 255;
    }
    public SDFInfo(byte type, SphereSDF sphere)
    {
        this.type = type;
        this.mat = sphere.mat;
        this.order = sphere.order;
        this.sphere = sphere;
        if (mat == 255) type = 255;
    }

    public bool Equals(SDFInfo sdf)
    {
        return order == sdf.order && type == sdf.type;
    }
    
    //0:Terrain, 1 : Cube, 2 : Sphere
    public byte type;
    public byte mat;
    public uint order;

    public SphereSDF sphere;
    
    public void GetSurfacePosNor(float3 edgeMinPos, char edgeDir, int edgeSign, float edgeLength, out float3 surPos, out float3 surNor)
    {
        if (type == 1)
        {
            if (edgeDir == 'x')
            {
                surPos = edgeMinPos;
                surPos.x = CubeUtil.GetCubeGridPointCeiled(surPos.x);
                surNor = edgeSign > 0 ? new float3(1f, 0f, 0f) : new float3(-1f, 0f, 0f);
            }
            else if (edgeDir == 'y')
            {
                surPos = edgeMinPos;
                surPos.y = CubeUtil.GetCubeGridPointCeiled(surPos.y);
                surNor = edgeSign > 0 ? new float3(0f, 1f, 0f) : new float3(0f, -1f, 0f);
            }
            else // 'z'
            {
                surPos = edgeMinPos;
                surPos.z = CubeUtil.GetCubeGridPointCeiled(surPos.z);
                surNor = edgeSign > 0 ? new float3(0f, 0f, 1f) : new float3(0f, 0f, -1f);
            }
        }
        else//type == 2
        {
            Debug.Assert(type == 2);

            float3 p1 = edgeMinPos;
            float3 p2 = edgeDir == 'x'
                ? p1 + edgeLength * new float3(1, 0, 0)
                : (edgeDir == 'y' ? p1 + edgeLength * new float3(0, 1, 0) : p1 + edgeLength * new float3(0, 0, 1));

            float v1 = math.abs(sphere.Value(p1)), v2 = math.abs(sphere.Value(p2));


            surPos = math.lerp(p1, p2, v1 / (v1 + v2));
            surNor = math.normalize(surPos - sphere.center) * (mat != 0 ? 1 : -1);
        }
    }
    
    
}

[BurstCompile]
public struct CubeData
{
    public CubeData(uint order, byte mat)
    {
        this.order = order;
        this.mat = mat;
    }
    public uint order;
    public byte mat;
}