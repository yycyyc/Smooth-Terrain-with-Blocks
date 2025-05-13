using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct Chunk_Node_LOD : IEquatable<Chunk_Node_LOD>
{
    public Work.NodeBound cnb;
    public Work.NodeBound nb;
    public int cubeLOD;

    public Chunk_Node_LOD(Work.NodeBound chunk, Work.NodeBound node, int cubeLOD)
    {
        this.cnb = chunk;
        this.nb = node;
        this.cubeLOD = cubeLOD;
    }

    public bool Equals(Chunk_Node_LOD other)
    {
        return cnb.Equals(other.cnb) && nb.Equals(other.nb) && cubeLOD == other.cubeLOD;
    }

    public override bool Equals(object obj)
    {
        return obj is Chunk_Node_LOD other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = cnb.GetHashCode();
            hash = (hash * 397) ^ nb.GetHashCode();
            hash = (hash * 397) ^ cubeLOD;
            return hash;
        }
    }
}
[BurstCompile]
public struct Chunk_Node : IEquatable<Chunk_Node>
{
    public Work.NodeBound cnb;
    public Work.NodeBound nb;

    public Chunk_Node(Work.NodeBound chunk, Work.NodeBound node)
    {
        this.cnb = chunk;
        this.nb = node;
    }

    public bool Equals(Chunk_Node other)
    {
        return cnb.Equals(other.cnb) && nb.Equals(other.nb);
    }

    public override bool Equals(object obj)
    {
        return obj is Chunk_Node other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = cnb.GetHashCode();
            hash = (hash * 397) ^ nb.GetHashCode();
            return hash;
        }
    }
}

[BurstCompile]
public struct SingleNodeData
{
    public Work.NodeBound nodeB;
    public SDFInfo sdf0, sdf1, sdf2, sdf3, sdf4, sdf5, sdf6, sdf7;

    public float3 surPos0, surPos1, surPos2, surPos3, surPos4, surPos5, surPos6, surPos7, surPos8, surPos9, surPos10, surPos11;
    public float3 surNor0, surNor1, surNor2, surNor3, surNor4, surNor5, surNor6, surNor7, surNor8, surNor9, surNor10, surNor11;
    public byte mat0, mat1, mat2, mat3, mat4, mat5, mat6, mat7, mat8, mat9, mat10, mat11;
    public char dir0, dir1, dir2, dir3, dir4, dir5, dir6, dir7, dir8, dir9, dir10, dir11;
    
    public int count;
    public bool hasCubeData, hasSphereData, hasTerrainData;

    public static SingleNodeData Create(Work.NodeBound nb)
    {
        return new SingleNodeData
        {
            nodeB = nb,
            count = 0,
            hasCubeData = false,
            hasSphereData = false,
            hasTerrainData = false,
        };
    }

    public void SetCornerSDF(int3 corner, SDFInfo type)
    {
        int idx = corner.x * 4 + corner.y * 2 + corner.z;

        switch (idx)
        {
            case 0 : sdf0 = type; 
                break;
            case 1: sdf1 = type;
                break;
            case 2: sdf2 = type;
                break;
            case 3: sdf3 = type;
                break;
            case 4: sdf4 = type;
                break;
            case 5: sdf5 = type;
                break;
            case 6: sdf6 = type;
                break;
            default: sdf7 = type;
                break;
        }
    }

    public SDFInfo GetCornerSDF(int idx)
    {
        switch (idx)
        {
            case 0:
                return sdf0;

            case 1:
                return sdf1;

            case 2:
                return sdf2;

            case 3:
                return sdf3;

            case 4:
                return sdf4;

            case 5:
                return sdf5;

            case 6:
                return sdf6;

            default:
                return sdf7;
        }
    }
    
