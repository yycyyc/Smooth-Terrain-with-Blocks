using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

public static class OctreeUtil
{
    public static char SignedDirToChar(int dir)
    {
        if (dir <= 2) return 'x';
        else if (dir <= 4) return 'y';
        else return 'z';
    }
    public static NodeID GetNodeIDBySize(Vector3 pos, float size)
    {
        int LOD = Mathf.RoundToInt(Mathf.Log(size / OctreeParam.ChunkSize, 2));
        return GetNodeID(pos, LOD);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int FloorToMultiple(int val, int size)
    {
        return val >= 0 ? (val / size) * size : ((val - size + 1) / size) * size;
    }
    
    public static NodeID GetNodeID(Vector3 pos, int LOD)
    {
        Debug.Assert(LOD <= OctreeParam.MaxLOD);
        float size = Mathf.RoundToInt(Mathf.Pow(2, LOD)) * OctreeParam.ChunkSize;

        pos += Vector3.one * 0.001f;
        
        float x = Mathf.Floor(pos.x / size);
        float y = Mathf.Floor(pos.y / size);
        float z = Mathf.Floor(pos.z / size);
        float w = LOD;
        
        return new NodeID(x, y, z, w);
    }

    public static Work.NodeBound GetChunkNodeBound(int3 worldPos, int LOD)
    {
        int size = (1 << LOD) * OctreeParam.ChunkSize;

        float3 minpos = new float3()
        {
            x = FloorToMultiple(worldPos.x, size),
            y = FloorToMultiple(worldPos.y, size),
            z = FloorToMultiple(worldPos.z, size),
        };
        return new Work.NodeBound(minpos, size);
    }
    

    public static bool ContainsPoint(Vector4 a, Vector3 point)
    {
        return a.x - 0.001f < point.x && point.x < a.x + a.w + 0.001f &&
               a.y - 0.001f < point.y && point.y < a.y + a.w + 0.001f &&
               a.z - 0.001f < point.z && point.z < a.z + a.w + 0.001f;
    }
    public static bool Contains(Vector4 a, Vector4 b)//(minpos, size)
    {
        if (b.w > a.w + 0.001f) return false;
        if (a.Equals(b)) return true;
        
        float asize = a.w;
        float ax0 = a.x;
        float ay0 = a.y;
        float az0 = a.z;
        
        float bsize = b.w;
        float bx0 = b.x;
        float by0 = b.y;
        float bz0 = b.z;

        return ax0 < bx0 + 0.001f && ax0 + asize > bx0 + bsize - 0.001f
                          && ay0 < by0 + 0.001f && ay0 + asize > by0 + bsize - 0.001f
                          && az0 < bz0 + 0.001f && az0 + asize > bz0 + bsize - 0.001f;
    }
    public static bool Contains(Work.NodeBound a, Work.NodeBound b)//(minpos, size)
    {
        if (b.w > a.w + 0.001f) return false;
        if (a.Equals(b)) return true;
        
        float asize = a.w;
        float ax0 = a.x;
        float ay0 = a.y;
        float az0 = a.z;
        
        float bsize = b.w;
        float bx0 = b.x;
        float by0 = b.y;
        float bz0 = b.z;

        return ax0 < bx0 + 0.001f && ax0 + asize > bx0 + bsize - 0.001f
                                  && ay0 < by0 + 0.001f && ay0 + asize > by0 + bsize - 0.001f
                                  && az0 < bz0 + 0.001f && az0 + asize > bz0 + bsize - 0.001f;
    }
    
}

public static class CubeUtil
{
    static Matrix4x4 WorldToCubeGrid => CubeParam.WorldToCubeGrid;
    static Matrix4x4 CubeGridToWorld => CubeParam.CubeGridToWorld;
    private static int CubeOctreeSize => CubeParam.cubeOctreeSize;
    
    //cube origin : 0.125f, 0.125f, 0.125f
    public static Vector3 GetFixedCubeVertex(Vector3 nodeMinWorldPos)
    {
        return nodeMinWorldPos + Vector3.one * 0.125f;
    }

