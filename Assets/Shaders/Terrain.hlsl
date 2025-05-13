float3 TriplanarBlend(float3 normal)
{
    float3 blend = abs(normal);
    blend = pow(blend, 4.0);
    return blend / (blend.x + blend.y + blend.z);
}

float2 ParallaxOffset1(float2 uv, float3 viewDir)
{
    float height = SAMPLE_TEXTURE2D(_TerrainHeight, sampler_TerrainHeight, uv).r;
    float offset = (height - 0.5) * _HeightScale;

    float2 offsetUV = viewDir.xy * offset;
    
    return uv + offsetUV;
}
float2 ParallaxOffset2(float2 uv, float3 viewDir)
{
    float height = SAMPLE_TEXTURE2D(_TerrainHeight2, sampler_TerrainHeight2, uv).r;
    float offset = (height - 0.5) * _HeightScale;

    float2 offsetUV = viewDir.xy * offset;

    return uv + offsetUV;
}
float2 ParallaxOffsetBlock(float2 uv, float3 viewDir)
{
    float height = SAMPLE_TEXTURE2D(_BlockHeight, sampler_BlockHeight, uv).r;
    float offset = (height - 0.5) * _HeightScale;

    float2 offsetUV = viewDir.xy * offset;
    
    return uv + offsetUV;
}

float4 SampleTriplanarAlbedo(float3 positionWS, float3 normalWS, float scaleMul)
{
    float scale = _TerrainScale * scaleMul;
    float3 blend = TriplanarBlend(normalWS);
    float3 scalePos = positionWS * scale;
    float3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);

    
    if((normalWS.y) > 0.5)
    {
        float2 uvX = ParallaxOffset1(scalePos.yz, viewDir.yzx);
        float2 uvY = ParallaxOffset1(scalePos.xz, viewDir.xzy);
        float2 uvZ = ParallaxOffset1(scalePos.xy, viewDir.xyz);
        float4 x = SAMPLE_TEXTURE2D(_TerrainAlbedo, sampler_TerrainAlbedo, uvX);
        float4 y = SAMPLE_TEXTURE2D(_TerrainAlbedo, sampler_TerrainAlbedo, uvY);
        float4 z = SAMPLE_TEXTURE2D(_TerrainAlbedo, sampler_TerrainAlbedo, uvZ);

        return x * blend.x + y * blend.y + z * blend.z;
    }
    else
    {
        float2 uvX = ParallaxOffset2(scalePos.yz, viewDir.yzx);
        float2 uvY = ParallaxOffset2(scalePos.xz, viewDir.xzy);
        float2 uvZ = ParallaxOffset2(scalePos.xy, viewDir.xyz);
        float4 x = SAMPLE_TEXTURE2D(_TerrainAlbedo2, sampler_TerrainAlbedo2, uvX);
        float4 y = SAMPLE_TEXTURE2D(_TerrainAlbedo2, sampler_TerrainAlbedo2, uvY);
        float4 z = SAMPLE_TEXTURE2D(_TerrainAlbedo2, sampler_TerrainAlbedo2, uvZ);
        return x * blend.x + y * blend.y + z * blend.z;
    }
}

float3 SampleTriplanarNormal(float3 positionWS, float3 normalWS, float scaleMul)
{
    float3 blend = TriplanarBlend(normalWS);
    float scale = _TerrainScale * scaleMul; // Texture scale
    float3 scalePos = positionWS * scale;
    float3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);

    if((normalWS.y) > 0.5)
    {
        float2 uvX = ParallaxOffset1(scalePos.yz, viewDir.yzx);
        float2 uvY = ParallaxOffset1(scalePos.xz, viewDir.xzy);
        float2 uvZ = ParallaxOffset1(scalePos.xy, viewDir.xyz);
        float3 xp = UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainNormal, sampler_TerrainNormal, uvX), _BumpScale);
        float3 yp = UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainNormal, sampler_TerrainNormal, uvY), _BumpScale);
        float3 zp = UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainNormal, sampler_TerrainNormal, uvZ), _BumpScale);
    
        float3 axisSign = sign(normalWS);
        xp.z *= axisSign.x;
        yp.z *= axisSign.y;
        zp.z *= axisSign.z;
	 
        return normalize(xp.zyx * blend.x + yp.xzy * blend.y + zp.xyz * blend.z);
    }
    else
    {
        float2 uvX = ParallaxOffset2(scalePos.yz, viewDir.yzx);
        float2 uvY = ParallaxOffset2(scalePos.xz, viewDir.xzy);
        float2 uvZ = ParallaxOffset2(scalePos.xy, viewDir.xyz);
        float3 xp = UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainNormal2, sampler_TerrainNormal2, uvX), _BumpScale);
        float3 yp = UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainNormal2, sampler_TerrainNormal2, uvY), _BumpScale);
        float3 zp = UnpackNormalScale(SAMPLE_TEXTURE2D(_TerrainNormal2, sampler_TerrainNormal2, uvZ), _BumpScale);
    
        float3 axisSign = sign(normalWS);
        xp.z *= axisSign.x;
        yp.z *= axisSign.y;
        zp.z *= axisSign.z;
	 
        return normalize(xp.zyx * blend.x + yp.xzy * blend.y + zp.xyz * blend.z);
    }
}