    public void AddSurfaceData(SurfaceData sd)
    {
        if (!sd.valid || count >= 12)
            return;

        switch (count)
        {
            case 0: surPos0 = sd.surPos; surNor0 = sd.surNor; mat0 = sd.mat; dir0 = sd.dir; break;
            case 1: surPos1 = sd.surPos; surNor1 = sd.surNor; mat1 = sd.mat; dir1 = sd.dir; break;
            case 2: surPos2 = sd.surPos; surNor2 = sd.surNor; mat2 = sd.mat; dir2 = sd.dir; break;
            case 3: surPos3 = sd.surPos; surNor3 = sd.surNor; mat3 = sd.mat; dir3 = sd.dir; break;
            case 4: surPos4 = sd.surPos; surNor4 = sd.surNor; mat4 = sd.mat; dir4 = sd.dir; break;
            case 5: surPos5 = sd.surPos; surNor5 = sd.surNor; mat5 = sd.mat; dir5 = sd.dir; break;
            case 6: surPos6 = sd.surPos; surNor6 = sd.surNor; mat6 = sd.mat; dir6 = sd.dir; break;
            case 7: surPos7 = sd.surPos; surNor7 = sd.surNor; mat7 = sd.mat; dir7 = sd.dir; break;
            case 8: surPos8 = sd.surPos; surNor8 = sd.surNor; mat8 = sd.mat; dir8 = sd.dir; break;
            case 9: surPos9 = sd.surPos; surNor9 = sd.surNor; mat9 = sd.mat; dir9 = sd.dir; break;
            case 10: surPos10 = sd.surPos; surNor10 = sd.surNor; mat10 = sd.mat; dir10 = sd.dir; break;
            case 11: surPos11 = sd.surPos; surNor11 = sd.surNor; mat11 = sd.mat; dir11 = sd.dir; break;
        }

        count++;
    }
    public SurfaceData GetSurfaceData(int index)
    {
        if (index < 0 || index >= count) return default;

        var sd = new SurfaceData { valid = true };

        switch (index)
        {
            case 0: sd.surPos = surPos0; sd.surNor = surNor0; sd.mat = mat0; sd.dir = dir0; break;
            case 1: sd.surPos = surPos1; sd.surNor = surNor1; sd.mat = mat1; sd.dir = dir1; break;
            case 2: sd.surPos = surPos2; sd.surNor = surNor2; sd.mat = mat2; sd.dir = dir2; break;
            case 3: sd.surPos = surPos3; sd.surNor = surNor3; sd.mat = mat3; sd.dir = dir3; break;
            case 4: sd.surPos = surPos4; sd.surNor = surNor4; sd.mat = mat4; sd.dir = dir4; break;
            case 5: sd.surPos = surPos5; sd.surNor = surNor5; sd.mat = mat5; sd.dir = dir5; break;
            case 6: sd.surPos = surPos6; sd.surNor = surNor6; sd.mat = mat6; sd.dir = dir6; break;
            case 7: sd.surPos = surPos7; sd.surNor = surNor7; sd.mat = mat7; sd.dir = dir7; break;
            case 8: sd.surPos = surPos8; sd.surNor = surNor8; sd.mat = mat8; sd.dir = dir8; break;
            case 9: sd.surPos = surPos9; sd.surNor = surNor9; sd.mat = mat9; sd.dir = dir9; break;
            case 10: sd.surPos = surPos10; sd.surNor = surNor10; sd.mat = mat10; sd.dir = dir10; break;
            case 11: sd.surPos = surPos11; sd.surNor = surNor11; sd.mat = mat11; sd.dir = dir11; break;
        }

        return sd;
    }
    
}
[BurstCompile]
public struct SurfaceData
{
    public bool valid;
    public float3 surPos, surNor;
    public byte mat;
    public byte sdfType;//0 : terrain, 1 : cube, 2 : sphere
    public char dir;

    public bool IsTerrain => valid && sdfType == 0;
    public bool IsCube => valid && sdfType == 1;
    public bool IsSphere => valid && sdfType == 2;
    public static SurfaceData Invalid()
    {
        var s = new SurfaceData();
        s.valid = false;
        s.surNor = s.surPos = float3.zero;
        s.mat = 0;
        s.sdfType = 255;
        s.dir = ' ';
        return s;
    }
    
}


public struct WorldDataNative
{
    public struct CubeInfo
    {
        public CubeInfo(byte mat, bool isLeaf, uint order)
        {
            this.mat = mat;
            this.isLeaf = isLeaf;
            this.order = order;
        }
        public byte mat;
        public uint order;
        public bool isLeaf;
    }
    
