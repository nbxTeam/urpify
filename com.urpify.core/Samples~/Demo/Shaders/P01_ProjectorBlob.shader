Shader "Lab/P01 Projector Blob Shadow" {
    Properties {
        _ShadowTex ("Cookie", 2D) = "gray" {}
        _Strength ("Strength", Range(0,1)) = 0.8
    }
    SubShader {
        Tags { "Queue"="Transparent" }
        Pass {
            ZWrite Off
            ColorMask RGB
            Blend DstColor Zero
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 uvShadow : TEXCOORD0; float4 pos : SV_POSITION; };
            float4x4 unity_Projector;
            sampler2D _ShadowTex; float _Strength;
            v2f vert (float4 vertex : POSITION) {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uvShadow = mul(unity_Projector, vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.uvShadow.xy / i.uvShadow.w;
                float d = saturate(1 - length(uv - 0.5) * 2);
                float inside = step(0, i.uvShadow.w);
                return lerp(fixed4(1,1,1,1), fixed4(0,0,0,1), d * _Strength * inside);
            }
            ENDCG
        }
    }
}
