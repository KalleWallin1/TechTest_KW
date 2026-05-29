Shader "TechnicalTask/ShapeMorphGeometric"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)

        // Width of the smoothstep alpha fade at each border, in UV.x units.
        // The inner border sits at uv.x = 0 and the outer border at uv.x = 1,
        // so this softens both edges symmetrically for fake antialiasing.
        _EdgeSoftness ("Edge Softness (fake AA)", Range(0.0, 0.5)) = 0.05

        // Debug: when on, output the edge softness as grayscale (black at the
        // faded borders, white in the opaque interior) so the falloff is visible.
        [Toggle(_DEBUG_UV)] _DebugUV ("Debug Edge Softness", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ShapeMorphGeometric"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _DEBUG_UV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Soften both borders of the band: inner border (uv.x = 0) and
                // outer border (uv.x = 1). A zero softness gives a hard edge.
                float edge = max(_EdgeSoftness, 1e-5);
                float inner = smoothstep(0.0, edge, IN.uv.x);
                float outer = smoothstep(0.0, edge, 1.0 - IN.uv.x);
                float aa = inner * outer;

            #if defined(_DEBUG_UV)
                // Visualize the edge softness as grayscale: black at the faded
                // borders, white in the fully-opaque interior. Opaque so the
                // falloff is readable against any background.
                return half4(aa, aa, aa, 1.0);
            #endif

                // Grayscale falloff (aa) drives alpha: black borders -> transparent.
                // The white interior is multiplied by the Tint color.
                half4 col;
                col.rgb = _Color.rgb * aa;
                col.a   = aa;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
