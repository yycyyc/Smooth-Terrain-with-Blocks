using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;


public static class OctreeEdgeIterator
{
    struct CellProcData
    {
        public CellProcData(Octree.Node n1)
        {
            this.n1 = n1;
        }
        public Octree.Node n1;
    }

    struct FaceProcData
    {
        //n1 is at Left/Below/Back of n2   
        public FaceProcData(Octree.Node n1, Octree.Node n2, char dir)
        {
            this.n1 = n1;
            this.n2 = n2;
            this.dir = dir;
        }
        public Octree.Node n1, n2;

        public Octree.Node GetNode(int idx)
        {
            return idx == 0 ? n1 : n2;
        }
        
        public char dir;
    }

    struct EdgeProcData
    {
        //When looking from Right/Above/Back
        //n1 : LT, n2 : RT, n3 : LB, n4 : RB
        public EdgeProcData(Octree.Node n1, Octree.Node n2, Octree.Node n3, Octree.Node n4, char dir)
        {
            this.n1 = n1;
            this.n2 = n2;
            this.n3 = n3;
            this.n4 = n4;
            this.dir = dir;
        }
        
        public Octree.Node n1, n2, n3, n4;
        public char dir;
        
        public int GetEdgeSign(out bool isCubeEdge, out bool isSphereEdge, out SDFInfo c1, out SDFInfo c2)
        {
            //Need to use minimum node
            if (n4.size < n1.size + 0.001f && n4.size < n2.size + 0.001f && n4.size < n3.size + 0.001f)
            {
                if (dir == 'x')
                {
                    Vector3Int cornerIndexLeft = new Vector3Int(0, 1, 0);
                    Vector3Int cornerIndexRight = new Vector3Int(1, 1, 0);
                    n4.LeafNodeData.GetCornerData(cornerIndexLeft, out c1);
                    n4.LeafNodeData.GetCornerData(cornerIndexRight, out c2);
                }
                else if (dir == 'y')
                {
                    Vector3Int cornerIndexDown = new Vector3Int(0, 0, 1);
                    Vector3Int cornerIndexUp = new Vector3Int(0, 1, 1);
                    
                    n4.LeafNodeData.GetCornerData(cornerIndexDown, out c1);
                    n4.LeafNodeData.GetCornerData(cornerIndexUp, out c2);
                }
                else// if (dir == 'z')
                {
                    Vector3Int cornerIndexBack = new Vector3Int(0, 1, 0);
                    Vector3Int cornerIndexFront = new Vector3Int(0, 1, 1);
                    n4.LeafNodeData.GetCornerData(cornerIndexBack, out c1);
                    n4.LeafNodeData.GetCornerData(cornerIndexFront, out c2);
                }
            }
            else if(n1.size < n2.size + 0.001f && n1.size < n3.size + 0.001f && n1.size < n4.size + 0.001f)
            {
                if (dir == 'x')
                {
                    Vector3Int cornerIndexLeft = new Vector3Int(0, 0, 1);
                    Vector3Int cornerIndexRight = new Vector3Int(1, 0, 1);
                    n1.LeafNodeData.GetCornerData(cornerIndexLeft, out c1);
                    n1.LeafNodeData.GetCornerData(cornerIndexRight, out c2);
                }
                else if (dir == 'y')
                {
                    Vector3Int cornerIndexDown = new Vector3Int(1, 0, 0);
                    Vector3Int cornerIndexUp = new Vector3Int(1, 1, 0);
                    n1.LeafNodeData.GetCornerData(cornerIndexDown, out c1);
                    n1.LeafNodeData.GetCornerData(cornerIndexUp, out c2);
                    
                }
                else// if (dir == 'z')
                {
                    Vector3Int cornerIndexBack = new Vector3Int(1, 0, 0);
                    Vector3Int cornerIndexFront = new Vector3Int(1, 0, 1);
                    n1.LeafNodeData.GetCornerData(cornerIndexBack, out c1);
                    n1.LeafNodeData.GetCornerData(cornerIndexFront, out c2);
                }
            }
            else if(n2.size < n1.size + 0.001f && n2.size < n3.size + 0.001f && n2.size < n4.size + 0.001f)
            {
                if (dir == 'x')
                {
                    Vector3Int cornerIndexLeft = new Vector3Int(0, 0, 0);
                    Vector3Int cornerIndexRight = new Vector3Int(1, 0, 0);
                    n2.LeafNodeData.GetCornerData(cornerIndexLeft, out c1);
                    n2.LeafNodeData.GetCornerData(cornerIndexRight, out c2);
                }
                else if (dir == 'y')
                {
                    Vector3Int cornerIndexDown = new Vector3Int(0, 0, 0);
                    Vector3Int cornerIndexUp = new Vector3Int(0, 1, 0);
                    n2.LeafNodeData.GetCornerData(cornerIndexDown, out c1);
                    n2.LeafNodeData.GetCornerData(cornerIndexUp, out c2);
                    
                }
                else// if (dir == 'z')
                {
                    Vector3Int cornerIndexBack = new Vector3Int(0, 0, 0);
                    Vector3Int cornerIndexFront = new Vector3Int(0, 0, 1);
                    n2.LeafNodeData.GetCornerData(cornerIndexBack, out c1);
                    n2.LeafNodeData.GetCornerData(cornerIndexFront, out c2);
                }
            }
            else// if(n3.size < n1.size + 0.001f && n3.size < n2.size + 0.001f && n3.size < n4.size + 0.001f)
            {
                if (dir == 'x')
                {
                    Vector3Int cornerIndexLeft = new Vector3Int(0, 1, 1);
                    Vector3Int cornerIndexRight = new Vector3Int(1, 1, 1);
                    n3.LeafNodeData.GetCornerData(cornerIndexLeft, out c1);
                    n3.LeafNodeData.GetCornerData(cornerIndexRight, out c2);
                }
                else if (dir == 'y')
                {
                    Vector3Int cornerIndexDown = new Vector3Int(1, 0, 1);
                    Vector3Int cornerIndexUp = new Vector3Int(1, 1, 1);
                    n3.LeafNodeData.GetCornerData(cornerIndexDown, out c1);
                    n3.LeafNodeData.GetCornerData(cornerIndexUp, out c2);
                    
                }
                else// if (dir == 'z')
                {
                    Vector3Int cornerIndexBack = new Vector3Int(1, 1, 0);
                    Vector3Int cornerIndexFront = new Vector3Int(1, 1, 1);
                    n3.LeafNodeData.GetCornerData(cornerIndexBack, out c1);
                    n3.LeafNodeData.GetCornerData(cornerIndexFront, out c2);
                }
            }

            var si = c1.order > c2.order ? c1 : c2;

            isCubeEdge = si.type == 1;
            isSphereEdge = si.type == 2;
            

            if (c1.mat == 255 || c2.mat == 255 || (c1.mat != 0 && c2.mat != 0))
            {
                return 0;//No surface
            }

            if (c1.mat == 0 && c2.mat != 0)
            {
                return -1;
            }

            if (c1.mat != 0 && c2.mat == 0)
            {
                return 1;
            }

            
            return 0;
        }
        
