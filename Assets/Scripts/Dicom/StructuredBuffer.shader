Shader "StructuredBuffer"
{

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain

            StructuredBuffer<float4> pixels;
            int resolution;

            void VSMain(inout float4 vertex:POSITION,inout float2 uv : TEXCOORD0)
            {
                vertex = UnityObjectToClipPos(vertex);
            }

            float4 PSMain(float4 vertex:POSITION,float2 uv : TEXCOORD0) : SV_TARGET
            {
                int x = int(floor(uv.x * resolution));
                int y = int(floor(uv.y * resolution));
                return pixels[x * resolution + y];
            }
            ENDCG
        }
    }
}