Shader "Custom/MyPrimitive"
{
    Properties
    { 
         _MainTex("Texture", 3D) = "white" {}
        _Alpha("Alpha", float) = 1
        _StepSize("Step Size", float) = 0.01
        _MaxStepCount("Step Count", float) = 512//Maximum amount of raymarching samples
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler3D _MainTex;
            float _Alpha;
            float _StepSize;
            float _MaxStepCount;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 wPos : TEXCOORD0;//world position
                float4 pos : SV_POSITION;//position that we want
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);//clip space position of our vertex 
                o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;//world position of the vertex
                o.centerSphere = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));//same behaviour with o.centerSphere  = unity_ObjectToWorld._m03_m13_m23;
                return o;
            }

            #define STEPS 640//how many steps we take along the ray before we stop looking for a hit value
            #define STEP_SIZE 0.01

            float4 BlendUnder(float4 color, float4 newColor)
            {
                color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                color.a += (1.0 - color.a) * newColor.a;
                return color;
            }

            //create raymarch hit. returns a float3=depth=position we hit at. we march along the ray through position stepping
            float RaymarchHit(float3 position, float3 direction)//position of our pixel. direction is the actual ray from cam to cube holding shader
            {
                float4 color = float4(0, 0, 0, 0);

                for (int i = 0; i < _MaxStepCount; i++)
                {
                    float4 sampledColor = tex3D(_MainTex, position + float3(0.5f, 0.5f, 0.5f));
                    sampledColor.a *= _Alpha;
                    color = BlendUnder(color, sampledColor);

                    position += direction * _MaxStepCount;
                }

                return color;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDirection = normalize(i.wPos - _WorldSpaceCameraPos);
                float3 worldPosition = i.wPos;//world position of our pixel
                float depth = RaymarchHit(worldPosition, viewDirection);

                if (depth != 0)
                    return fixed4(depth, depth, depth, 1);//red
                else
                    return fixed4(1, 0, 0, 0);//transparent
            }
            ENDCG
        }
    }
}