    public static float GetCubeGridPointCeiled(float worldP)
    {
        var cp = (worldP - 0.125f) / CubeParam.CubeSize;
        int icp = (int)math.ceil(cp);
        return (icp * CubeParam.CubeSize) + 0.125f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3Int GetCubePos(Vector3 worldPos)
    {
        Vector3 c = WorldToCubeGrid.MultiplyPoint3x4(worldPos);
        return new Vector3Int(
            Mathf.FloorToInt(c.x),
            Mathf.FloorToInt(c.y),
            Mathf.FloorToInt(c.z)
        );
    }
    
    public static Vector3 GetWorldPos(Vector3Int cubePos)
    {
        return CubeGridToWorld.MultiplyPoint3x4(cubePos);
    }
    static Vector3Int GetCubeOctreeID(Vector3Int cubeGridPos)
    {
        return new Vector3Int(Mathf.FloorToInt(cubeGridPos.x / (float)CubeOctreeSize), Mathf.FloorToInt(cubeGridPos.y / (float)CubeOctreeSize),
            Mathf.FloorToInt(cubeGridPos.z / (float)CubeOctreeSize));
    }
    
    static CubeOctree GetOrCreateCubeOctree(WorldData worldData, Vector3Int id, byte mat)
    {
        return worldData.GetOrCreateCubeOctree(id, mat);
    }
    static CubeOctree FindCubeOctree(WorldData worldData, Vector3Int id)
    {
        return worldData.FindCubeOctree(id);
    }
    
    public static void AddCube(WorldData worldData, Vector3 posInWorld, byte mat)
    {
        Vector3Int cubeGridPos = GetCubePos(posInWorld);
        Vector3Int oid = GetCubeOctreeID(cubeGridPos);
        CubeOctree cubeOc = GetOrCreateCubeOctree(worldData, oid, mat);
        
        cubeOc.SetData(worldData, cubeGridPos, mat, WorldData.PlaceOrder++);
    }
    public static byte FindCube(WorldData worldData, Vector3 worldPos, int lod)
    {
        return FindCubeGridPos(worldData, GetCubePos(worldPos), lod);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int FloorToMultiple(int val, int size)
    {
        return val >= 0 ? (val / size) * size : ((val - size + 1) / size) * size;
    }
    static byte FindCubeGridPos(WorldData worldData, Vector3Int cubeGridPos, int lod)
    {
        int size = 1 << lod;

        var floored = new Vector3Int(
            FloorToMultiple(cubeGridPos.x, size),
            FloorToMultiple(cubeGridPos.y, size),
            FloorToMultiple(cubeGridPos.z, size)
        );
        var key = new Vector4Int(floored.x, floored.y, floored.z, size);


        if (worldData.AllCubeNodes.TryGetValue(key, out CubeOctree.CubeNode n))
        {
            return n.value;
        }
        
        return 255;
    }
    static int FindOverlappingMaxChunk(WorldData worldData, CubeOctree.CubeNode cubeNode, HashSet<Octree.Node> chunks, out bool lodDiff)
    {
        Vector3Int basePos = cubeNode.minPos;
        int size = cubeNode.size;
        int maxLOD = -1;
        int referenceLOD = -1;
        lodDiff = false;

        // 8 corner offsets for a cube
        ReadOnlySpan<Vector3Int> offsets = stackalloc Vector3Int[]
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 1, 1),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 0, 1),
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, 1, 1)
        };
        
        foreach (var offset in offsets)
        {
            Vector3Int worldPos = basePos + offset * size;
            Octree.Node node = worldData.FindChunkNode(GetWorldPos(worldPos));
            
            if (node != null)
            {
                chunks?.Add(node);

                int lod = node.chunkLOD;
                if (lod > maxLOD) maxLOD = lod;

                if (referenceLOD == -1)
                    referenceLOD = lod;
                else if (referenceLOD != lod)
                    lodDiff = true;
            }
        }

