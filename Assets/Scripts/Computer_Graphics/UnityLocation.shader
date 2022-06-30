Shader "Custom/UnityLoation"
{
    Properties
    {
        _MainTex("Texture", 3D) = "white" {}
        _MainTex2("Texture", 3D) = "white" {}
        _Alpha("Alpha", float) = 0.02
        _StepSize("Step Size", float) = 0.01
        _MaxStepCount("Step Count", float) = 512//Maximum amount of raymarching samples
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            Blend One OneMinusSrcAlpha
            LOD 100

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                // Allowed floating point inaccuracy
                #define EPSILON 0.00001f

                struct appdata
                {
                    float4 vertex : POSITION;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float3 objectVertex : TEXCOORD0;
                    float3 vectorToSurface : TEXCOORD1;
                    float3 worldVertex : TEXCOORD2;
                };

                sampler3D _MainTex;
                sampler3D _MainTex2;
                float _Alpha;
                float _StepSize;
                float _MaxStepCount;

                v2f vert(appdata v)
                {
                    v2f o;

                    // Vertex in object space this will be the starting point of raymarching
                    o.objectVertex = v.vertex;

                    // Calculate vector from camera to vertex in world space
                    float3 worldVertex = mul(unity_ObjectToWorld, v.vertex).xyz;
                    o.worldVertex = worldVertex;
                    //float3 worldVertex = mul(unity_ObjectToWorld, float4(v.vertex,1.0)).xyz;/*amended as explained here https ://forum.unity.com/threads/solved-clip-space-to-world-space-in-a-vertex-shader.531492*/
                    o.vectorToSurface = worldVertex - _WorldSpaceCameraPos;

                    o.vertex = UnityObjectToClipPos(v.vertex);
                    return o;
                }

                float4 BlendUnder(float4 color, float4 newColor)
                {
                    color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                    color.a += (1.0 - color.a) * newColor.a;
                    return color;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    // Start raymarching at the front surface of the object
                    float3 rayOrigin = i.worldVertex;

                    //loop over z location
                    //float3 samplePosition = tex3D(_MainTex2, i.worldVertex + float3(0.5f, 0.5f, 0.5f));

                    //i0 = int(texture2D(mask, maskCoord).r);
                    //for(int i=0;i<)

                    float3 samplePosition = tex3D(_MainTex2, i.worldVertex + float3(0.5f, 0.5f, 0.5f));
                    float4 sampledColor = tex3D(_MainTex, samplePosition);

                    return sampledColor;
                    //return (1,1,1,1);
                }
                ENDCG
            }
        }
}