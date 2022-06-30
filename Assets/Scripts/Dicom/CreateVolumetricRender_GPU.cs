using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using FellowOakDicom.Imaging.Render;

using Rendergon.Managers;
using Unity.Mathematics;

using System.Linq;
using System;
using UnityEditor;

namespace Rendergon.Dicom
{
    public class CreateVolumetricRender_GPU : MonoBehaviour
    {
        //Compute Shader
        public Material material;
        public ComputeShader m_VolumetricComputeShader;//attach .compute script
        string computeShaderName = "CSVolumetricWindowSample";
        int kernelID;
        Vector3Int m_CubeDim = new Vector3Int(0, 0, 0);

        //Compute Buffers
        ComputeBuffer m_AxialRaw16bitDataArray_CB;
        ComputeBuffer m_CoronalRaw16bitDataArray_CB;
        ComputeBuffer m_SagittalRaw16bitDataArray_CB;
        
        ComputeBuffer m_AxialColorData_CB;
        ComputeBuffer m_CoronalColorData_CB;
        ComputeBuffer m_SagittalColorData_CB;

        ComputeBuffer m_ReadRenderTex3DColorData_CB;

        //Arrays
        int[] m_AxialRaw16bitData_Array;
        int[] m_CoronalRaw16bitData_Array;
        int[] m_SagittalRaw16bitData_Array;

        float4[] m_AxialColorData_Array;
        float4[] m_CoronalColorData_Array;
        float4[] m_SagittalColorData_Array;
        float4[] m_AxialColorDataToRead_Array;
        float4[] m_CoronalColorDataToRead_Array;
        float4[] m_SagittalColorDataToRead_Array;

        int groupSizeX, groupSizeY;
        [HideInInspector]
        public int groupSizeZ;

        RenderTexture m_Axial_3D_OutputRenderTexture;
        string m_Axial_3D_OutputRenderTextureName = "m_Axial_3D_OutputRenderTexture";
        RenderTexture m_Coronal_3D_OutputRenderTexture;
        string m_Coronal_3D_OutputRenderTextureName = "m_Coronal_3D_OutputRenderTexture";
        RenderTexture m_Sagittal_3D_OutputRenderTexture;
        string m_Sagittal_3D_OutputRenderTextureName = "m_Sagittal_3D_OutputRenderTexture";

        Texture3D m_3D_ColorTexture;
        //Texture3D m_Axial_3D_LocationTexture;
        //public float max_X_Location = 0.0f, max_Y_Location = 0.0f, max_Z_Location = 0.0f;
        struct m_3D_ColorTextureProperties
        {
            public Color[] tempColFinal;
            public int m_ImageCount;
            public int m_TexWidth;
            public int m_TexHeight;
            public int m_SingleImageRaw16BitDataLen;
            public float4 m_ActiveStudyType;
        }

        m_3D_ColorTextureProperties m_3D_ColorTexturePropertiesStruct;

        float m_CurrentWindowWidth=400;
        float m_CurrentWindowCenter=40;
        float m_MaxColorValue;
        //float4 m_ActiveStudyType = new float4(0, 0, 0, 0);

        DicomManager m_DicomManagerScript;

        bool b_DebugMssg;
        bool b_FirstMessageCompleted;

        //Shader UI Controls
        Renderer cubeRenderer;
        [SerializeField] Color color = Color.white;
        [Range(0f, 1f)] public float threshold = 0.5f;
        [Range(0.5f, 5f)] public float intensity = 1.5f;
        [Range(0f, 1f)] public float sliceXMin = 0.0f, sliceXMax = 1.0f;
        [Range(0f, 1f)] public float sliceYMin = 0.0f, sliceYMax = 1.0f;
        [Range(0f, 1f)] public float sliceZMin = 0.0f, sliceZMax = 1.0f;
        public Quaternion axis = Quaternion.identity;

        bool b_ComputeBuffersInitCompleted;

        private void Start()
        {
            m_3D_ColorTexturePropertiesStruct = new m_3D_ColorTextureProperties();

            cubeRenderer = GetComponent<Renderer>();
            cubeRenderer.material = material;

            m_DicomManagerScript = GameObject.FindGameObjectWithTag("Managers").GetComponent<DicomManager>();

            m_DicomManagerScript.OnWindowWidth_LevelChange += WindowWidthLevelChangeHandler;

            b_DebugMssg = false;
        }

