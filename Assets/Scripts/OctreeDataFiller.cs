using System.Collections.Generic;
using UnityEngine;

public static class OctreeDataFiller
{
    public static void FillChunkHeightMap(Octree.Node chunkNode, WorldData worldData, HashSet<NodeBound> allActualTerrainNodes = null)
    {
        float minNodeSize = chunkNode.size / OctreeParam.ChunkSize * OctreeParam.TerrainResMul;     
        int gridCount = OctreeParam.ChunkSize / OctreeParam.TerrainResMul;         
        Vector3 minPos = chunkNode.minPos;
        //iterate x, z, while calculating the exact y 
        
        for (int i = 0; i <= gridCount; i++)
        {
            for (int k = 0; k <= gridCount; k++)
            {
                bool xMaxEdge = i == gridCount;
                bool zMaxEdge = k == gridCount;

                float height = worldData.GetHeight(minPos.x + minNodeSize * i, minPos.z + minNodeSize * k);
                float heightX = !xMaxEdge ? worldData.GetHeight(minPos.x + minNodeSize * (i + 1), minPos.z + minNodeSize * k) : 0;
                float heightZ = !zMaxEdge ? worldData.GetHeight(minPos.x + minNodeSize * i, minPos.z + minNodeSize * (k + 1)) : 0;
                
                float height0 = height - minPos.y;
                float heightX0 = !xMaxEdge ? heightX - minPos.y : 0;
                float heightZ0 = !zMaxEdge ? heightZ - minPos.y : 0;

                int j = Mathf.CeilToInt(height0 / minNodeSize) - 1;
                int jx = !xMaxEdge ? Mathf.CeilToInt(heightX0 / minNodeSize) - 1 : 0;
                int jz = !zMaxEdge ? Mathf.CeilToInt(heightZ0 / minNodeSize) - 1 : 0;

                Vector3 nodeMinPos = minPos + new Vector3(i, j, k) * minNodeSize;
                
                if (height0 >= 0 && height0 < chunkNode.size)
                {
                    Vector3 surPos = new Vector3(nodeMinPos.x, height, nodeMinPos.z);
                    
                    FillEdgeDataHeightmap(chunkNode, worldData, nodeMinPos, 3, minNodeSize, 
                        surPos, worldData.GetNormal(surPos.x, surPos.z, minNodeSize), allActualTerrainNodes);
                }

                if (!xMaxEdge && j != jx)
                {
                    int xMinIdx = j > jx ? jx + 1 : j + 1;
                    int xMaxIdx = jx > j ? jx : j;
                    var edgeDir = j > jx ? 1 : 2;
                    for (int a = Mathf.Max(0, xMinIdx); a <= Mathf.Min(gridCount, xMaxIdx); a++)
                    {
                        float nodeHeight = minPos.y + a * minNodeSize;
                        float d1 = Mathf.Abs(nodeHeight - height);
                        float d2 = Mathf.Abs(nodeHeight - heightX);
                        float disToSurface = Mathf.Lerp(0, minNodeSize, d1 / (d1 + d2));
                        Vector3 emp = new Vector3(nodeMinPos.x, minPos.y + a * minNodeSize, nodeMinPos.z);
                        Vector3 surPos = emp + Vector3.right * disToSurface;
                        
                        FillEdgeDataHeightmap(chunkNode, worldData, emp, edgeDir, minNodeSize, 
                            surPos, worldData.GetNormal(surPos.x, surPos.z, minNodeSize), allActualTerrainNodes);
                    }
                }
                
                if (!zMaxEdge && j != jz)
                {
                    int zMinIdx = j > jz ? jz + 1 : j + 1;
                    int zMaxIdx = jz > j ? jz : j;
                    var edgeDir = j > jz ? 5 : 6;

                    for (int a = Mathf.Max(0, zMinIdx); a <= Mathf.Min(gridCount, zMaxIdx); a++)
                    {
                        float nodeHeight = minPos.y + a * minNodeSize;
                        float d1 = Mathf.Abs(nodeHeight - height);
                        float d2 = Mathf.Abs(nodeHeight - heightZ);
                        float disToSurface = Mathf.Lerp(0, minNodeSize, d1 / (d1 + d2));
                        Vector3 emp = new Vector3(nodeMinPos.x, minPos.y + a * minNodeSize, nodeMinPos.z);
                        Vector3 surPos = emp + Vector3.forward * disToSurface;
                        
                        FillEdgeDataHeightmap(chunkNode, worldData, emp, edgeDir, minNodeSize, 
                            surPos, worldData.GetNormal(surPos.x, surPos.z, minNodeSize), allActualTerrainNodes);
                    }
                }
            }
        }
        
    }

