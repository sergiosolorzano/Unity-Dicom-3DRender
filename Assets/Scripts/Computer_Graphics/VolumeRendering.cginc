#ifndef __VOLUME_RENDERING_INCLUDED__
#define __VOLUME_RENDERING_INCLUDED__

#include "UnityCG.cginc"

#ifndef ITERATIONS
#define ITERATIONS 100
#endif

#define PI2 6.28318530718

half4 _Color;
sampler3D _UsedMainTex;
sampler3D _AxialMainTex;
sampler3D _CoronalMainTex;
sampler3D _SagittalMainTex;
half _Intensity, _Threshold;
half3 _SliceMin, _SliceMax;
float4x4 _AxisRotationMatrix;
float _CoronalAngle;
float _Scale;

struct Ray {
    float3 origin;
    float3 dir;
};

struct AABB {
    float3 min;
    float3 max;
};

// https http.download.nvidia.com/developer/presentations/2005/GDC/Audio_and_Slides/VolumeRendering_files/GDC_2_files/GDC_2005_VolumeRenderingForGames_files/Slide0073.htm
bool intersect(Ray r, AABB aabb, out float t0, out float t1)
{
    //compute intersection of ray with all six box planes
    float3 invR = 1.0 / r.dir;
    float3 tbot = invR * (aabb.min - r.origin);
    float3 ttop = invR * (aabb.max - r.origin);

    //reorder interesections to find smallest and largest on each axis
    float3 tmin = min(ttop, tbot);
    float3 tmax = max(ttop, tbot);
    
    //find largest tmin and smallest tmax
    float2 t = max(tmin.xx, tmin.yz);
    t0 = max(t.x, t.y);
    t = min(tmax.xx, tmax.yz);
    t1 = min(t.x, t.y);
    return t0 <= t1;
}

/*float3 localize(float3 p) {
    return mul(unity_WorldToObject, float4(p, 1)).xyz;
}*/

float3 get_uv(float3 p) {
    // float3 local = localize(p);
    return (p + 0.5);
}

//https forum.unity.com/threads/moving-uvs-of-a-texture-by-an-angle-0-360.486045/
float2x2 Rot(float angleDegrees)
{
    float rad = angleDegrees * PI2 / 360;
    float cosAngle = cos(rad);
    float sinAngle = sin(rad);
    float2x2 rot = float2x2(cosAngle, -sinAngle, sinAngle, cosAngle);
    return rot;
}

// Rotation with angle (in radians) and axis
//https gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
float3x3 AngleAxis3x3(float angleDegrees, float3 axis)
{
    float rad = angleDegrees * PI2 / 360;

    float c, s;
    sincos(rad, s, c);

    float t = 1 - c;
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;

    return float3x3(
        t * x * x + c, t * x * y - s * z, t * x * z + s * y,
        t * x * y + s * z, t * y * y + c, t * y * z - s * x,
        t * x * z - s * y, t * y * z + s * x, t * z * z + c
        );
}