        private void WindowWidthLevelChangeHandler(string m_StudyListType, List<DicomParameters> m_StudyDicomParametersList, DicomManager.WindowWidth_Level newWindowWidth_Level, Vector2 windowWidth_Center)
        {
            if (m_DicomManagerScript.b_VolumetricRender && !m_DicomManagerScript.b_3D_RenderTexture && b_FirstMessageCompleted)
            {
                Debug.LogWarning("Hounsfield windowding not built for volumetric render with 3D Textures (only with 3D Render Textures).");

                if (!m_DicomManagerScript.b_3D_RenderTexture)
                    Debug.LogWarning("Volumetric Render using 3DTextures.");

                b_FirstMessageCompleted = true;
            }
            else
            {
                if(!b_FirstMessageCompleted)
                {
                    Debug.LogWarning("Volumetric Render using 3D Render Textures.");
                    b_FirstMessageCompleted = true;
                }

                for (int i = 0; i < m_StudyDicomParametersList.Count; i++)
                {
                    if (b_DebugMssg) Debug.Log("I'm at delegate Function :" + m_StudyDicomParametersList[i].ImageName);

                    if (newWindowWidth_Level == DicomManager.WindowWidth_Level.Manufacturer)
                    {
                        m_CurrentWindowWidth = m_StudyDicomParametersList[i].ManufacturerWindowWidth;
                        m_CurrentWindowCenter = m_StudyDicomParametersList[i].ManufacturerWindowCenter;
                    }
                    else
                    {
                        m_CurrentWindowWidth = windowWidth_Center.x;
                        m_CurrentWindowCenter = windowWidth_Center.y;
                    }
                }

                DispatchManager();
            }
        }

        private void OnValidate()
        {
            if(b_ComputeBuffersInitCompleted)
            {
                Constrain(ref sliceXMin, ref sliceXMax);
                Constrain(ref sliceYMin, ref sliceYMax);
                Constrain(ref sliceZMin, ref sliceZMax);

                SetComputeShaderProperties();
            }
        }

        void Constrain(ref float min, ref float max)
        {
            const float threshold = 0.025f;
            if (min > max - threshold)
            {
                min = max - threshold;
            }
            else if (max < min + threshold)
            {
                max = min + threshold;
            }
        }

        void InitDataArrays()
        {
            if(m_DicomManagerScript.m_AxialStudyDicomParametersList.Count>0)
            {
                m_AxialColorData_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_AxialColorDataToRead_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_AxialRaw16bitData_Array = new int[m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_Raw16bitDataArray.Length * m_DicomManagerScript.m_AxialStudyDicomParametersList.Count];
            }

            if (m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count>0)
            {
                m_CoronalColorData_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_CoronalColorDataToRead_Array= new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_CoronalRaw16bitData_Array = new int[m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_Raw16bitDataArray.Length * m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count];
            }

            if (m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count>0)
            {
                m_SagittalColorData_Array= new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_SagittalColorDataToRead_Array= new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_SagittalRaw16bitData_Array = new int[m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_Raw16bitDataArray.Length * m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count];
            }
        }

        void InitComputeBuffers()
        {
            if(m_DicomManagerScript.m_AxialStudyDicomParametersList.Count>0)
            {
                m_AxialRaw16bitDataArray_CB = new ComputeBuffer(m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_Raw16bitDataArray.Length * m_DicomManagerScript.m_AxialStudyDicomParametersList.Count, (1) * sizeof(int));
                m_AxialColorData_CB = new ComputeBuffer(m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount, (4) * sizeof(float));
            }
            else
                m_AxialRaw16bitDataArray_CB = new ComputeBuffer(1, (1) * sizeof(int));

            if (m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count>0)
            {
                m_CoronalRaw16bitDataArray_CB = new ComputeBuffer(m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_Raw16bitDataArray.Length * m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count, (1) * sizeof(int));
                m_CoronalColorData_CB = new ComputeBuffer(m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount, (4) * sizeof(float));
            }
            else
                m_CoronalRaw16bitDataArray_CB = new ComputeBuffer(1, (1) * sizeof(int));

            if (m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count>0)
            {
                m_SagittalRaw16bitDataArray_CB = new ComputeBuffer(m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_Raw16bitDataArray.Length * m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count, (1) * sizeof(int));
                m_SagittalColorData_CB = new ComputeBuffer(m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount, (4) * sizeof(float));
            }
            else
                m_SagittalRaw16bitDataArray_CB = new ComputeBuffer(1, (1) * sizeof(int));

            if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0 || m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0 || m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
                m_ReadRenderTex3DColorData_CB = new ComputeBuffer(m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount, (4) * sizeof(float));
        }

