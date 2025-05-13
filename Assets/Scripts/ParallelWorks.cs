using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using Debug = UnityEngine.Debug;

//Non Job parallel works
public static class Work2
{
    struct NodeRecalcTarget
    {
        public Work.NodeBound targetBound;
        public int cubeLOD;

        public NodeRecalcTarget(Work.NodeBound targetBound, int cubeLOD)
        {
            this.targetBound = targetBound;
            this.cubeLOD = cubeLOD;
        }
    }
    public struct NodeCache
    {
        public Work.NodeBound chunkBound;
        public Octree.Node node;
        public int cubeLOD;

        public NodeCache(Work.NodeBound chunkBound, Octree.Node node, int cubeLOD)
        {
            this.chunkBound = chunkBound;
            this.node = node;
            this.cubeLOD = cubeLOD;
        }
    }
    
    public static IEnumerator SetCacheParallel(WorldData worldData, NativeHashSet<Chunk_Node_LOD> dic, ConcurrentDictionary<Work.NodeBound, NodeCache> cache)
    {
        Dictionary<Work.NodeBound, List<NodeRecalcTarget>> d = DictionaryPool<Work.NodeBound, List<NodeRecalcTarget>>.Get();
        d.Clear();

        bool isWorking = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                foreach (var n in dic)
                {
                    if (d.TryGetValue(n.cnb, out var l)) l.Add(new NodeRecalcTarget(n.nb, n.cubeLOD));
                    else
                    {
                        d[n.cnb] = new List<NodeRecalcTarget> { new NodeRecalcTarget(n.nb, n.cubeLOD) };
                    }
                }

                foreach (var p in d)
                {
                    p.Value.Sort((a, b) => a.targetBound.Size.CompareTo(b.targetBound.Size));
                }
                
                
                Parallel.ForEach(
                    Partitioner.Create(d, EnumerablePartitionerOptions.NoBuffering),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    p =>
                    {
                        var chunk = p.Key;
                        var l = p.Value;

                        var cn = worldData.AllChunkNodes[new NodeBound(chunk)];

                        foreach (var nt in l)
                        {
                            var t = nt.targetBound;
                            var cubeLOD = nt.cubeLOD;

                            var n = cn.FindNodeOrParent(new NodeBound(t.MinPos, t.Size));

                            while (!Mathf.Approximately(n.size, t.Size))
                            {
                                n.Divide();

                                var childN = n.FindNodeOrParent(new NodeBound(t.MinPos, t.Size));

                                if (n.size < (cn.size / OctreeParam.ChunkSize) * OctreeParam.TerrainResMul + 0.1f)
                                {
                                    for (int i = 0; i < 8; i++)
                                    {
                                        var child = n.FindChild(i);
                                        if (child != childN)
                                        {
                                            cache[child.bound] = new NodeCache(chunk, child, cubeLOD);
                                        }
                                    }
                                }

                                n = childN;
                            }

                            if (n.IsLeaf)
                                cache[n.bound] = new NodeCache(t, n, cubeLOD);
                        }
                    });

                isWorking = false;
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        });
        while (isWorking) yield return null;

        d.Clear();
        DictionaryPool<Work.NodeBound, List<NodeRecalcTarget>>.Release(d);
    }
    public static void SetCacheInstant(WorldData worldData, NativeHashSet<Chunk_Node_LOD> dic, ConcurrentDictionary<Work.NodeBound, NodeCache> cache)
    {
        Dictionary<Work.NodeBound, List<NodeRecalcTarget>> d = DictionaryPool<Work.NodeBound, List<NodeRecalcTarget>>.Get();
        d.Clear();

        foreach (var n in dic)
        {
            if (d.TryGetValue(n.cnb, out var l)) l.Add(new NodeRecalcTarget(n.nb, n.cubeLOD));
            else
            {
                d[n.cnb] = new List<NodeRecalcTarget> { new NodeRecalcTarget(n.nb, n.cubeLOD) };
            }
        }

        foreach (var p in d)
        {
            p.Value.Sort((a, b) => a.targetBound.Size.CompareTo(b.targetBound.Size));
        }
        
        
        Parallel.ForEach(
            Partitioner.Create(d, EnumerablePartitionerOptions.NoBuffering),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            p =>
            {
                var chunk = p.Key;
                var l = p.Value;

                var cn = worldData.AllChunkNodes[new NodeBound(chunk)];

                foreach (var nt in l)
                {
                    var t = nt.targetBound;
                    var cubeLOD = nt.cubeLOD;

                    var n = cn.FindNodeOrParent(new NodeBound(t.MinPos, t.Size));

                    while (!Mathf.Approximately(n.size, t.Size))
                    {
                        n.Divide();

                        var childN = n.FindNodeOrParent(new NodeBound(t.MinPos, t.Size));

                        if (n.size < (cn.size / OctreeParam.ChunkSize) * OctreeParam.TerrainResMul + 0.1f)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                var child = n.FindChild(i);
                                if (child != childN)
                                {
                                    cache[child.bound] = new NodeCache(chunk, child, cubeLOD);
                                }
                            }
                        }

                        n = childN;
                    }

                    if (n.IsLeaf)
                        cache[n.bound] = new NodeCache(t, n, cubeLOD);
                }
            });
        
        d.Clear();
        DictionaryPool<Work.NodeBound, List<NodeRecalcTarget>>.Release(d);
    }
    
    
    public static IEnumerator FillChunkHeightMapParallel(WorldData worldData, List<Work.NodeBound> chunks)
    {
        bool isWorking = true;
        if (chunks.Count == 0) yield break;
        
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                Parallel.ForEach(
                    Partitioner.Create(0, chunks.Count),
                    (range, _) =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            var c = chunks[i];
                            var chunkNode = worldData.FindChunkNode(new NodeBound(c.x, c.y, c.z, c.w));
                            chunkNode.CollapseAll();
                            if (c.MaxPos.y > WorldTerrain.MinHeight && c.MinPos.y < WorldTerrain.MaxHeight)
                                OctreeDataFiller.FillChunkHeightMap(chunkNode, worldData);
                        }
                    });
                isWorking = false;
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        });

        while (isWorking) yield return null;
        
    }
    public static void FillChunkHeightMapInstant(WorldData worldData, List<Work.NodeBound> chunks)
    {
        if (chunks.Count == 0) return;
        Parallel.ForEach(
            Partitioner.Create(0, chunks.Count),
            (range, _) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var c = chunks[i];
                    var chunkNode = worldData.FindChunkNode(new NodeBound(c.x, c.y, c.z, c.w));
                    chunkNode.CollapseAll();
                    if (c.MaxPos.y > WorldTerrain.MinHeight && c.MinPos.y < WorldTerrain.MaxHeight)
                        OctreeDataFiller.FillChunkHeightMap(chunkNode, worldData);
                }
            });
    }

    
    public static IEnumerator FillNodeDataParallel(NativeList<SingleNodeData> res, ConcurrentDictionary<Work.NodeBound, Work2.NodeCache> cache)
    {
        bool isWorking = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                if (res.Length > 0)
                {
                    Parallel.ForEach(
                        Partitioner.Create(0, res.Length),
                        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                        (range, _) =>
                        {
                            for (int i = range.Item1; i < range.Item2; i++)
                            {
                                var r = res[i];
                                var node = cache[r.nodeB].node;

                                node.LeafNodeData.Reset();

                                for (int j = 0; j < 8; j++)
                                {
                                    node.LeafNodeData.SetCornerData(new Vector3Int(j / 4, (j % 4) / 2, j % 2), r.GetCornerSDF(j));
                                }

                                for (int j = 0; j < r.count; j++)
                                {
                                    var d = r.GetSurfaceData(j);
                                    node.LeafNodeData.AddEdgeData(new EdgeData(d.surPos, d.surNor, d.dir, d.sdfType), d.mat);
                                }

                                node.LeafNodeData.hasCubeData = r.hasCubeData;
                                node.LeafNodeData.hasSphereData = r.hasSphereData;
                                node.LeafNodeData.hasTerrainData = r.hasTerrainData;
                            }
                        });
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }

            isWorking = false;
        });

        while (isWorking) yield return null;
    }
    public static void FillNodeDataInstant(NativeList<SingleNodeData> res, ConcurrentDictionary<Work.NodeBound, Work2.NodeCache> cache)
    {
        if (res.Length == 0) return;
        
        Parallel.ForEach(
            Partitioner.Create(0, res.Length),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (range, _) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var r = res[i];
                    var node = cache[r.nodeB].node;

                    node.LeafNodeData.Reset();

                    for (int j = 0; j < 8; j++)
                    {
                        node.LeafNodeData.SetCornerData(new Vector3Int(j / 4, (j % 4) / 2, j % 2), r.GetCornerSDF(j));
                    }

                    for (int j = 0; j < r.count; j++)
                    {
                        var d = r.GetSurfaceData(j);
                        node.LeafNodeData.AddEdgeData(new EdgeData(d.surPos, d.surNor, d.dir, d.sdfType), d.mat);
                    }

                    node.LeafNodeData.hasCubeData = r.hasCubeData;
                    node.LeafNodeData.hasSphereData = r.hasSphereData;
                    node.LeafNodeData.hasTerrainData = r.hasTerrainData;
                }
            });
    }

    
    public static IEnumerator CalculateVerticesParallel(WorldData worldData, List<Work.NodeBound> chunks)
    {
        bool isWorking = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                Parallel.ForEach(
                    Partitioner.Create(0, chunks.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    (range, _) =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            var c = chunks[i];
                            var chunkNode = worldData.AllChunkNodes[new NodeBound(c.x, c.y, c.z, c.w)];
                            var perChunkQef = worldData.GetQefSolver(chunkNode.nodeID);
                            perChunkQef.InitializeFull();
                            chunkNode.GetAllDataNodes(perChunkQef.dataNodes);

                            foreach (var n in perChunkQef.dataNodes)
                            {
                                perChunkQef.InitializeBeforeCalc();
                                worldData.FillSolver(n, perChunkQef);

                                var lnd = n.LeafNodeData;

                                if (!lnd.HasSDFData)
                                    lnd.SetVertex(perChunkQef.MassPoint());
                                else if (lnd.hasCubeData && !lnd.hasTerrainData && !lnd.hasSphereData)
                                {
                                    lnd.SetVertex(CubeUtil.GetFixedCubeVertex(n.minPos));
                                }
                                else
                                    lnd.SetVertex(perChunkQef.Solve());


                                lnd.normal = perChunkQef.AvgNormal();
                            }
                        }
                    });
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }

            isWorking = false;
        });

        while (isWorking) yield return null;
    }
    public static void CalculateVerticesInstant(WorldData worldData, List<Work.NodeBound> chunks)
    {
        Parallel.ForEach(
            Partitioner.Create(0, chunks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (range, _) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var c = chunks[i];
                    var chunkNode = worldData.AllChunkNodes[new NodeBound(c.x, c.y, c.z, c.w)];
                    var perChunkQef = worldData.GetQefSolver(chunkNode.nodeID);
                    perChunkQef.InitializeFull();
                    chunkNode.GetAllDataNodes(perChunkQef.dataNodes);

                    foreach (var n in perChunkQef.dataNodes)
                    {
                        perChunkQef.InitializeBeforeCalc();
                        worldData.FillSolver(n, perChunkQef);

                        var lnd = n.LeafNodeData;

                        if (!lnd.HasSDFData)
                            lnd.SetVertex(perChunkQef.MassPoint());
                        else if (lnd.hasCubeData && !lnd.hasTerrainData && !lnd.hasSphereData)
                        {
                            lnd.SetVertex(CubeUtil.GetFixedCubeVertex(n.minPos));
                        }
                        else
                            lnd.SetVertex(perChunkQef.Solve());


                        lnd.normal = perChunkQef.AvgNormal();
                    }
                }
            });
    }

    
    static void _FilterChunkNodes(Octree.Node parentNode, int side, List<Octree.Node> chunkNodes, List<Octree.Node> filteredChunkNodes)
    {
        foreach (var n in chunkNodes)
        {
            if (side == 1)//xface
            {
                if (Mathf.Approximately(n.minPos.x, parentNode.minPos.x))
                    filteredChunkNodes.Add(n);
            }
            else if (side == 2)
            {
                if (Mathf.Approximately(n.minPos.y, parentNode.minPos.y))
                    filteredChunkNodes.Add(n);
            }
            else if (side == 3)
            {
                if (Mathf.Approximately(n.minPos.z, parentNode.minPos.z))
                    filteredChunkNodes.Add(n);
            }
            else if (side == 4)//xedge
            {
                if (Mathf.Approximately(n.minPos.y, parentNode.minPos.y) ||
                    Mathf.Approximately(n.minPos.z, parentNode.minPos.z))
                    filteredChunkNodes.Add(n);
            }
            else if (side == 5)
            {
                if (Mathf.Approximately(n.minPos.x, parentNode.minPos.x) ||
                    Mathf.Approximately(n.minPos.z, parentNode.minPos.z))
                    filteredChunkNodes.Add(n);
            }
            else if (side == 6)
            {
                if (Mathf.Approximately(n.minPos.x, parentNode.minPos.x) ||
                    Mathf.Approximately(n.minPos.y, parentNode.minPos.y))
                    filteredChunkNodes.Add(n);
            }
        }
    }
    static Octree.Node FindNodeOrParent(WorldData worldData, NodeBound bound)
    {
        var o = worldData.FindOctree(bound);
        if (o == null) return null;
        else return o.root.FindNodeOrParent(bound);
    }
    static void _GenerateSeamMeshData(WorldData worldData, Octree.Node chunkNode, ChunkMeshData meshData, int seamIndex)
    {
        var nid = chunkNode.nodeID;
        
        if (seamIndex == 1)
        {
            var nxID = nid + new NodeID(-1, 0, 0, 0);
            var nx = FindNodeOrParent(worldData, nxID);
            
            if(nx != null)
                OctreeEdgeIterator.GenerateQuadsInFace(worldData, meshData.GetMeshData(1), nx, chunkNode, 'x');
        }
        else if (seamIndex == 2)
        {
            var nyID = nid + new NodeID(0, -1, 0, 0);
            var ny = FindNodeOrParent(worldData, nyID);
            if(ny != null)
                OctreeEdgeIterator.GenerateQuadsInFace(worldData, meshData.GetMeshData(2), ny, chunkNode, 'y');
        }
        else if (seamIndex == 3)
        {
            var nzID = nid + new NodeID(0, 0, -1, 0);
            var nz = FindNodeOrParent(worldData, nzID);
            if(nz != null)
                OctreeEdgeIterator.GenerateQuadsInFace(worldData, meshData.GetMeshData(3), nz, chunkNode, 'z');
        }
        else if (seamIndex == 4)
        {
            var nx0ID = nid + new NodeID(0, 0, -1, 0);
            var nx1ID = nid;
            var nx2ID = nid + new NodeID(0, -1, -1, 0);
            var nx3ID = nid + new NodeID(0, -1, 0, 0);
            var nx0 = FindNodeOrParent(worldData, nx0ID);
            var nx1 = FindNodeOrParent(worldData, nx1ID);
            var nx2 = FindNodeOrParent(worldData, nx2ID);
            var nx3 = FindNodeOrParent(worldData, nx3ID);
            
            if(nx0 != null && nx1 != null && nx2 != null && nx3 != null)
                OctreeEdgeIterator.GenerateQuadsInEdge(worldData, meshData.GetMeshData(4), nx0, nx1, nx2, nx3, 'x');
        }
        else if (seamIndex == 5)
        {
            var ny0ID = nid + new NodeID(-1, 0, 0, 0);
            var ny1ID = nid;
            var ny2ID = nid + new NodeID(-1, 0, -1, 0);
            var ny3ID = nid + new NodeID(0, 0, -1, 0);
            var ny0 = FindNodeOrParent(worldData, ny0ID);
            var ny1 = FindNodeOrParent(worldData, ny1ID);
            var ny2 = FindNodeOrParent(worldData, ny2ID);
            var ny3 = FindNodeOrParent(worldData, ny3ID);
            if (ny0 != null && ny1 != null && ny2 != null && ny3 != null)
            {
                OctreeEdgeIterator.GenerateQuadsInEdge(worldData, meshData.GetMeshData(5), ny0, ny1, ny2, ny3, 'y');
            }
        }
        else if (seamIndex == 6)
        {
            var nz0ID = nid + new NodeID(-1, 0, 0, 0);
            var nz1ID = nid;
            var nz2ID = nid + new NodeID(-1, -1, 0, 0);
            var nz3ID = nid + new NodeID(0, -1, 0, 0);
            var nz0 = FindNodeOrParent(worldData, nz0ID);
            var nz1 = FindNodeOrParent(worldData, nz1ID);
            var nz2 = FindNodeOrParent(worldData, nz2ID);
            var nz3 = FindNodeOrParent(worldData, nz3ID);
            if(nz0 != null && nz1 != null && nz2 != null && nz3 != null)
                OctreeEdgeIterator.GenerateQuadsInEdge(worldData, meshData.GetMeshData(6), nz0, nz1, nz2, nz3, 'z');
        }
        else
        {
            Debug.LogError("??");
        }
        
    }
    public static IEnumerator GenerateMeshDataParallel(WorldData worldData, List<NodeBound> chunks, List<Tuple<NodeBound, int>> neighborSeams)
    {
        bool isRunning = true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                Parallel.ForEach(
                    Partitioner.Create(0, chunks.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    (range, _) =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            var nb = chunks[i];
                            var cn = worldData.FindChunkNode(nb);

                            var meshData = worldData.GetOctreeMeshData(cn.nodeID);
                            meshData.Initialize(cn.nodeID);

                            OctreeEdgeIterator.GenerateQuadsInNode(worldData, meshData.GetMeshData(0), cn);

                            _GenerateSeamMeshData(worldData, cn, meshData, 1);
                            _GenerateSeamMeshData(worldData, cn, meshData, 2);
                            _GenerateSeamMeshData(worldData, cn, meshData, 3);
                            _GenerateSeamMeshData(worldData, cn, meshData, 4);
                            _GenerateSeamMeshData(worldData, cn, meshData, 5);
                            _GenerateSeamMeshData(worldData, cn, meshData, 6);
                        }
                    });
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }

            isRunning = false;
        });

        while (isRunning) yield return null;


        isRunning = true;

        HashSet<Tuple<Octree.Node, int>> finalSeamCalcNodeH = HashSetPool<Tuple<Octree.Node, int>>.Get();
        List<Tuple<Octree.Node, int>> finalSeamCalcNode = ListPool<Tuple<Octree.Node, int>>.Get();
        var chunkNodes = OctreePool.GetNodeList();
        var filteredChunkNodes = OctreePool.GetNodeList();
        var nq = OctreePool.GetNodeQueue();
        
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                foreach(var t in neighborSeams)
                {
                    var nb = t.Item1;
                    var n = FindNodeOrParent(worldData, nb);
                    int seamMeshIndex = t.Item2;

                    if (n == null) continue; // octree not loaded

                    var cn = n.FindParentChunkNode();
                    
                    //If chunk node is above n, recalculate seams from that chunk node
                    //If not, recalculate from all chunk nodes under n that makes the required seam
                    if (cn != null)
                    {
                        finalSeamCalcNodeH.Add(new Tuple<Octree.Node, int>(cn, seamMeshIndex));
                    }
                    else
                    {
                        nq.Clear();
                        chunkNodes.Clear();
                        filteredChunkNodes.Clear();

                        n.FindAllChunkNodes(chunkNodes, nq);
                        _FilterChunkNodes(n, seamMeshIndex, chunkNodes, filteredChunkNodes);

                        foreach(var sn in filteredChunkNodes)
                        {
                            finalSeamCalcNodeH.Add(new Tuple<Octree.Node, int>(sn, seamMeshIndex));
                        }
                    }
                }

                foreach (var h in finalSeamCalcNodeH) finalSeamCalcNode.Add(h);

                if (finalSeamCalcNode.Count > 0)
                {
                    Parallel.ForEach(
                        Partitioner.Create(0, finalSeamCalcNode.Count),
                        new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                        (range, _) =>
                        {
                            for (int i = range.Item1; i < range.Item2; i++)
                            {
                                var t = finalSeamCalcNode[i];
                                var node = t.Item1;
                                int seamIndex = t.Item2;

                                var meshData = worldData.GetOctreeMeshData(node.nodeID);
                                meshData.GetMeshData(seamIndex).Clear();
                                _GenerateSeamMeshData(worldData, node, meshData, seamIndex);
                            }
                        });
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
 
            isRunning = false;
        });

        while (isRunning) yield return null;

        chunkNodes.Clear();
        filteredChunkNodes.Clear();
        nq.Clear();
        OctreePool.ReturnNodeQueue(nq);
        OctreePool.ReturnNodeList(chunkNodes);
        OctreePool.ReturnNodeList(filteredChunkNodes);
        finalSeamCalcNode.Clear();
        finalSeamCalcNodeH.Clear();
        HashSetPool<Tuple<Octree.Node, int>>.Release(finalSeamCalcNodeH);
        ListPool<Tuple<Octree.Node, int>>.Release(finalSeamCalcNode);
    }
    public static void GenerateMeshDataInstant(WorldData worldData, List<NodeBound> chunks, List<Tuple<NodeBound, int>> neighborSeams)
    {
        Parallel.ForEach(
            Partitioner.Create(0, chunks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            (range, _) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var nb = chunks[i];
                    var cn = worldData.FindChunkNode(nb);

                    var meshData = worldData.GetOctreeMeshData(cn.nodeID);
                    meshData.Initialize(cn.nodeID);

                    OctreeEdgeIterator.GenerateQuadsInNode(worldData, meshData.GetMeshData(0), cn);

                    _GenerateSeamMeshData(worldData, cn, meshData, 1);
                    _GenerateSeamMeshData(worldData, cn, meshData, 2);
                    _GenerateSeamMeshData(worldData, cn, meshData, 3);
                    _GenerateSeamMeshData(worldData, cn, meshData, 4);
                    _GenerateSeamMeshData(worldData, cn, meshData, 5);
                    _GenerateSeamMeshData(worldData, cn, meshData, 6);
                }
            });



        HashSet<Tuple<Octree.Node, int>> finalSeamCalcNodeH = HashSetPool<Tuple<Octree.Node, int>>.Get();
        List<Tuple<Octree.Node, int>> finalSeamCalcNode = ListPool<Tuple<Octree.Node, int>>.Get();
        var chunkNodes = OctreePool.GetNodeList();
        var filteredChunkNodes = OctreePool.GetNodeList();
        var nq = OctreePool.GetNodeQueue();
        
        
        foreach(var t in neighborSeams)
        {
            var nb = t.Item1;
            var n = FindNodeOrParent(worldData, nb);
            int seamMeshIndex = t.Item2;

            if (n == null) continue; // octree not loaded

            var cn = n.FindParentChunkNode();

            if (cn != null)
            {
                finalSeamCalcNodeH.Add(new Tuple<Octree.Node, int>(cn, seamMeshIndex));
            }
            else
            {
                nq.Clear();
                chunkNodes.Clear();
                filteredChunkNodes.Clear();

                n.FindAllChunkNodes(chunkNodes, nq);
                _FilterChunkNodes(n, seamMeshIndex, chunkNodes, filteredChunkNodes);

                foreach(var sn in filteredChunkNodes)
                {
                    finalSeamCalcNodeH.Add(new Tuple<Octree.Node, int>(sn, seamMeshIndex));
                }
            }
        }

        foreach (var h in finalSeamCalcNodeH) finalSeamCalcNode.Add(h);

        if (finalSeamCalcNode.Count > 0)
        {
            Parallel.ForEach(
                Partitioner.Create(0, finalSeamCalcNode.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                (range, _) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        var t = finalSeamCalcNode[i];
                        var node = t.Item1;
                        int seamIndex = t.Item2;

                        var meshData = worldData.GetOctreeMeshData(node.nodeID);
                        meshData.GetMeshData(seamIndex).Clear();
                        _GenerateSeamMeshData(worldData, node, meshData, seamIndex);
                    }
                });
        }

        chunkNodes.Clear();
        filteredChunkNodes.Clear();
        nq.Clear();
        OctreePool.ReturnNodeQueue(nq);
        OctreePool.ReturnNodeList(chunkNodes);
        OctreePool.ReturnNodeList(filteredChunkNodes);
        finalSeamCalcNode.Clear();
        finalSeamCalcNodeH.Clear();
        HashSetPool<Tuple<Octree.Node, int>>.Release(finalSeamCalcNodeH);
        ListPool<Tuple<Octree.Node, int>>.Release(finalSeamCalcNode);
    }
    
}