float sample_volume(float3 uv, float3 p)
{
    //Axial
    /*uv = mul(uv - 0.5, AngleAxis3x3(_CoronalAngle, float3(1, 0, 0))) + 0.5;
    //uv *= _Scale;
    float v = tex3D(_AxialMainTex, uv).r * _Intensity;
    
    float3 axis = mul(_AxisRotationMatrix, float4(p, 0)).xyz;
    axis = get_uv(axis);
    float min = step(_SliceMin.x, axis.x) * step(_SliceMin.y, axis.y) * step(_SliceMin.z, axis.z);
    float max = step(axis.x, _SliceMax.x) * step(axis.y, _SliceMax.y) * step(axis.z, _SliceMax.z);

    float axisResult = v * min * max;
    //return axisResult;
    
    //Coronal
    uv = mul(uv - 0.5, AngleAxis3x3(_CoronalAngle, float3(1, 0, 0))) + 0.5;
    //uv *= _Scale;
    float v2 = tex3D(_CoronalMainTex, uv).r * _Intensity;

    axis = mul(_AxisRotationMatrix, float4(p, 0)).xyz;
    axis = get_uv(axis);
    min = step(_SliceMin.x, axis.x) * step(_SliceMin.y, axis.y) * step(_SliceMin.z, axis.z);
    max = step(axis.x, _SliceMax.x) * step(axis.y, _SliceMax.y) * step(axis.z, _SliceMax.z);

    float coronalResult = v2 * min * max + axisResult;
    //return coronalResult;

    //Coronal
    uv = mul(uv - 0.5, AngleAxis3x3(_CoronalAngle, float3(1, 0, 0))) + 0.5;
    //uv *= _Scale;
    float v3 = tex3D(_SagittalMainTex, uv).r * _Intensity;

    axis = mul(_AxisRotationMatrix, float4(p, 0)).xyz;
    axis = get_uv(axis);
    min = step(_SliceMin.x, axis.x) * step(_SliceMin.y, axis.y) * step(_SliceMin.z, axis.z);
    max = step(axis.x, _SliceMax.x) * step(axis.y, _SliceMax.y) * step(axis.z, _SliceMax.z);

    float sagittalResult = v3 * min * max + coronalResult;
    return sagittalResult;*/

    uv = mul(uv - 0.5, AngleAxis3x3(_CoronalAngle, float3(1, 0, 0))) + 0.5;
    //uv *= _Scale;
    float v3 = tex3D(_UsedMainTex, uv).r * _Intensity;

    float3 axis = mul(_AxisRotationMatrix, float4(p, 0)).xyz;
    axis = get_uv(axis);
    float min = step(_SliceMin.x, axis.x) * step(_SliceMin.y, axis.y) * step(_SliceMin.z, axis.z);
    float max = step(axis.x, _SliceMax.x) * step(axis.y, _SliceMax.y) * step(axis.z, _SliceMax.z);

    float studyResult = v3 * min * max;
    return studyResult;
}

/*bool outside(float3 uv)
{
    const float EPSILON = 0.01;
    float lower = -EPSILON;
    float upper = 1 + EPSILON;
    return (
        uv.x < lower || uv.y < lower || uv.z < lower ||
        uv.x > upper || uv.y > upper || uv.z > upper
        );
}*/

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 world : TEXCOORD1;
    float3 local : TEXCOORD2;
};

v2f vert(appdata v)
{
    v2f o;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.local = v.vertex.xyz;
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
  Ray ray;
    // ray.origin = localize(i.world);
    ray.origin = i.local;

    // world space direction to object space
    float3 dir = (i.world - _WorldSpaceCameraPos);
    ray.dir = normalize(mul(unity_WorldToObject, dir));

    AABB aabb;
    aabb.min = float3(-0.5, -0.5, -0.5);
    aabb.max = float3(0.5, 0.5, 0.5);

    float tnear;
    float tfar;
    intersect(ray, aabb, tnear, tfar);

    tnear = max(0.0, tnear);

    // float3 start = ray.origin + ray.dir * tnear;
    float3 start = ray.origin;
    float3 end = ray.origin + ray.dir * tfar;
    float dist = abs(tfar - tnear);//float dist = distance(start, end);
    float step_size = dist / float(ITERATIONS);
    float3 ds = normalize(end - start) * step_size;

    float4 dst = float4(0, 0, 0, 0);
    float3 p = start;

    [unroll]
    for (int iter = 0; iter < ITERATIONS; iter++)
    {
      float3 uv = get_uv(p);

      float v = sample_volume(uv, p);
      
      float4 src = float4(v, v, v, v);
      src.a *= 0.5;
      src.rgb *= src.a;

      // blend
      dst = (1.0 - dst.a) * src + dst;
      p += ds;

      if (dst.a > _Threshold) break;
}

return saturate(dst) * _Color;
}

#endif 