using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using Debug = UnityEngine.Debug;

public class WorldUpdater
{
    Stopwatch s = new Stopwatch();
    
    public static bool IsUpdating => IsUpdatingWorldData || IsUpdatingSingleChunkData;
    private static bool IsUpdatingWorldData;
    private static bool IsUpdatingSingleChunkData;
    
    private World world;
    private WorldData worldData;

    private Queue<Vector3> worldUpdateQueue = new Queue<Vector3>();
    private List<Vector3> placedBlockUpdateQueue = new List<Vector3>();
    private List<Vector4> placedSphereUpdateQueue = new List<Vector4>();
    private List<NodeBound> chunkToProcess = new List<NodeBound>();

    private Dictionary<NodeID, Octree> octrees => worldData.octrees;

    public void QueueWorldUpdate(Vector3 pos)
    {
        worldUpdateQueue.Enqueue(pos);
    }
    public void QueueChunkUpdateBlock(Vector3 wPos)
    {
        placedBlockUpdateQueue.Add(wPos);
    }
    public void QueueChunkUpdateSphere(Vector4 sphere)
    {
        placedSphereUpdateQueue.Add(sphere);
    }
    
    public WorldUpdater(World world, WorldData worldData)
    {
        this.world = world;
        this.worldData = worldData;
        IsUpdatingSingleChunkData = IsUpdatingWorldData = false;
    }
    
    
    void _FindRequiredChunkNodes(Vector3 playerPos, HashSet<NodeBound> result)
    {
        var nodeBounds = OctreePool.GetNodeBoundQueue();
        nodeBounds.Clear();
        
        foreach (var o in octrees.Values)
            nodeBounds.Enqueue(o.root.bound);
        
        while (nodeBounds.Count > 0)
        {
            var n = nodeBounds.Dequeue();

            int nodeChunkLOD = Mathf.RoundToInt(Mathf.Log(n.Size / OctreeParam.ChunkSize, 2));
            Debug.Assert(nodeChunkLOD <= OctreeParam.MaxLOD);
            
            NodeID nodeID = n;
            NodeID playerID = OctreeUtil.GetNodeID(playerPos, nodeChunkLOD);

            if (Vector3.Distance(nodeID, playerID) <= OctreeParam.LODDis[nodeChunkLOD])
            {
                if (nodeChunkLOD == 0)
                {
                    result.Add(n);
                    continue;
                }

                bool hasChunkBelow = false;
                
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            NodeID nid = OctreeUtil.GetNodeID(n.MinPos + new Vector3(i, j, k) * n.Size * .5f, nodeChunkLOD - 1);
                            NodeID pid = OctreeUtil.GetNodeID(playerPos, nodeChunkLOD - 1);
                            //Chunk node exists below this node
                            if (Vector3.Distance(nid, pid) <= OctreeParam.LODDis[nodeChunkLOD - 1])
                            {
                                hasChunkBelow = true;
                            }
                        }
                    }
                }

