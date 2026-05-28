Shader "TechnicalTask/ShapeMorphSDF"
{
    Properties
    {
        _Color           ("Tint",                       Color)             = (1,1,1,1)
        _StrokeWidth     ("Stroke Width",               Range(0.005, 0.2)) = 0.04
        _Radius          ("Shape Radius (normalized)",  Range(0.1, 1.0))   = 0.7
        _MorphCurrent    ("Current Shape (0=tri,1=hex,2=circle,3=square)", Range(0,3)) = 0
        _MorphNext       ("Next Shape",                 Range(0,3))        = 0
        _MorphT          ("Transition T",               Range(0,1))        = 0
        _Rotation        ("Z Rotation (radians)",       Float)             = 0
        _PulseAmount     ("Pulse Amount (0=off,1=on)",  Range(0,1))        = 0
        _PulseFrequency  ("Pulse Frequency Hz",         Float)             = 0.5
        _PulseStrokeSwing("Pulse Stroke Swing",         Range(0,1))        = 0.25
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
            Name "ShapeMorphSDF"

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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _StrokeWidth;
                float  _Radius;
                float  _MorphCurrent;
                float  _MorphNext;
                float  _MorphT;
                float  _Rotation;
                float  _PulseAmount;
                float  _PulseFrequency;
                float  _PulseStrokeSwing;
            CBUFFER_END

            #define PI 3.14159265

            // Maps the 4 discrete state indices to N (vertex count) for the regular-polygon SDF.
            // Circle is just a high-N polygon (visually identical to a true circle).
            float ShapeIndexToN(float idx)
            {
                if (idx < 0.5) return 3.0;   // triangle
                if (idx < 1.5) return 6.0;   // hexagon
                if (idx < 2.5) return 64.0;  // circle (high-N polygon)
                return 4.0;                  // square
            }

            // Per-shape orientation so the polygon sits in its "natural" pose
            // (triangle vertex-up, square/hex flat-top). Circle is rotationally symmetric.
            float ShapeOrientationOffset(float idx)
            {
                if (idx < 0.5) return 0.0;
                if (idx < 1.5) return PI / 6.0;
                if (idx < 2.5) return 0.0;
                return PI / 4.0;
            }

            // Regular polygon SDF with circumradius R, N sides (works for fractional N).
            float sdRegularPolygon(float2 p, float R, float N)
            {
                float halfAngle = PI / N;
                float a = atan2(p.x, p.y);
                a -= 2.0 * halfAngle * floor((a + halfAngle) / (2.0 * halfAngle));
                return length(p) * cos(a) - R * cos(halfAngle);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;
                return OUT;
            }

            float2 Rotate2D(float2 p, float angleRad)
            {
                float cs = cos(angleRad), sn = sin(angleRad);
                return float2(cs * p.x - sn * p.y, sn * p.x + cs * p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;

                p = Rotate2D(p, -_Rotation);

                float2 p_current = Rotate2D(p, -ShapeOrientationOffset(_MorphCurrent));
                float2 p_next    = Rotate2D(p, -ShapeOrientationOffset(_MorphNext));

                float d_current = sdRegularPolygon(p_current, _Radius, ShapeIndexToN(_MorphCurrent));
                float d_next    = sdRegularPolygon(p_next,    _Radius, ShapeIndexToN(_MorphNext));
                float d         = lerp(d_current, d_next, _MorphT);

                float pulse     = _PulseStrokeSwing * sin(_Time.y * 2.0 * PI * _PulseFrequency);
                float strokeMod = _StrokeWidth * (1.0 + pulse * _PulseAmount);

                float halfStroke = strokeMod * 0.5;
                float aa         = fwidth(d);
                float outline    = 1.0 - smoothstep(halfStroke - aa, halfStroke + aa, abs(d));

                return half4(_Color.rgb, _Color.a * outline);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
