Shader "Lab/S09 Surface Tessellation Displacement" {
    Properties {
        _MainTex ("Albedo", 2D) = "white" {}
        _DispTex ("Height", 2D) = "gray" {}
        _Displacement ("Displacement", Range(0,0.3)) = 0.08
        _Tess ("Tessellation", Range(1,32)) = 8
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard addshadow fullforwardshadows vertex:disp tessellate:tessFixed nolightmap
        #pragma target 4.6
        float _Tess, _Displacement;
        sampler2D _DispTex, _MainTex;
        struct appdata { float4 vertex : POSITION; float4 tangent : TANGENT; float3 normal : NORMAL; float2 texcoord : TEXCOORD0; };
        float4 tessFixed() { return _Tess; }
        void disp (inout appdata v) {
            float d = tex2Dlod(_DispTex, float4(v.texcoord.xy,0,0)).r * _Displacement;
            v.vertex.xyz += v.normal * d;
        }
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutputStandard o) {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Smoothness = 0.1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
