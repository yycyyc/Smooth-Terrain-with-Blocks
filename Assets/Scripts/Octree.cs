using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class Octree
{
    private WorldData worldData;

    public class Node
    {
        WorldData worldData;

        public void Initialize(WorldData worldData, Node parent, Vector3 minPos, float size)
        {
            this.worldData = worldData;
            this.minPos = minPos;
            this.size = size;
            this.parent = parent;
            this.center = minPos + size * Vector3.one * .5f;
            this.maxPos = minPos + size * Vector3.one;
            this.bound = new NodeBound(minPos, size);
            this.nodeID = bound;
            
            for (int i = 0; i < 8; i++)
            {
                child[i] = null;
            }

            chunkLOD = -1;
            isChunkNode = false;
        }

        public void OnOctreeRootNodeCreated()
        {
            RegisterAsLeafNode();
        }

        public NodeBound bound { get; private set; }
        public NodeID nodeID { get; private set; }
        public Vector3 minPos;
        public Vector3 maxPos;
        Vector3 center;        
        public float size;
        
        public bool isChunkNode = false;
        public bool needsMeshUpdate = false;

        public int chunkLOD;
        
        Node parent;
        Node[] child = new Node[8];

        public bool IsLeaf => child[0] == null;
        public WorldData.LeafNodeData LeafNodeData
        {
            get;
            private set;
        }
        
        public NodeID GetParentChunkNodeID()
        {
            Node n = this;
            
            if (n.isChunkNode) return n.nodeID;
            while (n.parent != null)
            {
                n = n.parent;

                if (n.isChunkNode) return n.nodeID;
            }
            
            //This node is above chunk node. ex) while root node dividing into chunk nodes
            return NodeID.Invalid;
        }
        public Node FindParentChunkNode()
        {
            Node n = this;
            
            if (n.isChunkNode) return n;
            while (n.parent != null)
            {
                n = n.parent;

                if (n.isChunkNode) return n;
            }
            
            //This node is above chunk node. ex) while root node dividing into chunk nodes
            return null;
        }
        
        
        public int GetMeshDataVertexIndex(ChunkMeshData.MeshData meshData)
        {
            return LeafNodeData.GetMeshDataVertexIndex(meshData);
        }
        
        //Returns self if no child exists(Important for LOD edge iter)
        public Node FindChild(int idx)
        {
            if (IsLeaf)
            {
                return this;
            }
            
            return child[idx];
        }
        
        void RegisterAsLeafNode()
        {
            LeafNodeData = worldData.AddLeafNode(GetParentChunkNodeID(), nodeID);
        }
        public void UnregisterLeafNode()
        {
            worldData.RemoveLeafNode(nodeID);
            
            LeafNodeData = null;
        }
        
        public void Divide()
        {
            Debug.Assert(size > (OctreeParam.NodeMinSize * 2 - 0.01f));
            Debug.Assert(child[0] == null);
            
            UnregisterLeafNode();
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var n = child[i * 4 + j * 2 + k] = 
                            OctreePool.GetNode(worldData, this, minPos + new Vector3(i, j, k) * size * .5f, size * .5f);
                        
                        n.RegisterAsLeafNode();
                    }
                }
            }
        }
        public void CollapseAll()
        {
            if (child[0] != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    child[i].CollapseAll();
                    child[i].UnregisterLeafNode();
                    OctreePool.ReturnNode(child[i]);
                    child[i] = null;
                }
                RegisterAsLeafNode();
            }
        }

        public void GetAllDataNodes(List<Node> nodes)
        {
            if (IsLeaf && LeafNodeData.HasVertex) nodes.Add(this);
  
            if (child[0] != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    child[i].GetAllDataNodes(nodes);
                }
            }
        }
        
        public Node GetNode(NodeBound node)
        {
            //Debug.Assert(OctreeUtil.IsProperNodeBound(node));
            if (!OctreeUtil.Contains((Vector4)new NodeBound(minPos.x, minPos.y, minPos.z, size), node))
            {
                return null;
            }

            return _GetNode(node);
        }
        Node _GetNode(NodeBound node)
        {
            //This always contains node
            if (Mathf.Approximately(node.w, size)) return this;

            if (Mathf.Approximately(size, OctreeParam.NodeMinSize)) return null;//cant divide
            
            if (child[0] == null)
            {
                Divide();
            }
            
            int x = node.x < center.x - 0.001f ? 0 : 1;
            int y = node.y < center.y - 0.001f ? 0 : 1;
            int z = node.z < center.z - 0.001f ? 0 : 1;
            
            int childIdx = x * 4 + y * 2 + z;

            return child[childIdx]._GetNode(node);
        }
        
        public Node FindNodeOrParent(NodeBound node)
        {
            //Debug.Assert(OctreeUtil.IsProperNodeBound(node));
            if (OctreeUtil.Contains((Vector4)bound, node))
                return _FindNodeOrParent(node);
            else
                return null;
        }
        Node _FindNodeOrParent(NodeBound node)
        {
            if (Mathf.Approximately(node.w, size)) return this;
            if (child[0] == null) return this;
            
            int x = node.x < center.x - 0.001f ? 0 : 1;
            int y = node.y < center.y - 0.001f ? 0 : 1;
            int z = node.z < center.z - 0.001f ? 0 : 1;

            int idx = x * 4 + y * 2 + z;

            return child[idx]._FindNodeOrParent(node);
        }
        
        public void FindAllChunkNodes(List<Node> nodes, Queue<Node> queue)
        {
            //chunknode is 100% under this node
            queue.Clear();
            nodes.Clear();
            
            queue.Enqueue(this);

            while (queue.Count > 0)
            {
                var n = queue.Dequeue();

                if (n.isChunkNode)
                    nodes.Add(n);
                else if(!n.IsLeaf && n.size > OctreeParam.ChunkSize * 2 - 0.001f)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        queue.Enqueue(n.child[i]);
                    }
                }
            }
        }
    }
    
    public Node root;
    private List<Node> chunkNodes = new List<Node>();
    public List<Node> ChunkNodes => chunkNodes;
    public void InitializeOctree(WorldData worldData, Vector3 minPos)
    {
        this.worldData = worldData;
        root = OctreePool.GetNode(worldData, null, minPos, OctreeParam.OctreeSize);
        root.OnOctreeRootNodeCreated();
        chunkNodes.Clear();
    }

    public void CleanUpOctree()
    {
        root.CollapseAll();
        root.UnregisterLeafNode();
        OctreePool.ReturnNode(root);
        root = null;
        chunkNodes.Clear();
        worldData = null;
    }
    
    public Node CreateChunkNode(NodeBound nb)
    {
        Debug.Assert(OctreeUtil.Contains((Vector4)root.bound, nb));
        
        var node = root.GetNode(nb);
        node.CollapseAll();
        
        Debug.Assert(!node.isChunkNode);
        
        node.isChunkNode = true;
        node.needsMeshUpdate = true;
        node.chunkLOD = Mathf.RoundToInt(Mathf.Log(node.size / OctreeParam.ChunkSize, 2));
        
        if(!chunkNodes.Contains(node))
            chunkNodes.Add(node);
        else
        {
            Debug.LogError("Trying to create existing chunk node");
        }

        return node;
    }
    public void DestroyChunkNode(NodeBound nb)
    {
        Debug.Assert(OctreeUtil.Contains((Vector4)root.bound, nb));

        var node = root.GetNode(nb);
        node.CollapseAll();
        
        Debug.Assert(node.isChunkNode);
        
        node.isChunkNode = false;
        node.needsMeshUpdate = false;
        node.chunkLOD = -1;

        if (chunkNodes.Contains(node))
            chunkNodes.Remove(node);
        else
        {
            Debug.LogError("Chunk node missing");
        }
    }
} 

