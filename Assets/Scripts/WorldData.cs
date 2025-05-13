using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;


public class WorldData
{
    public static uint PlaceOrder = 1;
    
    public static WorldData debug;
    public readonly WorldTerrain terrain; 
    public WorldData(WorldTerrain terrain)
    {
        debug = this;
        this.terrain = terrain;
        worldDataNative = new WorldDataNative()//Dispose from UnityWorld OnDestroy()
        {
            AllChunks = new NativeHashSet<Work.NodeBound>(1000, Allocator.Persistent),
            AllCubeNodes = new NativeHashMap<Work.NodeBoundInt, WorldDataNative.CubeInfo>(2000000, Allocator.Persistent),
            AllCubeOctrees = new NativeHashSet<int3>(1000, Allocator.Persistent),
            AllSpheres = new NativeList<SphereSDF>(Allocator.Persistent),
            cubeOctreeSize = CubeParam.cubeOctreeSize,
        };
    }
    
    public class LeafNodeData
    {
        public void Initialize(WorldData worldData, NodeID chunkID, NodeID nodeID)
        {
            this.chunkID = chunkID;
            this.nodeID = nodeID;
            this.nb = nodeID;
            
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < 8; i++)
            {
                cornerType[i] = SDFInfo.Invalid();
            }
            
            edgeData.Clear();
            meshDataIndex.Clear();

            vertex = new Vector3(0, 0, 0);
            HasFakeVertex = false;
            vertexMaterial = 0;
            hasCubeData = hasSphereData = hasTerrainData = false;
        }
        
        
        public NodeID chunkID;
        public NodeID nodeID;
        public NodeBound nb;
        
        public Vector3 vertex;
        public Vector3 normal;

        public void SetVertex(Vector3 vertex)
        {
            bool isCubeBlock = vertexMaterial % 255 != 0 && vertexMaterial != 1;
            
            if (HasSDFData && isCubeBlock)
            {
                var v = CubeUtil.GetFixedCubeVertex(nb.MinPos);

                //if (canClampX && cubeXDiff) vertex.x = v.x;
                //if (canClampY && cubeYDiff) vertex.y = v.y;
                //if (canClampZ && cubeZDiff) vertex.z = v.z;
            }

            //if (this.vertex.x < nb.MinPos.x) this.vertex.x = nb.MinPos.x;
            //if (this.vertex.y < nb.MinPos.y) this.vertex.y = nb.MinPos.y;
            //if (this.vertex.z < nb.MinPos.z) this.vertex.z = nb.MinPos.z;
            //if (this.vertex.x > nb.MaxPos.x) this.vertex.x = nb.MaxPos.x;
            //if (this.vertex.y > nb.MaxPos.y) this.vertex.y = nb.MaxPos.y;
            //if (this.vertex.z > nb.MaxPos.z) this.vertex.z = nb.MaxPos.z;
            
            this.vertex = vertex;
        }
        
        SDFInfo[] cornerType = new SDFInfo[8];

        
        public bool HasSDFData => hasCubeData || hasSphereData;
        public bool hasCubeData = false;
        public bool hasSphereData = false;
        public bool hasTerrainData = false;
        
        public byte vertexMaterial;
        
        public bool HasVertex => HasEdgeData || HasFakeVertex;
        
        public bool HasEdgeData => edgeData.Count > 0;

        public bool HasFakeVertex { get; set; }
        List<EdgeData> edgeData = new List<EdgeData>();
        public List<EdgeData> EdgeData => edgeData;

        //index from MeshData that has this vertex saved (meshID, idx)
        private Dictionary<Vector2Int, int> meshDataIndex = new Dictionary<Vector2Int, int>();
        
        public int GetMeshDataVertexIndex(ChunkMeshData.MeshData meshData)
        {
            lock (meshDataIndex)
            {
                if (!meshDataIndex.ContainsKey(meshData.MeshID))
                {
                    int idx = meshData.vertex.Count;
                    meshData.AddVertexNormal(vertex, normal, vertexMaterial);

                    meshDataIndex.Add(meshData.MeshID, idx);
                    return idx;
                }

                return meshDataIndex[meshData.MeshID];
            }
        }
        public void AddEdgeData(EdgeData ed, byte solidMat)
        {
            edgeData.Add(ed);
            vertexMaterial = (byte)Mathf.Max(vertexMaterial, solidMat);//terrain : 1 block : 2
        }


