using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public static class OctreePool
{
    private static Queue<Octree> octrees = new Queue<Octree>();
    private static Queue<Queue<NodeBound>> nodeBounds = new Queue<Queue<NodeBound>>();
    private static Queue<Queue<CubeOctree.CubeNode>> cubeNodeQueues = new Queue<Queue<CubeOctree.CubeNode>>();
    static ConcurrentQueue<Octree.Node> nodes = new ConcurrentQueue<Octree.Node>();
    private static ConcurrentQueue<List<Octree.Node>> nodeList = new ConcurrentQueue<List<Octree.Node>>();
    private static ConcurrentQueue<Queue<Octree.Node>> nodeQueue = new ConcurrentQueue<Queue<Octree.Node>>();
    static Queue<CubeOctree.CubeNode> cubeNodes = new Queue<CubeOctree.CubeNode>();
    private static Queue<ChunkMeshData> meshData = new Queue<ChunkMeshData>();
    private static ConcurrentQueue<WorldData.LeafNodeData> leafData = new ConcurrentQueue<WorldData.LeafNodeData>();
    private static Queue<PerChunkQef> qefPool = new Queue<PerChunkQef>();


    public static Queue<NodeBound> GetNodeBoundQueue()
    {
        if (nodeBounds.Count > 0) return nodeBounds.Dequeue();
        else return new Queue<NodeBound>();
    }
    public static void ReturnNodeBoundQueue(Queue<NodeBound> n)
    {
        nodeBounds.Enqueue(n);
    }
    
    public static Queue<CubeOctree.CubeNode> GetCubeNodeQueue()
    {
        if (cubeNodeQueues.Count > 0) return cubeNodeQueues.Dequeue();
        else return new Queue<CubeOctree.CubeNode>();
    }
    public static void ReturnCubeNodeQueue(Queue<CubeOctree.CubeNode> n)
    {
        cubeNodeQueues.Enqueue(n);
    }

    
    public static List<Octree.Node> GetNodeList()
    {
        List<Octree.Node> n;
        if (!nodeList.TryDequeue(out n))
            n = new List<Octree.Node>();
        return n;
    }

    public static void ReturnNodeList(List<Octree.Node> n)
    {
        nodeList.Enqueue(n);
    }
    public static Queue<Octree.Node> GetNodeQueue()
    {
        Queue<Octree.Node> n;
        if (!nodeQueue.TryDequeue(out n))
            n = new Queue<Octree.Node>();
        return n;
    }

    public static void ReturnNodeQueue(Queue<Octree.Node> n)
    {
        nodeQueue.Enqueue(n);
    }
    
    public static PerChunkQef GetQEFSolver()
    {
        if (qefPool.Count > 0) return qefPool.Dequeue();
        else return new PerChunkQef();
    }

    public static void ReturnQEFSolver(PerChunkQef s)
    {
        qefPool.Enqueue(s);
    }
    
    public static Octree GetOctree()
    {
        if (octrees.Count > 0) return octrees.Dequeue();
        else return new Octree();
    }

    public static void ReturnOctree(Octree o)
    {
        octrees.Enqueue(o);
    }
    
    public static Octree.Node GetNode(WorldData worldData, Octree.Node parent, Vector3 minPos, float size)
    {
        Octree.Node n = null;

        if (!nodes.TryDequeue(out n)) 
            n = new Octree.Node();
        
        n.Initialize(worldData, parent, minPos, size);
        return n;
    }

    public static CubeOctree.CubeNode GetCubeNode()
    {
        if (cubeNodes.Count > 0)
            return cubeNodes.Dequeue();
        else
            return new CubeOctree.CubeNode();
    }
    public static void ReturnCubeNode(CubeOctree.CubeNode cn)
    {
        cubeNodes.Enqueue(cn);
    }
    
    public static void ReturnNode(Octree.Node node)
    {
        nodes.Enqueue(node);
    }
    

    public static ChunkMeshData GetOctreeMeshData()
    {
        if (meshData.Count > 0)
        {
            var omd = meshData.Dequeue();
            return omd;
        }
        else return new ChunkMeshData();
    }

    public static void ReturnOctreeMeshData(ChunkMeshData om)
    {
        meshData.Enqueue(om);
    }

    public static WorldData.LeafNodeData GetLeafNodeData()
    {
        WorldData.LeafNodeData l;
        if (!leafData.TryDequeue(out l)) 
            l = new WorldData.LeafNodeData();
        return l;
    }

    public static void ReturnLeafNodeData(WorldData.LeafNodeData d)
    {
        leafData.Enqueue(d);
    }
    
}

