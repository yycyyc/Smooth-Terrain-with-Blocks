using System;
using UnityEngine;

public static class OctreeParam
{
    public const int seed = 101;
    public const float NodeMinSize = 0.25f;
    
    public static readonly int[] LODDis = new int[]{ 4, 4, 4, 3, 3, 4 };
    public static int MaxLOD => LODDis.Length - 1;
    public static int OctreeSize => ChunkSize * (int)Mathf.Pow(2, MaxLOD);
    public const int ChunkSize = 16;
    public const int TerrainResMul = 2;
}

public static class CubeParam
{
    public const int CubeSize = 1;
    public static Matrix4x4 WorldToCubeGrid{
        get
        {
            if (initialized) return worldToCubeGrid;
            else throw new Exception();
        }
    }
    public static Matrix4x4 CubeGridToWorld
    {
        get
        {
            if (initialized) return cubeGridToWorld;
            else throw new Exception();
        }
    }
    static Matrix4x4 worldToCubeGrid;
    static Matrix4x4 cubeGridToWorld;
    public static int cubeOctreeSize;
    
    static bool initialized = false;
    public static void Initialize()
    {
        worldToCubeGrid = Matrix4x4.Scale(Vector3.one / CubeSize) * Matrix4x4.Translate(-Vector3.one * 0.125f);
        cubeGridToWorld = worldToCubeGrid.inverse;
        cubeOctreeSize = OctreeParam.OctreeSize / CubeSize;
        initialized = true;
    }
}