void SampleAlbedoNormal(float3 positionWS, float3 normalWS, out float4 color, out float3 normal)
{
    float4 baseColor = SampleTriplanarAlbedo(positionWS, normalWS, 1.0);

    color = baseColor;
    float3 baseNormal = SampleTriplanarNormal(positionWS, normalWS, 1.0);

    normal = baseNormal;
}


float4 SampleTriplanarAlbedoBlock(float3 positionWS, float3 normalWS, float scaleMul)
{
    float scale = _TerrainScale * scaleMul;
    float3 blend = TriplanarBlend(normalWS);
    float3 scalePos = positionWS * scale;
    float3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);

    
    float2 uvX = ParallaxOffsetBlock(scalePos.yz, viewDir.yzx);
    float2 uvY = ParallaxOffsetBlock(scalePos.xz, viewDir.xzy);
    float2 uvZ = ParallaxOffsetBlock(scalePos.xy, viewDir.xyz);
    float4 x = SAMPLE_TEXTURE2D(_BlockAlbedo, sampler_BlockAlbedo, uvX);
    float4 y = SAMPLE_TEXTURE2D(_BlockAlbedo, sampler_BlockAlbedo, uvY);
    float4 z = SAMPLE_TEXTURE2D(_BlockAlbedo, sampler_BlockAlbedo, uvZ);
    return x * blend.x + y * blend.y + z * blend.z;
    
}

float3 SampleTriplanarNormalBlock(float3 positionWS, float3 normalWS, float scaleMul)
{
    float3 blend = TriplanarBlend(normalWS);
    float scale = _TerrainScale * scaleMul;
    float3 scalePos = positionWS * scale;
    float3 viewDir = normalize(_WorldSpaceCameraPos - positionWS);
    
    float2 uvX = ParallaxOffsetBlock(scalePos.yz, viewDir.yzx);
    float2 uvY = ParallaxOffsetBlock(scalePos.xz, viewDir.xzy);
    float2 uvZ = ParallaxOffsetBlock(scalePos.xy, viewDir.xyz);
    float3 xp = UnpackNormalScale(SAMPLE_TEXTURE2D(_BlockNormal, sampler_BlockNormal, uvX), _BumpScale);
    float3 yp = UnpackNormalScale(SAMPLE_TEXTURE2D(_BlockNormal, sampler_BlockNormal, uvY), _BumpScale);
    float3 zp = UnpackNormalScale(SAMPLE_TEXTURE2D(_BlockNormal, sampler_BlockNormal, uvZ), _BumpScale);

    float3 axisSign = sign(normalWS);
    xp.z *= axisSign.x;
    yp.z *= axisSign.y;
    zp.z *= axisSign.z;
 
    return normalize(xp.zyx * blend.x + yp.xzy * blend.y + zp.xyz * blend.z);
}

void SampleAlbedoNormalBlock(float3 positionWS, float3 normalWS, out float4 color, out float3 normal)
{

    float4 baseColor = SampleTriplanarAlbedoBlock(positionWS, normalWS, 1.0);

    color = baseColor;
    float3 baseNormal = SampleTriplanarNormalBlock(positionWS, normalWS, 1.0);

    normal = baseNormal;
}