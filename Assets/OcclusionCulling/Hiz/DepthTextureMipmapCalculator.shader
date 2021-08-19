Shader "Custom/DepthTextureMipmapCalculator"
{
    Properties{
        [HideInInspector] _MainTex("Previous Mipmap", 2D) = "black" {}
        [HideInInspector] _MainTexSize("Texture Size", int) = 0
    }
        SubShader{
            Pass {
                Cull Off
                ZWrite Off
                ZTest Always

                CGPROGRAM
                #pragma target 3.0
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                int _MainTexSize;

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };
                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };

                inline float CalculatorMipmapDepth(float2 uv)
                {
                    float4 depth;
                    float offset = 0.5f / _MainTexSize;
                    depth.x = tex2D(_MainTex, uv);
                    depth.y = tex2D(_MainTex, uv + float2(0, offset));
                    depth.z = tex2D(_MainTex, uv + float2(offset, 0));
                    depth.w = tex2D(_MainTex, uv + float2(offset, offset));
    #if defined(UNITY_REVERSED_Z)
                    return min(min(depth.x, depth.y), min(depth.z, depth.w));
    #else
                    return max(max(depth.x, depth.y), max(depth.z, depth.w));
    #endif
                }
                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex.xyz);
                    o.uv = v.uv;
                    return o;
                }
                float frag(v2f input) : SV_TARGET
                {
                    float depth = CalculatorMipmapDepth(input.uv);
                    return depth;
                }
                ENDCG
            }
            
            Pass {
                Cull Off
                ZWrite Off
                ZTest Always

                CGPROGRAM
                #pragma target 3.0
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"

                sampler2D _CameraDepthTexture;

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };
                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };
                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex.xyz);
                    o.uv = v.uv;
                    return o;
                }

                float frag(v2f input) : SV_TARGET
                {
                    // input.uv.y = 1 - input.uv.y;
                    float depth = tex2D(_CameraDepthTexture,input.uv);
                    return depth;
                }
                ENDCG
            }
        }
}