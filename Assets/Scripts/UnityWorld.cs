using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class UnityWorld : MonoBehaviour
{
    public static bool ShowWireframe;
    
    public static float updateChunkTime;
    public static float updateDataTime;
    public static float iterateEdgeTime;
    public static float placeBlockTime;
    public static bool IsUpdatingMesh;
    
    private static Queue<GameObject> meshPool = new Queue<GameObject>();

    static GameObject GetMeshObject(Material terrainMat, Material wireframeMat)
    {
        if (meshPool.Count > 0)
        {
            return meshPool.Dequeue();
        }
        else
        {
            var g = new GameObject();
            var mf = g.AddComponent<MeshFilter>();
            var mr = g.AddComponent<MeshRenderer>();
            var mc = g.AddComponent<MeshCollider>();
            mf.sharedMesh = new Mesh();
            mf.sharedMesh.indexFormat = IndexFormat.UInt32;
            mr.sharedMaterials = new Material[] { new Material(terrainMat), wireframeMat };
            
            return g;
        }
    }
    static void ReturnMeshObject(GameObject mo)
    {
        var mf = mo.GetComponent<MeshFilter>();
        mf.sharedMesh.Clear();
        mf.sharedMesh.RecalculateBounds();
        meshPool.Enqueue(mo);
    }
    
    public GameObject player;
    public Camera cam;
    public Material terrainMat;
    public Material wireframeMat;

    public TextMeshProUGUI textChunk, textData, textEdge, textPlace, textBlockMode;

    public bool placeBlocks = false;
    
    private World world = new World();
    private bool blockMode = true;
    private float sphereSize = 2;
    
    IEnumerator Start()
    {
        textBlockMode.text = blockMode ? "Mode : Block" : "Mode : Sphere(" + sphereSize + ")";
        var c = wireframeMat.GetColor("_WireColor");
        c.a = ShowWireframe ? 1 : 0;
        wireframeMat.SetColor("_WireColor", c);

        IsUpdatingMesh = false;
        meshPool.Clear();
        world.Initialize();

        if (placeBlocks)
        {
            world.AddRandomHalfMillionCubes();
            Debug.Log("Added 0.5M cubes");
            yield return null;
        }

        player.SetActive(false);
        
        yield return StartCoroutine(world.StartWorld(this, player.transform.position));
        
        Debug.Log("World Data Loaded");
        while (!World.WorldRunning)
            yield return null;
        
        UpdateMesh();
        
        player.SetActive(true);
        
        Physics.Raycast(new Vector3(player.transform.position.x, 200, player.transform.position.z), Vector3.down, out RaycastHit hit, 400);

        player.transform.position = hit.point + Vector3.up;
    }
    private void OnDestroy()
    {
        WorldData.debug.worldDataNative.Dispose();
    }
    
    private Dictionary<int, MeshFilter> loadedMesh = new Dictionary<int, MeshFilter>();
    
    void UpdateMesh()
    {
        IsUpdatingMesh = true;
        while (world.destroyedChunkMeshIDs.Count > 0)
        {
            var mid = world.destroyedChunkMeshIDs.Dequeue();
            DestroyMesh(mid);
        }
        
        List<ChunkMeshData> meshData = new List<ChunkMeshData>();
        world.GetAllMeshData(meshData);
        foreach (var omd in meshData)
        {
            foreach (var md in omd.GetAllMeshData())//All existing mesh data
            {
                if (md.needsMeshUpdate)
                {
                    if (md.HasData)
                    {
                        CreateOrUpdateUnityMesh(md);
                    }
                    else
                    {
                        DestroyMesh(md);
                    }
                }
            }
        }
        
        IsUpdatingMesh = false;
    }

    void CreateOrUpdateUnityMesh(ChunkMeshData.MeshData md)
    {
        md.needsMeshUpdate = false;

        MeshFilter mf;
        MeshCollider mc;
        MeshRenderer mr;
        
        if (loadedMesh.ContainsKey(md.UniqueID))
        {
            var g = loadedMesh[md.UniqueID].gameObject;
            mf = g.GetComponent<MeshFilter>();
            mc = g.GetComponent<MeshCollider>();
            mr = g.GetComponent<MeshRenderer>();
        }
        else
        {
            var g = GetMeshObject(terrainMat, wireframeMat);
            mf = g.GetComponent<MeshFilter>();
            mc = g.GetComponent<MeshCollider>();
            mr = g.GetComponent<MeshRenderer>();
            g.name = ((NodeBound)md.chunkID).ToString();
        }
        
        Mesh mesh = mf.sharedMesh;
        mesh.Clear();

        mesh.SetVertices(md.vertex);
        mesh.SetNormals(md.normal);
        mesh.SetTriangles(md.tris, 0);
        mesh.SetUVs(0, md.material);
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        
        if(md.chunkID.w < 0.1f)
            mc.sharedMesh = mesh;
        
        if (md.meshNum == 1)
        {
            mr.sharedMaterial.color = new Color(1, 0.8f, 0.8f);
        }
        else if (md.meshNum == 2)
        {
            mr.sharedMaterial.color = new Color(1f, 1, 0.8f);
        }
        else if (md.meshNum == 3)
        {
            mr.sharedMaterial.color = new Color(0.8f, 0.8f, 1);
        }
        else if (md.meshNum == 4)
        {
            mr.sharedMaterial.color = new Color(1f, 0.7f, 0.7f);
        }
        else if (md.meshNum == 5)
        {
            mr.sharedMaterial.color = new Color(1f, 1f, 0.7f);
        }
        else if (md.meshNum == 6)
        {
            mr.sharedMaterial.color = new Color(0.7f, 0.7f, 1f);
        }
        else
        {
            mr.sharedMaterial.color = Color.white;
        }
        
        
        if(loadedMesh.ContainsKey(md.UniqueID) == false)
            loadedMesh.Add(md.UniqueID, mf);
    }
    void DestroyMesh(int uniqueID)
    {
        if (loadedMesh.ContainsKey(uniqueID) == false)
        {
            return;
        }

        var mf = loadedMesh[uniqueID];
        mf.sharedMesh.Clear();
        mf.sharedMesh = mf.sharedMesh;
        
        ReturnMeshObject(mf.gameObject);
        loadedMesh.Remove(uniqueID);
    }
    void DestroyMesh(ChunkMeshData.MeshData md)
    {
        if (loadedMesh.ContainsKey(md.UniqueID) == false)
        {
            return;
        }

        md.needsMeshUpdate = false;
        var mf = loadedMesh[md.UniqueID];

        mf.sharedMesh.Clear();
        mf.sharedMesh = mf.sharedMesh;
        
        ReturnMeshObject(mf.gameObject);
        loadedMesh.Remove(md.UniqueID);
    }
     
    private void Update()
    {
        if (!World.WorldRunning) return;
        
        if(!IsUpdatingMesh)
            world.OnUpdate(this, player.transform.position);
        
        if (!WorldUpdater.IsUpdating)
            UpdateMesh();

        textData.text = "Data : " + updateDataTime + "ms";
        textChunk.text = "Chunk : " + updateChunkTime + "ms";
        textEdge.text = "Edge Iteration : " + iterateEdgeTime + "ms";
        textPlace.text = "Block Update : " + placeBlockTime + "ms";
        
        if (!WorldUpdater.IsUpdating)
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                blockMode = !blockMode;
                textBlockMode.text = blockMode ? "Mode : Block" : "Mode : Sphere(" + sphereSize + ")";
            }

            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                sphereSize += Mathf.Sign(Input.mouseScrollDelta.y) * 0.5f;
                sphereSize = Mathf.Clamp(sphereSize, 0.5f, 4f);
                textBlockMode.text = blockMode ? "Mode : Block" : "Mode : Sphere(" + sphereSize + ")";
            }
            
            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 32))
                {
                    if (blockMode)
                    {
                        Vector3 wPos = hit.point + hit.normal * 0.1f;
                        world.AddCube(wPos, 2, true);
                    }
                    else
                    {
                        Vector3 wPos = hit.point;
                        wPos.x = Mathf.Floor(wPos.x / 0.25f) * 0.25f;
                        wPos.y = Mathf.Floor(wPos.y / 0.25f) * 0.25f;
                        wPos.z = Mathf.Floor(wPos.z / 0.25f) * 0.25f;
                        wPos += Vector3.one * 0.125f;
                        world.AddSphere(wPos, sphereSize, 2, true);
                    }
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 32))
                {
                    if (blockMode)
                    {
                        Vector3 wPos = hit.point - hit.normal * 0.01f;
                        world.AddCube(wPos, 0, true);
                    }
                    else
                    {
                        Vector3 wPos = hit.point;
                        world.AddSphere(wPos, sphereSize, 0, true);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                ShowWireframe = !ShowWireframe;
                var c = wireframeMat.GetColor("_WireColor");
                c.a = ShowWireframe ? 1 : 0;
                wireframeMat.SetColor("_WireColor", c);
            }
        }
    }
}
