Shader "Custom/Terrain"
{
    Properties
    {
        _TerrainAlbedo ("Texture", 2D) = "white" {}
        _TerrainAlbedo2 ("Texture2", 2D) = "white" {}
        _BlockAlbedo ("Block Texture", 2D) = "white" {}
        _TerrainNormal ("Normal", 2D) = "bump" {}
        _TerrainNormal2 ("Normal2", 2D) = "bump" {}
        _BlockNormal ("Block Normal", 2D) = "bump" {}
        _TerrainHeight ("Height", 2D) = "white" {}
        _TerrainHeight2 ("Height2", 2D) = "white" {}
        _BlockHeight ("Block Height", 2D) = "white" {}
    	_Smoothness("Smoothness", Range(0,1)) = 0.0
		_Metallic("Metallic", Range(0,1)) = 0.0
		_TerrainScale("Terrain Scale", Float) = 0.1
		_BumpScale("Bump Scale", Float) = 1
		_HeightScale("Height Scale", Float) = 0.0001
		_Color("Color", Color) = (1,1,1,1)
        
    }
    SubShader
    {
        Tags {             
        	"RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        	}
        LOD 100

            Pass
            {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitGBufferPassVertex
            #pragma fragment LitGBufferPassFragment
            
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            
            // -------------------------------------
            // Includes

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            
            float4 _Color;
            half _Smoothness;
            half _Metallic;



            inline void InitializeStandardLitSurfaceData(float3 albedo, float alpha, out SurfaceData outSurfaceData)
            {
                outSurfaceData.alpha = alpha;
                outSurfaceData.albedo = albedo;
                
                outSurfaceData.metallic = _Metallic;
                outSurfaceData.specular = half3(0.0, 0.0, 0.0);

                outSurfaceData.smoothness = _Smoothness;
                outSurfaceData.normalTS = float3(0,0,1);
                outSurfaceData.occlusion = 1;
                outSurfaceData.emission = 0;
                
                outSurfaceData.clearCoatMask       = half(0.0);
                outSurfaceData.clearCoatSmoothness = half(0.0);
            }


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

            // keep this file in sync with LitForwardPass.hlsl

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float3 material : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
                float3 positionWS               : TEXCOORD1;
                half3 normalWS                  : TEXCOORD2;
                float4 shadowCoord              : TEXCOORD3;
                float3 material                 : TEXCOORD4;
                float3 vertexSH                 : TEXCOORD5;
                float4 positionCS               : SV_POSITION;
            };

            void InitializeInputData(Varyings input, out InputData inputData)
            {
                inputData = (InputData)0;

                inputData.positionWS = input.positionWS;

                inputData.positionCS = input.positionCS;
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                inputData.normalWS = input.normalWS;
                inputData.viewDirectionWS = viewDirWS;


                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);

                inputData.fogCoord = 0.0; // we don't apply fog in the guffer pass
                
                inputData.vertexLighting = half3(0, 0, 0);

                inputData.bakedGI = SAMPLE_GI(0, input.vertexSH, inputData.normalWS);

                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = 0;//SAMPLE_SHADOWMASK(input.staticLightmapUV);
            }

            TEXTURE2D(_TerrainAlbedo); SAMPLER(sampler_TerrainAlbedo);
            TEXTURE2D(_TerrainNormal); SAMPLER(sampler_TerrainNormal);
            TEXTURE2D(_TerrainHeight); SAMPLER(sampler_TerrainHeight);
            TEXTURE2D(_TerrainAlbedo2); SAMPLER(sampler_TerrainAlbedo2);
            TEXTURE2D(_TerrainNormal2); SAMPLER(sampler_TerrainNormal2);
            TEXTURE2D(_TerrainHeight2); SAMPLER(sampler_TerrainHeight2);
            TEXTURE2D(_BlockAlbedo); SAMPLER(sampler_BlockAlbedo);
            TEXTURE2D(_BlockNormal); SAMPLER(sampler_BlockNormal);
            TEXTURE2D(_BlockHeight); SAMPLER(sampler_BlockHeight);
            float _TerrainScale;
            float _BumpScale;
            float _HeightScale;
            
            #include "Terrain.hlsl"
            

            Varyings LitGBufferPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.material = input.material;
                
                return output;
            }


            // Used in Standard (Physically Based) shader
            FragmentOutput LitGBufferPassFragment(Varyings input)
            {
                SurfaceData surfaceData;

                float3 mat = input.material;
                float msx = mat.x % 1;
                float msy = mat.y % 1;
                float msz = mat.z % 1;
                
                float maxMat;
                if(msx > msy && msx > msz)
                    maxMat = floor(mat.x) + 0.001;
                else if(msy > msx && msy > msz)
                    maxMat = floor(mat.y) + 0.001;
                else
                {
                    maxMat = floor(mat.z) + 0.001;
                }
                
                float4 surfaceColor;
                float3 surfaceNormal;

                if(maxMat < 1.1)
                {
                    SampleAlbedoNormal(input.positionWS, input.normalWS, surfaceColor, surfaceNormal);
                }
                else
                {
                    SampleAlbedoNormalBlock(input.positionWS, input.normalWS, surfaceColor, surfaceNormal);
                }
                

                
                InitializeStandardLitSurfaceData(surfaceColor * _Color, 1, surfaceData);
                
                InputData inputData;
                InitializeInputData(input, inputData);
                inputData.normalWS = surfaceNormal;
                
                BRDFData brdfData;
                InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                MixRealtimeAndBakedGI(mainLight, surfaceNormal, inputData.bakedGI, inputData.shadowMask);
                half3 color = GlobalIllumination(brdfData, inputData.bakedGI, surfaceData.occlusion, inputData.positionWS, surfaceNormal, inputData.viewDirectionWS);
                
                return BRDFDataToGbuffer(brdfData, inputData, surfaceData.smoothness, surfaceData.emission + color, surfaceData.occlusion);
            }

            
            ENDHLSL
        }
    	
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            
            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
               // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
