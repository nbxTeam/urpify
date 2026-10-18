Shader "Lab/M01 Mirror" {
    Properties {
        _Tint ("Tint", Color) = (0.85,0.9,1,1)
        [HideInInspector] _ReflectionTex ("", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float4 refl : TEXCOORD0; };
            sampler2D _ReflectionTex; fixed4 _Tint;
            v2f vert (float4 vertex : POSITION) {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.refl = ComputeScreenPos(o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                return tex2Dproj(_ReflectionTex, UNITY_PROJ_COORD(i.refl)) * _Tint;
            }
            ENDCG
        }
    }
}
