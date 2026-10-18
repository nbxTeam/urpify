Shader "Lab/G01 GrabPass Refraction Glass" {
    Properties {
        _BumpMap ("Distortion Normal", 2D) = "bump" {}
        _Distortion ("Distortion", Range(0,100)) = 30
        _Tint ("Tint", Color) = (0.8,0.95,1,1)
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" }
        GrabPass { }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float4 grabPos : TEXCOORD0; float2 uv : TEXCOORD1; };
            sampler2D _GrabTexture; float4 _GrabTexture_TexelSize;
            sampler2D _BumpMap; float4 _BumpMap_ST; float _Distortion; fixed4 _Tint;
            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.uv = TRANSFORM_TEX(v.texcoord, _BumpMap);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                half2 bump = UnpackNormal(tex2D(_BumpMap, i.uv)).rg;
                i.grabPos.xy += bump * _Distortion * _GrabTexture_TexelSize.xy * i.grabPos.z;
                return tex2Dproj(_GrabTexture, UNITY_PROJ_COORD(i.grabPos)) * _Tint;
            }
            ENDCG
        }
    }
}