        public Octree.Node GetNode(int idx)
        {
            if (idx == 0) return n1;
            else if (idx == 1) return n2;
            else if (idx == 2) return n3;
            else if (idx == 3) return n4;
            else return null;
        }
    }
    
    
    private static readonly int[] cellCellProcChildIndex =
    {
        0, 1, 2, 3, 4, 5, 6, 7
    };
    
    //- +
    private static readonly int2[] cellFaceProcChildIndex =
    {
        new int2(0, 1), new int2(2, 3), new int2(4, 5), new int2(6, 7),
        new int2(0, 2), new int2(4, 6), new int2(1, 3), new int2(5, 7),
        new int2(0, 4), new int2(1, 5), new int2(2, 6), new int2(3, 7)
    };

    private static readonly char[] cellFaceProcDir =
    {
        'z', 'z', 'z', 'z', 'y', 'y', 'y', 'y', 'x', 'x', 'x', 'x'
    };
    
    //x : zy plane, y : xz plane, z : xy plane
    //nodes are passed by (LT, RT, LB, RB) order , viewing from above/right/BACK (matching unity view)
    private static readonly int4[] cellEdgeProcChildIndex =
    {
        new int4(2, 6, 0, 4), new int4(3, 7, 1, 5),
        new int4(2, 3, 0, 1), new int4(6, 7, 4, 5),
        new int4(3, 7, 2, 6), new int4(1, 5, 0, 4)
    };

