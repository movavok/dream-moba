Shader "DreamMoba/BehindBuildingPattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TeamColor ("Team Color", Color) = (1, 1, 1, 1)
        _PatternDirection ("Pattern Direction", Float) = 1
        _PatternEnabled ("Pattern Enabled", Float) = 1
        _PatternDensity ("Pattern Density", Float) = 6
        _PatternLineWidth ("Pattern Line Width", Range(0.05, 0.5)) = 0.18
        _PatternSpeed ("Pattern Speed", Float) = 0.35
        _PatternSoftness ("Pattern Softness", Range(0.001, 0.3)) = 0.06
        _VisibleAlpha ("Visible Alpha", Range(0, 1)) = 0.42
        _GapAlpha ("Gap Alpha", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "true"
        }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TeamColor;
                float _PatternDirection;
                float _PatternEnabled;
                float _PatternDensity;
                float _PatternLineWidth;
                float _PatternSpeed;
                float _PatternSoftness;
                float _VisibleAlpha;
                float _GapAlpha;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half spriteAlpha =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;

                float time = _Time.y * _PatternSpeed;
                float edge = max(0.001, _PatternSoftness);

                float hatchA =
                    frac(
                        (input.uv.x * _PatternDirection + input.uv.y)
                        * _PatternDensity
                        + time
                    );

                float hatchB =
                    frac(
                        (input.uv.x * _PatternDirection - input.uv.y)
                        * _PatternDensity * 1.37
                        - time * 0.7
                    );

                float bandA =
                    smoothstep(
                        _PatternLineWidth - edge,
                        _PatternLineWidth + edge,
                        hatchA
                    );

                float bandB =
                    smoothstep(
                        _PatternLineWidth - edge,
                        _PatternLineWidth + edge,
                        hatchB
                    );

                float stripe = saturate(bandA + bandB * 0.55);
                float shimmer =
                    0.85 + 0.15 * sin((input.uv.y + time) * 6.28318);

                stripe *= shimmer;

                half alpha =
                    spriteAlpha
                    * lerp(
                        _VisibleAlpha,
                        lerp(_GapAlpha, _VisibleAlpha, stripe),
                        _PatternEnabled
                    )
                    * input.color.a;

                return half4(
                    _TeamColor.rgb * input.color.rgb * alpha,
                    alpha
                );
            }
            ENDHLSL
        }
    }
}