        void AddDataToBuffer()
        {
            if(m_DicomManagerScript.m_AxialStudyDicomParametersList.Count>0)
            {
                m_AxialColorData_CB.SetData(m_AxialColorData_Array);
                m_ReadRenderTex3DColorData_CB.SetData(m_AxialColorDataToRead_Array);
                m_AxialRaw16bitDataArray_CB.SetData(m_AxialRaw16bitData_Array);
            }

            if (m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0)
            {
                m_CoronalColorData_CB.SetData(m_CoronalColorData_Array);
                m_ReadRenderTex3DColorData_CB.SetData(m_CoronalColorDataToRead_Array);
                m_CoronalRaw16bitDataArray_CB.SetData(m_CoronalRaw16bitData_Array);
            }

            if (m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
            {
                m_SagittalColorData_CB.SetData(m_SagittalColorData_Array);
                m_ReadRenderTex3DColorData_CB.SetData(m_SagittalColorDataToRead_Array);
                m_SagittalRaw16bitDataArray_CB.SetData(m_SagittalRaw16bitData_Array);
            }
        }

        public void InitManager(DicomManager.SerializedTextureFormat texFormat, Vector3 m_VolumeDimensions)
        {
            m_CubeDim = new Vector3Int((int)m_VolumeDimensions.x, (int)m_VolumeDimensions.y, (int)m_VolumeDimensions.z);

            //Texture3D t = (Texture3D)AssetDatabase.LoadAssetAtPath("Assets/m_3D_ColorTexture.asset", typeof(Texture3D));

            //assign kernelID
            kernelID = m_VolumetricComputeShader.FindKernel(computeShaderName);

            //Init 3DTex Properties
            FunctionExecute_By_StudySelector(Set_3DTexture_Properties);

            //Init Arrays
            InitDataArrays();
            
            //Initialize Compute Buffers
            InitComputeBuffers();

            //Populate DataArrays
            PopulateDataArrays("Axial");
            PopulateDataArrays("Coronal");
            PopulateDataArrays("Sagittal");

            //Add VolumetricDataArray to compute buffer
            AddDataToBuffer();

            m_MaxColorValue = (texFormat == DicomManager.SerializedTextureFormat.RGBAFloat ? 1 : 255);

            //Populate Compute Shader with kernel name and compute buffers
            //Init 3DTex Properties
            FunctionExecute_By_StudySelector(PopulateComputeShader);

            //Set depth for compute shader
            groupSizeZ = (int)m_VolumeDimensions.z;

            //Create Render Textures
            FunctionExecute_By_StudySelector(CreateRenderTexture);

            b_ComputeBuffersInitCompleted = true;

            //Dispatch
            DispatchManager();
        }

        public void PopulateComputeShader(string m_StudyType)
        {
            m_VolumetricComputeShader.SetBuffer(kernelID, "m_AxialRaw16bitDataArray_CB", m_AxialRaw16bitDataArray_CB);
            m_VolumetricComputeShader.SetBuffer(kernelID, "m_CoronalRaw16bitDataArray_CB", m_CoronalRaw16bitDataArray_CB);
            m_VolumetricComputeShader.SetBuffer(kernelID, "m_SagittalRaw16bitDataArray_CB", m_SagittalRaw16bitDataArray_CB);

            if(m_AxialColorData_CB!=null)
                m_VolumetricComputeShader.SetBuffer(kernelID, "m_AllColorData_CB", m_AxialColorData_CB);

            if(m_CoronalColorData_CB!=null)
                m_VolumetricComputeShader.SetBuffer(kernelID, "m_AllColorData_CB", m_CoronalColorData_CB);
            
            if(m_SagittalColorData_CB!=null)
                m_VolumetricComputeShader.SetBuffer(kernelID, "m_AllColorData_CB", m_SagittalColorData_CB);

            m_VolumetricComputeShader.SetBuffer(kernelID, "m_ReadRenderTex3DColorData_CB", m_ReadRenderTex3DColorData_CB);
        }

        public void CreateRenderTexture(string m_StudyType)
        {
            int depthBuffer = 0;
            RenderTextureFormat thisTexFormat = (m_DicomManagerScript.m_SerializedDicomTextureFormat == DicomManager.SerializedTextureFormat.RGBAFloat ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32);

            //compute buffer requires initiating all render textures
            m_Axial_3D_OutputRenderTexture = new RenderTexture(m_CubeDim.x, m_CubeDim.y, depthBuffer, thisTexFormat);//, RenderTextureReadWrite.Linear
            m_Axial_3D_OutputRenderTexture.enableRandomWrite = true;
            m_Axial_3D_OutputRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            m_Axial_3D_OutputRenderTexture.volumeDepth = m_CubeDim.z;
            m_Axial_3D_OutputRenderTexture.Create();

            m_Coronal_3D_OutputRenderTexture = new RenderTexture(m_CubeDim.x, m_CubeDim.y, depthBuffer, thisTexFormat);//, RenderTextureReadWrite.Linear
            m_Coronal_3D_OutputRenderTexture.enableRandomWrite = true;
            m_Coronal_3D_OutputRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            m_Coronal_3D_OutputRenderTexture.volumeDepth = m_CubeDim.z;
            m_Coronal_3D_OutputRenderTexture.Create();

            m_Sagittal_3D_OutputRenderTexture = new RenderTexture(m_CubeDim.x, m_CubeDim.y, depthBuffer, thisTexFormat);//, RenderTextureReadWrite.Linear
            m_Sagittal_3D_OutputRenderTexture.enableRandomWrite = true;
            m_Sagittal_3D_OutputRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            m_Sagittal_3D_OutputRenderTexture.volumeDepth = m_CubeDim.z;
            m_Sagittal_3D_OutputRenderTexture.Create();

            if (m_DicomManagerScript.b_3D_RenderTexture)
            {
                //Set Texture to material
                if (m_Axial_3D_OutputRenderTexture != null)
                    cubeRenderer.material.SetTexture("_AxialMainTex", m_Axial_3D_OutputRenderTexture);

                if (m_Coronal_3D_OutputRenderTexture != null)
                    cubeRenderer.material.SetTexture("_CoronalMainTex", m_Coronal_3D_OutputRenderTexture);

                if (m_Sagittal_3D_OutputRenderTexture != null)
                    cubeRenderer.material.SetTexture("_SagittalMainTex", m_Sagittal_3D_OutputRenderTexture);
            }

            //Set Texture to compute shader
            m_VolumetricComputeShader.SetTexture(kernelID, m_Axial_3D_OutputRenderTextureName, m_Axial_3D_OutputRenderTexture);
            if (b_DebugMssg) Debug.Log("Format output:" + m_Axial_3D_OutputRenderTexture.format);

            m_VolumetricComputeShader.SetTexture(kernelID, m_Coronal_3D_OutputRenderTextureName, m_Coronal_3D_OutputRenderTexture);
            if (b_DebugMssg) Debug.Log("Format output:" + m_Coronal_3D_OutputRenderTexture.format);

            m_VolumetricComputeShader.SetTexture(kernelID, m_Sagittal_3D_OutputRenderTextureName, m_Sagittal_3D_OutputRenderTexture);
            if (b_DebugMssg) Debug.Log("Format output:" + m_Sagittal_3D_OutputRenderTexture.format);
        }

        public void DispatchManager()
        {
            if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0 || m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0 || m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
                DispatchComputeShader();
        }

        public void DispatchComputeShader()
        {
            SetComputeShaderProperties();

            uint threadsX;
            uint threadsY;
            uint threadsZ;

            float thisDicomWidth = (float) m_3D_ColorTexturePropertiesStruct.m_TexWidth;
            float thisDicomHeight = (float) m_3D_ColorTexturePropertiesStruct.m_TexHeight;
            float m_StudyCount = (float)m_3D_ColorTexturePropertiesStruct.m_ImageCount;

            m_VolumetricComputeShader.GetKernelThreadGroupSizes(kernelID, out threadsX, out threadsY, out threadsZ);
            groupSizeX = Mathf.CeilToInt((float)thisDicomWidth / (float)threadsX);
            groupSizeY = Mathf.CeilToInt((float)thisDicomHeight / (float)threadsY);
            groupSizeZ = Mathf.CeilToInt(m_StudyCount / (float)threadsZ);
            m_VolumetricComputeShader.Dispatch(kernelID, groupSizeX, groupSizeY, groupSizeZ);

            //Debug.Log("Ended volumetric compute shader" + m_AxialColorData_CB);

            //Set Texture to material and compute shader
            /*if(cubeRenderer!=null)
            {
                cubeRenderer.enabled = true;
                cubeRenderer.material = material;
            }*/

            //Debug.Log("Finished " + m_Axial_3D_OutputRenderTexture);

            //Create 3DTexture
            if (!m_DicomManagerScript.b_3D_RenderTexture)
                FunctionExecute_By_StudySelector(Create3DColorTexture);
            //else: no worries, 3d render texture was already created

            //float4[] tempArray = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];

            //if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0)
            //{
            //float4[] tempArray = m_AxialColorDataToRead_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
            float4[] tempArray = m_AxialColorDataToRead_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
            m_ReadRenderTex3DColorData_CB.GetData(tempArray);
            //}

            if (m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0)
            {
                //float4[] tempArray = m_CoronalColorDataToRead_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_ReadRenderTex3DColorData_CB.GetData(tempArray);
            }

            if (m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
            {
                //float4[] tempArray = m_SagittalColorDataToRead_Array = new float4[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_3D_ColorTexturePropertiesStruct.m_ImageCount];
                m_ReadRenderTex3DColorData_CB.GetData(tempArray);
            }

            //Debug.Log(m_ReadRenderTex3DColorData_CB + " is now populated. Study is " + m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType);
        }

        void FunctionExecute_By_StudySelector(Action<string> m_Function)
        {
            if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count == 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count == 0)
                m_Function("Axial");
            else if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count == 0)
                m_Function("Axial_Coronal");
            else if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count == 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
                m_Function("Axial_Sagittal");
            else if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count == 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
                m_Function("Coronal_Sagittal");
            else if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
                m_Function("Axial_Coronal_Sagittal");
            else if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count == 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count > 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count == 0)
                m_Function("Coronal");
            else if (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count == 0 && m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count == 0 && m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count > 0)
                m_Function("Sagittal");
        }