    private static readonly char[] cellEdgeProcDir =
    {
        'z', 'z', 'x', 'x', 'y', 'y'
    };
    
    //(child index, FaceProcData node index)
    private static readonly int2x2[] xFaceFaceProcChildIndex =
    {
        new int2x2(new int2(6, 0), new int2(2, 1)), new int2x2(new int2(7, 0), new int2(3, 1)), 
        new int2x2(new int2(4, 0), new int2(0, 1)), new int2x2(new int2(5, 0), new int2(1, 1))
    };
    private static readonly int2x2[] yFaceFaceProcChildIndex =
    {
        new int2x2(new int2(2, 0), new int2(0, 1)), new int2x2(new int2(6, 0), new int2(4, 1)), 
        new int2x2(new int2(3, 0), new int2(1, 1)), new int2x2(new int2(7, 0), new int2(5, 1))
    };

    private static readonly int2x2[] zFaceFaceProcChildIndex =
    {
        new int2x2(new int2(3, 0), new int2(2, 1)), new int2x2(new int2(7, 0), new int2(6, 1)), 
        new int2x2(new int2(1, 0), new int2(0, 1)), new int2x2(new int2(5, 0), new int2(4, 1))
    };
    
    private static readonly int2x4[] xFaceEdgeProcChildIndex =
    {
        new int2x4(new int2(7, 0), new int2(3, 1), new int2(6, 0), new int2(2, 1)), 
        new int2x4(new int2(6, 0), new int2(2, 1), new int2(4, 0), new int2(0, 1)),
        new int2x4(new int2(5, 0), new int2(1, 1), new int2(4, 0), new int2(0, 1)), 
        new int2x4(new int2(7, 0), new int2(3, 1), new int2(5, 0), new int2(1, 1))
    };

    private static readonly char[] xFaceEdgeProcDir =
    {
        'y', 'z', 'y', 'z'
    };

    private static readonly int2x4[] yFaceEdgeProcChildIndex =
    {
        new int2x4(new int2(1, 1), new int2(5, 1), new int2(3, 0), new int2(7, 0)), 
        new int2x4(new int2(0, 1), new int2(1, 1), new int2(2, 0), new int2(3, 0)),
        new int2x4(new int2(0, 1), new int2(4, 1), new int2(2, 0), new int2(6, 0)), 
        new int2x4(new int2(4, 1), new int2(5, 1), new int2(6, 0), new int2(7, 0))
    };

    private static readonly char[] yFaceEdgeProcDir =
    {
        'z', 'x', 'z', 'x'
    };
    
    private static readonly int2x4[] zFaceEdgeProcChildIndex =
    {
        new int2x4(new int2(2, 1), new int2(6, 1), new int2(3, 0), new int2(7, 0)), 
        new int2x4(new int2(3, 0), new int2(2, 1), new int2(1, 0), new int2(0, 1)),
        new int2x4(new int2(0, 1), new int2(4, 1), new int2(1, 0), new int2(5, 0)), 
        new int2x4(new int2(7, 0), new int2(6, 1), new int2(5, 0), new int2(4, 1))
    };

