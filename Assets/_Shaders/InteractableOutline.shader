Shader "Cyrulik/InteractableOutline"
{
    Properties
    {
        [Header(Outline Settings)]
        [HDR] _OutlineColor("Outline Color", Color) = (1.5, 1.1, 0.3, 1.0) // Ciepły bursztynowy złoty HDR
        _OutlineThickness("Outline Thickness", Range(0.001, 0.08)) = 0.015
        _OutlineIntensity("Outline Intensity", Range(0.0, 3.0)) = 1.0
        
        [Header(Gleam and Pulse)]
        _PulseSpeed("Gleam Speed", Range(0.0, 10.0)) = 3.0
        _PulseAmount("Gleam Pulse Amount", Range(0.0, 0.5)) = 0.15
        
        [Header(Render State)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4 // LEqual by default
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
        }

        Pass
        {
            Name "OutlinePass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _OutlineIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                float3 camPosWS = GetCameraPositionWS();
                float distToCam = length(camPosWS - posWS);

                // Kompensacja odległości kamery, aby grubość obwódki była stała w pikselach
                float distFactor = clamp(distToCam * 0.12, 0.4, 2.5);
                float thickness = _OutlineThickness * distFactor;

                posWS += normWS * thickness;

                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS   = normWS;
                output.viewDirWS  = normalize(camPosWS - posWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if (_OutlineIntensity <= 0.001)
                    discard;

                // Subtelne lśnienie / pulsowanie blasku (gleam effect)
                float pulse = 1.0;
                if (_PulseSpeed > 0.01)
                {
                    pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                }

                half4 col = _OutlineColor * (_OutlineIntensity * pulse);
                col.a = saturate(_OutlineColor.a * _OutlineIntensity);

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