        void Set_3DTexture_Properties(string m_StudyType)
        {
            switch (m_StudyType)
            {
                case "Axial":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_DicomManagerScript.m_AxialStudyDicomParametersList.Count];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_AxialStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(1, 0, 0, 0);

                    break;

                case "Coronal":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(0, 1, 0, 0);

                    break;

                case "Sagittal":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(0, 0, 1, 0);

                    break;

                case "Axial_Coronal":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count + m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count)];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_AxialStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(1, 1, 0, 0);

                    break;

                case "Axial_Sagittal":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count + m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count)];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_SagittalStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(1, 0, 1, 0);

                    break;

                case "Coronal_Sagittal":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * (m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count + m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count)];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_CoronalStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(0, 1, 1, 0);

                    break;

                case "Axial_Coronal_Sagittal":
                    m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_Raw16bitDataArray.Length;
                    m_3D_ColorTexturePropertiesStruct.tempColFinal = new Color[m_3D_ColorTexturePropertiesStruct.m_SingleImageRaw16BitDataLen * (m_DicomManagerScript.m_AxialStudyDicomParametersList.Count + m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count + m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count)];
                    m_3D_ColorTexturePropertiesStruct.m_ImageCount = m_DicomManagerScript.m_AxialStudyDicomParametersList.Count + m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count + m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count;
                    m_3D_ColorTexturePropertiesStruct.m_TexWidth = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureWidth;
                    m_3D_ColorTexturePropertiesStruct.m_TexHeight = m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureHeight;
                    m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType = new float4(1, 1, 1, 0);

                    break;
            }
        }

        void Create3DColorTexture(string m_StudyType)
        {
            if(m_StudyType.Equals("Axial"))
                m_AxialColorData_CB.GetData(m_3D_ColorTexturePropertiesStruct.tempColFinal);
            
            if (m_StudyType.Equals("Coronal"))
                m_CoronalColorData_CB.GetData(m_3D_ColorTexturePropertiesStruct.tempColFinal);
            
            if (m_StudyType.Equals("Sagittal"))
                m_SagittalColorData_CB.GetData(m_3D_ColorTexturePropertiesStruct.tempColFinal);

            m_3D_ColorTexture = new Texture3D(m_3D_ColorTexturePropertiesStruct.m_TexWidth, m_3D_ColorTexturePropertiesStruct.m_TexHeight, m_3D_ColorTexturePropertiesStruct.m_ImageCount, TextureFormat.RGBAFloat, false);
            m_3D_ColorTexture.filterMode = FilterMode.Point;
            m_3D_ColorTexture.wrapMode = TextureWrapMode.Clamp;

            m_3D_ColorTexture.SetPixels(m_3D_ColorTexturePropertiesStruct.tempColFinal);
            
            //m_3D_ColorTexturePropertiesStruct.tempColFinal = null;//TODO set to null?
            
            m_3D_ColorTexture.Apply();

            if(m_StudyType.Equals("Axial"))
                cubeRenderer.material.SetTexture("_AxialMainTex", m_3D_ColorTexture);
                
            if (m_StudyType.Equals("Coronal"))
                cubeRenderer.material.SetTexture("_CoronalMainTex", m_3D_ColorTexture);

            if (m_StudyType.Equals("Sagittal"))
                cubeRenderer.material.SetTexture("_SagittalMainTex", m_3D_ColorTexture);

            cubeRenderer.material.SetTexture("_UsedMainTex", m_3D_ColorTexture);//Only 1 volumetric render at a time with 3DColorTexture

            //AssetDatabase.CreateAsset(m_3D_ColorTexture, "Assets/m_3D_ColorTexture.asset");
        }

        void SetComputeShaderProperties()
        {
            if(m_VolumetricComputeShader!=null)
            {
                m_VolumetricComputeShader.SetFloat("m_MaxColorValue", m_MaxColorValue);
                m_VolumetricComputeShader.SetFloat("m_CurrentWindowWidth", m_CurrentWindowWidth);
                m_VolumetricComputeShader.SetFloat("m_CurrentWindowCenter", m_CurrentWindowCenter);
                m_VolumetricComputeShader.SetInt("m_ImageCols", m_3D_ColorTexturePropertiesStruct.m_TexWidth);
                m_VolumetricComputeShader.SetInt("m_ImageRows", m_3D_ColorTexturePropertiesStruct.m_TexHeight);
                m_VolumetricComputeShader.SetVector("m_ActiveStudyType", m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType);
                m_VolumetricComputeShader.SetBool("b_RenderTexture", m_DicomManagerScript.b_3D_RenderTexture);
            }
            
            if(cubeRenderer!=null)
            {
                if(m_DicomManagerScript.b_3D_RenderTexture)
                {
                    if (m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType[0] == 1)
                    {
                        cubeRenderer.material.SetTexture("_AxialMainTex", m_Axial_3D_OutputRenderTexture);
                        cubeRenderer.material.SetTexture("_UsedMainTex", m_Axial_3D_OutputRenderTexture);//Only 1 volumetric render at a time
                    }

                    if (m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType[1] == 1)
                    {
                        cubeRenderer.material.SetTexture("_CoronalMainTex", m_Coronal_3D_OutputRenderTexture);
                        cubeRenderer.material.SetTexture("_UsedMainTex", m_Coronal_3D_OutputRenderTexture);//Only 1 volumetric render at a time
                    }

                    if (m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType[2] == 1)
                    {
                        cubeRenderer.material.SetTexture("_SagittalMainTex", m_Sagittal_3D_OutputRenderTexture);
                        cubeRenderer.material.SetTexture("_UsedMainTex", m_Sagittal_3D_OutputRenderTexture);//Only 1 volumetric render at a time
                    }

                }
                else
                {
                    if (m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType[0]==1)
                        cubeRenderer.material.SetTexture("_AxialMainTex", m_3D_ColorTexture);

                    if (m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType[1]==1)
                        cubeRenderer.material.SetTexture("_CoronalMainTex", m_3D_ColorTexture);

                    if (m_3D_ColorTexturePropertiesStruct.m_ActiveStudyType[2]==1)
                        cubeRenderer.material.SetTexture("_SagittalMainTex", m_3D_ColorTexture);
                }

                cubeRenderer.material.SetColor("_Color", color);
                cubeRenderer.material.SetFloat("_Threshold", threshold);
                cubeRenderer.material.SetFloat("_Intensity", intensity);
                cubeRenderer.material.SetVector("_SliceMin", new Vector3(sliceXMin, sliceYMin, sliceZMin));
                cubeRenderer.material.SetVector("_SliceMax", new Vector3(sliceXMax, sliceYMax, sliceZMax));
                cubeRenderer.material.SetMatrix("_AxisRotationMatrix", Matrix4x4.Rotate(axis));

                //if (b_DebugMssg) Debug.Log("SetMaterial Properties: _Threshold:" + threshold + " _Intensity:" + intensity + " _SliceMin:" + sliceXMin);
            }
            //if (b_DebugMssg) Debug.Log("SetComputeShaderProperties: m_MaxColorValue:" + m_MaxColorValue + " m_CurrentWindowWidth:"+ m_CurrentWindowWidth+ " m_CurrentWindowCenter:"+ m_CurrentWindowCenter + " m_VolumeDepth:"+ m_VolumeDepth);
        }

        public void PopulateDataArrays(string studyListType)
        {
            int currImgStartPos = 0;
            int cumulativePos = 0;

            //float xMax = 0.0f, yMax = 0.0f, zMax = 0.0f;
            #region Populate individual m_Study
            switch (studyListType)
            {
                case "Axial":
                    for (int i = 0; i < m_DicomManagerScript.m_AxialStudyDicomParametersList.Count; i++)
                    {

                        /*m_AxialImageNumberRow_Array[i] = m_DicomManagerScript.m_AxialStudyDicomParametersList[i].ImageNumberRow;
                        m_AxialImageNumberColumn_Array[i] = m_DicomManagerScript.m_AxialStudyDicomParametersList[i].ImageNumberCol;
                        m_AxialManufacturerWindowWidth_Array[i] = m_DicomManagerScript.m_AxialStudyDicomParametersList[i].ManufacturerWindowWidth;
                        m_AxialManufacturerWindowCenter_Array[i] = m_DicomManagerScript.m_AxialStudyDicomParametersList[i].ManufacturerWindowCenter;
                        m_AxialPosStartImageIndex_Array[i] = currImgStartPos;*/

                        for (int r = 0; r < (m_DicomManagerScript.m_AxialStudyDicomParametersList[i].m_DicomTextureHeight); r++)
                        {
                            for (int c = 0; c < (m_DicomManagerScript.m_AxialStudyDicomParametersList[i].m_DicomTextureWidth); c++)
                            {
                                m_AxialRaw16bitData_Array[currImgStartPos + (m_DicomManagerScript.m_AxialStudyDicomParametersList[i].ImageNumberCol * r + c)] = 
                                                (int)m_DicomManagerScript.m_AxialStudyDicomParametersList[i].m_Raw16bitDataArray[m_DicomManagerScript.m_AxialStudyDicomParametersList[i].ImageNumberCol * r + c];

                                cumulativePos++;
                            }
                        }

                        currImgStartPos=cumulativePos;
                    }
                    
                    break;

                case "Coronal":
                    for (int i = 0; i < m_DicomManagerScript.m_CoronalStudyDicomParametersList.Count; i++)
                    {
                        /*m_CoronalImageNumberRow_Array[i] = m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].ImageNumberRow;
                        m_CoronalImageNumberColumn_Array[i] = m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].ImageNumberCol;
                        m_CoronalManufacturerWindowWidth_Array[i] = m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].ManufacturerWindowWidth;
                        m_CoronalManufacturerWindowCenter_Array[i] = m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].ManufacturerWindowCenter;
                        m_CoronalPosStartImageIndex_Array[i] = currImgStartPos;*/

                        for (int r = 0; r < (m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].m_DicomTextureHeight); r++)
                        {
                            for (int c = 0; c < (m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].m_DicomTextureWidth); c++)
                            {
                                m_CoronalRaw16bitData_Array[currImgStartPos + (m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].ImageNumberCol * r + c)] =
                                                (int)m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].m_Raw16bitDataArray[m_DicomManagerScript.m_CoronalStudyDicomParametersList[i].ImageNumberCol * r + c];

                                cumulativePos++;
                            }
                        }

                        currImgStartPos = cumulativePos;
                    }

                    break;

                case "Sagittal":
                    for (int i = 0; i < m_DicomManagerScript.m_SagittalStudyDicomParametersList.Count; i++)
                    {
                        /*m_SagittalImageNumberRow_Array[i] = m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].ImageNumberRow;
                        m_SagittalImageNumberColumn_Array[i] = m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].ImageNumberCol;
                        m_SagittalManufacturerWindowWidth_Array[i] = m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].ManufacturerWindowWidth;
                        m_SagittalManufacturerWindowCenter_Array[i] = m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].ManufacturerWindowCenter;
                        m_SagittalPosStartImageIndex_Array[i] = currImgStartPos;*/

                        for (int r = 0; r < (m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].m_DicomTextureHeight); r++)
                        {
                            for (int c = 0; c < (m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].m_DicomTextureWidth); c++)
                            {
                                m_SagittalRaw16bitData_Array[currImgStartPos + (m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].ImageNumberCol * r + c)] =
                                                (int)m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].m_Raw16bitDataArray[m_DicomManagerScript.m_SagittalStudyDicomParametersList[i].ImageNumberCol * r + c];

                                cumulativePos++;
                            }
                        }

                        currImgStartPos = cumulativePos;
                    }
                    break;

                case "Axial_Coronal":
                    PopulateDataArrays("Axial");
                    PopulateDataArrays("Coronal");
                    break;

                case "Axial_Sagittal":
                    PopulateDataArrays("Axial");
                    PopulateDataArrays("Sagittal");
                    break;

                case "Coronal_Sagittal":
                    PopulateDataArrays("Coronal");
                    PopulateDataArrays("Sagittal");
                    break;

                case "Axial_Coronal_Sagittal":
                    PopulateDataArrays("Axial");
                    PopulateDataArrays("Coronal");
                    PopulateDataArrays("Sagittal");
                    break;
            }
            #endregion
        }

        void ReleaseBuffers()
        {
            if (m_AxialRaw16bitDataArray_CB != null)
                m_AxialRaw16bitDataArray_CB.Release();

            if (m_CoronalRaw16bitDataArray_CB != null)
                m_CoronalRaw16bitDataArray_CB.Release();

            if (m_SagittalRaw16bitDataArray_CB != null)
                m_SagittalRaw16bitDataArray_CB.Release();

            if (m_AxialColorData_CB != null)
                m_AxialColorData_CB.Release();

            if (m_CoronalColorData_CB!= null)
                m_CoronalColorData_CB.Release();

            if (m_SagittalColorData_CB != null)
                m_SagittalColorData_CB.Release();

            if (m_ReadRenderTex3DColorData_CB != null)
                m_ReadRenderTex3DColorData_CB.Release();
        }

        private void OnDestroy()
        {
            ReleaseBuffers();

            m_DicomManagerScript.OnWindowWidth_LevelChange -= WindowWidthLevelChangeHandler;
        }

        private void OnDisable()
        {
            ReleaseBuffers();

            m_DicomManagerScript.OnWindowWidth_LevelChange -= WindowWidthLevelChangeHandler;
        }
    }
}



