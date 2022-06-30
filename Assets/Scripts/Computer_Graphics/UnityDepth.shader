Shader "Custom/UnityDepth"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        ZWrite Off Lighting Off Cull Off Fog { Mode Off } Blend One Zero

        GrabPass {}

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            //make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 ro : TEXCOORD3;
                float3 hitPosition : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float4 uvgrab : TEXCOORD6;
            };

            sampler2D _GrabTexture;
            sampler2D _CameraDepthTexture;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.ro = mul(unity_WorldToObject, float4 (_WorldSpaceCameraPos, 1));
                o.hitPosition = v.vertex;
                o.uvgrab = ComputeGrabScreenPos(o.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            float GetTorus(float3 p) //torus primitive
            {
                #define MAIN_RADIUS 0.3
                #define SECTION_RADIUS 0.1
                float d = length(float2 (length(p.xz) - MAIN_RADIUS, p.y)) - SECTION_RADIUS; //torus oriented in xz plane, y is main axis
                return d;
            }

            float GetDensity(float3 p) //density calculation, actual code will go in here
            {
                return 0.05;
            }

            #define SURFACE_DISTANCE 1e-3

            //volumetric raymarcher()
            //raymarch until ray intersects with SDF sphere, record that point in p and then collect density
            //ro = ray origin
            //rd = ray direction (normalized vector)
            //maxdepth = compare if raymarched point is intersecting with background geometry
            float VolumetricRaymarch(float3 ro, float3 rd, float maxdepth)
            {
                float dS = 0.0; //distance to surface
                float dO = 0.0; //total distance travelled along the ray
                float3 p; //point to sample from
                //first raymarcher seeks point on a SDF surface
                //this is required to ensure consistent sampling in second raymarch, indepedent of camera world position
                for (int i = 0; i < 60; i++) //finite count of loops. tweak the number of steps to minimum
                {
                    p = ro + dO * rd;
                    dS = GetTorus(p); //get distance to surface of primitive found along the ray
                    if (dS < SURFACE_DISTANCE) break; //near surface ? stop loop
                    dO += dS; //advance along the ray
                    //if sampling point intersects with background geometry, stop immediately and return 0 density
                    if (dO > maxdepth) return 0.0;
                }
                //second raymarcher does the density accumulation once inside volume of primitive
                float accumulatedOcclusion = 0.0; //accumulated density for all occluding particles along the ray
                dO = 0.0; //reset travelling distance back to 0
                ro = p; //set the starting point for second raymarch at surface of primitive
                for (i = 0; i < 60; i++) //finite count of loops. more loops + smaller increments = finer detail
                {
                    if (dO > maxdepth) break; //if sampling point intersects with background geometry, stop accumulating density
                    //if density is close to 1, stop. some transparency must be preserved
                    if (accumulatedOcclusion > 0.9) break;
                    //crude speedup by using sphere primitive slightly larger than mesh object, consider it entire-volume-checking
                    //ray itself goes beyond unity object rendered because of ray origin, this puts limit on that
                    if (dS <= (length(p) - 1.0)) break;
                    //get distance to primitive surface
                    //for dS < 0, point is inside volume
                    //at dS = 0 point is at the surface (rare)
                    //and for dS > 0 point is outside primitive
                    dS = GetTorus(p);
                    //if point is inside volume, sample density at point and accumulate it
                    if (dS < SURFACE_DISTANCE) accumulatedOcclusion += GetDensity(p);
                    dO += 1.0 / 60; //advance the sampling step
                    p = ro + dO * rd; //and calculate new sampling position
                }
                return accumulatedOcclusion; //return collected occlusion along the ray
            }

            fixed4 frag(v2f i) : SV_Target
            {

                //raymarch inputs
                float3 ro = i.ro;
                float3 rd = normalize(i.hitPosition - ro);

                //grabbed coolor for transparency effect
                fixed4 grabCol = tex2Dproj(_GrabTexture, UNITY_PROJ_COORD(i.uvgrab));

                //grabbed for depth calculation
                // taken from https://github.com/IronWarrior/ToonWaterShader
                float cameraDepth = tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)).r;
                float eyeDepthLinear = LinearEyeDepth(cameraDepth);


                //bgolus guidance: https://forum.unity.com/threads/raymarcher-with-depth-buffer.877936/
                float3 ray = i.hitPosition - ro;//object space
                float3 rayWorldSpace = mul(unity_ObjectToWorld, ray).xyz;
                float3 viewSpaceForwardDir = mul(float3 (0, 0, -1), (float3x3)UNITY_MATRIX_V);
                rayWorldSpace = rayWorldSpace / dot(rayWorldSpace, viewSpaceForwardDir);
                float3 maxrayworldspace = eyeDepthLinear * rayWorldSpace;
                float3 maxrayobjspace = mul(unity_WorldToObject, maxrayworldspace);
                float maxdepthused = length(maxrayobjspace);
                //ray = ray / dot(ray, viewSpaceForwardDir);
                //float3 maxray = eyeDepthLinear * ray;

                
                float maxdepth = length(ro + rd * eyeDepthLinear); //raymarcher limited by this maximum depth
                //float occlusion = VolumetricRaymarch(ro, rd, maxdepth); //raymarch inside primitive and return occlusion factor
                
                float occlusion = VolumetricRaymarch(ro, rd, maxdepthused); //raymarch inside primitive and return occlusion factor
                
                fixed4 col = lerp(grabCol, fixed4(1,1,1,1), occlusion); //simple gradient between background and opaque white using above occlusion
                //apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}