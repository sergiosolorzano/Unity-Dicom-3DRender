Shader "Custom/ObjectRaymarch"
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_UsedMainTex("UsedMainTex", 3D) = "" {}
		_AxialMainTex("_AxialMainTex", 3D) = "" {}
		_CoronalMainTex("_CoronalMainTex", 3D) = "" {}
		_SagittalMainTex("_SagittalMainTex", 3D) = "" {}
		_Intensity("Intensity", Range(1.0, 5.0)) = 1.2
		_Threshold("Threshold", Range(0.0, 1.0)) = 0.95
		_SliceMin("Slice min", Vector) = (0.0, 0.0, 0.0, -1.0)
		_SliceMax("Slice max", Vector) = (1.0, 1.0, 1.0, -1.0)
		_CoronalAngle("Coronal Angle", Range(-90.0,  90.0)) = 90.0
		_Scale("Scale", Range(0,  1)) = 1
	}

		CGINCLUDE

			ENDCG

			SubShader{
				Cull Back
				Blend SrcAlpha OneMinusSrcAlpha
			// ZTest Always

			Pass
			{
				CGPROGRAM

		  #define ITERATIONS 100
				#include "./VolumeRendering.cginc"
				#pragma vertex vert
				#pragma fragment frag

				ENDCG
			}
		}
}