/*void Create3LocationTexture()
{
    Color[] tempLocatFinal = new Color[m_DicomManagerScript.m_AxialStudyDicomParametersList[0].PixelLocationArray.Length * m_DicomManagerScript.m_AxialStudyDicomParametersList.Count];

    m_AxialPixelLocation_CB.GetData(tempLocatFinal);

    m_Axial_3D_LocationTexture = new Texture3D(m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureWidth,
                                        m_DicomManagerScript.m_AxialStudyDicomParametersList[0].m_DicomTextureHeight,
                                        m_DicomManagerScript.m_AxialStudyDicomParametersList.Count, TextureFormat.RGBAFloat, false);

    m_Axial_3D_LocationTexture.filterMode = FilterMode.Trilinear;

    m_Axial_3D_LocationTexture.wrapMode = TextureWrapMode.Clamp;

    m_Axial_3D_LocationTexture.SetPixels(tempLocatFinal);
    m_Axial_3D_LocationTexture.Apply();

    Renderer cubelRend = GetComponent<Renderer>();
    cubelRend.enabled = true;

    //Set Texture to material and compute shader
    cubelRend.material = material;
    cubelRend.material.SetTexture("_MainTex2", m_Axial_3D_LocationTexture);

    Debug.Log("Ended");
}*/

