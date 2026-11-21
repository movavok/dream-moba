Shader "Custom/PiggyBankRadialFill"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 1
        _Clockwise ("Clockwise", Float) = 1
        _StartAngle ("Start Angle", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _FillAmount;
            float _Clockwise;
            float _StartAngle;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 centeredUV = i.uv - 0.5;
                float angle = atan2(centeredUV.y, centeredUV.x);
                angle = angle * 180.0 / 3.14159265;
                angle = 90.0 - angle;
                if (angle < 0) angle += 360.0;

                angle += _StartAngle;
                if (angle >= 360.0) angle -= 360.0;

                float fillAngle = _FillAmount * 360.0;

                float alpha = 0.0;
                if (_Clockwise > 0.5)
                {
                    if (angle <= fillAngle)
                        alpha = 1.0;
                }
                else
                {
                    if (angle >= (360.0 - fillAngle))
                        alpha = 1.0;
                }

                fixed4 texCol = tex2D(_MainTex, i.uv);
                return fixed4(_Color.rgb, _Color.a * texCol.a * alpha);
            }
            ENDCG
        }
    }
}