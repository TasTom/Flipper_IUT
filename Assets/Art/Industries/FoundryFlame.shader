Shader "Vosges Mania/Industries/Foundry Flame"
{
    Properties
    {
        _Intensity("Intensité", Range(0.25, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            float Tongue(float2 uv, float time, float origin, float width, float height)
            {
                float y = uv.y / height;
                float turbulence = Noise(float2(uv.x * 5.0 + origin * 8.0, uv.y * 7.0 - time * 3.2));
                float bend = (turbulence - .5) * .22 * y + .036 * sin(time * 5 + uv.y * 12);
                float taper = width * pow(saturate(1 - y), .77);
                float edge = taper - abs(uv.x - .5 - origin - bend);
                return smoothstep(-.025, .025, edge) * smoothstep(.02, .13, y) * (1 - smoothstep(.86, 1, y));
            }

            float Spark(float2 uv, float time, float seed)
            {
                float cycle = floor(time);
                float age = frac(time);
                float seedX = Hash(float2(cycle, seed));
                float2 center = float2(lerp(.28, .70, seedX) + .065 * sin(age * 4.4 + seed), .22 + .71 * age);
                float2 delta = (uv - center) * float2(1, .65);
                return (1 - smoothstep(.014, .035, length(delta))) * smoothstep(0, .12, age) * (1 - smoothstep(.62, .94, age));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                float a = Tongue(uv, time, -.11, .18, .64 + .105 * sin(time * 3.9));
                float b = Tongue(uv, time + 3.1, .08, .21, .77 + .09 * sin(time * 4.6 + 1));
                float c = Tongue(uv, time + 1.7, .21, .115, .46 + .08 * sin(time * 5.5));
                float mask = saturate(max(a, max(b, c)) + .12 * min(a, b));
                float hot = saturate((1 - uv.y * 2.25) * (.48 + .52 * mask));
                float3 fire = lerp(float3(2.7, .17, .014), float3(4.1, 2.1, .32), hot);
                float sparks = max(Spark(uv, time * .77 + .51, 2), Spark(uv, time * .57, 7) * .57);
                float opacity = saturate(mask * .88 + sparks * .69);
                float3 color = _Intensity * (fire * mask * .88 + float3(3.7, 1.68, .17) * sparks);
                return half4(color, opacity);
            }
            ENDHLSL
        }
    }
}
