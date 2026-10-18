Shader "Lab/V02 VertFrag ForwardBase Lit Shadows" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                SHADOW_COORDS(2)
                fixed3 ambient : TEXCOORD3;
            };
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.ambient = ShadeSH9(half4(o.worldNormal, 1));
                TRANSFER_SHADOW(o)
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                fixed3 n = normalize(i.worldNormal);
                fixed ndl = saturate(dot(n, _WorldSpaceLightPos0.xyz));
                fixed shadow = SHADOW_ATTENUATION(i);
                fixed3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                return fixed4(albedo * (_LightColor0.rgb * ndl * shadow + i.ambient), 1);
            }
            ENDCG
        }
        Pass {
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct v2f { float4 pos : SV_POSITION; float3 worldNormal : TEXCOORD0; float3 worldPos : TEXCOORD1; };
            fixed4 _Color;
            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float3 l = normalize(_WorldSpaceLightPos0.xyz - i.worldPos * _WorldSpaceLightPos0.w);
                UNITY_LIGHT_ATTENUATION(atten, 0, i.worldPos);
                return fixed4(_Color.rgb * _LightColor0.rgb * saturate(dot(normalize(i.worldNormal), l)) * atten, 1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}
