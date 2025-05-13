using UnityEngine;

public class WorldTerrain
{
    public virtual float GetHeight(float x, float z) 
    {
        var v = Noise.Simplex(OctreeParam.seed, 0.005f, x, z);
        
        return BaseHeight + v * Size;
    }

    public const float BaseHeight = 60;
    public const float Size = 25;

    public const float MaxHeight = BaseHeight + Size + 1;
    public const float MinHeight = BaseHeight - Size - 1;

    public virtual Vector3 GetNormal(float x, float z, float size)
    {
        return _GetNormal(x, z, 1);
    }
    
    Vector3 _GetNormal(float x, float z, float size)
    {
        float h1 = GetHeight(x - size, z);
        float h2 = GetHeight(x + size, z);
        float h3 = GetHeight(x, z - size);
        float h4 = GetHeight(x, z + size);

        Vector3 v1 = new Vector3(x - size, h1, z);
        Vector3 v2 = new Vector3(x + size, h2, z);
        Vector3 v3 = new Vector3(x, h3, z - size);
        Vector3 v4 = new Vector3(x, h4, z + size);


        Vector3 cal1 = v2 - v1;
        Vector3 cal2 = v4 - v3;

        Vector3 nor = new Vector3(cal1.y * cal2.z - cal1.z * cal2.y, cal1.z * cal2.x - cal1.x * cal2.z, cal1.x * cal2.y - cal1.y * cal2.x).normalized;
        if (nor.y < 0)
            nor *= -1;
        
        return nor;
    }
    
}
