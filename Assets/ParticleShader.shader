Shader "Custom/ParticleShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
    #pragma surface surf Standard fullforwardshadows
    #pragma target 3.0

    sampler2D _MainTex;

    struct Input {
        float2 uv_MainTex;

        // Modification 1: add a vertex colour parameter to the Input struct.
        float4 vertexColor : COLOR;
    };

    half _Glossiness;
    half _Metallic;
    fixed4 _Color;

    void surf(Input IN, inout SurfaceOutputStandard o) {
        fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

        // Modification 2: multiply the albedo by this vertex colour.            
        //c *= IN.vertexColor;
        c = IN.vertexColor;

        o.Albedo = c.rgb;
        o.Metallic = _Metallic;
        o.Smoothness = _Glossiness;
        o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