        return maxLOD;
    }
    
    public static void FindNeighborSmallLODChunkWithCubeInBetween(WorldData worldData, NodeBound nb, float gridSize, int cubeLOD, HashSet<Octree.Node> foundChunks)
    {
        var cubeQ = OctreePool.GetCubeNodeQueue();
        cubeQ.Clear();
        
        Vector3 worldPosMin = nb.MinPos;
        Vector3 worldPosMax = nb.MaxPos;
        int cubeSize = Mathf.RoundToInt(Mathf.Pow(2, cubeLOD));
        
        //grid index of original world of node, grid size : gridsize
        int minX = Mathf.RoundToInt(worldPosMin.x / gridSize);
        int maxX = Mathf.RoundToInt(worldPosMax.x / gridSize);
        int minZ = Mathf.RoundToInt(worldPosMin.z / gridSize);
        int maxZ = Mathf.RoundToInt(worldPosMax.z / gridSize);
        int minY = Mathf.RoundToInt(worldPosMin.y / gridSize);
        int maxY = Mathf.RoundToInt(worldPosMax.y / gridSize);
        
        Vector3 gridMinWPos = new Vector3(minX, minY, minZ) * gridSize;
        Vector3 gridMaxWPos = new Vector3(maxX, maxY, maxZ) * gridSize;

        Vector3Int cubeGridMinPos = GetCubePos(gridMinWPos);
        Vector3Int cubeGridMaxPos = GetCubePos(gridMaxWPos);

        Vector3Int octreeIDMin = GetCubeOctreeID(cubeGridMinPos);
        Vector3Int octreeIDMax = GetCubeOctreeID(cubeGridMaxPos);

        HashSet<Octree.Node> found = HashSetPool<Octree.Node>.Get();
        found.Clear();
        
        for (int i = octreeIDMin.x; i <= octreeIDMax.x; i++)
        {
            for (int j = octreeIDMin.y; j <= octreeIDMax.y; j++)
            {
                for (int k = octreeIDMin.z; k <= octreeIDMax.z; k++)
                {
                    var o = FindCubeOctree(worldData, new Vector3Int(i, j, k));
                    if (o == null) continue;

                    cubeQ.Clear();
                    cubeQ.Enqueue(o.root);
                    
                    while (cubeQ.Count > 0)
                    {
                        var n = cubeQ.Dequeue();
                        Vector3Int minP = n.minPos;
                        Vector3Int maxP = n.maxPos;

                        //cubeGridMin, MaxPos : 체크해야 할 큐브 범위, min, maxP : 현재 큐브의 범위
                        if (cubeGridMinPos.x > maxP.x || cubeGridMinPos.y > maxP.y || cubeGridMinPos.z > maxP.z ||
                            cubeGridMaxPos.x < minP.x || cubeGridMaxPos.y < minP.y || cubeGridMaxPos.z < minP.z)
                            continue;

                        if (n.size == cubeSize && n.value != 255)
                        {
                            var cmin = worldData.FindChunkNode(GetWorldPos(n.minPos));
                            var cmax = worldData.FindChunkNode(GetWorldPos(n.maxPos));

                            //경계선에 존재한다면
                            if (cmin != cmax)
                            {
                                int cmaxLod = FindOverlappingMaxChunk(worldData, n, found, out bool lodDiff);
                                
                                if (cubeLOD > cmaxLod)
                                {
                                    foreach (var f in found)
                                        foundChunks.Add(f);
                                }
                            }
                        }
                        else if(!n.IsLeaf)
                        {
                            for (int l = 0; l < 8; l++)
                            {
                                var nn = n.child[l];
                                cubeQ.Enqueue(nn);
                            }
                        }
                    }
                }
            }
        }

        found.Clear();
        HashSetPool<Octree.Node>.Release(found);
        cubeQ.Clear();
        OctreePool.ReturnCubeNodeQueue(cubeQ);
    }
}