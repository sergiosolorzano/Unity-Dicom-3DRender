Shader "Custom/Github_1_DirectVolume"
{
    Properties
    {
        _MainTex("Texture", 3D) = "white" {}
        _MinVal("Min val", Range(0.0, 1.0)) = 0.0
        _MaxVal("Max val", Range(0.0, 1.0)) = 1.0
        _Alpha("Alpha", float) = 0.02
        _MaxStepCount("Step Count", float) = 512.0//Maximum amount of raymarching samples
        _StepSize("Step Size", float) = 0.02//Maximum amount of raymarching samples
        _BoxWidth("Box Width", float) = 1.0 //greatest distance in box
    }
        SubShader
        {
            Tags {"Queue" = "Transparent" "RenderType" = "Transparent"}
            /*Cull Front
            ZTest LEqual
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha*/
            Blend One OneMinusSrcAlpha

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct vert_in
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct frag_in
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 vertexLocal : TEXCOORD1;
                float3 normal : NORMAL;
            };

            struct frag_out
            {
                float4 colour : SV_TARGET;
            };

                sampler3D _MainTex;
                float _MinVal;
                float _MaxVal;
                float _Alpha;
                float _MaxStepCount;
                float _StepSize;
                float _BoxWidth;

                frag_in vert_main(vert_in v)
                {
                    frag_in o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    o.vertexLocal = v.vertex;
                    o.normal = UnityObjectToWorldNormal(v.normal);
                    return o;
                }

                float4 BlendUnder(float4 color, float4 newColor)
                {
                    color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                    color.a += (1.0 - color.a) * newColor.a;
                    return color;
                }

                // Gets the density at the specified position
                float getDensity(float3 pos)
                {
                    return tex3Dlod(_MainTex, float4(pos.x, pos.y, pos.z, 0.0f));
                }

                // Converts local position to depth value
                /*float localToDepth(float3 localPos)
                {
                    float4 clipPos = UnityObjectToClipPos(float4(localPos, 1.0f));

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_OPENGL) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    return (clipPos.z / clipPos.w) * 0.5 + 0.5;
#else
                    return clipPos.z / clipPos.w;
#endif
                }*/

                // Direct Volume Rendering
                frag_out frag_dvr(frag_in i)
                {
                    //const float stepSize = _BoxWidth / (float)_MaxStepCount;
                    
                    float3 rayStartPos = i.vertexLocal + float3(0.5f, 0.5f, 0.5f);
                    float3 rayDir = ObjSpaceViewDir(float4(i.vertexLocal, 0.0f));
                    rayDir = normalize(rayDir);

                    float4 col = float4(0.0f, 0.0f, 0.0f, 0.0f);
                    uint iDepth = 0;
                    float t = 0.0;
                    float3 currPos = rayStartPos;

                    for (uint iStep = 0; iStep < (uint)_MaxStepCount; iStep++)
                    {
                        t = iStep * _StepSize;
                        currPos += rayDir * t;

                        if (currPos.x < 0.0f || currPos.x >= 1.0f || currPos.y < 0.0f || currPos.y > 1.0f || currPos.z < 0.0f || currPos.z > 1.0f) // TODO: avoid branch?
                            break;

                        // Get the dansity/sample value of the current position
                        const float density = getDensity(currPos);

                        // Apply transfer function
                        //float4 src = getTF1DColour(density);
                        
                        float4 src = density;

                        if (density < _MinVal || density > _MaxVal)
                            src.a = 0.0f;

                        col.rgb = src.a * src.rgb + (1.0f - src.a) * col.rgb;
                        col.a = src.a + (1.0f - src.a) * col.a;

                        if (src.a > 0.15f)
                            iDepth = iStep;

                        if (col.a > 1.0f)
                            break;
                    }

                    // Write fragment output
                    frag_out output;
                    output.colour = col;
#if DEPTHWRITE_ON
                    //if (iDepth != 0)
                      //  output.depth = localToDepth(rayStartPos + rayDir * (iDepth * stepSize) - float3(0.5f, 0.5f, 0.5f));
                    //else
                        output.depth = 0;
#endif
                    return output;
                }

                frag_in vert(vert_in v)
                {
                    return vert_main(v);
                }

                frag_out frag(frag_in i)
                {
                    return frag_dvr(i);
                }

                ENDCG
            }
        }
}