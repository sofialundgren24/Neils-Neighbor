// Neighborhood Garden – stiliserad palett-shader för URP (Unity 6 / URP 17).
//
// Färg: T_Palette_256 via UV0.
// Vertexfärg: R = bakad AO, G = vind-vikt (växter), B = mask för spelarens målarfärg.
// Ljus: Half-Lambert -> mjuk ramp i N steg, färgad (blåtonad) skugga, rim light, gradient-ambient.
// Spelparametrar (sätts via MaterialPropertyBlock från PlaceableView):
//   _Desaturate (0..1)  grå/sliten -> full färg
//   _DirtAmount (0..1)  smutsighet
//   _PaintColor (rgb + a = styrka) spelarens valda målarfärg på B-maskade ytor
Shader "NG/Stylized"
{
    Properties
    {
        [MainTexture] _BaseMap ("Palette", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)

        [Header(Ramp)]
        _RampSteps ("Ramp Steps", Range(2, 5)) = 3
        _RampSmooth ("Ramp Smoothness", Range(0.01, 0.5)) = 0.08

        [Header(Shadow and Ambient)]
        _ShadowTint ("Shadow Tint", Color) = (0.416, 0.482, 0.851, 1)
        _ShadowStrength ("Shadow Tint Strength", Range(0, 1)) = 0.35
        _ShadowBrightness ("Shadow Brightness", Range(0, 1)) = 0.6
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.35
        _AOStrength ("Vertex AO Strength", Range(0, 1)) = 1

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.9, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.15
        _RimPower ("Rim Power", Range(1, 8)) = 3

        [Header(Gameplay)]
        _Desaturate ("Desaturate (Decay)", Range(0, 1)) = 0
        _DirtAmount ("Dirt Amount", Range(0, 1)) = 0
        _DirtColor ("Dirt Color", Color) = (0.43, 0.41, 0.37, 1)
        _PaintColor ("Paint Color (A = amount)", Color) = (1, 1, 1, 0)

        [Header(Wind)]
        [Toggle(_WIND)] _Wind ("Wind (foliage)", Float) = 0
        _WindStrength ("Wind Strength", Range(0, 0.3)) = 0.06
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.6

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _RampSteps;
            half _RampSmooth;
            half4 _ShadowTint;
            half _ShadowStrength;
            half _ShadowBrightness;
            half _AmbientStrength;
            half _AOStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _Desaturate;
            half _DirtAmount;
            half4 _DirtColor;
            half4 _PaintColor;
            half _WindStrength;
            half _WindSpeed;
        CBUFFER_END

        float3 ApplyWind(float3 positionWS, half windWeight)
        {
        #if defined(_WIND)
            float t = _Time.y * _WindSpeed;
            float phase = positionWS.x * 0.7 + positionWS.z * 0.5;
            float2 sway = float2(sin(t + phase), sin(t * 0.73 + phase * 1.3 + 1.7)) * 0.5
                        + float2(sin(t * 2.3 + phase * 3.1), cos(t * 1.9 + phase * 2.7)) * 0.15;
            positionWS.xz += sway * _WindStrength * windWeight;
        #endif
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma shader_feature_local _WIND
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  color      : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = ApplyWind(positionWS, input.color.g);

                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Ramp(half x)
            {
                half steps = max(_RampSteps, 2.0h);
                half v = saturate(x) * (steps - 1.0h);
                half band = floor(v);
                half f = v - band;
                return saturate((band + smoothstep(0.5h - _RampSmooth, 0.5h + _RampSmooth, f)) / (steps - 1.0h));
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                // Spelarens målarfärg på maskade ytor (vertexfärg B). Behåll palettens ljusgradient.
                half paintMask = input.color.b * _PaintColor.a;
                half lumaBase = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                half3 painted = _PaintColor.rgb * saturate(0.6h + lumaBase * 0.5h);
                albedo = lerp(albedo, painted, paintMask);

                // Förfall: smuts + avmättning.
                albedo = lerp(albedo, albedo * _DirtColor.rgb * 1.4h, _DirtAmount);
                half luma = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                albedo = lerp(albedo, luma.xxx * 0.92h, _Desaturate);

                half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half halfLambert = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;
                half lightAmount = min(Ramp(halfLambert), mainLight.shadowAttenuation);

                half3 ambient = SampleSH(normalWS);
                half3 shadowTone = lerp(half3(1, 1, 1), _ShadowTint.rgb, _ShadowStrength) * _ShadowBrightness;

                half3 lit = albedo * mainLight.color;
                half3 shade = albedo * shadowTone;
                half3 color = lerp(shade, lit, lightAmount) + albedo * ambient * _AmbientStrength;

                // Bakad AO (vertexfärg R) och ev. SSAO (desktop).
                half ao = lerp(1.0h, input.color.r, _AOStrength);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ssao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    ao *= ssao.indirectAmbientOcclusion;
                #endif
                color *= ao;

                // Rim light ger leksakssiluett.
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower);
                color += _RimColor.rgb * fresnel * _RimStrength * (0.4h + 0.6h * lightAmount);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _WIND
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color.g);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _WIND
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color.g);
                return TransformWorldToHClip(positionWS);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _WIND
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color.g);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