    private static readonly char[] zFaceEdgeProcDir =
    {
        'y', 'x', 'y', 'x'
    };

    
    //(child index, EdgeProcData node index)
    private static readonly int2x4[] xEdgeEdgeProcChildIndex =
    {
        new int2x4(new int2(1, 0), new int2(0, 1), new int2(3, 2), new int2(2, 3)), 
        new int2x4(new int2(5, 0), new int2(4, 1), new int2(7, 2), new int2(6, 3))
    };
    private static readonly int2x4[] yEdgeEdgeProcChildIndex =
    {
        new int2x4(new int2(6, 0), new int2(2, 1), new int2(7, 2), new int2(3, 3)), 
        new int2x4(new int2(4, 0), new int2(0, 1), new int2(5, 2), new int2(1, 3))
    };
    private static readonly int2x4[] zEdgeEdgeProcChildIndex =
    {
        new int2x4(new int2(4, 0), new int2(0, 1), new int2(6, 2), new int2(2, 3)), 
        new int2x4(new int2(5, 0), new int2(1, 1), new int2(7, 2), new int2(3, 3))
    };


    static void CellProc(WorldData worldData, ChunkMeshData.MeshData meshData, CellProcData d)
    {
        if (!d.n1.IsLeaf)
        {
            foreach (var i in cellCellProcChildIndex)
            {
                CellProc(worldData, meshData, new CellProcData(d.n1.FindChild(i)));
            }
            
            for(int i = 0; i < 12; i++)
            {
                var idx = cellFaceProcChildIndex[i];
                var dir = cellFaceProcDir[i];
                FaceProc(worldData, meshData, new FaceProcData(d.n1.FindChild(idx.x), d.n1.FindChild(idx.y), dir));
            }

            for (int i = 0; i < 6; i++)
            {
                var idx = cellEdgeProcChildIndex[i];
                var dir = cellEdgeProcDir[i];
                EdgeProc(worldData, meshData, new EdgeProcData(d.n1.FindChild(idx.x), d.n1.FindChild(idx.y), d.n1.FindChild(idx.z), d.n1.FindChild(idx.w), dir));
            }
        }
    }
    
    static void FaceProc(WorldData worldData, ChunkMeshData.MeshData meshData, FaceProcData d)
    {
        if (!d.n1.IsLeaf || !d.n2.IsLeaf)
        {
            int2x2[] ff = null;
            int2x4[] fe = null;
            char[] fed = null;
            if (d.dir == 'x')
            {
                ff = xFaceFaceProcChildIndex;
                fe = xFaceEdgeProcChildIndex;
                fed = xFaceEdgeProcDir;
            }
            else if (d.dir == 'y')
            {
                ff = yFaceFaceProcChildIndex;
                fe = yFaceEdgeProcChildIndex;
                fed = yFaceEdgeProcDir;
            }
            else if (d.dir == 'z')
            {
                ff = zFaceFaceProcChildIndex;
                fe = zFaceEdgeProcChildIndex;
                fed = zFaceEdgeProcDir;
            }
            else Debug.Assert(false);
            
            foreach (var i in ff)
            {
                var n1 = i.c0;
                var n2 = i.c1;
                var dir = d.dir;
                FaceProc(worldData, meshData,
                    new FaceProcData(d.GetNode(n1.y).FindChild(n1.x), d.GetNode(n2.y).FindChild(n2.x), dir));
            }

            for (int i = 0; i < 4; i++)
            {
                var a = fe[i];
                var dir = fed[i];
                var n1 = a.c0;
                var n2 = a.c1;
                var n3 = a.c2;
                var n4 = a.c3;
                
                EdgeProc(worldData, meshData,
                    new EdgeProcData(d.GetNode(n1.y).FindChild(n1.x), d.GetNode(n2.y).FindChild(n2.x),
                        d.GetNode(n3.y).FindChild(n3.x), d.GetNode(n4.y).FindChild(n4.x), dir));
            }
        }
    }

