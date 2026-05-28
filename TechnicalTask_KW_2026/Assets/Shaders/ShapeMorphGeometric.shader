Shader "TechnicalTask/ShapeMorphGeometric"
{
    Properties
    {
        _Color              ("Tint",                Color)      = (1,1,1,1)
        _MorphCurrent       ("Current Shape (0=tri,1=hex,2=cir,3=sqr)", Range(0,3)) = 0
        _MorphNext          ("Next Shape",          Range(0,3)) = 0
        _MorphT             ("Transition T",        Range(0,1)) = 0
        _PulseAmount        ("Pulse Amount",        Range(0,1)) = 0
        _PulseFrequency     ("Pulse Frequency Hz",  Float)      = 0.5
        _PulseBrightSwing   ("Pulse Brightness Swing", Range(0,1)) = 0.15
        _EdgeSoftness       ("Edge Softness (px)",  Range(0.0, 4.0)) = 1.0
        [Toggle] _UseBakedMorphTargets ("Use Baked Morph Targets (off for blendshape mesh)", Float) = 1
        [Toggle] _UseStrokeSideAA      ("Use Stroke-Side Edge AA (needs TEXCOORD5)",        Float) = 1
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
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 triTarget  : TEXCOORD1;
                float2 hexTarget  : TEXCOORD2;
                float2 cirTarget  : TEXCOORD3;
                float2 sqrTarget  : TEXCOORD4;
                float2 strokeSide : TEXCOORD5;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float  strokeSide : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _MorphCurrent;
                float  _MorphNext;
                float  _MorphT;
                float  _PulseAmount;
                float  _PulseFrequency;
                float  _PulseBrightSwing;
                float  _EdgeSoftness;
                float  _UseBakedMorphTargets;
                float  _UseStrokeSideAA;
            CBUFFER_END

            #define PI 3.14159265

            float2 PickTarget(float idx, float2 tri, float2 hex, float2 cir, float2 sqr)
            {
                if (idx < 0.5) return tri;
                if (idx < 1.5) return hex;
                if (idx < 2.5) return cir;
                return sqr;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float2 morphed = lerp(
                    PickTarget(_MorphCurrent, IN.triTarget, IN.hexTarget, IN.cirTarget, IN.sqrTarget),
                    PickTarget(_MorphNext,    IN.triTarget, IN.hexTarget, IN.cirTarget, IN.sqrTarget),
                    _MorphT);

                float useBaked = step(0.5, _UseBakedMorphTargets);
                float3 posOS = lerp(IN.positionOS.xyz, float3(morphed, 0.0), useBaked);
                OUT.positionCS = TransformObjectToHClip(posOS);

                OUT.color      = _Color;
                OUT.strokeSide = lerp(0.5, IN.strokeSide.x, step(0.5, _UseStrokeSideAA));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = IN.color;
                float pulse = _PulseBrightSwing * sin(_Time.y * 2.0 * PI * _PulseFrequency);
                col.rgb *= lerp(1.0, 1.0 + pulse, _PulseAmount);

                float s = IN.strokeSide;
                float aaWidth = fwidth(s) * _EdgeSoftness;
                float innerAA = smoothstep(0.0, aaWidth, s);
                float outerAA = 1.0 - smoothstep(1.0 - aaWidth, 1.0, s);
                float aa = innerAA * outerAA;
                col.a *= lerp(1.0, aa, step(0.5, _UseStrokeSideAA));

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