        public void SetCornerData(Vector3Int corner, SDFInfo cornerSDFInfo)
        {
            this.cornerType[corner.x * 4 + corner.y * 2 + corner.z] = cornerSDFInfo;
        }
        public void GetCornerData(Vector3Int corner, out SDFInfo cornerSDFInfo)
        {
            cornerSDFInfo = this.cornerType[corner.x * 4 + corner.y * 2 + corner.z];
        }
    }

    public Dictionary<NodeID, Octree.Node> AllChunkNodes => chunkNodes;

    
    ///Data shouldn't change while in update
    public Dictionary<Vector4Int, CubeOctree.CubeNode> AllCubeNodes = new Dictionary<Vector4Int, CubeOctree.CubeNode>();

    public Dictionary<NodeID, Octree> octrees = new Dictionary<NodeID, Octree>();
    private Dictionary<NodeID, Octree.Node> chunkNodes = new Dictionary<NodeID, Octree.Node>();
    private Dictionary<NodeID, ChunkMeshData> octreeMeshData = new Dictionary<NodeID, ChunkMeshData>();
    private ConcurrentDictionary<NodeID, LeafNodeData> dataNodeData = new ConcurrentDictionary<NodeID, LeafNodeData>();
    
    private Dictionary<Vector3Int, CubeOctree> allCubeOctrees = new Dictionary<Vector3Int, CubeOctree>();

    private Dictionary<NodeID, PerChunkQef> qefSolverPerChunk = new Dictionary<NodeID, PerChunkQef>();
    
    public WorldDataNative worldDataNative;    
    ///Data shouldn't change while in update
    
    public Octree FindOctree(Vector3 point)
    {
        NodeID octreeID = OctreeUtil.GetNodeIDBySize(point, OctreeParam.OctreeSize);
        
        if (octrees.ContainsKey(octreeID))
            return octrees[octreeID];
        else
        {
            return null;
        }
    }
    public LeafNodeData AddLeafNode(NodeID chunkID, NodeID nodeID)
    {
        var lnd = OctreePool.GetLeafNodeData();
        lnd.Initialize(this, chunkID, nodeID);
        dataNodeData.TryAdd(nodeID, lnd);
        return lnd;
    }
    
    public void RemoveLeafNode(NodeID nid)
    {
        var lnd = dataNodeData[nid];
        
        lnd.Reset();
        OctreePool.ReturnLeafNodeData(lnd);
        
        dataNodeData.TryRemove(nid, out var v);
    }
    LeafNodeData GetLeafNodeData(NodeID nid)
    {
        return dataNodeData[nid];
    }

    public PerChunkQef GetQefSolver(NodeID chunkID)
    {
        return qefSolverPerChunk[chunkID];
    }
    
    public void AddChunkNode(NodeID cid, Octree.Node node)
    {
        chunkNodes.Add(cid, node);
        worldDataNative.AllChunks.Add(node.bound);
        
        var om = OctreePool.GetOctreeMeshData();
        om.Initialize(cid);
        octreeMeshData.Add(cid, om);

        var li = HashSetPool<EdgeID>.Get();
        li.Clear();

        li = HashSetPool<EdgeID>.Get();
        li.Clear();

        qefSolverPerChunk.Add(cid, OctreePool.GetQEFSolver());
    }
    public void RemoveChunkNode(NodeID cid)
    {
        chunkNodes.Remove(cid);
        worldDataNative.AllChunks.Remove((NodeBound)cid);

        
        OctreePool.ReturnOctreeMeshData(octreeMeshData[cid]);
        octreeMeshData.Remove(cid);
        

        var s = qefSolverPerChunk[cid];
        OctreePool.ReturnQEFSolver(s);
        qefSolverPerChunk.Remove(cid);
    }

    public Octree.Node FindChunkNode(NodeID nodeID)
    {
        return chunkNodes[nodeID];
    }
    public Octree.Node FindChunkNode(Vector3 worldPos)
    {
        Octree.Node cn = null;
        for (int i = 0; i <= OctreeParam.MaxLOD; i++)
        {
            NodeID nid = OctreeUtil.GetNodeID(worldPos, i);
            if (chunkNodes.TryGetValue(nid, out cn))
                break;
        }

        return cn;
    }
    
    public ChunkMeshData GetOctreeMeshData(NodeID chunkID)
    {
        return octreeMeshData[chunkID];
    }

    public void FillSolver(Octree.Node node, PerChunkQef solver)
    {
        foreach (var ed in GetLeafNodeData(node.nodeID).EdgeData)
        {
            if(ed.sdfType == 0)
                solver.Add(ed.surfacePos, ed.surfaceNor);
            else
                solver.Add(ed.surfacePos, ed.surfaceNor * 1);
        }
    }
    
    public float GetHeight(float x, float z)
    {
        return terrain.GetHeight(x, z);
    }
    public Vector3 GetNormal(float x, float z, float gridSize)
    {
        return terrain.GetNormal(x, z, gridSize);
    }

    //initialize the created octree with mat
    public CubeOctree GetOrCreateCubeOctree(Vector3Int id, byte mat)
    {
        CubeOctree o;
        if (!allCubeOctrees.TryGetValue(id, out o))
        {
            o = new CubeOctree();
            o.Initialize(this, id, mat);
            allCubeOctrees.Add(id, o);
            worldDataNative.AllCubeOctrees.Add(new int3(id.x, id.y, id.z));
        }

        return o;
    }
    public CubeOctree FindCubeOctree(Vector3Int id)
    {
        CubeOctree o;

        if (!allCubeOctrees.TryGetValue(id, out o))
        {
            o = null;
        }

        return o;
    }
    
    public void AddCubeNode(Vector3Int minPos, int size, CubeOctree.CubeNode n, byte mat)
    {
        if (mat == 255) return;
        
        AllCubeNodes.Add(new Vector4Int(minPos, size), n);
        worldDataNative.AllCubeNodes.Add(new Work.NodeBoundInt(minPos.x, minPos.y, minPos.z, size), new WorldDataNative.CubeInfo(mat, n.IsLeaf, n.order));
        if (n.parent != null)
        {
            var parent = new Work.NodeBoundInt(n.parent.minPos.x, n.parent.minPos.y, n.parent.minPos.z, n.parent.size);
            if (worldDataNative.AllCubeNodes.ContainsKey(parent))
                worldDataNative.AllCubeNodes[parent] = new WorldDataNative.CubeInfo(n.parent.value, false, n.order);
        }
    }
    public void CubeValueUpdate(Vector3Int minPos, int size, byte mat, uint order)
    {
        var key = new Work.NodeBoundInt(minPos.x, minPos.y, minPos.z, size);
        
        var c = worldDataNative.AllCubeNodes[key];
        c.mat = mat;
        c.order = order;
        worldDataNative.AllCubeNodes[key] = c;
    }
    
    public void AddSphere(Vector3 worldPos, float size, byte mat)
    {
        worldDataNative.AllSpheres.Add(new SphereSDF(worldPos, size, WorldData.PlaceOrder++, mat));
    }
}

