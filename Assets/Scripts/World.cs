using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class World
{
    public static bool WorldRunning = false;

    private WorldTerrain terrain;
    private WorldUpdater updater;
    private WorldData worldData;
    private NodeID lastQueuedPlayerChunk;
    
    public Queue<int> destroyedChunkMeshIDs = new Queue<int>();
    
    public void Initialize()
    {
        CubeParam.Initialize();
        terrain = new WorldTerrain();
        worldData = new WorldData(terrain);
        
        updater = new WorldUpdater(this, worldData);
    }

    public IEnumerator StartWorld(UnityWorld unity, Vector3 startPos)
    {
        lastQueuedPlayerChunk = OctreeUtil.GetNodeID(startPos, 0);
        
        yield return unity.StartCoroutine(updater.TriggerChunkUpdate(unity, startPos));
        
        WorldRunning = true;
    }

    public void AddCube(Vector3 posInWorld, byte m, bool triggerUpdate)
    {
        CubeUtil.AddCube(worldData, posInWorld, m);
        
        if(triggerUpdate)
        {
            updater.QueueChunkUpdateBlock(posInWorld);
        }
    }
    public void AddSphere(Vector3 posInWorld, float size, byte m, bool triggerUpdate)
    {
        worldData.AddSphere(posInWorld, size, m);
        
        if(triggerUpdate)
        {
            updater.QueueChunkUpdateSphere(new Vector4(posInWorld.x, posInWorld.y, posInWorld.z, size));
        }
    }
    
    public void GetAllMeshData(List<ChunkMeshData> md)
    {
        if (worldData.octrees == null) return;
        
        foreach (var o in worldData.octrees.Values)
        {
            foreach (var cn in o.ChunkNodes)
            {
                md.Add(worldData.GetOctreeMeshData(cn.nodeID));
            }
        }
    }
    public void OnUpdate(UnityWorld unity, Vector3 playerPos)
    {
        if (!WorldRunning) return;
        
        if (!WorldUpdater.IsUpdating)
        {
            var nid = OctreeUtil.GetNodeID(playerPos, 0);
            if (!nid.Equals(lastQueuedPlayerChunk))
            {
                lastQueuedPlayerChunk = nid;
                updater.QueueWorldUpdate(playerPos);
            }

            updater.UpdateWorld(unity);
        }
    }
    
    public void AddRandomHalfMillionCubes()
    {
        Vector2Int min = new Vector2Int(-1000, -1000);
        Vector2Int max = new Vector2Int(1000, 1000);
        int seed = 1011;

        Random.InitState(seed);
    
        for (int i = 0; i < 500000; i++)
        {
            int x = Random.Range(min.x, max.x + 1);
            int z = Random.Range(min.y, max.y + 1);

            float baseHeight = terrain.GetHeight(x, z);
            int yOffset = Random.Range(-1, 2);
            int y = Mathf.FloorToInt(baseHeight) + yOffset;

            int cubeType = Random.Range(0, 2) == 0 ? 0 : 2;
            
            
            AddCube(new Vector3Int(x, y, z), (byte)cubeType, false);
        }
    }
}



