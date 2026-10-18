Shader "Lab/D01 Depth Intersection Water" {
    Properties {
        _ShallowColor ("Shallow", Color) = (0.3,0.8,0.9,0.6)
        _DeepColor ("Deep", Color) = (0.05,0.2,0.4,0.9)
        _DepthRange ("Depth Range", Float) = 1.5
        _FoamColor ("Foam", Color) = (1,1,1,1)
        _FoamDistance ("Foam Distance", Float) = 0.3
        _WaveTex ("Wave Normal", 2D) = "bump" {}
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; float2 uv : TEXCOORD1; };
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _ShallowColor, _DeepColor, _FoamColor; float _DepthRange, _FoamDistance;
            sampler2D _WaveTex; float4 _WaveTex_ST;
            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                o.uv = TRANSFORM_TEX(v.texcoord, _WaveTex) + _Time.x;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float diff = scene - i.screen.z;
                fixed4 col = lerp(_ShallowColor, _DeepColor, saturate(diff / _DepthRange));
                half3 n = UnpackNormal(tex2D(_WaveTex, i.uv));
                col.rgb += n.x * 0.05;
                col = lerp(_FoamColor, col, saturate(diff / _FoamDistance));
                return col;
            }
            ENDCG
        }
    }
}