/*if (m_AxialPosStartImageIndex_CB != null)
                m_AxialPosStartImageIndex_CB.Release();

            if (m_CoronalPosStartImageIndex_CB != null)
                m_CoronalPosStartImageIndex_CB.Release();

            if (m_SagittalPosStartImageIndex_CB != null)
                m_SagittalPosStartImageIndex_CB.Release();*/

/*if (m_AxialPixelLocation_CB != null)
             m_AxialPixelLocation_CB.Release();

         if (m_CoronalPixelLocation_CB != null)
             m_CoronalPixelLocation_CB.Release();

         if (m_SagittalPixelLocation_CB != null)
             m_SagittalPixelLocation_CB.Release();

         if (m_AxialImageNumberRow_CB != null)
             m_AxialImageNumberRow_CB.Release();

         if (m_CoronalImageNumberRow_CB != null)
             m_CoronalImageNumberRow_CB.Release();

         if (m_SagittalImageNumberRow_CB != null)
             m_SagittalImageNumberRow_CB.Release();

         if (m_AxialImageNumberColumn_CB != null)
             m_AxialImageNumberColumn_CB.Release();

         if (m_CoronalImageNumberColumn_CB != null)
             m_CoronalImageNumberColumn_CB.Release();

         if (m_SagittalImageNumberColumn_CB != null)
             m_SagittalImageNumberColumn_CB.Release();

         if (m_AxialManufacturerWindowWidth_CB != null)
             m_AxialManufacturerWindowWidth_CB.Release();

         if (m_CoronalManufacturerWindowWidth_CB != null)
             m_CoronalManufacturerWindowWidth_CB.Release();

         if (m_SagittalManufacturerWindowWidth_CB != null)
             m_SagittalManufacturerWindowWidth_CB.Release();

         if (m_AxialManufacturerWindowCenter_CB != null)
             m_AxialManufacturerWindowCenter_CB.Release();

         if (m_CoronalManufacturerWindowCenter_CB != null)
             m_CoronalManufacturerWindowCenter_CB.Release();

         if (m_SagittalManufacturerWindowCenter_CB != null)
             m_SagittalManufacturerWindowCenter_CB.Release();*/


