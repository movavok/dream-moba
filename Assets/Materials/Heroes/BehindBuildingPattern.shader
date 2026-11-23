Shader "DreamMoba/BehindBuildingPattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TeamColor ("Team Color", Color) = (1, 1, 1, 1)
        _PatternDirection ("Pattern Direction", Float) = 1
        _PatternEnabled ("Pattern Enabled", Float) = 1
        _PatternSpeed ("Pattern Speed", Float) = 0.35
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
                float2 localPosition : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TeamColor;
                float _PatternDirection;
                float _PatternEnabled;
                float _PatternSpeed;
                float _VisibleAlpha;
                float _GapAlpha;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                output.localPosition = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half spriteAlpha =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;

                // Sprite art is 16 pixels per world unit. Quantize in local space
                // so atlas UVs and differently sized animation frames cannot skew
                // the diagonal. Advance one whole pixel at a time, downward.
                float2 pixel = floor(input.localPosition * 16.0);
                float shift = floor(_Time.y * _PatternSpeed * 16.0);
                float stripe = 1.0 - step(2.0,
                    frac((pixel.x * _PatternDirection + pixel.y + shift) / 8.0) * 8.0);

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
