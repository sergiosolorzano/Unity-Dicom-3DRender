Shader "Custom/MyCloud"
{
    Properties
    {
         _MainTex("Texture", 3D) = "white" {}

        _Scale ("Scale",Range(0.1,10.0)) = 1.0
        _StepScale("Step Scale",Range(0.1, 100.0)) = 1.0//steps between my scales
        _Steps("Steps", Range(1,512))=512
        _MinHeight("Min Height", Range(0.0, 5.0)) = 0
        _MaxHeight("Max Height", Range(6.0, 10.0)) = 10
        _FadeDist("Fade Distance", Range(0.0, 10.0)) = 0.5//how far into the distance the clouds appear before they fade out
        _SunDir("Sun Direction", Vector) = (1,0,0,0)//sun is facing along the positive x direction
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
            Cull Off Lighting Off ZWrite Off
            ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos: SV_POSITION;
                float3 view: TEXCOORD0;
                float4 projPos : TEXCOORD1;
                float3 wpos: TEXCOORD2;//world pos 
            };

            float _MinHeight;
            float _MaxHeight;
            float _FadeDist;
            float _Scale;
            float _StepScale;
            float _Steps;
            float4 _SunDir;
            sampler3D _MainTex;

            float getDensity(float3 pos)
            {
                return tex3D(_MainTex, pos + float3(0.5f, 0.5f, 0.5f));
                //return tex3Dlod(_MainTex, float4(pos.x, pos.y, pos.z, 0.0f));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.view = (o.wpos - _WorldSpaceCameraPos);
                o.projPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 integrate(fixed4 sum, float diffuse, float density, fixed4 bgcol, float t)//t number of times we step
            {
                fixed3 lighting = fixed3(0.65, 0.68, 0.7) * 1.3 + 0.5 * fixed3(0.7, 0.5, 0.3) * diffuse;//translucent quality added to diffuse
                fixed3 colrgb = lerp(fixed3(1.0, 0.95, 0.8), fixed3(0.65, 0.65, 0.65), density);
                fixed4 col = fixed4(colrgb.r, colrgb.g, colrgb.b, density);
                col.rgb *= lighting;
                col.rgb = lerp(col.rgb, bgcol, 1.0 - exp(-0.003 * t * t));//the exp gives us a fall off value, so when t is bigger and we get more towards the back of the mesh we'll get a bigger blend into the actual color rather than the bgcol
                col.a *= 0.5;
                col.rgb *= col.a;
                
                return sum + col * (1.0 - sum.a);
            }

            float4 BlendUnder(float4 color, float4 newColor)
            {
                color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                color.a += (1.0 - color.a) * newColor.a;
                return color;
            }

            float4 raymarch(float3 camPos, float3 viewDir, float depth)
            {
                float density = 0.0;
                float ct = 0;
                float4 bgcol = fixed4(0, 0, 0, 0);//set alpha as zero cos we want transparent where there are no clouds
                float4 clouds = fixed4(0, 0, 0, 0);//final color
                float3 pos = float3(0, 0, 0);

                for (int i = 0; i < _Steps+1; i++)
                {
                    pos = camPos + ct * viewDir;

                    density = getDensity(pos);

                    // Accumulate color only within unit cube bounds
                    /*if (density > 0.99)
                    {
                        continue;
                    }*/

                    //if (ct > depth) break;

                    if (density > 0.01)
                    {
                        //float diffuse = clamp((density - getDensity(pos + 0.3 * _SunDir)) / 0.6, 0.0, 1.0);
                        clouds = BlendUnder(density, bgcol);
                        //clouds = integrate(clouds, diffuse, density, bgcol, depth);
                    }

                    ct += max(0.01, 0.02 * ct);
                }

                return clouds;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float depth = length(i.view);//length vector between camera and pixel location. We can use the technique used in the sphericalFog example using _CameraDepthTexture
                
                //float3 pos = _WorldSpaceCameraPos;
                float3 camPos = _WorldSpaceCameraPos;
                float3 viewDir = normalize(i.view) * _StepScale;

                // Raymarch through object space
                float4 clouds=raymarch(camPos, viewDir, depth);

                return clouds;
                //fixed3 mixedCol = bgcol * (1.0 - clouds.a) + clouds.rgb;//put color and clouds together//TODO I DON'T UNDERSTAND**************
                //return fixed4(mixedCol, clouds.a);
            }
            ENDCG
        }
    }
}
