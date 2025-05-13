using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkMeshData
{
    public class MeshData
    {
        static int I = 0;
        static int II = 0;
        public MeshData(int num)
        {
            meshNum = num;
            meshID = I++;
            UniqueID = II++;
        }

        public void Initialize(NodeID chunkID)
        {
            this.chunkID = chunkID;
            this.meshID = I++;
            vertex.Clear();
            normal.Clear();
            tris.Clear();
            material.Clear();
            needsMeshUpdate = true;
        }
        
        public void Clear()
        {
            //ID needs to change so cached indices somewhere in neighbor isn't used afterwards
            meshID = I++;
            int cnt = vertex.Count;
            vertex.Clear();
            normal.Clear();
            tris.Clear();
            material.Clear();
            needsMeshUpdate = cnt > 0;
            needsMeshUpdate = true;
        }

        public int UniqueID;
        
        public Vector2Int MeshID => new Vector2Int(meshID, meshNum);
        
        int meshID;
        
        public bool HasData => tris.Count > 0;
        public NodeID chunkID;
        public readonly int meshNum;
        public List<Vector3> vertex = new List<Vector3>();
        public List<Vector3> normal = new List<Vector3>();
        public List<Vector3> material = new List<Vector3>();
        
        public List<int> tris = new List<int>();

        public bool needsMeshUpdate = false;
    
        //duplicate triangle
        public void AddVertexNormal(Vector3 v, Vector3 n)
        {
            vertex.Add(v);
            normal.Add(n);
        }
        //non-duplicate triangle
        public void AddVertexNormal(Vector3 v, Vector3 n, byte mat)
        {
            vertex.Add(v);
            normal.Add(n);
            material.Add(new Vector3(mat, mat, mat));
        }
        
        //Non-duplicate triangle
        public void AddTriangleMat(int idx1, int idx2, int idx3)
        {
            needsMeshUpdate = true;

            tris.Add(idx1);
            tris.Add(idx2);
            tris.Add(idx3);
        }
        //Duplicate triangle
        public void AddTriangleMat(int idx1, int idx2, int idx3, byte mat1, byte mat2, byte mat3)
        {
            needsMeshUpdate = true;

            tris.Add(idx1);
            tris.Add(idx2);
            tris.Add(idx3);

            material.Add(new Vector3(mat1 + 0.999f, mat2, mat3));
            material.Add(new Vector3(mat1, mat2 + 0.999f, mat3));
            material.Add(new Vector3(mat1, mat2, mat3 + 0.999f));
        }
    }

    public void Initialize(NodeID chunkID)
    {
        this.chunkID = chunkID;
        main.Initialize(chunkID);
        faceX.Initialize(chunkID);
        faceY.Initialize(chunkID);
        faceZ.Initialize(chunkID);
        edgeX.Initialize(chunkID);
        edgeY.Initialize(chunkID);
        edgeZ.Initialize(chunkID);
    }
    
    public NodeID chunkID;
    MeshData main = new MeshData(0);
    MeshData faceX = new MeshData(1);
    MeshData faceY = new MeshData(2);
    MeshData faceZ = new MeshData(3);
    MeshData edgeX = new MeshData(4);
    MeshData edgeY = new MeshData(5);
    MeshData edgeZ = new MeshData(6);

    public MeshData GetMeshData(int idx)
    {
        Debug.Assert(idx >= 0 && idx <= 7);
        if (idx == 0) return main;
        if (idx == 1) return faceX;
        if (idx == 2) return faceY;
        if (idx == 3) return faceZ;
        if (idx == 4) return edgeX;
        if (idx == 5) return edgeY;
        if (idx == 6) return edgeZ;
        return null;
    }
    

    private List<MeshData> data = new List<MeshData>();
    public List<MeshData> GetAllMeshData()
    {
        data.Clear();
        if(main.needsMeshUpdate) data.Add(main);
        if(faceX.needsMeshUpdate) data.Add(faceX);
        if(faceY.needsMeshUpdate) data.Add(faceY);
        if(faceZ.needsMeshUpdate) data.Add(faceZ);
        if(edgeX.needsMeshUpdate) data.Add(edgeX);
        if(edgeY.needsMeshUpdate) data.Add(edgeY);
        if(edgeZ.needsMeshUpdate) data.Add(edgeZ);
        return data;
    }
}