public class CubeOctree
{
    public void Initialize(WorldData worldData, Vector3Int id, byte mat)
    {
        minPos = id * CubeParam.cubeOctreeSize;
        root = OctreePool.GetCubeNode();
        root.Initialize(null, minPos, CubeParam.cubeOctreeSize, mat, 0);
        worldData.AddCubeNode(root.minPos, root.size, root, mat);
    }
    public Vector3Int minPos;
    public CubeNode root;
    //No collapsing when 8 children has same value, this case is rare and it disables direct search for a node in a hashset of leaf nodes.
    //Also, because it assumes a non-existing cube node uses value of its parent, you are forced to create empty nodes when subdividing to indicate that node is empty.

    public class CubeNode
    {
        public void Initialize(CubeNode parent, Vector3Int minPos, int size, byte value, uint order)
        {
            this.parent = parent;
            this.minPos = minPos;
            this.size = size;
            maxPos = minPos + Vector3Int.one * size;
            center = minPos + Vector3Int.one * size / 2;
            for (int i = 0; i < 8; i++)
                child[i] = null;
            this.value = value;
            this.order = order;
        }
        
        public uint order;
        public byte value;
        
        public Vector3Int minPos, maxPos;
        public int size;
        private Vector3Int center;
          
        public CubeNode parent;
        public CubeNode[] child = new CubeNode[8];
        public bool IsLeaf => child[0] == null;
        void Divide(WorldData worldData, int childIdx, byte mat, uint order)
        {
            Debug.Assert(size > 1);
            Debug.Assert(child[0] == null);
            
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        int idx = i * 4 + j * 2 + k;
                        var n = child[idx] = OctreePool.GetCubeNode();
                        Vector3Int minPos = this.minPos + new Vector3Int(i, j, k) * this.size / 2;
                        int size = this.size / 2;


                        if (idx == childIdx)
                        {
                            n.Initialize(this, minPos, size, value, order);
                            worldData.AddCubeNode(minPos, size, n, mat);
                        }
                        else
                        {
                            n.Initialize(this, minPos, size, 255, 0);
                            worldData.AddCubeNode(minPos, size, n, 255);
                        }
                    }
                }
            }
        }
        
        public CubeNode GetNodeAndSetVal(WorldData worldData, Vector3Int pos, int findSize, byte mat, uint order)
        {
            Debug.Assert(size >= findSize);
            this.order = order;
            
            if (value == 255)
            {
                value = mat;
                worldData.AddCubeNode(minPos, size, this, mat);
            }
            else 
            {
                value = mat;
                worldData.CubeValueUpdate(minPos, size, mat, order); 
            }

            //This always contains node
            if (findSize == size) return this;

            int x = pos.x < center.x ? 0 : 1;
            int y = pos.y < center.y ? 0 : 1;
            int z = pos.z < center.z ? 0 : 1;
            
            int childIdx = x * 4 + y * 2 + z;

            if (IsLeaf)
            {
                Divide(worldData, childIdx, mat, order);
            }
            

            return child[childIdx].GetNodeAndSetVal(worldData, pos, findSize, mat, order);
        }
    }
    
    public void SetData(WorldData worldData, Vector3Int cubeGridPos, byte val, uint order)
    {
        root.GetNodeAndSetVal(worldData, cubeGridPos, 1, val, order);
    }
}
    