    static void EdgeProc(WorldData worldData, ChunkMeshData.MeshData meshData, EdgeProcData d)
    {
        if (!d.n1.IsLeaf || !d.n2.IsLeaf || !d.n3.IsLeaf || !d.n4.IsLeaf)
        {
            int2x4[] ee = null;
            if (d.dir == 'x') ee = xEdgeEdgeProcChildIndex;
            else if (d.dir == 'y') ee = yEdgeEdgeProcChildIndex;
            else if (d.dir == 'z') ee = zEdgeEdgeProcChildIndex;
            else Debug.Assert(false);

            for (int i = 0; i < 2; i++)
            {
                var n1 = ee[i].c0;
                var n2 = ee[i].c1;
                var n3 = ee[i].c2;
                var n4 = ee[i].c3;
                var dir = d.dir;

                EdgeProc(worldData, meshData,
                    new EdgeProcData(d.GetNode(n1.y).FindChild(n1.x), d.GetNode(n2.y).FindChild(n2.x),
                        d.GetNode(n3.y).FindChild(n3.x), d.GetNode(n4.y).FindChild(n4.x), dir));
            }
        }
        else
        {
            CreateQuad(worldData, meshData, d);
        }
    }
     
    
    static void _GetMidPosNormal(ref EdgeProcData d, out Vector3 pos, out Vector3 nor)
    {
        var tmp = ListPool<Octree.Node>.Get();
        tmp.Clear();
        
        Vector3 p = Vector3.zero, n = Vector3.zero;
        int c = 0;

        tmp.Clear();
        tmp.Add(d.n1);
        if(!tmp.Contains(d.n2)) tmp.Add(d.n2);
        if(!tmp.Contains(d.n3)) tmp.Add(d.n3);
        if(!tmp.Contains(d.n4)) tmp.Add(d.n4);

        foreach (var node in tmp)
        {
            if (node.LeafNodeData.HasEdgeData)
            {
                p += node.LeafNodeData.vertex;
                n += node.LeafNodeData.normal;
                c++;
            }
        }

        pos = p / c;
        nor = n.normalized;
        
        if (nor.magnitude < 0.1f) nor = Vector3.up;

        tmp.Clear();
        ListPool<Octree.Node>.Release(tmp);
    }
    
