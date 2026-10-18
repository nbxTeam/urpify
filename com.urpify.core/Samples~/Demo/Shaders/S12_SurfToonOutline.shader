Shader "Lab/S12 Toon Outline Two Pass" {
    Properties {
        _Color ("Color", Color) = (0.9,0.8,0.6,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _OutlineColor ("Outline", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.02
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Name "OUTLINE"
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _OutlineWidth; fixed4 _OutlineColor;
            float4 vert (appdata_base v) : SV_POSITION {
                v.vertex.xyz += v.normal * _OutlineWidth;
                return UnityObjectToClipPos(v.vertex);
            }
            fixed4 frag () : SV_Target { return _OutlineColor; }
            ENDCG
        }
        CGPROGRAM
        #pragma surface surf Lambert
        sampler2D _MainTex; fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutput o) {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