    //Looking from Above, Right, Back
    //Order : LT, RT, LB, RB
    private static Vector3[] xOffsets = new Vector3[] { Vector3.back, Vector3.zero, new Vector3(0, -1, -1), Vector3.down };
    private static Vector3[] zOffsets = new Vector3[] { Vector3.left, Vector3.zero, new Vector3(-1, -1, 0), Vector3.down };
    private static Vector3[] yOffsets = new Vector3[] { Vector3.left, Vector3.zero, new Vector3(-1, 0, -1), Vector3.back };

    static void GetEdgePosInNode(int offsetIndex, int edgeDir, out Vector3Int air, out Vector3Int nonAir)
    {
        if (edgeDir <= 2)//x
        {
            if (offsetIndex == 0)
            {
                air = new Vector3Int(1, 0, 1);
                nonAir = new Vector3Int(0, 0, 1);
            }
            else if (offsetIndex == 1)
            {
                air = new Vector3Int(1, 0, 0);
                nonAir = new Vector3Int(0, 0, 0);
            }
            else if (offsetIndex == 2)
            {
                air = new Vector3Int(1, 1, 1);
                nonAir = new Vector3Int(0, 1, 1);
            }
            else
            {
                Debug.Assert(offsetIndex == 3);
                air = new Vector3Int(1, 1, 0);
                nonAir = new Vector3Int(0, 1, 0);
            }
        }
        else if (edgeDir <= 4)//y
        {
            if (offsetIndex == 0)
            {
                air = new Vector3Int(1, 1, 0);
                nonAir = new Vector3Int(1, 0, 0);
            }
            else if (offsetIndex == 1)
            {
                air = new Vector3Int(0, 1, 0);
                nonAir = new Vector3Int(0, 0, 0);
            }
            else if (offsetIndex == 2)
            {
                air = new Vector3Int(1, 1, 1);
                nonAir = new Vector3Int(1, 0, 1);
            }
            else
            {
                Debug.Assert(offsetIndex == 3);
                air = new Vector3Int(0, 1, 1);
                nonAir = new Vector3Int(0, 0, 1);
            }
        }
        else
        {
            Debug.Assert(edgeDir <= 6);
            if (offsetIndex == 0)
            {
                air = new Vector3Int(1, 0, 1);
                nonAir = new Vector3Int(1, 0, 0);
            }
            else if (offsetIndex == 1)
            {
                air = new Vector3Int(0, 0, 1);
                nonAir = new Vector3Int(0, 0, 0);
            }
            else if (offsetIndex == 2)
            {
                air = new Vector3Int(1, 1, 1);
                nonAir = new Vector3Int(1, 1, 0);
            }
            else
            {
                Debug.Assert(offsetIndex == 3);
                air = new Vector3Int(0, 1, 1);
                nonAir = new Vector3Int(0, 1, 0);
            }
        }

        if (edgeDir % 2 == 0)//surface normal : minus
        {
            (air, nonAir) = (nonAir, air);
        }
    }
    
    //EdgeDir : 1 : x+, 2 : x-, 3 : y+, 4 : y-, 5 : z+, 6 : z- (surface normal direction)
    static void FillEdgeDataHeightmap(Octree.Node chunkNode, WorldData worldData, Vector3 edgeMinPos, int edgeDir, float edgeLength, Vector3 surfacePos, Vector3 surfaceNor, HashSet<NodeBound> allActualTerrainNodes)
    {
        int lod = chunkNode.chunkLOD;
        Vector3[] offsets;
        if (edgeDir <= 2) offsets = xOffsets;
        else if (edgeDir <= 4) offsets = yOffsets;
        else offsets = zOffsets;

        Vector3 p1 = edgeMinPos;
        Vector3 p2;
        if (edgeDir <= 2) p2 = edgeMinPos + Vector3.right * edgeLength;
        else if (edgeDir <= 4) p2 = edgeMinPos + Vector3.up * edgeLength;
        else p2 = edgeMinPos + Vector3.forward * edgeLength;
        
        var c1 = CubeUtil.FindCube(worldData, p1, lod);
        var c2 = CubeUtil.FindCube(worldData, p2, lod);
        
        for(int i = 0; i < 4; i++)
        {
            var v = offsets[i] * edgeLength;
            var pos = edgeMinPos + v;
            var size = edgeLength;
            var nb = new NodeBound(pos, size);
            GetEdgePosInNode(i, edgeDir, out Vector3Int air, out Vector3Int nonAir);
            
            allActualTerrainNodes?.Add(nb);
            
            var node = chunkNode.GetNode(new NodeBound(pos, size));
            if (node == null)//Node from neighbor chunk
                continue;

            if (c1 != 255 || c2 != 255)
                continue;
            
            Debug.Assert(node.IsLeaf);
            node.LeafNodeData.AddEdgeData(new EdgeData(surfacePos, surfaceNor, OctreeUtil.SignedDirToChar(edgeDir), 0), 1);
            node.LeafNodeData.SetCornerData(air, SDFInfo.Terrain(0));
            node.LeafNodeData.SetCornerData(nonAir, SDFInfo.Terrain(1));
        }
        
    }
    
}


