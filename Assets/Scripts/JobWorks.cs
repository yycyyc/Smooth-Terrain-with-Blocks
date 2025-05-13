using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class Work
{
    [BurstCompile]
    public struct SignedEdgeID : IEquatable<SignedEdgeID>
    {
        public SignedEdgeID(float3 pos, int dir, int mat = -1, bool flag1 = false)
        {
            x = pos.x;
            y = pos.y;
            z = pos.z;
            w = dir;
            this.mat = mat;
            this.flag1 = flag1;
        }

        public SignedEdgeID(float x, float y, float z, int dir, int mat = -1, bool flag1 = false)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = dir;
            this.mat = mat;
            this.flag1 = flag1;
        }

        public float x, y, z;
        public int w;
        public int mat;
        public bool flag1;

        public EdgeID EdgeID => new EdgeID(x, y, z, w <= 2 ? 'x' : (w <= 4 ? 'y' : 'z'));

        public bool Equals(SignedEdgeID v)
        {
            return math.abs(v.x - x) < 0.0001f &&
                   math.abs(v.y - y) < 0.0001f &&
                   math.abs(v.z - z) < 0.0001f &&
                   v.w == w;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)math.floor(x * 100f);
                hash = (hash * 397) ^ (int)math.floor(y * 100f);
                hash = (hash * 397) ^ (int)math.floor(z * 100f);
                hash = (hash * 397) ^ w;
                return hash;
            }
        }

        public static implicit operator int3(SignedEdgeID v)
        {
            return new int3((int)math.round(v.x), (int)math.round(v.y), (int)math.round(v.z));
        }

        public static implicit operator float3(SignedEdgeID v)
        {
            return new float3(v.x, v.y, v.z);
        }
    }
    
    [BurstCompile]
    public struct NodeBound : IEquatable<NodeBound>
    {
        public bool Valid => w > 0;
        public float x, y, z, w;

        public NodeBound(float3 pos, float size)
        {
            x = pos.x;
            y = pos.y;
            z = pos.z;
            w = size;
        }

        public float3 MinPos => new float3(x, y, z);
        public float3 MaxPos => new float3(x + w, y + w, z + w);
        public float Size => w;

        public bool Equals(NodeBound v)
        {
            return math.abs(x - v.x) < 0.001f &&
                   math.abs(y - v.y) < 0.001f &&
                   math.abs(z - v.z) < 0.001f &&
                   math.abs(w - v.w) < 0.001f;
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)math.floor(x * 100f);
                hash = (hash * 397) ^ (int)math.floor(y * 100f);
                hash = (hash * 397) ^ (int)math.floor(z * 100f);
                hash = (hash * 397) ^ (int)math.floor(w * 100f);
                return hash;
            }
        }
        public override string ToString()
        {
            return x + ", " + y + ", " + z + ", " + w;
        }
    }

    [BurstCompile]
    public struct NodeBoundInt : IEquatable<NodeBoundInt>
    {
        public static NodeBoundInt Invalid()
        {
            return new NodeBoundInt(0, -1);
        }
        
        public bool Valid => size > 0;
        public int x, y, z;
        public int size;        
        public int3 MinPos => new int3(x, y, z);

        public NodeBoundInt(int3 minPos, int size)
        {
            x = minPos.x;
            y = minPos.y;
            z = minPos.z;
            this.size = size;
        }
        public NodeBoundInt(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.size = w;
        }

        public int3 MaxPos => MinPos + new int3(size);

        public bool Equals(NodeBoundInt other)
        {
            return math.all(MinPos == other.MinPos) && size == other.size;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ size * (int)2654435761;
                return hash;
            }
        }

        public override string ToString()
        {
            return x + ", " + y + ", " + z + ", " + size;
        }
    }

    
    public static void Initialize()
    {
        float scale = 1f / CubeParam.CubeSize;
        float3 translation = new float3(-0.125f, -0.125f, -0.125f);

        float4x4 scaleMatrix = float4x4.Scale(new float3(scale, scale, scale));
        float4x4 translationMatrix = float4x4.Translate(translation);

        worldToCubeGrid = math.mul(scaleMatrix, translationMatrix);
        cubeGridToWorld = math.inverse(worldToCubeGrid);
    }
    
    private static float4x4 cubeGridToWorld, worldToCubeGrid;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int3 GetCubePos(float4x4 worldToCubeGrid, float3 worldPos)
    {
        float3 c = math.transform(worldToCubeGrid, worldPos);
        return (int3)math.floor(c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float3 GetWorldPos(float4x4 cubeGridToWorld, int3 cubePos)
    {
        return math.transform(cubeGridToWorld, (float3)cubePos);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int3 GetCubeOctreeID(float cubeOctreeSize, int3 cubeGridPos)
    {
        return new int3(Mathf.FloorToInt(cubeGridPos.x / cubeOctreeSize), Mathf.FloorToInt(cubeGridPos.y / cubeOctreeSize), Mathf.FloorToInt(cubeGridPos.z / cubeOctreeSize));
    }
    
    static float GetHeight(float x, float z)
    {
        var v = Noise.Simplex(OctreeParam.seed, 0.005f, x, z);
        
        return WorldTerrain.BaseHeight + v * WorldTerrain.Size;
    }
    static float3 GetNormal(float x, float z, float size)
    {
        float h1 = GetHeight(x - size, z);
        float h2 = GetHeight(x + size, z);
        float h3 = GetHeight(x, z - size);
        float h4 = GetHeight(x, z + size);

        float3 v1 = new float3(x - size, h1, z);
        float3 v2 = new float3(x + size, h2, z);
        float3 v3 = new float3(x, h3, z - size);
        float3 v4 = new float3(x, h4, z + size);


        float3 cal1 = v2 - v1;
        float3 cal2 = v4 - v3;

        float3 nor = math.normalize(math.cross(cal1, cal2));

        if (nor.y < 0)
            nor *= -1;
        
        return nor;
    }
    
    static NodeBound FindChunkNode(WorldDataNative worldData, float3 worldPos)
    {
        var chunk = worldData.FindChunk(worldPos);

        return chunk;
    }
    static NodeBoundInt FindCubeOctree(WorldDataNative worldData, int3 id)
    {
        return worldData.HasCubeOctree(id)
            ? new NodeBoundInt(id * worldData.cubeOctreeSize, worldData.cubeOctreeSize)
            : NodeBoundInt.Invalid();
    }
    
    //Find chunks cubeNB passes
    static int FindOverlappingMaxChunk(WorldDataNative worldData, float4x4 cubeGridToWorld, NodeBoundInt cubeNB, out float maxChunkSize, out bool lodDiff)
    {
        int3 basePos = cubeNB.MinPos;
        int size = cubeNB.size;
        
        int maxLOD = -1;
        maxChunkSize = -1;
        int referenceLOD = -1;
        lodDiff = false;
        
        ReadOnlySpan<int3> offsets = stackalloc int3[]
        {
            new int3(0, 0, 0),
            new int3(0, 0, 1),
            new int3(0, 1, 0),
            new int3(0, 1, 1),
            new int3(1, 0, 0),
            new int3(1, 0, 1),
            new int3(1, 1, 0),
            new int3(1, 1, 1)
        };

        foreach (var offset in offsets)
        {
            int3 cubePos = basePos + offset * size;

            var chunkNB = FindChunkNode(worldData, GetWorldPos(cubeGridToWorld, cubePos));
            if (chunkNB.Valid)
            {
                int lod = (int)(math.round(math.log2(chunkNB.Size / OctreeParam.ChunkSize)) + 0.001f);
                if (lod > maxLOD) maxLOD = lod;
                if (chunkNB.Size > maxChunkSize) maxChunkSize = chunkNB.Size;

                if (referenceLOD == -1)
                    referenceLOD = lod;
                else if (referenceLOD != lod)
                    lodDiff = true;
            }
        }

        return maxLOD;
    }
    
    //Cube value without override values
    static byte FindCubePureValue(WorldDataNative worldData, NodeBoundInt cubeNB, out uint order)
    {
        return worldData.GetCubePureValue(cubeNB, out order);
    }
    
    //Cube value with override values
    static byte FindCubeGridPos(WorldDataNative worldData, int3 cubeGridPos, NativeHashMap<NodeBoundInt, CubeData> overrideValues, int cubeLOD)
    {
        return worldData.GetCubeValue(cubeGridPos, cubeLOD, overrideValues, out uint order);
    }
    static byte FindCubeGridPos(WorldDataNative worldData, int3 cubeGridPos, NativeHashMap<NodeBoundInt, CubeData> overrideValues, int cubeLOD, out uint order)
    {
        return worldData.GetCubeValue(cubeGridPos, cubeLOD, overrideValues, out order);
    }
    static byte FindCubeWorldPos(WorldDataNative worldData, float4x4 worldToCubeGrid, float3 worldPos, NativeHashMap<NodeBoundInt, CubeData> overrideValues, int cubeLOD)
    {
        return FindCubeGridPos(worldData, GetCubePos(worldToCubeGrid, worldPos), overrideValues, cubeLOD, out uint order);
    }
    static byte FindCubeWorldPos(WorldDataNative worldData, float4x4 worldToCubeGrid, float3 worldPos, NativeHashMap<NodeBoundInt, CubeData> overrideValues, int cubeLOD, out uint order)
    {
        return FindCubeGridPos(worldData, GetCubePos(worldToCubeGrid, worldPos), overrideValues, cubeLOD, out order);
    }
    
    static SphereSDF FindSphere(WorldDataNative worldData, float3 worldPos, out uint order)
    {
        SphereSDF lastSDF = new SphereSDF(0, 0, 0, 255);
        foreach (var s in worldData.AllSpheres)
        {
            if (math.distance(worldPos, s.center) < s.radius)
            {
                if (s.order > lastSDF.order)
                {
                    lastSDF = s;
                }
            }
        }

        order = lastSDF.order;
        return lastSDF;
    }
    
    static void Add8Children(ref NativeSimpleQueue<NodeBoundInt> q, NodeBoundInt cubeNB)
    {
        int s = cubeNB.size / 2;
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 2; k++)
                {
                    q.Enqueue(new NodeBoundInt(cubeNB.MinPos + new int3(i, j, k) * s, s));
                }
            }
        }
        
    }
    
    //For all child of cubeNB, override their value to match cubeNB, for cubes between chunks with different LOD
    static void SetOverrideValues(WorldDataNative worldData, NativeHashMap<NodeBoundInt, CubeData>.ParallelWriter overrideValues, NodeBoundInt cubeNB)
    {
        byte val = FindCubePureValue(worldData, cubeNB, out uint order);
        int divCnt = 31 - math.lzcnt((uint)(cubeNB.size / 2));
        divCnt = math.max(2, divCnt);//Assume max LOD diff between neighboring chunks is 2
        
        for (int i = 0; i < divCnt; i++)
        {
            int v = 1 << i;
            int s = cubeNB.size / v;
            for (int a = 0; a < v; a++)
            {
                for (int b = 0; b < v; b++)
                {
                    for (int c = 0; c < v; c++)
                    {
                        overrideValues.TryAdd(new NodeBoundInt(cubeNB.MinPos + new int3(a, b, c) * s, s), new CubeData(order, val));
                    }
                }
            }

        }
        
    }
    static void SetOverrideValues(WorldDataNative worldData, NativeHashMap<NodeBoundInt, CubeData> overrideValues, NodeBoundInt cubeNB)
    {
        byte val = FindCubePureValue(worldData, cubeNB, out uint order);
        int divCnt = 31 - math.lzcnt((uint)(cubeNB.size / 2));
        divCnt = math.max(2, divCnt);

        
        for (int i = 0; i < divCnt; i++)
        {
            int v = 1 << i;
            int s = cubeNB.size / v;
            for (int a = 0; a < v; a++)
            {
                for (int b = 0; b < v; b++)
                {
                    for (int c = 0; c < v; c++)
                    {
                        overrideValues.TryAdd(new NodeBoundInt(cubeNB.MinPos + new int3(a, b, c) * s, s), new CubeData(order, val));
                    }
                }
            }

        }
    }
    
    static void _AddFoundEdge(float4x4 cubeGridToWorld, int3 cubeMinPos, int cubeSize, byte cubeMat, NodeBound node, int3 gridMinIdx, int3 gridMaxIdx, int dir, float gridSize, NativeList<SignedEdgeID> result)
    {
        float3 wMinPos = GetWorldPos(cubeGridToWorld, cubeMinPos);
        float3 wMaxPos = GetWorldPos(cubeGridToWorld, cubeMinPos + cubeSize);

        int minX = gridMinIdx.x;
        int minY = gridMinIdx.y;
        int minZ = gridMinIdx.z;
        int maxX = gridMaxIdx.x;
        int maxY = gridMaxIdx.y;
        int maxZ = gridMaxIdx.z;

        int cellWorldMinXIdx = (int)math.ceil(wMinPos.x / gridSize);
        int cellWorldMaxXIdx = (int)math.floor(wMaxPos.x / gridSize);
        int cellWorldMinYIdx = (int)math.ceil(wMinPos.y / gridSize);
        int cellWorldMaxYIdx = (int)math.floor(wMaxPos.y / gridSize);
        int cellWorldMinZIdx = (int)math.ceil(wMinPos.z / gridSize);
        int cellWorldMaxZIdx = (int)math.floor(wMaxPos.z / gridSize);

        if (dir == 2 && node.MinPos.x < wMinPos.x && wMinPos.x < node.MaxPos.x)
        {
            if (cubeMat == 0) dir = 1;

            int worldXIdx = (int)math.floor(wMinPos.x / gridSize);
            if (worldXIdx != maxX)
            {
                for (int l = math.max(minY, cellWorldMinYIdx); l <= math.min(maxY, cellWorldMaxYIdx); l++)
                {
                    for (int m = math.max(minZ, cellWorldMinZIdx); m <= math.min(maxZ, cellWorldMaxZIdx); m++)
                    {
                        result.Add(new SignedEdgeID(worldXIdx, l, m, dir, cubeMat, cubeMat == 0));
                    }
                }
            }
        }

        if (dir == 1 && node.MinPos.x < wMaxPos.x && wMaxPos.x < node.MaxPos.x)
        {
            if (cubeMat == 0) dir = 2;

            int worldXIdx = (int)math.floor(wMaxPos.x / gridSize);
            if (worldXIdx != maxX)
            {
                for (int l = math.max(minY, cellWorldMinYIdx); l <= math.min(maxY, cellWorldMaxYIdx); l++)
                {
                    for (int m = math.max(minZ, cellWorldMinZIdx); m <= math.min(maxZ, cellWorldMaxZIdx); m++)
                    {
                        result.Add(new SignedEdgeID(worldXIdx, l, m, dir, cubeMat, cubeMat == 0));
                    }
                }
            }
        }

        if (dir == 4 && node.MinPos.y < wMinPos.y && wMinPos.y < node.MaxPos.y)
        {
            if (cubeMat == 0) dir = 3;

            int worldYIdx = (int)math.floor(wMinPos.y / gridSize);
            if (worldYIdx != maxY)
            {
                for (int l = math.max(minX, cellWorldMinXIdx); l <= math.min(maxX, cellWorldMaxXIdx); l++)
                {
                    for (int m = math.max(minZ, cellWorldMinZIdx); m <= math.min(maxZ, cellWorldMaxZIdx); m++)
                    {
                        result.Add(new SignedEdgeID(l, worldYIdx, m, dir, cubeMat, cubeMat == 0));
                    }
                }
            }
        }

        if (dir == 3 && node.MinPos.y < wMaxPos.y && wMaxPos.y < node.MaxPos.y)
        {
            if (cubeMat == 0) dir = 4;

            int worldYIdx = (int)math.floor(wMaxPos.y / gridSize);
            if (worldYIdx != maxY)
            {
                for (int l = math.max(minX, cellWorldMinXIdx); l <= math.min(maxX, cellWorldMaxXIdx); l++)
                {
                    for (int m = math.max(minZ, cellWorldMinZIdx); m <= math.min(maxZ, cellWorldMaxZIdx); m++)
                    {
                        result.Add(new SignedEdgeID(l, worldYIdx, m, dir, cubeMat, cubeMat == 0));
                    }
                }
            }
        }

        if (dir == 6 && node.MinPos.z < wMinPos.z && wMinPos.z < node.MaxPos.z)
        {
            if (cubeMat == 0) dir = 5;

            int worldZIdx = (int)math.floor(wMinPos.z / gridSize);
            if (worldZIdx != maxZ)
            {
                for (int l = math.max(minX, cellWorldMinXIdx); l <= math.min(maxX, cellWorldMaxXIdx); l++)
                {
                    for (int m = math.max(minY, cellWorldMinYIdx); m <= math.min(maxY, cellWorldMaxYIdx); m++)
                    {
                        result.Add(new SignedEdgeID(l, m, worldZIdx, dir, cubeMat, cubeMat == 0));
                    }
                }
            }
        }

        if (dir == 5 && node.MinPos.z < wMaxPos.z && wMaxPos.z < node.MaxPos.z)
        {
            if (cubeMat == 0) dir = 6;

            int worldZIdx = (int)math.floor(wMaxPos.z / gridSize);
            if (worldZIdx != maxZ)
            {
                for (int l = math.max(minX, cellWorldMinXIdx); l <= math.min(maxX, cellWorldMaxXIdx); l++)
                {
                    for (int m = math.max(minY, cellWorldMinYIdx); m <= math.min(maxY, cellWorldMaxYIdx); m++)
                    {
                        result.Add(new SignedEdgeID(l, m, worldZIdx, dir, cubeMat, cubeMat == 0));
                    }
                }
            }
        }
    }
    //Find all edges in world octree that intersects with cubeNB
    static void FindWorldEdgesInCube(WorldDataNative worldData, float4x4 cubeGridToWorld, int baseCubeLOD, NodeBound node, int3 gridMinIdx, int3 gridMaxIdx, float gridSize, NodeBoundInt cubeNB, NativeHashMap<NodeBoundInt, CubeData> overrideValues, NativeList<SignedEdgeID> result)
    {
        int s = 1 << baseCubeLOD;
        int3 minPos = cubeNB.MinPos;
        int3 maxPos = cubeNB.MaxPos - new int3(s);
        
        for (int i = minPos.x; i <= maxPos.x; i += s)
        {
            for (int j = minPos.y; j <= maxPos.y; j += s)
            {
                int3 cube0 = new int3(i, j, minPos.z);
                int3 cube1 = new int3(i, j, maxPos.z);

                byte cube0Val = FindCubeGridPos(worldData, cube0, overrideValues, baseCubeLOD);
                byte cube1Val = FindCubeGridPos(worldData, cube1, overrideValues, baseCubeLOD);

                byte cubeZ0Val = FindCubeGridPos(worldData, cube0 + new int3(0, 0, -s), overrideValues, baseCubeLOD);
                byte cubeZ1Val = FindCubeGridPos(worldData, cube1 + new int3(0, 0, s), overrideValues, baseCubeLOD);

                bool air0 = cube0Val == 0 && cubeZ0Val == 255;
                bool air1 = cube1Val == 0 && cubeZ1Val == 255;
                bool solid0 = cube0Val % 255 != 0 && (cubeZ0Val == 0 || cubeZ0Val == 255);
                bool solid1 = cube1Val % 255 != 0 && (cubeZ1Val == 0 || cubeZ1Val == 255);

                if (air0 || solid0) _AddFoundEdge(cubeGridToWorld, cube0, s, cube0Val, node, gridMinIdx, gridMaxIdx, 6, gridSize, result);
                if (air1 || solid1) _AddFoundEdge(cubeGridToWorld, cube1, s, cube1Val, node, gridMinIdx, gridMaxIdx, 5, gridSize, result);

                
            }
        }

        for (int i = minPos.x; i <= maxPos.x; i += s)
        {
            for (int k = minPos.z; k <= maxPos.z; k += s)
            {

                int3 cube0 = new int3(i, minPos.y, k);
                int3 cube1 = new int3(i, maxPos.y, k);

                byte cube0Val = FindCubeGridPos(worldData, cube0, overrideValues, baseCubeLOD);
                byte cube1Val = FindCubeGridPos(worldData, cube1, overrideValues, baseCubeLOD);

                byte cubeY0Val = FindCubeGridPos(worldData, cube0 + new int3(0, -s, 0), overrideValues, baseCubeLOD);
                byte cubeY1Val = FindCubeGridPos(worldData, cube1 + new int3(0, s, 0), overrideValues, baseCubeLOD);

                bool air0 = cube0Val == 0 && cubeY0Val == 255;
                bool air1 = cube1Val == 0 && cubeY1Val == 255;
                bool solid0 = cube0Val % 255 != 0 && (cubeY0Val == 0 || cubeY0Val == 255);
                bool solid1 = cube1Val % 255 != 0 && (cubeY1Val == 0 || cubeY1Val == 255);

                if (air0 || solid0) _AddFoundEdge(cubeGridToWorld, cube0, s, cube0Val, node, gridMinIdx, gridMaxIdx, 4, gridSize, result);
                if (air1 || solid1) _AddFoundEdge(cubeGridToWorld, cube1, s, cube1Val, node, gridMinIdx, gridMaxIdx, 3, gridSize, result);
                
            }
        }

        for (int j = minPos.y; j <= maxPos.y; j += s)
        {
            for (int k = minPos.z; k <= maxPos.z; k += s)
            {

                int3 cube0 = new int3(minPos.x, j, k);
                int3 cube1 = new int3(maxPos.x, j, k);

                byte cube0Val = FindCubeGridPos(worldData, cube0, overrideValues, baseCubeLOD);
                byte cube1Val = FindCubeGridPos(worldData, cube1, overrideValues, baseCubeLOD);

                byte cubeX0Val = FindCubeGridPos(worldData, cube0 + new int3(-s, 0, 0), overrideValues, baseCubeLOD);
                byte cubeX1Val = FindCubeGridPos(worldData, cube1 + new int3(s, 0, 0), overrideValues, baseCubeLOD);

                bool air0 = cube0Val == 0 && cubeX0Val == 255;
                bool air1 = cube1Val == 0 && cubeX1Val == 255;
                bool solid0 = cube0Val % 255 != 0 && (cubeX0Val == 0 || cubeX0Val == 255);
                bool solid1 = cube1Val % 255 != 0 && (cubeX1Val == 0 || cubeX1Val == 255);

                if (air0 || solid0) _AddFoundEdge(cubeGridToWorld, cube0, s, cube0Val, node, gridMinIdx, gridMaxIdx, 2, gridSize, result);
                if (air1 || solid1) _AddFoundEdge(cubeGridToWorld, cube1, s, cube1Val, node, gridMinIdx, gridMaxIdx, 1, gridSize, result);
                
            }
        }
    }
    //Find terrain that would only be revealed when node is subdivided
    static bool NodeHasTerrainIfDiv(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound nb, int div, int cubeLOD, NativeHashMap<NodeBoundInt, CubeData> overrideValues)
    {
        float3 minPos = nb.MinPos;
        float3 maxPos = nb.MaxPos;
        float gridSize = nb.Size / math.pow(2, div);

        int minX = (int)math.round(minPos.x / gridSize);
        int minY = (int)math.round(minPos.y / gridSize);
        int minZ = (int)math.round(minPos.z / gridSize);
        int maxX = (int)math.round(maxPos.x / gridSize);
        int maxY = (int)math.round(maxPos.y / gridSize);
        int maxZ = (int)math.round(maxPos.z / gridSize);

        for (int i = minX; i <= maxX; i++)
        {
            for (int k = minZ; k <= maxZ; k++)
            {
                bool xMaxEdge = i == maxX;
                bool zMaxEdge = k == maxZ;

                float height = GetHeight(i * gridSize, k * gridSize);
                float heightX = GetHeight((i + 1) * gridSize, k * gridSize);
                float heightZ = GetHeight(i * gridSize, (k + 1) * gridSize);

                int j = (int)math.floor(height / gridSize);
                int jx = (int)math.floor(heightX / gridSize);
                int jz = (int)math.floor(heightZ / gridSize);

                float3 leafMinPos = new float3(i, j, k) * gridSize;

                if (height >= minY * gridSize && height <= maxY * gridSize)
                {
                    float3 solidPos = leafMinPos;
                    float3 airPos = leafMinPos + new float3(0, 1, 0) * gridSize;
                    bool actualTerrainEdge = FindCubeWorldPos(worldData, worldToCubeGrid, solidPos, overrideValues, cubeLOD) == 255 &&
                                             FindCubeWorldPos(worldData, worldToCubeGrid, airPos, overrideValues, cubeLOD) == 255;
                    if (actualTerrainEdge)
                        return true;
                }

                if (!xMaxEdge && j != jx)
                {
                    int xMinIdx = j > jx ? jx + 1 : j + 1;
                    int xMaxIdx = jx > j ? jx : j;
                    int edgeDir = j > jx ? 1 : 2;

                    for (int a = math.max(minY, xMinIdx); a <= math.min(maxY, xMaxIdx); a++)
                    {
                        float3 emp = new float3(leafMinPos.x, a * gridSize, leafMinPos.z);
                        float3 solidPos = edgeDir == 1 ? emp : emp + new float3(1, 0, 0) * gridSize;
                        float3 airPos = edgeDir == 1 ? emp + new float3(1, 0, 0) * gridSize : emp;

                        bool actualTerrainEdge = FindCubeWorldPos(worldData, worldToCubeGrid, solidPos, overrideValues, cubeLOD) == 255 &&
                                                 FindCubeWorldPos(worldData, worldToCubeGrid, airPos, overrideValues, cubeLOD) == 255;
                        if (actualTerrainEdge)
                            return true;
                    }
                }

                if (!zMaxEdge && j != jz)
                {
                    int zMinIdx = j > jz ? jz + 1 : j + 1;
                    int zMaxIdx = jz > j ? jz : j;
                    int edgeDir = j > jz ? 5 : 6;

                    for (int a = math.max(minY, zMinIdx); a <= math.min(maxY, zMaxIdx); a++)
                    {
                        float3 emp = new float3(leafMinPos.x, a * gridSize, leafMinPos.z);
                        float3 solidPos = edgeDir == 5 ? emp : emp + new float3(0, 0, 1) * gridSize;
                        float3 airPos = edgeDir == 5 ? emp + new float3(0, 0, 1) * gridSize : emp;

                        bool actualTerrainEdge = FindCubeWorldPos(worldData, worldToCubeGrid, solidPos, overrideValues, cubeLOD) == 255 &&
                                                 FindCubeWorldPos(worldData, worldToCubeGrid, airPos, overrideValues, cubeLOD) == 255;
                        if (actualTerrainEdge)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    static bool NodeHasSphereIntersection(WorldDataNative worldData, NodeBound nb, out bool forceDivisionIfIntersect)
    {
        forceDivisionIfIntersect = false;
        bool found = false;
        foreach (var s in worldData.AllSpheres)
        {
            bool isVerySmallSphere = s.radius < nb.Size + 0.1f;
            bool isSmallSphere = s.radius < nb.Size * 2 + 0.1f;
            if (isVerySmallSphere)
            {
                if (math.distance(s.center, nb.MinPos + new float3(0.5f) * nb.Size) < nb.Size * 0.5f * 1.74f + s.radius)
                {
                    forceDivisionIfIntersect = true;
                    return true;
                }
                continue;
            }
            else if (isSmallSphere)
            {
                if (math.distance(s.center, nb.MinPos + new float3(0.5f) * nb.Size) < nb.Size * 0.5f * 1.74f + s.radius)
                {
                    forceDivisionIfIntersect = true;
                }
            }

            float min = 1000, max = -1000;
            
            var v1 = s.Value(nb.MinPos); if (v1 < min) min = v1; if (v1 > max) max = v1;
            var v2 = s.Value(nb.MinPos + new float3(0, 0, 1) * nb.Size); if (v2 < min) min = v2; if (v2 > max) max = v2;
            var v3 = s.Value(nb.MinPos + new float3(0, 1, 0) * nb.Size); if (v3 < min) min = v3; if (v3 > max) max = v3;
            var v4 = s.Value(nb.MinPos + new float3(0, 1, 1) * nb.Size); if (v4 < min) min = v4; if (v4 > max) max = v4;
            var v5 = s.Value(nb.MinPos + new float3(1, 0, 0) * nb.Size); if (v5 < min) min = v5; if (v5 > max) max = v5;
            var v6 = s.Value(nb.MinPos + new float3(1, 0, 1) * nb.Size); if (v6 < min) min = v6; if (v6 > max) max = v6;
            var v7 = s.Value(nb.MinPos + new float3(1, 1, 0) * nb.Size); if (v7 < min) min = v7; if (v7 > max) max = v7;
            var v8 = s.Value(nb.MinPos + new float3(1, 1, 1) * nb.Size); if (v8 < min) min = v8; if (v8 > max) max = v8;

            if (min * max < 0) 
                found = true;
        }

        return found;
    }
    static bool NodeHasSphereIntersection(SphereSDF s, NodeBound nb, out bool forceDivision)
    {
        forceDivision = false;
        bool isSmall = s.radius < nb.Size + 0.1f;
        if (isSmall)
        {
            if (math.distance(s.center, nb.MinPos + new float3(0.5f) * nb.Size) < nb.Size * 1.74f + s.radius)
            {
                forceDivision = true;
                return true;
            }
            return false;
        }
        
        
        float min = 1000, max = -1000;

        var v1 = s.Value(nb.MinPos); if (v1 < min) min = v1; if (v1 > max) max = v1;
        var v2 = s.Value(nb.MinPos + new float3(0, 0, 1) * nb.Size); if (v2 < min) min = v2; if (v2 > max) max = v2;
        var v3 = s.Value(nb.MinPos + new float3(0, 1, 0) * nb.Size); if (v3 < min) min = v3; if (v3 > max) max = v3;
        var v4 = s.Value(nb.MinPos + new float3(0, 1, 1) * nb.Size); if (v4 < min) min = v4; if (v4 > max) max = v4;
        var v5 = s.Value(nb.MinPos + new float3(1, 0, 0) * nb.Size); if (v5 < min) min = v5; if (v5 > max) max = v5;
        var v6 = s.Value(nb.MinPos + new float3(1, 0, 1) * nb.Size); if (v6 < min) min = v6; if (v6 > max) max = v6;
        var v7 = s.Value(nb.MinPos + new float3(1, 1, 0) * nb.Size); if (v7 < min) min = v7; if (v7 > max) max = v7;
        var v8 = s.Value(nb.MinPos + new float3(1, 1, 1) * nb.Size); if (v8 < min) min = v8; if (v8 > max) max = v8;

        if (min * max < 0) return true;
        else return false;
    }
    static bool NodeHasCube(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound nb, NativeHashMap<NodeBoundInt, CubeData> overrideValues, int cubeLOD)//nb : datanode under
    {
        if (FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(0, 0, 0) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(0, 0, 1) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(0, 1, 0) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(0, 1, 1) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(1, 0, 0) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(1, 0, 1) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(1, 1, 0) * nb.Size, overrideValues,
                cubeLOD) != 255 ||
            FindCubeWorldPos(worldData, worldToCubeGrid, nb.MinPos + new float3(1, 1, 1) * nb.Size, overrideValues,
                cubeLOD) != 255)
            return true;
        return false;
    }
    
    static void _FindNodesAroundEdge(SignedEdgeID edge, out int3 node1, out int3 node2, out int3 node3, out int3 node4)
    {
        int3 edgeV = edge;

        if (edge.w == 1 || edge.w == 2)
        {
            node1 = edgeV + new int3(0, 0, -1);
            node2 = edgeV;
            node3 = edgeV + new int3(0, -1, -1);
            node4 = edgeV + new int3(0, -1, 0);
        }
        else if (edge.w == 3 || edge.w == 4)
        {
            node1 = edgeV + new int3(-1, 0, 0);
            node2 = edgeV;
            node3 = edgeV + new int3(-1, 0, -1);
            node4 = edgeV + new int3(0, 0, -1);
        }
        else // edge.w == 5 || edge.w == 6
        {
            node1 = edgeV + new int3(-1, 0, 0);
            node2 = edgeV;
            node3 = edgeV + new int3(-1, -1, 0);
            node4 = edgeV + new int3(0, -1, 0);
        }
    }
    static void FindAllTerrainIntersectingNodes(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound node, int cubeLOD, int divCnt, NativeHashMap<NodeBoundInt, CubeData> overrideValues, NativeHashSet<int3> allActualTerrainNodes)
    {
        float gridSize = node.Size / math.pow(2, divCnt);
        int minX = (int)math.round(node.MinPos.x / gridSize);
        int minY = (int)math.round(node.MinPos.y / gridSize);
        int minZ = (int)math.round(node.MinPos.z / gridSize);
        int maxX = (int)math.round(node.MaxPos.x / gridSize);
        int maxY = (int)math.round(node.MaxPos.y / gridSize);
        int maxZ = (int)math.round(node.MaxPos.z / gridSize);

        for (int i = minX; i <= maxX; i++)
        {
            for (int k = minZ; k <= maxZ; k++)
            {
                bool xMaxEdge = i == maxX;
                bool zMaxEdge = k == maxZ;

                float height = GetHeight(i * gridSize, k * gridSize);
                float heightX = GetHeight((i + 1) * gridSize, k * gridSize);
                float heightZ = GetHeight(i * gridSize, (k + 1) * gridSize);

                int j = (int)math.floor(height / gridSize);
                int jx = (int)math.floor(heightX / gridSize);
                int jz = (int)math.floor(heightZ / gridSize);

                float3 leafMinPos = new float3(i, j, k) * gridSize;

                if (height >= minY * gridSize && height <= maxY * gridSize)
                {
                    float3 solidPos = leafMinPos;
                    float3 airPos = leafMinPos + new float3(0, 1, 0) * gridSize;

                    bool actualTerrainEdge = FindCubeWorldPos(worldData, worldToCubeGrid, solidPos, overrideValues, cubeLOD) == 255 &&
                                             FindCubeWorldPos(worldData, worldToCubeGrid, airPos, overrideValues, cubeLOD) == 255;

                    if (actualTerrainEdge)
                    {
                        var edgeDir = 3;
                        int3 empIndex = new int3(i, j, k);

                        _FindNodesAroundEdge(new SignedEdgeID(empIndex, edgeDir), out int3 n1, out int3 n2, out int3 n3, out int3 n4);
                        allActualTerrainNodes.Add(n1);
                        allActualTerrainNodes.Add(n2);
                        allActualTerrainNodes.Add(n3);
                        allActualTerrainNodes.Add(n4);
                    }
                }

                if (!xMaxEdge && j != jx)
                {
                    int xMinIdx = j > jx ? jx + 1 : j + 1;
                    int xMaxIdx = jx > j ? jx : j;
                    int edgeDir = j > jx ? 1 : 2;

                    for (int a = math.max(minY, xMinIdx); a <= math.min(maxY, xMaxIdx); a++)
                    {
                        float3 emp = new float3(i, a, k) * gridSize;
                        float3 solidPos = edgeDir == 1 ? emp : emp + new float3(1, 0, 0) * gridSize;
                        float3 airPos = edgeDir == 1 ? emp + new float3(1, 0, 0) * gridSize : emp;

                        bool actualTerrainEdge = FindCubeWorldPos(worldData, worldToCubeGrid, solidPos, overrideValues, cubeLOD) == 255 &&
                                                 FindCubeWorldPos(worldData, worldToCubeGrid, airPos, overrideValues, cubeLOD) == 255;

                        if (actualTerrainEdge)
                        {
                            int3 empIndex = new int3(i, a, k);
                            _FindNodesAroundEdge(new SignedEdgeID(empIndex, edgeDir), out int3 n1, out int3 n2, out int3 n3, out int3 n4);
                            allActualTerrainNodes.Add(n1);
                            allActualTerrainNodes.Add(n2);
                            allActualTerrainNodes.Add(n3);
                            allActualTerrainNodes.Add(n4);
                        }
                    }
                }

                if (!zMaxEdge && j != jz)
                {
                    int zMinIdx = j > jz ? jz + 1 : j + 1;
                    int zMaxIdx = jz > j ? jz : j;
                    int edgeDir = j > jz ? 5 : 6;

                    for (int a = math.max(minY, zMinIdx); a <= math.min(maxY, zMaxIdx); a++)
                    {
                        float3 emp = new float3(i, a, k) * gridSize;
                        float3 solidPos = edgeDir == 5 ? emp : emp + new float3(0, 0, 1) * gridSize;
                        float3 airPos = edgeDir == 5 ? emp + new float3(0, 0, 1) * gridSize : emp;

                        bool actualTerrainEdge = FindCubeWorldPos(worldData, worldToCubeGrid, solidPos, overrideValues, cubeLOD) == 255 &&
                                                 FindCubeWorldPos(worldData, worldToCubeGrid, airPos, overrideValues, cubeLOD) == 255;

                        if (actualTerrainEdge)
                        {
                            int3 empIndex = new int3(i, a, k);
                            _FindNodesAroundEdge(new SignedEdgeID(empIndex, edgeDir), out int3 n1, out int3 n2, out int3 n3, out int3 n4);
                            allActualTerrainNodes.Add(n1);
                            allActualTerrainNodes.Add(n2);
                            allActualTerrainNodes.Add(n3);
                            allActualTerrainNodes.Add(n4);
                        }
                    }
                }
            }
        }
    }
    static void FindAllCubeIntersectionNodesDiv1(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound node, int cubeLOD, NativeHashMap<NodeBoundInt, CubeData> overrideValue, NativeHashSet<NodeBound> allCubeNodes)
    {
        NativeList<NodeBound> children = new NativeList<NodeBound>(Allocator.Temp);
        NativeHashMap<int3, bool> hasCube = new NativeHashMap<int3, bool>(50, Allocator.Temp);
        Get8Children(node, children);
        
        for (int i = 0; i < 8; i++)
        {
            var n = children[i];
            
            int3 p1 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(0, 0, 0) * n.Size);
            int3 p2 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(0, 0, 1) * n.Size);
            int3 p3 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(0, 1, 0) * n.Size);
            int3 p4 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(0, 1, 1) * n.Size);
            int3 p5 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(1, 0, 0) * n.Size);
            int3 p6 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(1, 0, 1) * n.Size);
            int3 p7 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(1, 1, 0) * n.Size);
            int3 p8 = GetCubePos(worldToCubeGrid, n.MinPos + new float3(1, 1, 1) * n.Size);

            bool c1, c2, c3, c4, c5, c6, c7, c8;
            if(!hasCube.TryGetValue(p1, out c1)) c1 = FindCubeGridPos(worldData, p1, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p2, out c2)) c2 = FindCubeGridPos(worldData, p2, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p3, out c3)) c3 = FindCubeGridPos(worldData, p3, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p4, out c4)) c4 = FindCubeGridPos(worldData, p4, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p5, out c5)) c5 = FindCubeGridPos(worldData, p5, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p6, out c6)) c6 = FindCubeGridPos(worldData, p6, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p7, out c7)) c7 = FindCubeGridPos(worldData, p7, overrideValue, cubeLOD) != 255;
            if(!hasCube.TryGetValue(p8, out c8)) c8 = FindCubeGridPos(worldData, p8, overrideValue, cubeLOD) != 255;

            if (c1 || c2 || c3 || c4 || c5 || c6 || c7 || c8)
            {
                allCubeNodes.Add(n);
            }
        }
    }
    
    //Determine each of 8 children of node's, whether to divide it further or not
    static void DivideRecalculateNode(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound chunkNB, NodeBound node, NativeHashMap<NodeBoundInt, CubeData> overrideValue, int cubeLOD, NativeHashSet<NodeBound> finalNode, NativeHashSet<NodeBound> nextDivision)
    {
        int maxDiv = cubeLOD == 0 ? 2 : 1;

        float dataNodeSize = chunkNB.Size / OctreeParam.ChunkSize;
        int divCnt = math.max(0, maxDiv - (int)math.round(math.log2(dataNodeSize / node.Size)));
        float gridSize = node.Size / math.pow(2, divCnt);

        NativeHashSet<int3> allActualTerrainNodes = new NativeHashSet<int3>(100, Allocator.Temp);
        NativeHashSet<NodeBound> allCubeNodes = new NativeHashSet<NodeBound>(100, Allocator.Temp);
        
        FindAllTerrainIntersectingNodes(worldData, worldToCubeGrid, node, cubeLOD, divCnt, overrideValue, allActualTerrainNodes);
        FindAllCubeIntersectionNodesDiv1(worldData, worldToCubeGrid, node, cubeLOD, overrideValue, allCubeNodes);

        NativeList<NodeBound> children = new NativeList<NodeBound>(Allocator.Temp);
        Get8Children(node, children);

        for (int i = 0; i < 8; i++)
        {
            bool hasCube = allCubeNodes.Contains(children[i]);

            bool hasTerrain;
            if (divCnt == 1)
            {
                int x = (int)math.round(children[i].MinPos.x / gridSize);
                int y = (int)math.round(children[i].MinPos.y / gridSize);
                int z = (int)math.round(children[i].MinPos.z / gridSize);
                hasTerrain = allActualTerrainNodes.Contains(new int3(x, y, z));
            }
            else
            {
                NativeList<NodeBound> children2 = new NativeList<NodeBound>(Allocator.Temp);
                Get8Children(children[i], children2);
                hasTerrain = false;
                for (int j = 0; j < 8; j++)
                {
                    int x = (int)math.round(children2[j].MinPos.x / gridSize);
                    int y = (int)math.round(children2[j].MinPos.y / gridSize);
                    int z = (int)math.round(children2[j].MinPos.z / gridSize);

                    if (allActualTerrainNodes.Contains(new int3(x, y, z)))
                    {
                        hasTerrain = true;
                        break;
                    }
                }
            }


            bool intersectsSphere = NodeHasSphereIntersection(worldData, children[i], out bool forceDivisionIfIntersect);
            if (intersectsSphere && (hasCube || hasTerrain || forceDivisionIfIntersect) || hasCube && hasTerrain)
            {
                nextDivision.Add(children[i]);
            }
            else
            {
                finalNode.Add(children[i]);
            }
        }
    }
    
    
    static void Get8Children(NodeBound node, NativeList<NodeBound> child)
    {
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 2; k++)
                {
                    child.Add(new NodeBound(node.MinPos + new float3(i, j, k) * node.Size * .5f, node.Size * .5f));
                }
            }
        }
        
    }
    //From node, get all final nodes to recalculate, subdividing where needed
    static void ProcessNode(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound chunkNB, NodeBound node, NativeHashMap<NodeBoundInt, CubeData> overrideValues, NativeHashSet<Chunk_Node_LOD>.ParallelWriter nodesToRecalculate)
    {
        NativeHashSet<NodeBound> finalNode = new NativeHashSet<NodeBound>(30, Allocator.Temp);
        NativeHashSet<NodeBound> dn = new NativeHashSet<NodeBound>(30, Allocator.Temp);
        NativeHashSet<NodeBound> dn2 = new NativeHashSet<NodeBound>(30, Allocator.Temp);
        NativeHashSet<Chunk_Node> allSD = new NativeHashSet<Chunk_Node>(30, Allocator.Temp);
        NativeHashSet<Chunk_Node_LOD> nodesToRecalc = new NativeHashSet<Chunk_Node_LOD>(30, Allocator.Temp);

        int cubeLOD = (int)math.round(math.log2(chunkNB.Size / OctreeParam.ChunkSize));
        
        
        if (cubeLOD > 1)
        {
            nodesToRecalc.Add(new Chunk_Node_LOD(chunkNB, node, cubeLOD));
        }
        else
        {
            bool nodeInTerrainRange = node.MaxPos.y > WorldTerrain.MinHeight && node.MinPos.y < WorldTerrain.MaxHeight;

            int maxDiv = cubeLOD == 0 ? 2 : 1;

            bool intersectsTerrain = nodeInTerrainRange && NodeHasTerrainIfDiv(worldData, worldToCubeGrid, node, maxDiv, cubeLOD, overrideValues);
            bool intersectsSphere = NodeHasSphereIntersection(worldData, node, out bool forceDivision);
            bool hasCube = NodeHasCube(worldData, worldToCubeGrid, node, overrideValues, cubeLOD);
            
            if (!(intersectsTerrain && intersectsSphere || intersectsTerrain && hasCube || intersectsSphere && hasCube || intersectsSphere && forceDivision))
            { 
                nodesToRecalc.Add(new Chunk_Node_LOD(chunkNB, node, cubeLOD));
            }
            else
            {
                dn.Clear();
                finalNode.Clear();
                DivideRecalculateNode(worldData, worldToCubeGrid, chunkNB, node, overrideValues, cubeLOD, finalNode, dn);
                allSD.Add(new Chunk_Node(chunkNB, node));

                foreach (var nn in finalNode)
                    nodesToRecalc.Add(new Chunk_Node_LOD(chunkNB, nn, cubeLOD));
                
                
                foreach (var nn in dn)
                {
                    if (maxDiv == 2)
                    {
                        dn2.Clear();
                        finalNode.Clear();
                        DivideRecalculateNode(worldData, worldToCubeGrid, chunkNB, nn, overrideValues, cubeLOD, finalNode, dn2);
                        allSD.Add(new Chunk_Node(chunkNB, nn));

                        foreach (var nnn in finalNode)
                            nodesToRecalc.Add(new Chunk_Node_LOD(chunkNB, nnn, cubeLOD));

                        foreach (var nnn in dn2)
                            nodesToRecalc.Add(new Chunk_Node_LOD(chunkNB, nnn, cubeLOD));
                    }
                    else
                    {
                        nodesToRecalc.Add(new Chunk_Node_LOD(chunkNB, nn, cubeLOD));
                    }
                }
            }
        }
        
        foreach (var n in nodesToRecalc)
            nodesToRecalculate.Add(n);
    }

    
    //If cubeNode is on the boundary between multiple chunks of different size, get all non-max sized chunks
    static void FindExtraChunksToUpdate(WorldDataNative worldData, float maxChunkSize, NodeBoundInt cubeNode, NativeHashSet<NodeBound>.ParallelWriter extraChunkUpdates)
    {
        int3 basePos = cubeNode.MinPos;

        int3 w0 = basePos + new int3(0, 0, 0) * cubeNode.size;
        int3 w1 = basePos + new int3(0, 0, 1) * cubeNode.size;
        int3 w2 = basePos + new int3(0, 1, 0) * cubeNode.size;
        int3 w3 = basePos + new int3(0, 1, 1) * cubeNode.size;
        int3 w4 = basePos + new int3(1, 0, 0) * cubeNode.size;
        int3 w5 = basePos + new int3(1, 0, 1) * cubeNode.size;
        int3 w6 = basePos + new int3(1, 1, 0) * cubeNode.size;
        int3 w7 = basePos + new int3(1, 1, 1) * cubeNode.size;
        NodeBound chunk;
        if ((chunk = FindChunkNode(worldData, w0)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w1)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w2)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w3)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w4)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w5)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w6)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
        if ((chunk = FindChunkNode(worldData, w7)).Valid && chunk.Size < maxChunkSize - 0.1f) extraChunkUpdates.Add(chunk);
    }
    
    //Get all nodes of datanode size (ex: 1 / 16 of a chunk) that needs recalculation due to cubes or SDFs
    static void ProcessChunk(WorldDataNative worldData, float4x4 cubeGridToWorld, float4x4 worldToCubeGrid, NodeBound chunkNB, NativeHashSet<NodeBound>.ParallelWriter extraChunkUpdates, NativeHashSet<Chunk_Node_LOD>.ParallelWriter recalculateDataNodes, NativeHashMap<NodeBoundInt, CubeData>.ParallelWriter overrideValues, NativeHashSet<Chunk_Node_LOD>.ParallelWriter finalRecal)
    {
        NativeHashMap<NodeBoundInt, CubeData> overrideValueLocal = new NativeHashMap<NodeBoundInt, CubeData>(10000, Allocator.Temp);
        //NativeQueue in jobs have bug
        NativeSimpleQueue<NodeBoundInt> cubeQ = new NativeSimpleQueue<NodeBoundInt>(10000, Allocator.Temp);
        
        NativeHashSet<NodeBoundInt> foundCubes = new NativeHashSet<NodeBoundInt>(2000, Allocator.Temp);
        NativeHashSet<NodeBoundInt> lodOverlapCubes = new NativeHashSet<NodeBoundInt>(1000, Allocator.Temp);
        
        
        NativeHashSet<NodeBound> addedNode = new NativeHashSet<NodeBound>(4000, Allocator.Temp);
        
        int cubeOctreeSize = worldData.cubeOctreeSize;
        
        float gridSize = chunkNB.Size / OctreeParam.ChunkSize;
        int cubeLOD = (int)math.round(math.log2(chunkNB.Size / OctreeParam.ChunkSize));

        float3 worldPosMin = chunkNB.MinPos;
        float3 worldPosMax = chunkNB.MaxPos;
        int cubeSize = 1 << cubeLOD;

        int3 gridMinIdx = (int3)math.round(worldPosMin / gridSize);
        int3 gridMaxIdx = (int3)math.round(worldPosMax / gridSize);

        float3 gridMinWPos = (float3)gridMinIdx * gridSize;
        float3 gridMaxWPos = (float3)gridMaxIdx * gridSize;

        int3 cubeGridMinPos = GetCubePos(worldToCubeGrid, gridMinWPos);
        int3 cubeGridMaxPos = GetCubePos(worldToCubeGrid, gridMaxWPos);

        int3 octreeIDMin = GetCubeOctreeID(cubeOctreeSize, cubeGridMinPos);
        int3 octreeIDMax = GetCubeOctreeID(cubeOctreeSize, cubeGridMaxPos);
        
        //Find all cubes of 1 << cubeLOD size
        for (int i = octreeIDMin.x; i <= octreeIDMax.x; i++)
        {
            for (int j = octreeIDMin.y; j <= octreeIDMax.y; j++)
            {
                for (int k = octreeIDMin.z; k <= octreeIDMax.z; k++)
                {
                    var cubeOctree = FindCubeOctree(worldData, new int3(i, j, k));
                    if (cubeOctree.Valid)
                    {
                        cubeQ.Clear();
                        cubeQ.Enqueue(cubeOctree);
                        
                        while (cubeQ.Count > 0)
                        {
                            var n = cubeQ.Dequeue();
                            int3 minP = n.MinPos;
                            int3 maxP = n.MaxPos;
                            
                            if (cubeGridMinPos.x > maxP.x || cubeGridMinPos.y > maxP.y || cubeGridMinPos.z > maxP.z ||
                                cubeGridMaxPos.x < minP.x || cubeGridMaxPos.y < minP.y || cubeGridMaxPos.z < minP.z)
                                continue;
                            
                            var cmin = FindChunkNode(worldData, GetWorldPos(cubeGridToWorld, n.MinPos));
                            var cmax = FindChunkNode(worldData, GetWorldPos(cubeGridToWorld, n.MaxPos));
                            bool isOnChunkBoundary = cmin.Valid && cmax.Valid && !cmin.Equals(cmax);

                            byte pv = FindCubePureValue(worldData, n, out uint order);
                            
                            if (pv != 255)
                            {
                                if (isOnChunkBoundary)
                                {
                                    int cmaxLod = FindOverlappingMaxChunk(worldData, cubeGridToWorld, n,
                                        out float maxChunkSize, out bool lodDiff);

                                    //경계면에서 가장 큰 청크의 데이터노드일 시 
                                    if (lodDiff && n.size == (1 << cmaxLod))
                                    {
                                        lodOverlapCubes.Add(n);
                                        foundCubes.Add(n);
                                        if (cubeLOD == cmaxLod) //내가 가장 큰 노드면 주위 작은 청크들 강제 업데이트(override 적용 필요)
                                        {
                                            FindExtraChunksToUpdate(worldData, maxChunkSize, n, extraChunkUpdates);
                                        }
                                    }
                                    else if (n.size == cubeSize)
                                    {
                                        foundCubes.Add(n);
                                    }
                                    else if (!worldData.IsLeaf(n))
                                    {
                                        Add8Children(ref cubeQ, n);
                                    }
                                }
                                else if (n.size == cubeSize)
                                {
                                    foundCubes.Add(n);
                                }
                                else if (!worldData.IsLeaf(n))
                                {
                                    Add8Children(ref cubeQ, n);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        foreach (var b in lodOverlapCubes) 
        {
            SetOverrideValues(worldData, overrideValueLocal, b);
            SetOverrideValues(worldData, overrideValues, b);
        }
        
        //Get all nodes affected by cubes
        NativeList<SignedEdgeID> edges = new NativeList<SignedEdgeID>(Allocator.Temp);
        foreach (var c in foundCubes)
        {
            FindWorldEdgesInCube(worldData, cubeGridToWorld, cubeLOD, chunkNB, gridMinIdx, gridMaxIdx, gridSize, c, overrideValueLocal, edges); 
        }
        foreach (var e in edges)
        {
            var edgeDir = e.w;
            float3 edgeMinPos = (float3)e * gridSize;

            float3 pos0, pos1, pos2, pos3;
            
            if (edgeDir <= 2)
            {
                pos0 = edgeMinPos + new float3(0f, 0f, -1f) * gridSize;
                pos1 = edgeMinPos + new float3(0f, 0f, -0f) * gridSize;
                pos2 = edgeMinPos + new float3(0f, -1f, -1f) * gridSize;
                pos3 = edgeMinPos + new float3(0f, -1f, 0f) * gridSize;
            }
            else if (edgeDir <= 4)
            {
                pos0 = edgeMinPos + new float3(-1f, 0f, 0f) * gridSize;
                pos1 = edgeMinPos + new float3(0f, 0f, 0f) * gridSize;
                pos2 = edgeMinPos + new float3(-1f, 0f, -1f) * gridSize;
                pos3 = edgeMinPos + new float3(0f, 0f, -1f) * gridSize;
            }
            else
            {
                pos0 = edgeMinPos + new float3(-1f, 0f, 0f) * gridSize;
                pos1 = edgeMinPos + new float3(0f, 0f, 0f) * gridSize;
                pos2 = edgeMinPos + new float3(-1f, -1f, 0f) * gridSize;
                pos3 = edgeMinPos + new float3(0f, -1f, 0f) * gridSize;
            }
            
            float3 pos = pos0;
            var nnb = new NodeBound(pos, gridSize);
            if (OctreeUtil.Contains(chunkNB, nnb))
            {
                addedNode.Add(nnb);
                if (cubeLOD <= 1)
                    recalculateDataNodes.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
                else
                    finalRecal.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
            }

            pos = pos1;
            nnb = new NodeBound(pos, gridSize);
            if (OctreeUtil.Contains(chunkNB, nnb))
            {
                addedNode.Add(nnb);

                if (cubeLOD <= 1)
                    recalculateDataNodes.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
                else
                    finalRecal.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
            }
            
            pos = pos2;
            nnb = new NodeBound(pos, gridSize);
            if (OctreeUtil.Contains(chunkNB, nnb))
            {
                addedNode.Add(nnb);

                if (cubeLOD <= 1)
                    recalculateDataNodes.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
                else
                    finalRecal.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
            }
            
            pos = pos3;
            nnb = new NodeBound(pos, gridSize);
            if (OctreeUtil.Contains(chunkNB, nnb))
            {
                addedNode.Add(nnb);

                if (cubeLOD <= 1)
                    recalculateDataNodes.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
                else
                    finalRecal.Add(new Chunk_Node_LOD(chunkNB, nnb, cubeLOD));
            }
        }

        
        //Additional SDF nodes

        foreach (var s in worldData.AllSpheres)
        {
            if (chunkNB.MinPos.x < s.maxPos.x && chunkNB.MaxPos.x > s.minPos.x &&
                chunkNB.MinPos.y < s.maxPos.y && chunkNB.MaxPos.y > s.minPos.y &&
                chunkNB.MinPos.z < s.maxPos.z && chunkNB.MaxPos.z > s.minPos.z)
            {
                float3 min = s.minPos;
                float3 max = s.maxPos;
                
                int3 gMinIdx = (int3)math.floor(min / gridSize);
                int3 gMaxIdx = (int3)math.floor(max / gridSize);

                for (int i = gMinIdx.x; i <= gMaxIdx.x; i++)
                {
                    for (int j = gMinIdx.y; j <= gMaxIdx.y; j++)
                    {
                        for (int k = gMinIdx.z; k <= gMaxIdx.z; k++)
                        {
                            var nb = new NodeBound(new float3(i, j, k) * gridSize, gridSize);
                            
                            if (!addedNode.Contains(nb) && OctreeUtil.Contains(chunkNB, nb))
                            {
                                if(!NodeHasSphereIntersection(s, nb, out bool forceDivision) && !forceDivision)
                                    finalRecal.Add(new Chunk_Node_LOD(chunkNB, nb, cubeLOD));
                                else
                                {
                                    recalculateDataNodes.Add(new Chunk_Node_LOD(chunkNB, nb, cubeLOD));
                                }
                            }
                        }
                    }
                }
            }
        }
        
        
        cubeQ.Dispose();
    }
    public static IEnumerator ProcessChunks(WorldDataNative worldData, NativeList<NodeBound> chunkNodes, HashSet<global::NodeBound> allProcessedChunks, NativeHashSet<Chunk_Node_LOD> nodesToRecalculate, NativeHashMap<NodeBoundInt, CubeData> overrideValues)
    {
        NativeHashSet<NodeBound> extraChunkUpdates = new NativeHashSet<NodeBound>(1000, Allocator.Persistent);
        NativeHashSet<Chunk_Node_LOD> recalculateDataNodes = new NativeHashSet<Chunk_Node_LOD>(200000, Allocator.Persistent);
        NativeList<Chunk_Node_LOD> dataNodesToRecalculate = new NativeList<Chunk_Node_LOD>(Allocator.Persistent);
        
        NativeList<NodeBound> extraChunkNodes = new NativeList<NodeBound>(Allocator.TempJob);
        JobHandle jobHandle;
        
        var job = new ProcessChunkJob
        {
            worldData = worldData,
            chunkList = chunkNodes,
            worldToCubeGrid = worldToCubeGrid,
            cubeGridToWorld = cubeGridToWorld,
            extraChunkUpdates = extraChunkUpdates.AsParallelWriter(),
            recalculateDataNodes = recalculateDataNodes.AsParallelWriter(),
            recalculateFinalDataNodes = nodesToRecalculate.AsParallelWriter(),
            overrideValues = overrideValues.AsParallelWriter(),
        };
        
        jobHandle = job.Schedule(chunkNodes.Length, 1);
        
        while (!jobHandle.IsCompleted)
        {
            yield return null;
        }
        
        foreach(var chunkNB in chunkNodes)
            allProcessedChunks.Add(new global::NodeBound(chunkNB.MinPos, chunkNB.Size));
        
        jobHandle.Complete();
        
        //extrachunkupdates
        foreach (var n in extraChunkUpdates)
        {
            if(!allProcessedChunks.Contains(new global::NodeBound(n.MinPos, n.Size)))
                extraChunkNodes.Add(n);
        }
        extraChunkUpdates.Clear();
        

        job = new ProcessChunkJob
        {
            worldData = worldData,
            chunkList = extraChunkNodes,
            worldToCubeGrid = worldToCubeGrid,
            cubeGridToWorld = cubeGridToWorld,
            extraChunkUpdates = extraChunkUpdates.AsParallelWriter(),
            recalculateDataNodes = recalculateDataNodes.AsParallelWriter(),
            recalculateFinalDataNodes = nodesToRecalculate.AsParallelWriter(),
            overrideValues = overrideValues.AsParallelWriter(),
        };
        
        jobHandle = job.Schedule(extraChunkNodes.Length, 1);
        while (!jobHandle.IsCompleted)
            yield return null;
        
        foreach(var chunkNB in extraChunkNodes)
            allProcessedChunks.Add(new global::NodeBound(chunkNB.MinPos, chunkNB.Size));
        
        jobHandle.Complete();
        

        foreach (var r in recalculateDataNodes)
            dataNodesToRecalculate.Add(r);
        
        var job2 = new ProcessNodeJob()
        {
            worldData = worldData,
            worldToCubeGrid = worldToCubeGrid,
            nodeList = dataNodesToRecalculate,
            overrideValues = overrideValues,
            recalculateDataNodes = nodesToRecalculate.AsParallelWriter()
        };
        
        
        jobHandle = job2.Schedule(dataNodesToRecalculate.Length, 1);

        while (!jobHandle.IsCompleted)
            yield return null;
        
        jobHandle.Complete();
        
        
        extraChunkUpdates.Dispose();
        recalculateDataNodes.Dispose();
        extraChunkNodes.Dispose();
        dataNodesToRecalculate.Dispose();
    }
    public static void ProcessChunksInstant(WorldDataNative worldData, NativeList<NodeBound> chunkNodes, HashSet<global::NodeBound> allProcessedChunks, NativeHashSet<Chunk_Node_LOD> nodesToRecalculate, NativeHashMap<NodeBoundInt, CubeData> overrideValues)
    {
        NativeHashSet<NodeBound> extraChunkUpdates = new NativeHashSet<NodeBound>(1000, Allocator.Persistent);
        NativeHashSet<Chunk_Node_LOD> recalculateDataNodes = new NativeHashSet<Chunk_Node_LOD>(200000, Allocator.Persistent);
        NativeList<Chunk_Node_LOD> dataNodesToRecalculate = new NativeList<Chunk_Node_LOD>(Allocator.Persistent);
        
        NativeList<NodeBound> extraChunkNodes = new NativeList<NodeBound>(Allocator.TempJob);
        JobHandle jobHandle;
        
        var job = new ProcessChunkJob
        {
            worldData = worldData,
            chunkList = chunkNodes,
            worldToCubeGrid = worldToCubeGrid,
            cubeGridToWorld = cubeGridToWorld,
            extraChunkUpdates = extraChunkUpdates.AsParallelWriter(),
            recalculateDataNodes = recalculateDataNodes.AsParallelWriter(),
            recalculateFinalDataNodes = nodesToRecalculate.AsParallelWriter(),
            overrideValues = overrideValues.AsParallelWriter(),
        };
        
        jobHandle = job.Schedule(chunkNodes.Length, 1);
        jobHandle.Complete();

        foreach(var chunkNB in chunkNodes)
            allProcessedChunks.Add(new global::NodeBound(chunkNB.MinPos, chunkNB.Size));
        

        
        //extrachunkupdates
        foreach (var n in extraChunkUpdates)
        {
            if(!allProcessedChunks.Contains(new global::NodeBound(n.MinPos, n.Size)))
                extraChunkNodes.Add(n);
        }
        extraChunkUpdates.Clear();
        

        job = new ProcessChunkJob
        {
            worldData = worldData,
            chunkList = extraChunkNodes,
            worldToCubeGrid = worldToCubeGrid,
            cubeGridToWorld = cubeGridToWorld,
            extraChunkUpdates = extraChunkUpdates.AsParallelWriter(),
            recalculateDataNodes = recalculateDataNodes.AsParallelWriter(),
            recalculateFinalDataNodes = nodesToRecalculate.AsParallelWriter(),
            overrideValues = overrideValues.AsParallelWriter(),
        };
        
        jobHandle = job.Schedule(extraChunkNodes.Length, 1);
        jobHandle.Complete();
       
        foreach(var chunkNB in extraChunkNodes)
            allProcessedChunks.Add(new global::NodeBound(chunkNB.MinPos, chunkNB.Size));
        
        foreach (var r in recalculateDataNodes)
            dataNodesToRecalculate.Add(r);
        
        //Debug.Log(recalculateDataNodes.Count());
        var job2 = new ProcessNodeJob()
        {
            worldData = worldData,
            worldToCubeGrid = worldToCubeGrid,
            nodeList = dataNodesToRecalculate,
            overrideValues = overrideValues,
            recalculateDataNodes = nodesToRecalculate.AsParallelWriter()
        };
        
        
        jobHandle = job2.Schedule(dataNodesToRecalculate.Length, 1);
        
        jobHandle.Complete();
        
        
        extraChunkUpdates.Dispose();
        recalculateDataNodes.Dispose();
        extraChunkNodes.Dispose();
        dataNodesToRecalculate.Dispose();
    }
    
    static SurfaceData GetTerrainEdge(NodeBound node, float3 edgeMinPos, char edgeDir)
    {
        float3 surPos, surNor;
        if (edgeDir == 'y')
        {
            surPos = edgeMinPos;
            surPos.y = GetHeight(edgeMinPos.x, edgeMinPos.z);
            surNor = GetNormal(surPos.x, surPos.z, node.Size);
        }
        else if (edgeDir == 'x')
        {
            float hx0 = GetHeight(edgeMinPos.x, edgeMinPos.z);
            float hx1 = GetHeight(edgeMinPos.x + node.Size, edgeMinPos.z);

            float nh = edgeMinPos.y;

            float d0 = Mathf.Abs(hx0 - nh);
            float d1 = Mathf.Abs(hx1 - nh);
            float s01 = Mathf.Lerp(0f, node.Size, d0 / (d0 + d1));

            surPos = edgeMinPos + new float3(1f, 0f, 0f) * s01;
            surNor = GetNormal(surPos.x, surPos.z, node.Size);
        }
        else // edgeDir == 'z'
        {
            float hz0 = GetHeight(edgeMinPos.x, edgeMinPos.z);
            float hz1 = GetHeight(edgeMinPos.x, edgeMinPos.z + node.Size);

            float nh = edgeMinPos.y;

            float d0 = Mathf.Abs(hz0 - nh);
            float d1 = Mathf.Abs(hz1 - nh);
            float s01 = Mathf.Lerp(0f, node.Size, d0 / (d0 + d1));

            surPos = edgeMinPos + new float3(0f, 0f, 1f) * s01;
            surNor = GetNormal(surPos.x, surPos.z, node.Size);
        }

        var sd = new SurfaceData();
        sd.valid = true;
        sd.surPos = surPos;
        sd.surNor = surNor;
        sd.mat = 1;
        sd.dir = edgeDir;
        sd.sdfType = 0;
        return sd;
    }
    static SurfaceData GetSDFEdge(float3 edgeMinPos, char edgeDir, int edgeSign, float edgeLength, SDFInfo sdf, byte mat)
    {
        Debug.Assert(sdf.type != 0);
        sdf.GetSurfacePosNor(edgeMinPos, edgeDir, edgeSign, edgeLength, out float3 surPos, out float3 surNor);

        return new SurfaceData { surNor = surNor, surPos = surPos, valid = true, mat = mat, dir = edgeDir, sdfType = sdf.type };
    }
    static SurfaceData GetSurfaceData(NodeBound node, float3 edgeMinPos, char edgeDir, SDFInfo sdfMat1, SDFInfo sdfMat2)
    {
        bool SolidSDFSet(SDFInfo sdf) => (sdf.mat != 255 && sdf.mat != 0);
        bool AirSDFSet(SDFInfo sdf) => (sdf.mat == 0);
        
        SDFInfo mainSDF = sdfMat1.order > sdfMat2.order ? sdfMat1 : sdfMat2;
        SDFInfo subSDF = sdfMat1.order > sdfMat2.order ? sdfMat2 : sdfMat1;

        if (sdfMat1.type == 0 && sdfMat2.type == 0 && (sdfMat1.mat == 1 && sdfMat2.mat == 0 || sdfMat1.mat == 0 && sdfMat2.mat == 1))
        {
            return GetTerrainEdge(node, edgeMinPos, edgeDir); 
        }
        else if (SolidSDFSet(mainSDF) && subSDF.mat == 0)//sdf intersection
        {
            if (mainSDF.Equals(sdfMat1))
                return GetSDFEdge(edgeMinPos, edgeDir, 1, node.Size, sdfMat1, sdfMat1.mat);
            else
                return GetSDFEdge(edgeMinPos, edgeDir, -1, node.Size, sdfMat2, sdfMat2.mat);
        }
        else if (AirSDFSet(mainSDF) && subSDF.mat != 0)
        {
            if (mainSDF.Equals(sdfMat1))
                return GetSDFEdge(edgeMinPos, edgeDir, -1, node.Size, sdfMat1, sdfMat2.mat);
            else
                return GetSDFEdge(edgeMinPos, edgeDir, 1, node.Size, sdfMat2, sdfMat1.mat);
        }
        

        return SurfaceData.Invalid();
    }
    
    static void RecalculateSingleNode(WorldDataNative worldData, float4x4 worldToCubeGrid, NodeBound node, int cubeLOD, NativeHashMap<NodeBoundInt, CubeData> overrideValue, NativeList<SingleNodeData>.ParallelWriter result)
    {
        SingleNodeData r = SingleNodeData.Create(node);
        
        
        float3 p = node.MinPos;
        float3 p1 = node.MaxPos;

        bool t000, t001, t010, t011, t100, t101, t110, t111;
        
        float h00 = GetHeight(p.x, p.z);
        float h01 = GetHeight(p.x, p1.z);
        float h10 = GetHeight(p1.x, p.z);
        float h11 = GetHeight(p1.x, p1.z);

        //in terrain
        t000 = p.y < h00;
        t010 = p1.y < h00;
        t001 = p.y < h01;
        t011 = p1.y < h01;
        t100 = p.y < h10;
        t110 = p1.y < h10;
        t101 = p.y < h11;
        t111 = p1.y < h11;
        


        
        byte c000 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(0, 0, 0) * node.Size, overrideValue, cubeLOD, out uint co000);
        byte c001 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(0, 0, 1) * node.Size, overrideValue, cubeLOD, out uint co001);
        byte c010 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(0, 1, 0) * node.Size, overrideValue, cubeLOD, out uint co010);
        byte c011 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(0, 1, 1) * node.Size, overrideValue, cubeLOD, out uint co011);
        byte c100 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(1, 0, 0) * node.Size, overrideValue, cubeLOD, out uint co100);
        byte c101 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(1, 0, 1) * node.Size, overrideValue, cubeLOD, out uint co101);
        byte c110 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(1, 1, 0) * node.Size, overrideValue, cubeLOD, out uint co110);
        byte c111 = FindCubeWorldPos(worldData, worldToCubeGrid, p + new float3(1, 1, 1) * node.Size, overrideValue, cubeLOD, out uint co111);

        SphereSDF s000 = FindSphere(worldData, p + new float3(0, 0, 0) * node.Size, out uint so000);
        SphereSDF s001 = FindSphere(worldData, p + new float3(0, 0, 1) * node.Size, out uint so001);
        SphereSDF s010 = FindSphere(worldData, p + new float3(0, 1, 0) * node.Size, out uint so010);
        SphereSDF s011 = FindSphere(worldData, p + new float3(0, 1, 1) * node.Size, out uint so011);
        SphereSDF s100 = FindSphere(worldData, p + new float3(1, 0, 0) * node.Size, out uint so100);
        SphereSDF s101 = FindSphere(worldData, p + new float3(1, 0, 1) * node.Size, out uint so101);
        SphereSDF s110 = FindSphere(worldData, p + new float3(1, 1, 0) * node.Size, out uint so110);
        SphereSDF s111 = FindSphere(worldData, p + new float3(1, 1, 1) * node.Size, out uint so111);

        SDFInfo m000 = co000 > so000 ? new SDFInfo(1, c000, co000) : new SDFInfo(2, s000);
        SDFInfo m001 = co001 > so001 ? new SDFInfo(1, c001, co001) : new SDFInfo(2, s001);
        SDFInfo m010 = co010 > so010 ? new SDFInfo(1, c010, co010) : new SDFInfo(2, s010);
        SDFInfo m011 = co011 > so011 ? new SDFInfo(1, c011, co011) : new SDFInfo(2, s011);
        SDFInfo m100 = co100 > so100 ? new SDFInfo(1, c100, co100) : new SDFInfo(2, s100);
        SDFInfo m101 = co101 > so101 ? new SDFInfo(1, c101, co101) : new SDFInfo(2, s101);
        SDFInfo m110 = co110 > so110 ? new SDFInfo(1, c110, co110) : new SDFInfo(2, s110);
        SDFInfo m111 = co111 > so111 ? new SDFInfo(1, c111, co111) : new SDFInfo(2, s111);
        
        if (m000.mat == 255) m000 = SDFInfo.Terrain(t000 ? (byte)1 : (byte)0);
        if (m001.mat == 255) m001 = SDFInfo.Terrain(t001 ? (byte)1 : (byte)0);
        if (m010.mat == 255) m010 = SDFInfo.Terrain(t010 ? (byte)1 : (byte)0);
        if (m011.mat == 255) m011 = SDFInfo.Terrain(t011 ? (byte)1 : (byte)0);
        if (m100.mat == 255) m100 = SDFInfo.Terrain(t100 ? (byte)1 : (byte)0);
        if (m101.mat == 255) m101 = SDFInfo.Terrain(t101 ? (byte)1 : (byte)0);
        if (m110.mat == 255) m110 = SDFInfo.Terrain(t110 ? (byte)1 : (byte)0);
        if (m111.mat == 255) m111 = SDFInfo.Terrain(t111 ? (byte)1 : (byte)0);
        
        r.SetCornerSDF(new int3(0, 0, 0), m000);
        r.SetCornerSDF(new int3(0, 0, 1), m001);
        r.SetCornerSDF(new int3(0, 1, 0), m010);
        r.SetCornerSDF(new int3(0, 1, 1), m011);
        r.SetCornerSDF(new int3(1, 0, 0), m100);
        r.SetCornerSDF(new int3(1, 0, 1), m101);
        r.SetCornerSDF(new int3(1, 1, 0), m110);
        r.SetCornerSDF(new int3(1, 1, 1), m111);
        
        var e1 = GetSurfaceData(node, p + new float3(0, 0, 0) * node.Size, 'x', m000, m100);
        var e2 = GetSurfaceData(node, p + new float3(0, 0, 1) * node.Size, 'x', m001, m101);
        var e3 = GetSurfaceData(node, p + new float3(0, 1, 0) * node.Size, 'x', m010, m110);
        var e4 = GetSurfaceData(node, p + new float3(0, 1, 1) * node.Size, 'x', m011, m111);
        var e5 = GetSurfaceData(node, p + new float3(0, 0, 0) * node.Size, 'y', m000, m010);
        var e6 = GetSurfaceData(node, p + new float3(1, 0, 0) * node.Size, 'y', m100, m110);
        var e7 = GetSurfaceData(node, p + new float3(0, 0, 1) * node.Size, 'y', m001, m011);
        var e8 = GetSurfaceData(node, p + new float3(1, 0, 1) * node.Size, 'y', m101, m111);
        var e9 = GetSurfaceData(node, p + new float3(0, 0, 0) * node.Size, 'z', m000, m001);
        var e10 = GetSurfaceData(node, p + new float3(1, 0, 0) * node.Size, 'z', m100, m101);
        var e11 = GetSurfaceData(node, p + new float3(0, 1, 0) * node.Size, 'z', m010, m011);
        var e12 = GetSurfaceData(node, p + new float3(1, 1, 0) * node.Size, 'z', m110, m111);

        r.hasCubeData = e1.IsCube || e2.IsCube || e3.IsCube || e4.IsCube || e5.IsCube || e6.IsCube || e7.IsCube ||
                        e8.IsCube || e9.IsCube || e10.IsCube || e11.IsCube || e12.IsCube;
        r.hasSphereData = e1.IsSphere || e2.IsSphere || e3.IsSphere || e4.IsSphere || e5.IsSphere || e6.IsSphere || e7.IsSphere ||
                        e8.IsSphere || e9.IsSphere || e10.IsSphere || e11.IsSphere || e12.IsSphere;
        r.hasTerrainData = e1.IsTerrain || e2.IsTerrain || e3.IsTerrain || e4.IsTerrain || e5.IsTerrain || e6.IsTerrain || e7.IsTerrain ||
                          e8.IsTerrain || e9.IsTerrain || e10.IsTerrain || e11.IsTerrain || e12.IsTerrain;
        
        r.AddSurfaceData(e1);
        r.AddSurfaceData(e2);
        r.AddSurfaceData(e3);
        r.AddSurfaceData(e4);
        r.AddSurfaceData(e5);
        r.AddSurfaceData(e6);
        r.AddSurfaceData(e7);
        r.AddSurfaceData(e8);
        r.AddSurfaceData(e9);
        r.AddSurfaceData(e10);
        r.AddSurfaceData(e11);
        r.AddSurfaceData(e12);
        
        result.AddNoResize(r);
    }
    public static IEnumerator RecalculateSingleNodeResults(WorldDataNative worldData, NativeList<Chunk_Node_LOD> nodes, NativeHashMap<NodeBoundInt, CubeData> overrideValue, NativeList<SingleNodeData>.ParallelWriter result)
    {
        var job = new ProcessRecalculateNodeJob()
        {
            worldData = worldData,
            worldToCubeGrid = worldToCubeGrid,
            nodeList = nodes,
            overrideValues = overrideValue,
            results = result
        };

        var jobHandle = job.Schedule(nodes.Length, 1);

        while (!jobHandle.IsCompleted)
            yield return null;

        jobHandle.Complete();
        
    }
    public static void RecalculateSingleNodeResultsInstant(WorldDataNative worldData, NativeList<Chunk_Node_LOD> nodes, NativeHashMap<NodeBoundInt, CubeData> overrideValue, NativeList<SingleNodeData>.ParallelWriter result)
    {
        var job = new ProcessRecalculateNodeJob()
        {
            worldData = worldData,
            worldToCubeGrid = worldToCubeGrid,
            nodeList = nodes,
            overrideValues = overrideValue,
            results = result
        };

        var jobHandle = job.Schedule(nodes.Length, 1);
        
        jobHandle.Complete();
    }
    
    
    [BurstCompile]
    struct ProcessChunkJob : IJobParallelFor
    {
        [ReadOnly] public WorldDataNative worldData;
        [ReadOnly] public float4x4 worldToCubeGrid, cubeGridToWorld;
        [ReadOnly] public NativeArray<NodeBound> chunkList;
        
        public NativeHashSet<NodeBound>.ParallelWriter extraChunkUpdates;
        public NativeHashSet<Chunk_Node_LOD>.ParallelWriter recalculateDataNodes;
        public NativeHashSet<Chunk_Node_LOD>.ParallelWriter recalculateFinalDataNodes;
        public NativeHashMap<NodeBoundInt, CubeData>.ParallelWriter overrideValues;
        
        
        public void Execute(int i)
        {
            Work.ProcessChunk(worldData, cubeGridToWorld, worldToCubeGrid, chunkList[i], extraChunkUpdates, recalculateDataNodes, overrideValues, recalculateFinalDataNodes);
        }


        public void Run()
        {
            for (int i = 0; i < chunkList.Length; i++)
            {
                Work.ProcessChunk(worldData, cubeGridToWorld, worldToCubeGrid, chunkList[i], extraChunkUpdates,
                    recalculateDataNodes, overrideValues, recalculateFinalDataNodes);
            }
        }
    }
    
    [BurstCompile]
    struct ProcessNodeJob : IJobParallelFor
    {
        [ReadOnly] public WorldDataNative worldData;
        [ReadOnly] public float4x4 worldToCubeGrid;
        [ReadOnly] public NativeArray<Chunk_Node_LOD> nodeList;
        [ReadOnly] public NativeHashMap<NodeBoundInt, CubeData> overrideValues;        
        
        public NativeHashSet<Chunk_Node_LOD>.ParallelWriter recalculateDataNodes;

        
        
        public void Execute(int i)
        {
            Work.ProcessNode(worldData, worldToCubeGrid, nodeList[i].cnb, nodeList[i].nb, overrideValues, recalculateDataNodes);
        }

        public void Run()
        {
            for (int i = 0; i < nodeList.Count(); i++)
            {
                Work.ProcessNode(worldData, worldToCubeGrid, nodeList[i].cnb, nodeList[i].nb, overrideValues, recalculateDataNodes);
            }
        }
    }
    
    [BurstCompile]
    struct ProcessRecalculateNodeJob : IJobParallelFor
    {
        [ReadOnly] public WorldDataNative worldData;
        [ReadOnly] public float4x4 worldToCubeGrid;
        [ReadOnly] public NativeArray<Chunk_Node_LOD> nodeList;
        [ReadOnly] public NativeHashMap<NodeBoundInt, CubeData> overrideValues;

        public NativeList<SingleNodeData>.ParallelWriter results;
        
        
        public void Execute(int i)
        {
            Work.RecalculateSingleNode(worldData, worldToCubeGrid, nodeList[i].nb, nodeList[i].cubeLOD, overrideValues, results);
        }
    }
}