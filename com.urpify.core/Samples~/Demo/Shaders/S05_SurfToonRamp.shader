Shader "Lab/S05 Surface Toon Custom Lighting" {
    Properties {
        _Color ("Color", Color) = (1,0.6,0.3,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Steps ("Bands", Range(1,6)) = 3
        _ShadowTint ("Shadow Tint", Color) = (0.3,0.25,0.5,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows
        sampler2D _MainTex; fixed4 _Color, _ShadowTint; float _Steps;
        struct Input { float2 uv_MainTex; };
        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten) {
            half ndl = saturate(dot(s.Normal, lightDir)) * atten;
            ndl = floor(ndl * _Steps) / _Steps;
            half3 lit = lerp(_ShadowTint.rgb, 1, ndl);
            half4 c; c.rgb = s.Albedo * _LightColor0.rgb * lit; c.a = s.Alpha; return c;
        }
        void surf (Input IN, inout SurfaceOutput o) {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