/*xMax=m_DicomManagerScript.m_AxialStudyDicomParametersList[i].PixelLocationArray.Max(v => Mathf.Abs(v.x));
if (max_X_Location < xMax) max_X_Location = xMax;

yMax = m_DicomManagerScript.m_AxialStudyDicomParametersList[i].PixelLocationArray.Max(v => Mathf.Abs(v.y));
if (max_Y_Location < yMax) max_Y_Location = yMax;

zMax = m_DicomManagerScript.m_AxialStudyDicomParametersList[i].PixelLocationArray.Max(v => Mathf.Abs(v.z));
if (max_Z_Location < zMax) max_Z_Location = zMax;*/


/*
                    for (int i = 0; i < m_AxialPixelLocation_Array.Length; i++)
                    {
                        m_AxialPixelLocation_Array[i].x /= max_X_Location;
                        m_AxialPixelLocation_Array[i].y /= max_Y_Location;
                        m_AxialPixelLocation_Array[i].z /= max_Z_Location;
                    }*/


/*ComputeBuffer m_AxialPixelLocation_CB;
ComputeBuffer m_CoronalPixelLocation_CB;
ComputeBuffer m_SagittalPixelLocation_CB;

ComputeBuffer m_AxialImageNumberRow_CB;
ComputeBuffer m_CoronalImageNumberRow_CB;
ComputeBuffer m_SagittalImageNumberRow_CB;

ComputeBuffer m_AxialImageNumberColumn_CB;
ComputeBuffer m_CoronalImageNumberColumn_CB;
ComputeBuffer m_SagittalImageNumberColumn_CB;

ComputeBuffer m_AxialManufacturerWindowWidth_CB;
ComputeBuffer m_CoronalManufacturerWindowWidth_CB;
ComputeBuffer m_SagittalManufacturerWindowWidth_CB;

ComputeBuffer m_AxialManufacturerWindowCenter_CB;
ComputeBuffer m_CoronalManufacturerWindowCenter_CB;
ComputeBuffer m_SagittalManufacturerWindowCenter_CB;*/
/*ComputeBuffer m_AxialPosStartImageIndex_CB;
       ComputeBuffer m_CoronalPosStartImageIndex_CB;
       ComputeBuffer m_SagittalPosStartImageIndex_CB;*/



/*float4[] m_AxialPixelLocation_Array;
float3[] m_CoronalPixelLocation_Array;
float3[] m_SagittalLocation_Array;

int[] m_AxialImageNumberRow_Array;
int[] m_CoronalImageNumberRow_Array;
int[] m_SagittalImageNumberRow_Array;

int[] m_AxialImageNumberColumn_Array;
int[] m_CoronalImageNumberColumn_Array;
int[] m_SagittalImageNumberColumn_Array;

int[] m_AxialManufacturerWindowWidth_Array;
int[] m_CoronalManufacturerWindowWidth_Array;
int[] m_SagittalManufacturerWindowWidth_Array;

int[] m_AxialManufacturerWindowCenter_Array;
int[] m_CoronalManufacturerWindowCenter_Array;
int[] m_SagittalManufacturerWindowCenter_Array;*/
/*int[] m_AxialPosStartImageIndex_Array;
       int[] m_CoronalPosStartImageIndex_Array;
       int[] m_SagittalPosStartImageIndex_Array;*/