    public int cubeOctreeSize;
    public NativeHashSet<int3> AllCubeOctrees;
    public NativeHashMap<Work.NodeBoundInt, CubeInfo> AllCubeNodes;
    public NativeHashSet<Work.NodeBound> AllChunks;
    public NativeList<SphereSDF> AllSpheres;
    
    
    public Work.NodeBound FindChunk(float3 worldPos)
    {
        for (int i = 0; i <= OctreeParam.MaxLOD; i++)
        {
            Work.NodeBound n = OctreeUtil.GetChunkNodeBound((int3)math.floor(worldPos + (float3)0.001f), i);
            if (AllChunks.Contains(n))
                return n;
        }

        return new Work.NodeBound(0, -1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int FloorToMultiple(int val, int size)
    {
        return val >= 0 ? (val / size) * size : ((val - size + 1) / size) * size;
    }
    public bool HasCubeOctree(int3 id) => AllCubeOctrees.Contains(id);
    public byte GetCubePureValue(Work.NodeBoundInt cube, out uint order)
    {
        CubeInfo c;

        int size = cube.size;
        int3 minpos = new int3()
        {
            x = FloorToMultiple(cube.x, size),
            y = FloorToMultiple(cube.y, size),
            z = FloorToMultiple(cube.z, size)
        };

        if (AllCubeNodes.TryGetValue(new Work.NodeBoundInt(minpos, size), out c))
        {
            order = c.order;
            return c.mat;
        }

        order = 0;
        return 255;
    }

    public bool IsLeaf(Work.NodeBoundInt cube)
    {
        return AllCubeNodes.TryGetValue(cube, out CubeInfo c) && c.isLeaf;
    }
    
    public byte GetCubeValue(int3 cubeGridPos, int cubeLOD, NativeHashMap<Work.NodeBoundInt, CubeData> overrideValue, out uint order)
    {
        int size = 1 << cubeLOD;
        
        int3 minpos = new int3()
        {
            x = FloorToMultiple(cubeGridPos.x, size),
            y = FloorToMultiple(cubeGridPos.y, size),
            z = FloorToMultiple(cubeGridPos.z, size)
        };
        
        CubeInfo cube;
        CubeData val;
        var key = new Work.NodeBoundInt(minpos, size);
        if (overrideValue.TryGetValue(key, out val))
        {
            order = val.order;
            return val.mat; 
        }
        else if (AllCubeNodes.TryGetValue(key, out cube))
        {
            order = cube.order;
            return cube.mat;
        }
        else
        {
            order = 0;
            return 255;
        }
    }

    public void Dispose()
    {
        AllCubeNodes.Dispose();
        AllCubeOctrees.Dispose();
        AllChunks.Dispose();
        AllSpheres.Dispose();
    }
}

//NativeQueue TempAlloc inside jobs has bug
public struct NativeSimpleQueue<T> where T : unmanaged
{
    private NativeArray<T> buffer;
    private int head;
    private int tail;
    private int capacity;
    private int count;

    public NativeSimpleQueue(int capacity, Allocator allocator)
    {
        this.capacity = capacity;
        buffer = new NativeArray<T>(capacity, allocator);
        head = 0;
        tail = 0;
        count = 0;
    }

    public void Enqueue(T item)
    {
        if (count >= capacity)
            throw new System.InvalidOperationException("Queue is full.");

        buffer[tail] = item;
        tail = (tail + 1) % capacity;
        count++;
    }

    public T Dequeue()
    {
        if (count == 0)
            throw new System.InvalidOperationException("Queue is empty.");

        T item = buffer[head];
        head = (head + 1) % capacity;
        count--;
        return item;
    }

    public T Peek()
    {
        if (count == 0)
            throw new System.InvalidOperationException("Queue is empty.");
        return buffer[head];
    }

    public int Count => count;

    public bool IsEmpty => count == 0;

    public void Clear()
    {
        head = 0;
        tail = 0;
        count = 0;
    }

    public void Dispose()
    {
        if (buffer.IsCreated)
            buffer.Dispose();
    }

    public bool IsCreated => buffer.IsCreated;
}