    private static object lck = new object();
    static void _FixMissingData(ref EdgeProcData d)//modifies node data
    {
        lock (lck)
        {
            if (!d.n1.LeafNodeData.HasVertex || !d.n2.LeafNodeData.HasVertex || !d.n3.LeafNodeData.HasVertex || !d.n4.LeafNodeData.HasVertex)
            {
                _GetMidPosNormal(ref d, out Vector3 p, out Vector3 n);
                byte anyMat;
                if (d.n1.LeafNodeData.HasVertex) anyMat = d.n1.LeafNodeData.vertexMaterial;
                else if (d.n2.LeafNodeData.HasVertex) anyMat = d.n2.LeafNodeData.vertexMaterial;
                else if (d.n3.LeafNodeData.HasVertex) anyMat = d.n3.LeafNodeData.vertexMaterial;
                else anyMat = d.n4.LeafNodeData.vertexMaterial;

                if (!d.n1.LeafNodeData.HasVertex)
                {
                    d.n1.LeafNodeData.vertex = p;
                    d.n1.LeafNodeData.normal = n;
                    d.n1.LeafNodeData.vertexMaterial = anyMat;
                    d.n1.LeafNodeData.HasFakeVertex = true;
                }

                if (!d.n2.LeafNodeData.HasVertex)
                {
                    d.n2.LeafNodeData.vertex = p;
                    d.n2.LeafNodeData.normal = n;
                    d.n2.LeafNodeData.vertexMaterial = anyMat;
                    d.n2.LeafNodeData.HasFakeVertex = true;
                }

                if (!d.n3.LeafNodeData.HasVertex)
                {
                    d.n3.LeafNodeData.vertex = p;
                    d.n3.LeafNodeData.normal = n;
                    d.n3.LeafNodeData.vertexMaterial = anyMat;
                    d.n3.LeafNodeData.HasFakeVertex = true;
                }

                if (!d.n4.LeafNodeData.HasVertex)
                {
                    d.n4.LeafNodeData.vertex = p;
                    d.n4.LeafNodeData.normal = n;
                    d.n4.LeafNodeData.vertexMaterial = anyMat;
                    d.n4.LeafNodeData.HasFakeVertex = true;
                }
            }
        }
    }

    
    static void CreateQuad(WorldData worldData, ChunkMeshData.MeshData meshData, EdgeProcData d)
    {
        int edgeSign = d.GetEdgeSign(out bool isCubeEdge, out bool isSphereEdge, out SDFInfo c1, out SDFInfo c2);
        byte edgeMat = c2.mat == 0 ? c1.mat : c2.mat;
        SDFInfo si = c1.order > c2.order ? c1 : c2;
        
        if (edgeSign == 0) return;//No surface
        
        _FixMissingData(ref d);
        
        //Because I ordered n1, n2, n3, n4 by LT RT LB RB, looking from Right, Up, Back,
        //I need to change the Z direction from Back to Front to make all nodes ordered from the positive side,
        if (d.dir == 'z')
        {
            (d.n1, d.n2) = (d.n2, d.n1);
            (d.n3, d.n4) = (d.n4, d.n3);
        }

        //Set triangle order based on edgeSign
        Octree.Node node1 = edgeSign == 1 ? d.n1 : d.n2;
        Octree.Node node2 = edgeSign == 1 ? d.n2 : d.n1;
        Octree.Node node3 = edgeSign == 1 ? d.n3 : d.n4;
        Octree.Node node4 = edgeSign == 1 ? d.n4 : d.n3;

        
        //node1     node2
        //node3     node4
        if (node1 != node2 && node2 != node4 && node3 != node4 && node1 != node3)
        {
            CreateTri(worldData, meshData, node1, node2, node3, edgeMat, d.dir, isCubeEdge, isSphereEdge, si);
            CreateTri(worldData, meshData, node2, node4, node3, edgeMat, d.dir, isCubeEdge, isSphereEdge, si);
        }
        //LOD Difference
        else if(node1 == node2)
        {
            CreateTri(worldData, meshData, node2, node4, node3, edgeMat, d.dir, isCubeEdge, isSphereEdge, si);
        }
        else if (node2 == node4)
        {
            CreateTri(worldData, meshData, node1, node4, node3, edgeMat, d.dir, isCubeEdge, isSphereEdge, si);
        }
        else if (node3 == node4)
        {
            CreateTri(worldData, meshData, node1, node2, node4, edgeMat, d.dir, isCubeEdge, isSphereEdge, si);
        }
        else if (node1 == node3)
        {
            CreateTri(worldData, meshData, node2, node4, node3, edgeMat, d.dir, isCubeEdge, isSphereEdge, si);
        }
    }
    static Vector3 GetCubeNormal(char edgeDir, Vector3 normal)
    {
        if (edgeDir == 'x')
        {
            if (normal.x < 0) return Vector3.left;
            else return Vector3.right;
        }
        else if (edgeDir == 'y')
        {
            if (normal.y < 0) return Vector3.down;
            else return Vector3.up;
        }
        else
        {
            if (normal.z < 0) return Vector3.back;
            else return Vector3.forward;
        }
    }
    static void CreateTri(WorldData worldData, ChunkMeshData.MeshData meshData, Octree.Node n1, Octree.Node n2, Octree.Node n3, byte edgeMat, char edgeDir, bool isCubeEdge, bool isSphereEdge, SDFInfo sdf)
    {
        //Create duplicate verts/norms for cubes and SDFs
        if (isCubeEdge)
        {
            Vector3 normal = new Plane(n1.LeafNodeData.vertex, n2.LeafNodeData.vertex, n3.LeafNodeData.vertex).normal;
            normal = GetCubeNormal(edgeDir, normal);
            
            int idx1 = meshData.vertex.Count;
            meshData.AddVertexNormal(n1.LeafNodeData.vertex, normal);
            int idx2 = meshData.vertex.Count;
            meshData.AddVertexNormal(n2.LeafNodeData.vertex, normal);
            int idx3 = meshData.vertex.Count;
            meshData.AddVertexNormal(n3.LeafNodeData.vertex, normal);

            meshData.AddTriangleMat(idx1, idx2, idx3, edgeMat, edgeMat, edgeMat);
        }
        else if (isSphereEdge)
        {
            int idx1 = meshData.vertex.Count;
            meshData.AddVertexNormal(n1.LeafNodeData.vertex, sdf.sphere.Normal(n1.LeafNodeData.vertex));
            int idx2 = meshData.vertex.Count;
            meshData.AddVertexNormal(n2.LeafNodeData.vertex, sdf.sphere.Normal(n2.LeafNodeData.vertex));
            int idx3 = meshData.vertex.Count;
            meshData.AddVertexNormal(n3.LeafNodeData.vertex, sdf.sphere.Normal(n3.LeafNodeData.vertex));

            if(edgeMat != 1)
                meshData.AddTriangleMat(idx1, idx2, idx3, edgeMat, edgeMat, edgeMat);
            else
                meshData.AddTriangleMat(idx1, idx2, idx3, n1.LeafNodeData.vertexMaterial, n2.LeafNodeData.vertexMaterial, n3.LeafNodeData.vertexMaterial);
        }
        else if (n1.LeafNodeData.HasSDFData || n2.LeafNodeData.HasSDFData || n3.LeafNodeData.HasSDFData)
        {
            Vector3 n = new Plane(n1.LeafNodeData.vertex, n2.LeafNodeData.vertex, n3.LeafNodeData.vertex).normal;
            bool overrideNormal = n.y < 0.2f;
            
            int idx1 = meshData.vertex.Count;
            Vector3 v1 = n1.LeafNodeData.vertex;
            meshData.AddVertexNormal(v1, overrideNormal ? n : worldData.GetNormal(v1.x, v1.z, n1.size));
            int idx2 = meshData.vertex.Count;
            Vector3 v2 = n2.LeafNodeData.vertex;
            meshData.AddVertexNormal(v2, overrideNormal ? n : worldData.GetNormal(v2.x, v2.z, n2.size));
            int idx3 = meshData.vertex.Count;
            Vector3 v3 = n3.LeafNodeData.vertex;
            meshData.AddVertexNormal(v3, overrideNormal ? n : worldData.GetNormal(v3.x, v3.z, n3.size));
            

            meshData.AddTriangleMat(idx1, idx2, idx3, edgeMat, edgeMat, edgeMat);
        }
        else
        {
            meshData.AddTriangleMat(n1.GetMeshDataVertexIndex(meshData), n2.GetMeshDataVertexIndex(meshData), n3.GetMeshDataVertexIndex(meshData));
        }
    }
    
    
    public static void GenerateQuadsInNode(WorldData worldData, ChunkMeshData.MeshData meshData, Octree.Node node)
    {
        Debug.Assert(node.GetParentChunkNodeID().Equals(meshData.chunkID));
        
        CellProc(worldData, meshData, new CellProcData(node));
    }

    //For generating mesh between chunks
    public static void GenerateQuadsInFace(WorldData worldData, ChunkMeshData.MeshData meshData, Octree.Node node1, Octree.Node node2, char dir)
    {
        FaceProc(worldData, meshData, new FaceProcData(node1, node2, dir));
    }
    public static void GenerateQuadsInEdge(WorldData worldData, ChunkMeshData.MeshData meshData, Octree.Node node1, Octree.Node node2, Octree.Node node3, Octree.Node node4, char dir)
    {
        EdgeProc(worldData, meshData, new EdgeProcData(node1, node2, node3, node4, dir));
    }
}
