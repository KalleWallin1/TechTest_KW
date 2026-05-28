Shader "TechnicalTask/NumberCube"
{
    Properties
    {
        _MainTex          ("Number Strip (white digit on transparent)", 2D) = "white" {}
        _Color            ("Tint",                       Color)      = (1,1,1,1)
        _PulseAmount      ("Pulse Amount",               Range(0,1)) = 0
        _PulseFrequency   ("Pulse Frequency Hz",         Float)      = 0.5
        _PulseBrightSwing ("Pulse Brightness Swing",     Range(0,1)) = 0.15
        [Toggle] _UseLuminanceAsAlpha ("Use Luminance As Alpha (for white-on-black textures)", Float) = 0
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
        Cull Back

        Pass
        {
            Name "NumberCube"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _PulseAmount;
                float  _PulseFrequency;
                float  _PulseBrightSwing;
                float  _UseLuminanceAsAlpha;
            CBUFFER_END

            #define PI 3.14159265

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float lum = max(tex.r, max(tex.g, tex.b));
                float mask = lerp(tex.a, lum, step(0.5, _UseLuminanceAsAlpha));

                half4 col = _Color;
                float pulse = _PulseBrightSwing * sin(_Time.y * 2.0 * PI * _PulseFrequency);
                col.rgb *= lerp(1.0, 1.0 + pulse, _PulseAmount);
                col.a *= mask;

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
