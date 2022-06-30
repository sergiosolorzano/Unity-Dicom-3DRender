Shader "Holistic/RaymarchCloudsVolume"
{
    Properties
    {
        _Scale ("Scale",Range(0.1,10.0)) = 2.0
        _StepScale("Step Scale",Range(0.1, 100.0)) = 1.0//steps between my scales
        _Steps("Steps", Range(1,200))=60
        _MinHeight("Min Height", Range(0.0, 5.0)) = 0
        _MaxHeight("Max Height", Range(6.0, 10.0)) = 10
        _FadeDist("Fade Distance", Range(0.0, 10.0)) = 0.5//how far into the distance the clouds appear before they fade out
        _SunDir("Sun Direction", Vector) = (1,0,0,0)//sun is facing along the positive x direction
        _Speed("Speed",Range(0,10)) = 10
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
            float _Speed;
            sampler2D _CameraDepthTexture;//to sample our texture depth

            #define PI 3.1416

            float random(float3 value, float3 dotDir)//takes a 3d value and returns a random number. value is the xyz pos of the shader. 
            {
                float3 smallValue = sin(value);//we only want values that could be found in sin function and between -1 and 1 and smooth
                float random = dot(smallValue, dotDir);//dotDir is our random Vector
                random = frac(sin(random) * 1242343.32);//making it more random
                return random;//1D random value where I get 3 values coming in
            }

            float3 random3d(float3 value)//the 3D random which will run random once for x, once for y , once for z
            {
                return float3 ( random(value, float3(12.898, 68.54, 37.7298)),//all magic numbers
                                random(value, float3(39.898, 26.54, 85.7238)),
                                random(value, float3(76.898, 12.54, 8.6798)));
            }

            float noise3d(float3 value)//we create 2 x values and interpolate between those, 2 y values and interpolate between those, 2 z values and interpolate between those
            {
                value *= _Scale;//to make it bigger/smaller
                value.x += _Time.x * 5;//value is the 3d position of the pixel
                float3 interp = frac(value);
                interp = smoothstep(0.0, 1.0, interp);

                float3 ZValues[2];
                for (int z = 0; z <= 1; z++)
                {
                    float3 YValues[2];
                    for (int y = 0; y <= 1; y++)
                    {
                        float3 XValues[2];
                        for (int x = 0; x <= 1; x++)
                        {
                            float3 cell = floor(value) + float3(x, y, z);//e.g. 0,0,0+1,0,0
                            XValues[x] = random3d(cell);
                        }

                        YValues[y] = lerp(XValues[0], XValues[1], interp.x);
                    }

                    ZValues[z] = lerp(YValues[0], YValues[1], interp.y);
                }

                float noise = -1 + 2.0 * lerp(ZValues[0], ZValues[1], interp.z);//magic numbers to push the values apart and get a better range of noise, i.e. so there are patches between clouds
                return noise;
            }

            //integrate function blends pixel colors from back to front and make sure they all fade out the influence of the pixels I currently see. we use t to step into the environment, 
            //therefore I can use t as the amount of influence a particular color has, so the closer the color is to the camera (i.e. smaller value of t) the higher you want the value
            //and it's going to blend that into the background
            fixed4 integrate(fixed4 sum, float diffuse, float density, fixed4 bgcol, float t)//t number of times we step
            {
                fixed3 lighting = fixed3(0.65, 0.68, 0.7) * 1.3 + 0.5 * fixed3(0.7, 0.5, 0.3) * diffuse;//translucent quality added to diffuse
                //fixed3 lighting = fixed3(1, 0, 0) * 1.3 + 0.5 * fixed3(0.7, 0.5, 0.3) * diffuse;//translucent quality added to diffuse
                fixed3 colrgb = lerp(fixed3(1.0,0.95,0.8), fixed3(0.65, 0.65, 0.65), density);
                fixed4 col = fixed4(colrgb.r, colrgb.g, colrgb.b, density);
                col.rgb *= lighting;
                col.rgb = lerp(col.rgb, bgcol, 1.0 - exp(-0.003*t*t));//the exp gives us a fall off value, so when t is bigger and we get more towards the back of the mesh we'll get a bigger blend into the actual color rather than the bgcol
                //col.rgb = lerp(col.rgb, bgcol, 0.25*t);//the exp gives us a fall off value, so when t is bigger and we get more towards the back of the mesh we'll get a bigger blend into the actual color rather than the bgcol
                col.a *= 0.5;
                col.rgb *= col.a;
                return sum + col * (1.0 - sum.a);
            }

            #define MARCH(steps, noiseMap, cameraPos, viewDir, bgcol, sum, depth, t) { \
                for (int i = 0; i < steps  + 1; i++) \
                { \
                    if(t > depth) \
                        break; \
                    float3 pos = cameraPos + t * viewDir; \
                    if (pos.y < _MinHeight || pos.y > _MaxHeight || sum.a > 0.99) \
                    {\
                        t += max(0.1, 0.02*t); \
                        continue; \
                    }\
                    \
                    float density = noiseMap(pos); \
                    if (density > 0.01) \
                    { \
                        float diffuse = clamp((density - noiseMap(pos + 0.3 * _SunDir)) / 0.6, 0.0, 1.0);\
                        sum = integrate(sum, diffuse, density, bgcol, t); \
                    } \
                    t += max(0.1, 0.02 * t); \
                } \
            } 

            //float3 pos = cameraPos + t * viewDir; -> pos is the position of the pixel
            //at t += max(0.1, 0.02 * t); -> updates it by a little bit. TODO TEST DIFFERENT VALUES***********
            //at if (density > 0.01) -> that's a pretty large density apparently
            //at float diffuse = clamp((density - noiseMap(pos + 0.3 * _SunDir)) / 0.6, 0.0, 1.0); -> calculates the noise density again at a slightly different offset including the sun and based on the sun's direction. meaning if the sun is shining on a cloud, you can't reallyl see through it. this is what this is trying to capture
            //at sum = diffuse;  -> integrate(sum, diffuse, density, bgcol, t); \//it's an out variable but we can access it from the function that called it
            //at t += max(0.1, 0.02 * t); -> TODO TEST DIFFERENT VALUES***********


            #define NOISEPROC(N, P) 1.75 * N * saturate((_MaxHeight - P.y) / _FadeDist)//makes sure the range of values we have on cloud have sufficeint transparent and opaque, kind of spreads them out. And the values at the top of clouds are actually faded out so we don't get a hard cut out at the top 

            float map4(float3 q)//this calls our noise algo
            {
                float3 p = q;
                float f;//this is an accumulative of noise. it's considred f for frequency
                f = 0.5 * noise3d(q);
                q = q * 2;
                f += 0.25 * noise3d(q);
                q = q * 3;
                f += 0.125 * noise3d(q);
                q = q * 4;
                f += 0.0625 * noise3d(q);
                q = q * 5;
                f += 0.03125 * noise3d(q);
                return NOISEPROC(f, p);//we could return f directly instead of NOISEPROC
            }

            float map3(float3 q)//this calls our noise algo
            {
                float3 p = q;
                float f;//this is an accumulative of noise. it's considred f for frequency
                f = 0.5 * noise3d(q);
                q = q * 2;
                f += 0.25 * noise3d(q);
                q = q * 3;
                f += 0.125 * noise3d(q);
                q = q * 4;
                f += 0.06250 * noise3d(q);
                return NOISEPROC(f, p);//we could return f directly instead of NOISEPROC
            }

            float map2(float3 q)//this calls our noise algo
            {
                float3 p = q;
                float f;//this is an accumulative of noise. it's considred f for frequency
                f = 0.5 * noise3d(q);
                q = q * 2;
                f += 0.25 * noise3d(q);
                q = q * 3;
                f += 0.125 * noise3d(q);
                return NOISEPROC(f, p);//we could return f directly instead of NOISEPROC
            }

            float map1(float3 q)//this calls our noise algo
            {
                float3 p = q;
                float f;//this is an accumulative of noise. it's considred f for frequency
                f = 0.5 * noise3d(q);
                q = q * 2;
                f += 0.25 * noise3d(q);
                q = q * 3.5;
                f += 0.15 * noise3d(q);
                return NOISEPROC(f, p);//we could return f directly instead of NOISEPROC
            }

            fixed4 raymarch(float3 cameraPos, float3 viewDir, fixed4 bgcol, float depth)//bgcol=background color which is the color initally coming through //TODO - WHAT IS BGCOLOR?
            {
                fixed4 col = fixed4(0, 0, 0,0);
                float ct = 0;//ct keeps track of teh number of steps we have done and accumulate these steps, this will allow us later on to march a distance towards depth and beyond that if we want that

                MARCH(_Steps, map1, cameraPos, viewDir, bgcol, col, depth, ct);//map1 function performs the value noise calculations). This function gives us back col which is sum in the parameter of MARCH
                //MARCH(_Steps, map2, cameraPos, viewDir, bgcol, col, depth*2, ct);

                //MARCH(_Steps, map3, cameraPos, viewDir, bgcol, col, depth * 3, ct);
                //MARCH(_Steps, map4, cameraPos, viewDir, bgcol, col, depth * 4, ct);

                return clamp(col, 0.0, 1.0);//it'll clamp each of rgba
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                //o.wpos.z *= sin(2 * PI * _Time / 4);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.view = (o.wpos - _WorldSpaceCameraPos);
                o.projPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float depth = 1;
                depth *= length(i.view);//length vector between camera and pixel location. We can use the technique used in the sphericalFog example using _CameraDepthTexture
                fixed4 col = fixed4(1, 1, 1, 0);//set alpha as zero cos we want transparent where there are no clouds
                fixed4 clouds = raymarch(_WorldSpaceCameraPos, normalize(i.view) * _StepScale, col, depth);
                fixed3 mixedCol = col * (1.0 - clouds.a) + clouds.rgb;//put color and clouds together//TODO I DON'T UNDERSTAND**************
                
                //fixed4 finalCol = (mixedCol.r, mixedCol.g, mixedCol.b, clouds.a);
                //return finalCol;
                return fixed4(mixedCol, clouds.a);
            }
            ENDCG
        }
    }
}