                if (hasChunkBelow)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            for (int k = 0; k < 2; k++)
                            {
                                var nb = new NodeBound(n.MinPos + new Vector3(i, j, k) * n.Size * .5f, n.Size * .5f);
                                nodeBounds.Enqueue(nb);
                            }
                        }
                    }
                }
                else
                {
                    result.Add(n);
                }
            }
            else
            {
                //Not inside range, but needs to be chunk because its sibling is
                result.Add(n);
            }
        }

        nodeBounds.Clear();
        OctreePool.ReturnNodeBoundQueue(nodeBounds);
    }
    void _FindCurrentLoadedChunkNodes(HashSet<NodeBound> result)
    {
        foreach (var o in octrees.Values)
            foreach(var n in o.ChunkNodes)
                result.Add(n.bound);
    }
    void _FindChunkNodesToUpdate(HashSet<NodeBound> required, HashSet<NodeBound> current, HashSet<NodeBound> create, HashSet<NodeBound> destroy)
    {
         foreach(var n in current)
             if (!required.Contains(n))
             {
                 destroy.Add(n); 
             }       
         
        foreach(var n in required)
            if (!current.Contains(n))
            {
                create.Add(n);
            }
    }


    void ResetChunkHashSets()
    {
        requiredChunk.Clear();
        currentChunk.Clear();
        chunksToCreate.Clear();
        chunksToRemove.Clear();
        chunksToUpdate.Clear();
        neighborSeamsToUpdate.Clear();
        allProcessedChunks.Clear();
    }
    private HashSet<NodeID> requiredOctree = new HashSet<NodeID>();
    private HashSet<NodeID> octreesToRemove = new HashSet<NodeID>();
    private HashSet<NodeID> octreesToCreate = new HashSet<NodeID>();
    private HashSet<NodeBound> requiredChunk = new HashSet<NodeBound>();
    private HashSet<NodeBound> currentChunk = new HashSet<NodeBound>();
    private HashSet<NodeBound> chunksToRemove = new HashSet<NodeBound>();
    private HashSet<NodeBound> chunksToCreate = new HashSet<NodeBound>();
    private HashSet<NodeBound> chunksToUpdate = new HashSet<NodeBound>();
    private HashSet<NodeBound> allProcessedChunks = new HashSet<NodeBound>();
    private HashSet<Tuple<NodeBound, int>> neighborSeamsToUpdate = new HashSet<Tuple<NodeBound, int>>();
    
    private static Vector3[] neighborFaceSeams = new Vector3[]
    {
        new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)
    };
    private static int[] neighborFaceSeamMeshIndex = new int[]
    {
        1, 2, 3
    };
    private static Vector3[] neighborEdgeSeams = new Vector3[]
    {
        new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
        new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0),
        new Vector3(0, 0, 1), new Vector3(1, 0, 0)
    };
    private static int[] neighborEdgeSeamMeshIndex = new int[]
    {
        6, 4, 6, 4,
        5, 5, 5,
        4, 6
    };

    void UpdateOctrees(Vector3 playerPos)
    {
        requiredOctree.Clear();
        octreesToRemove.Clear();
        octreesToCreate.Clear();
        
        NodeID playerOctreeID = OctreeUtil.GetNodeID(playerPos, OctreeParam.MaxLOD);
        int octreeLoadRange = OctreeParam.LODDis[OctreeParam.MaxLOD];
        for (int i = -octreeLoadRange; i <= octreeLoadRange; i++)
        {
            for (int j = -octreeLoadRange; j <= octreeLoadRange; j++)
            {
                for (int k = -octreeLoadRange; k <= octreeLoadRange; k++)
                {
                    if (new Vector3(i, j, k).magnitude <= octreeLoadRange)
                    {
                        NodeID octreeID = playerOctreeID + new NodeID(i, j, k, 0);
                        requiredOctree.Add(octreeID);
                    }
                }
            }
        }
        
        foreach (var oid in octrees)
        {
            if (!requiredOctree.Contains(oid.Key))
            {
                octreesToRemove.Add(oid.Key);
            }
        }
        foreach (var oid in requiredOctree)
        {
            if (!octrees.ContainsKey(oid))
            {
                octreesToCreate.Add(oid);
            }
        }
        
        foreach (var key in octreesToRemove)
        {
            _DestroyOctree(key);
        }

        foreach (var key in octreesToCreate)
        {
            _CreateOctree(key);
        }
    }
    
    //Created, Destroyed chunks should update their neighbor's seam mesh
    void UpdateChunkNodes(Vector3 playerPos)
    {
        _FindRequiredChunkNodes(playerPos, requiredChunk);
        _FindCurrentLoadedChunkNodes(currentChunk);
        _FindChunkNodesToUpdate(requiredChunk, currentChunk, chunksToCreate, chunksToRemove);
        
        foreach (var n in chunksToRemove)
        {
            _DestroyChunkNode(n);
        }
        foreach (var n in chunksToCreate)
        {
            _CreateChunkNode(n);
        }
    }

    IEnumerator GenerateChunkData(UnityWorld unity)
    {
        s.Restart();

        yield return unity.StartCoroutine(_FillChunkNodes(unity));
        UnityWorld.updateDataTime = s.ElapsedMilliseconds;
        s.Restart();
        
        yield return unity.StartCoroutine(_GenerateMeshData(unity));
        UnityWorld.iterateEdgeTime = s.ElapsedMilliseconds;
        s.Stop();
    }
    
    Octree _GetParentOctree(NodeBound nodeBound)
    {
        NodeID octreeID;
        if (Mathf.Approximately(nodeBound.Size, OctreeParam.OctreeSize))//is Octree
        {
            octreeID = nodeBound;
        }
        else//is a child of an octree
        {
            octreeID = OctreeUtil.GetNodeIDBySize(nodeBound.MinPos, OctreeParam.OctreeSize);
        }
        
        if (octrees.ContainsKey(octreeID))
            return octrees[octreeID];
        else
        {
            Debug.LogError("Octree doesn't exist");
            return null;
        }
    }

    void _CreateOctree(NodeID oid)
    {
        var o = OctreePool.GetOctree();
        o.InitializeOctree(worldData, ((NodeBound)oid).MinPos);
        octrees.Add(oid, o);
    }
    void _DestroyOctree(NodeID oid)
    {
        var o = octrees[oid];

        var tmp = ListPool<Octree.Node>.Get();
        tmp.Clear();
        tmp.AddRange(o.ChunkNodes);
        foreach(var n in tmp)
            _DestroyChunkNode(n.bound);
        
        o.CleanUpOctree();
        
        octrees.Remove(oid);
        OctreePool.ReturnOctree(o);
        tmp.Clear();
        ListPool<Octree.Node>.Release(tmp);
    }
    
    //Just enqueue node next to the chunk, instead of finding the actual chunk node here
    void AddNeighborSeamsToUpdate(NodeBound updatedChunk)
    {
        var nodeBound = updatedChunk;
        for (int i = 0; i < 3; i++)
        {
            NodeID nid = nodeBound;
            nid += neighborFaceSeams[i];

            Tuple<NodeBound, int> tuple = new Tuple<NodeBound, int>(nid, neighborFaceSeamMeshIndex[i]);

            neighborSeamsToUpdate.Add(tuple);
        }
        
        for (int i = 0; i < 9; i++)
        {
            NodeID nid = nodeBound;
            nid += neighborEdgeSeams[i];

            Tuple<NodeBound, int> tuple = new Tuple<NodeBound, int>(nid, neighborEdgeSeamMeshIndex[i]);

            neighborSeamsToUpdate.Add(tuple);
        }
    }
    
    void _CreateChunkNode(NodeBound nodeBound)
    {
        Octree octree = _GetParentOctree(nodeBound);
        var node = octree.CreateChunkNode(nodeBound);
        
        worldData.AddChunkNode(nodeBound, node);
        AddNeighborSeamsToUpdate(nodeBound);
    }
    void _DestroyChunkNode(NodeBound nodeBound)
    {
        Octree octree = _GetParentOctree(nodeBound);
        octree.DestroyChunkNode(nodeBound);

        var omd = worldData.GetOctreeMeshData(nodeBound);
        for (int i = 0; i < 7; i++)
        {
            var mid = omd.GetMeshData(i).UniqueID;
            world.destroyedChunkMeshIDs.Enqueue(mid);
        }
        
        worldData.RemoveChunkNode(nodeBound);
        AddNeighborSeamsToUpdate(nodeBound);
    }
    
    IEnumerator _FillChunkNodes(UnityWorld unity)
    {
        Stopwatch s = new Stopwatch();
        s.Start();

        Work.Initialize();

        Debug.Log("0 : " + s.ElapsedMilliseconds);
        Debug.Log(WorldData.debug.worldDataNative.AllCubeNodes.Count());
        Debug.Log(WorldData.debug.worldDataNative.AllChunks.Count());
        Debug.Log(WorldData.debug.worldDataNative.AllCubeOctrees.Count());

        HashSet<Work.NodeBound> chunksToFill = new HashSet<Work.NodeBound>();
        HashSet<Octree.Node> requiredExtraUpdate = new HashSet<Octree.Node>();
        NativeList<Work.NodeBound> chunksToProcess = new NativeList<Work.NodeBound>(Allocator.Persistent);

        bool isWorking = true;
        
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                foreach (var nb in chunksToRemove)
                {
                    float gridSize = nb.Size / OctreeParam.ChunkSize;
                    int cubeLOD = Mathf.RoundToInt(Mathf.Log(nb.Size / OctreeParam.ChunkSize, 2));
                    CubeUtil.FindNeighborSmallLODChunkWithCubeInBetween(worldData, nb, gridSize, cubeLOD, requiredExtraUpdate);
                }

                foreach (var n in requiredExtraUpdate)
                {
                    AddNeighborSeamsToUpdate(n.bound);
                    chunksToFill.Add(n.bound);
                }

                foreach (var n in chunksToCreate)
                {
                    chunksToFill.Add(n);
                }

                foreach (var n in chunksToUpdate)
                {
                    chunksToFill.Add(n);
                }

                foreach (var n in chunksToFill) chunksToProcess.Add(n);
                isWorking = false;
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        });

        while (isWorking) yield return null;
        
        
        NativeHashMap<Work.NodeBoundInt, CubeData> overrideValues = new NativeHashMap<Work.NodeBoundInt, CubeData>(600000, Allocator.Persistent);
        var nodesToRecalculate = new NativeHashSet<Chunk_Node_LOD>(600000, Allocator.Persistent);
        ConcurrentDictionary<Work.NodeBound, Work2.NodeCache> cache = new ConcurrentDictionary<Work.NodeBound, Work2.NodeCache>();
        var finalNodesToRecalculate = new NativeList<Chunk_Node_LOD>(Allocator.Persistent);

        yield return unity.StartCoroutine(Work.ProcessChunks(worldData.worldDataNative, chunksToProcess, allProcessedChunks, nodesToRecalculate, overrideValues));

        List<Work.NodeBound> chunkNodesToFillHeight = new List<Work.NodeBound>();
        
        foreach (var cn in allProcessedChunks)
        {
            chunkNodesToFillHeight.Add(cn);
        }

        yield return unity.StartCoroutine(Work2.FillChunkHeightMapParallel(worldData, chunkNodesToFillHeight));

        Debug.Log("4 : " + s.ElapsedMilliseconds);
        
        yield return unity.StartCoroutine(Work2.SetCacheParallel(worldData, nodesToRecalculate, cache));

        Debug.Log("5 : " + s.ElapsedMilliseconds);

        isWorking = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                foreach (var c in cache)
                {
                    if (c.Value.node.IsLeaf)
                        finalNodesToRecalculate.Add(new Chunk_Node_LOD(c.Value.chunkBound, c.Value.node.bound, c.Value.cubeLOD));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }

            isWorking = false;
        });
        while (isWorking) yield return null;
        
        Debug.Log("6 : " + s.ElapsedMilliseconds);

        NativeList<SingleNodeData> res = new NativeList<SingleNodeData>(700000, Allocator.Persistent);
        
        yield return unity.StartCoroutine(Work.RecalculateSingleNodeResults(worldData.worldDataNative, finalNodesToRecalculate, overrideValues, res.AsParallelWriter()));

        Debug.Log("7 : " + s.ElapsedMilliseconds + ", Nodes : " + finalNodesToRecalculate.Length);
        
        yield return unity.StartCoroutine(Work2.FillNodeDataParallel(res, cache));

        Debug.Log("8 : " + s.ElapsedMilliseconds);

        yield return unity.StartCoroutine(Work2.CalculateVerticesParallel(worldData, chunkNodesToFillHeight));

        Debug.Log("9 : " + s.ElapsedMilliseconds);
        
        chunksToProcess.Dispose();
        overrideValues.Dispose();
        nodesToRecalculate.Dispose();
        finalNodesToRecalculate.Dispose();
        res.Dispose();
    }
    IEnumerator _GenerateMeshData(UnityWorld unity)
    {
        List<NodeBound> allChunks = ListPool<NodeBound>.Get();
        List<Tuple<NodeBound, int>> neighborSeams = ListPool<Tuple<NodeBound, int>>.Get();
        allChunks.Clear();
        foreach (var nb in allProcessedChunks)
            allChunks.Add(nb);
        neighborSeams.Clear();
        foreach (var t in neighborSeamsToUpdate)
            neighborSeams.Add(t);
        
        yield return unity.StartCoroutine(Work2.GenerateMeshDataParallel(worldData, allChunks, neighborSeams));
        
        allChunks.Clear();
        neighborSeams.Clear();
        ListPool<NodeBound>.Release(allChunks);
        ListPool<Tuple<NodeBound, int>>.Release(neighborSeams);
        
    }
    
    public IEnumerator TriggerChunkUpdate(UnityWorld unity, Vector3 playerPos)
    {
        IsUpdatingWorldData = true;
        ResetChunkHashSets();
        
        s.Restart();
        bool isWorking = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                UpdateOctrees(playerPos);
                UpdateChunkNodes(playerPos);
                isWorking = false;
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        });
        while (isWorking) yield return null;
        
        UnityWorld.updateChunkTime = s.ElapsedMilliseconds;
        s.Stop();
        
        yield return unity.StartCoroutine(GenerateChunkData(unity));
        
        
        IsUpdatingWorldData = false;
    }
    public IEnumerator ReloadAll(UnityWorld unity)
    {
        ResetChunkHashSets();
        
        foreach (var c in worldData.AllChunkNodes)
        {
            chunksToUpdate.Add(c.Key);
            AddNeighborSeamsToUpdate(c.Key);
        }
        
        yield return unity.StartCoroutine(GenerateChunkData(unity));
        
    }
    public void TriggerSingleChunkUpdate(UnityWorld unity, List<NodeBound> chunksToUpdate)
    {
        s.Restart();
        IsUpdatingSingleChunkData = true;

        ResetChunkHashSets();
        Work.Initialize();
        NativeHashMap<Work.NodeBoundInt, CubeData> overrideValues = new NativeHashMap<Work.NodeBoundInt, CubeData>(20000, Allocator.Persistent);
        var nodesToRecalculate = new NativeHashSet<Chunk_Node_LOD>(30000, Allocator.Persistent);
        NativeList<Work.NodeBound> chunksToProcess = new NativeList<Work.NodeBound>(Allocator.Persistent);
        var finalNodesToRecalculate = new NativeList<Chunk_Node_LOD>(Allocator.Persistent);
        NativeList<SingleNodeData> res = new NativeList<SingleNodeData>(30000, Allocator.Persistent);
        
        ConcurrentDictionary<Work.NodeBound, Work2.NodeCache> cache = new ConcurrentDictionary<Work.NodeBound, Work2.NodeCache>();
        List<Work.NodeBound> chunkNodesToFillHeight = new List<Work.NodeBound>();

        foreach (var c in chunksToUpdate)
        {
            chunksToProcess.Add(c);
            AddNeighborSeamsToUpdate(c);
        }
        
        Work.ProcessChunksInstant(worldData.worldDataNative, chunksToProcess, allProcessedChunks, nodesToRecalculate, overrideValues);
        
        foreach (var cn in allProcessedChunks)
        {
            chunkNodesToFillHeight.Add(cn);
        }

        Work2.FillChunkHeightMapInstant(worldData, chunkNodesToFillHeight);
        
        Work2.SetCacheInstant(worldData, nodesToRecalculate, cache);
        
        foreach (var c in cache)
        {
            if(c.Value.node.IsLeaf) finalNodesToRecalculate.Add(new Chunk_Node_LOD(c.Value.chunkBound, c.Value.node.bound, c.Value.cubeLOD));
        }
        
        Work.RecalculateSingleNodeResultsInstant(worldData.worldDataNative, finalNodesToRecalculate, overrideValues, res.AsParallelWriter());
        
        Work2.FillNodeDataInstant(res, cache);
        
        Work2.CalculateVerticesInstant(worldData, chunkNodesToFillHeight);
        

        overrideValues.Dispose();
        chunksToProcess.Dispose();
        nodesToRecalculate.Dispose();
        finalNodesToRecalculate.Dispose();
        res.Dispose();
        
        
        List<NodeBound> allChunks = ListPool<NodeBound>.Get();
        List<Tuple<NodeBound, int>> neighborSeams = ListPool<Tuple<NodeBound, int>>.Get();
        allChunks.Clear();
        foreach (var nb in allProcessedChunks)
            allChunks.Add(nb);
        neighborSeams.Clear();
        foreach (var t in neighborSeamsToUpdate)
            neighborSeams.Add(t);
        
        Work2.GenerateMeshDataInstant(worldData, allChunks, neighborSeams);
        
        allChunks.Clear();
        neighborSeams.Clear();
        ListPool<NodeBound>.Release(allChunks);
        ListPool<Tuple<NodeBound, int>>.Release(neighborSeams);
        
        IsUpdatingSingleChunkData = false;
        UnityWorld.placeBlockTime = s.ElapsedMilliseconds;
        s.Stop();
    }
    
    public void UpdateWorld(UnityWorld unity)
    {
        if (!IsUpdating)
        {
            if (worldUpdateQueue.Count > 0)
            {
                var v = worldUpdateQueue.Dequeue();
                
                unity.StartCoroutine(TriggerChunkUpdate(unity, v));
            }
        }
        
        if (!IsUpdating)
        {
            if (placedBlockUpdateQueue.Count > 0 || placedSphereUpdateQueue.Count > 0)
            {
                HashSet<NodeBound> chunksTP = HashSetPool<NodeBound>.Get();
                chunksTP.Clear();
                
                chunkToProcess.Clear();
                foreach (var wPos in placedBlockUpdateQueue)
                {
                    var w1 = CubeUtil.GetWorldPos(CubeUtil.GetCubePos(wPos));
                    
                    Vector3Int cid = OctreeUtil.GetNodeID(w1, 0);
                    Vector3Int cid2 = OctreeUtil.GetNodeID(w1 + Vector3.one * CubeParam.CubeSize, 0);

                    for (int i = cid.x; i <= cid2.x; i++)
                    {
                        for (int j = cid.y; j <= cid2.y; j++)
                        {
                            for (int k = cid.z; k <= cid2.z; k++)
                            {
                                var c = worldData.FindChunkNode(new NodeID(i, j, k, 0));

                                Debug.Assert(c != null);

                                chunksTP.Add(c.bound);
                            }
                        }
                    }
                }
                placedBlockUpdateQueue.Clear();

                foreach (var sphere in placedSphereUpdateQueue)
                {
                    var minpos = new Vector3(sphere.x, sphere.y, sphere.z) - Vector3.one * sphere.w;
                    var maxpos = new Vector3(sphere.x, sphere.y, sphere.z) + Vector3.one * sphere.w;

                    Vector3Int cid = OctreeUtil.GetNodeID(minpos, 0);
                    Vector3Int cid2 = OctreeUtil.GetNodeID(maxpos, 0);

                    for (int i = cid.x; i <= cid2.x; i++)
                    {
                        for (int j = cid.y; j <= cid2.y; j++)
                        {
                            for (int k = cid.z; k <= cid2.z; k++)
                            {
                                var c = worldData.FindChunkNode(new NodeID(i, j, k, 0));

                                Debug.Assert(c != null);

                                chunksTP.Add(c.bound);
                            }
                        }
                    }
                }
                placedSphereUpdateQueue.Clear();

                foreach (var b in chunksTP)
                    chunkToProcess.Add(b);
                
                chunksTP.Clear();
                HashSetPool<NodeBound>.Release(chunksTP);
                
                TriggerSingleChunkUpdate(unity, chunkToProcess);
            }
        }
    }
}

