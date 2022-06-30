using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using FellowOakDicom.Imaging.Render;

using Rendergon.Managers;
using UnityEditor;

namespace Rendergon.Dicom
{
    public class CreateImageMethods_GPU : MonoBehaviour
    {
        public Material material;
        ComputeShader[] m_AxialComputeShader;
        ComputeShader[] m_CoronalComputeShader;
        ComputeShader[] m_SagittalComputeShader;
        string computeShaderName = "WindowingCompute";
        int kernelID;

        //Image Params
        ComputeBuffer m_AxialSampleBuffer;
        ComputeBuffer m_CoronalSampleBuffer;
        ComputeBuffer m_SagittalSampleBuffer;

        DicomPropertiesData[] m_AxialDicomPropertiesDataArray;
        DicomPropertiesData[] m_CoronalDicomPropertiesDataArray;
        DicomPropertiesData[] m_SagittalDicomPropertiesDataArray;

        //Raw Data
        ComputeBuffer[] m_AxialRawDataBuffer;
        ComputeBuffer[] m_CoronalRawDataBuffer;
        ComputeBuffer[] m_SagittalRawDataBuffer;

        DicomRawData[,] m_AxialRawDataArray;
        DicomRawData[,] m_CoronalRawDataArray;
        DicomRawData[,] m_SagittalRawDataArray;

        int groupSizeX, groupSizeY;
        
        RenderTexture[] m_AxialOutputTextureArray;
        RenderTexture[] m_CoronalOutputTextureArray;
        RenderTexture[] m_SagittalOutputTextureArray;

        int m_MaxColorValue;
        float m_CurrentWindowWidth = 0;
        float m_CurrentWindowCenter = 0;
        //float m_ManufacturerWindowWidth = 0;
        //float m_ManufacturerWindowCenter = 0;

        DicomManager m_DicomManagerScript;

        struct DicomPropertiesData
        {
            public int m_ImagePositionInList;
            public float ImageNumberRow;
            public float ImageNumberColumn;
            public int ManufacturerWindowWidth;
            public int ManufacturerWindowCenter;
        }

        struct DicomRawData
        {
            public int m_Raw16bitDataPoint;
        }

        bool b_DebugMssg;
        bool b_FirstMessageCompleted;

        private void Start()
        {
            m_DicomManagerScript = GameObject.FindGameObjectWithTag("Managers").GetComponent<DicomManager>();
            
            //Hounslow windowing not built for volumetric render
            m_DicomManagerScript.OnWindowWidth_LevelChange += WindowWidthLevelChangeHandler;

            b_DebugMssg = false;
        }

        private void WindowWidthLevelChangeHandler(string m_StudyListType, List<DicomParameters> m_StudyDicomParametersList, DicomManager.WindowWidth_Level newWindowWidth_Level, Vector2 windowWidth_Center)
        {
            if (m_DicomManagerScript.b_VolumetricRender)
            {
                if(!m_DicomManagerScript.b_3D_RenderTexture && !b_FirstMessageCompleted)
                {
                    Debug.LogWarning("Hounsfield windowding not built for volumetric render with 3D Textures (only for 3D Render Textures).");
                    b_FirstMessageCompleted = true;
                }
            }
            else
            {
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

                DispatchManager(m_StudyListType, m_StudyDicomParametersList, false);
            }
        }

        public void ComputeBufferInitManager(string m_StudyListType, List<DicomParameters> m_StudyDicomParametersList, DicomManager.SerializedTextureFormat texFormat)
        {
            InitStructs(m_StudyListType, m_StudyDicomParametersList.Count, m_StudyDicomParametersList[0].m_Raw16bitDataArray.Length);

            InitComputeBuffers(m_StudyListType, m_StudyDicomParametersList.Count, m_StudyDicomParametersList[0].m_Raw16bitDataArray.Length);

            for (int i = 0; i < m_StudyDicomParametersList.Count; i++)
            {
                DicomParameters thisImgDicomParams = m_StudyDicomParametersList[i];

                if (b_DebugMssg) Debug.Log("Ive init Image:" + thisImgDicomParams.ImageName);

                PopulateComputeBufferDicomPropertiesData(ref thisImgDicomParams, m_StudyListType, i);
                
                PopulateComputeBufferRawPixelData(ref thisImgDicomParams, m_StudyListType, i);
            }

            //compute buffer
            SetDataComputeBuffer(m_StudyListType, m_StudyDicomParametersList.Count);

            m_MaxColorValue = (texFormat == DicomManager.SerializedTextureFormat.RGBAFloat ? 1 : 255);

            //Init Compute Shaders
            InitComputeShader(m_StudyListType, m_StudyDicomParametersList.Count);

            //Populate Compute Shaders with kernel name and compute buffers
            for (int i = 0; i < m_StudyDicomParametersList.Count; i++)
            {
                PopulateComputeShader(m_StudyListType, i);
            }

            InitRenderTexture(m_StudyListType, m_StudyDicomParametersList.Count);

            //Create Render Textures
            for (int i = 0; i < m_StudyDicomParametersList.Count; i++)
            {
                CreateRenderTexture(m_StudyDicomParametersList[i], m_StudyListType, i);
            }

            //Remove raw16 data from main struct
            //DeleteRaw16BitDataFromMainClass(m_StudyDicomParametersList);

            //Dispatch
            DispatchManager(m_StudyListType, m_StudyDicomParametersList, true);
        }

        public void InitComputeShader(string m_StudyListType, int m_DicomImagesInThisStudyList)
        {
            switch (m_StudyListType)
            {
                case "Axial":
                    m_AxialComputeShader = new ComputeShader[m_DicomImagesInThisStudyList];
                    break;
                case "Coronal":
                    m_CoronalComputeShader = new ComputeShader[m_DicomImagesInThisStudyList];
                    break;
                case "Sagittal":
                    m_SagittalComputeShader = new ComputeShader[m_DicomImagesInThisStudyList];
                    break;
                default:
                    break;
            }
        }

        public void InitRenderTexture(string m_StudyListType, int listCount)
        {
            switch (m_StudyListType)
            {
                case "Axial":
                    m_AxialOutputTextureArray = new RenderTexture[listCount];
                    break;
                case "Coronal":
                    m_CoronalOutputTextureArray = new RenderTexture[listCount];
                    break;
                case "Sagittal":
                    m_SagittalOutputTextureArray = new RenderTexture[listCount];
                    break;
                default:
                    break;
            }
        }

        public void PopulateComputeShader(string m_StudyListType, int m_ImgPositionInList)
        {
            //instantitate
            switch (m_StudyListType)
            {
                case "Axial":
                    m_AxialComputeShader[m_ImgPositionInList] = (ComputeShader)Instantiate(Resources.Load(computeShaderName));
                    kernelID = m_AxialComputeShader[m_ImgPositionInList].FindKernel("CSWindowSample");
                    //bind compute buffer to the compute shader
                    m_AxialComputeShader[m_ImgPositionInList].SetBuffer(kernelID, "dicomPropertiesBuffer", m_AxialSampleBuffer);
                    m_AxialComputeShader[m_ImgPositionInList].SetBuffer(kernelID, "dicomRawDataBuffer", m_AxialRawDataBuffer[m_ImgPositionInList]);
                    break;
                case "Coronal":
                    m_CoronalComputeShader[m_ImgPositionInList] = (ComputeShader)Instantiate(Resources.Load(computeShaderName));
                    kernelID = m_CoronalComputeShader[m_ImgPositionInList].FindKernel("CSWindowSample");
                    //bind compute buffer to the compute shader
                    m_CoronalComputeShader[m_ImgPositionInList].SetBuffer(kernelID, "dicomPropertiesBuffer", m_CoronalSampleBuffer);
                    m_CoronalComputeShader[m_ImgPositionInList].SetBuffer(kernelID, "dicomRawDataBuffer", m_CoronalRawDataBuffer[m_ImgPositionInList]);
                    break;
                case "Sagittal":
                    m_SagittalComputeShader[m_ImgPositionInList] = (ComputeShader)Instantiate(Resources.Load(computeShaderName));
                    kernelID = m_SagittalComputeShader[m_ImgPositionInList].FindKernel("CSWindowSample");
                    //bind compute buffer to the compute shader
                    m_SagittalComputeShader[m_ImgPositionInList].SetBuffer(kernelID, "dicomPropertiesBuffer", m_SagittalSampleBuffer);
                    m_SagittalComputeShader[m_ImgPositionInList].SetBuffer(kernelID, "dicomRawDataBuffer", m_SagittalRawDataBuffer[m_ImgPositionInList]);
                    break;
                default:
                    break;
            }
        }

        public void CreateRenderTexture(DicomParameters thisImgDicomParams, string m_StudyListType, int m_ImgPositionInList)
        {
            //Create output Texture and set it to material
            RenderTextureFormat thisTexFormat = (m_DicomManagerScript.m_SerializedDicomTextureFormat == DicomManager.SerializedTextureFormat.RGBAFloat ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32);

            switch (m_StudyListType)
            {
                case "Axial":
                    m_AxialOutputTextureArray[m_ImgPositionInList] = new RenderTexture(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, 0, thisTexFormat);
                    m_AxialOutputTextureArray[m_ImgPositionInList].enableRandomWrite = true;
                    m_AxialOutputTextureArray[m_ImgPositionInList].Create();
                    Renderer axialRend = thisImgDicomParams.ImagePlaneGO.GetComponent<Renderer>();
                    axialRend.enabled = true;

                    //Set Texture to material and compute shader
                    axialRend.material = material;
                    axialRend.material.SetTexture("_MainTex", m_AxialOutputTextureArray[m_ImgPositionInList]);
                    m_AxialComputeShader[m_ImgPositionInList].SetTexture(kernelID, "outputTex", m_AxialOutputTextureArray[m_ImgPositionInList]);
                    if (b_DebugMssg) Debug.Log("Format output:" + m_AxialOutputTextureArray[m_ImgPositionInList].format);

                    break;
                case "Coronal":
                    m_CoronalOutputTextureArray[m_ImgPositionInList] = new RenderTexture(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, 0, thisTexFormat);
                    m_CoronalOutputTextureArray[m_ImgPositionInList].enableRandomWrite = true;
                    m_CoronalOutputTextureArray[m_ImgPositionInList].Create();
                    Renderer coronalRend = thisImgDicomParams.ImagePlaneGO.GetComponent<Renderer>();
                    coronalRend.enabled = true;

                    //Set Texture to material and compute shader
                    coronalRend.material = material;
                    coronalRend.material.SetTexture("_MainTex", m_CoronalOutputTextureArray[m_ImgPositionInList]);
                    m_CoronalComputeShader[m_ImgPositionInList].SetTexture(kernelID, "outputTex", m_CoronalOutputTextureArray[m_ImgPositionInList]);
                    if (b_DebugMssg) Debug.Log("Format output:" + m_CoronalOutputTextureArray[m_ImgPositionInList].format);

                    break;
                case "Sagittal":
                    m_SagittalOutputTextureArray[m_ImgPositionInList] = new RenderTexture(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, 0, thisTexFormat);
                    m_SagittalOutputTextureArray[m_ImgPositionInList] = new RenderTexture(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, 0, thisTexFormat);
                    m_SagittalOutputTextureArray[m_ImgPositionInList].enableRandomWrite = true;
                    m_SagittalOutputTextureArray[m_ImgPositionInList].Create();
                    Renderer sagittalRend = thisImgDicomParams.ImagePlaneGO.GetComponent<Renderer>();
                    sagittalRend.enabled = true;

                    //Set Texture to material and compute shader
                    sagittalRend.material = material;
                    sagittalRend.material.SetTexture("_MainTex", m_SagittalOutputTextureArray[m_ImgPositionInList]);
                    m_SagittalComputeShader[m_ImgPositionInList].SetTexture(kernelID, "outputTex", m_SagittalOutputTextureArray[m_ImgPositionInList]);
                    if (b_DebugMssg) Debug.Log("Format output:" + m_SagittalOutputTextureArray[m_ImgPositionInList].format);
                    break;
                default:
                    break;
            }
        }

        public void DispatchManager(string m_StudyListType, List<DicomParameters> m_StudyDicomParametersList, bool b_FirstTimeInit)
        {
          for(int i=0; i<m_StudyDicomParametersList.Count;i++)
            {
                DispatchComputeShader(m_StudyDicomParametersList[i].m_DicomTextureWidth, m_StudyDicomParametersList[i].m_DicomTextureHeight, i, m_StudyListType, b_FirstTimeInit);
            }
        }

        public void DispatchComputeShader(int thisDicomWidth, int thisDicomHeight, int m_ImgPositionInList, string m_StudyListType, bool b_FirstTimeInit)
        {
            if (b_FirstTimeInit)
            {
                switch (m_StudyListType)
                {
                    case "Axial":
                        m_CurrentWindowWidth = m_AxialDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowWidth;
                        m_CurrentWindowCenter = m_AxialDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowCenter;
                        if(b_DebugMssg) Debug.Log("Axial Image " + m_ImgPositionInList + " width:" + m_CurrentWindowWidth + " center: " + m_CurrentWindowCenter);
                        break;
                    case "Coronal":
                        m_CurrentWindowWidth = m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowWidth;
                        m_CurrentWindowCenter = m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowCenter;
                        if (b_DebugMssg) Debug.Log("Coronal Image " + m_ImgPositionInList + " width:" + m_CurrentWindowWidth + " center: " + m_CurrentWindowCenter);
                        break;
                    case "Sagittal":
                        m_CurrentWindowWidth = m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowWidth;
                        m_CurrentWindowCenter = m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowCenter;
                        if (b_DebugMssg) Debug.Log("Sagittal Image " + m_ImgPositionInList + " width:" + m_CurrentWindowWidth + " center: " + m_CurrentWindowCenter);
                        break;
                }
            }

            SetComputeShaderProperties(m_ImgPositionInList, m_StudyListType, thisDicomWidth);

            uint threadsX;
            uint threadsY;

            switch (m_StudyListType)
            {
                case "Axial":
                    m_AxialComputeShader[m_ImgPositionInList].GetKernelThreadGroupSizes(kernelID, out threadsX, out threadsY, out _);
                    groupSizeX = Mathf.CeilToInt((float)thisDicomWidth / (float)threadsX);
                    groupSizeY = Mathf.CeilToInt((float)thisDicomHeight / (float)threadsY);
                    m_AxialComputeShader[m_ImgPositionInList].Dispatch(kernelID, groupSizeX, groupSizeY, 1);
                    break;
                case "Coronal":
                    m_CoronalComputeShader[m_ImgPositionInList].GetKernelThreadGroupSizes(kernelID, out threadsX, out threadsY, out _);
                    groupSizeX = Mathf.CeilToInt((float)thisDicomWidth / (float)threadsX);
                    groupSizeY = Mathf.CeilToInt((float)thisDicomHeight / (float)threadsY);
                    m_CoronalComputeShader[m_ImgPositionInList].Dispatch(kernelID, groupSizeX, groupSizeY, 1);
                    break;
                case "Sagittal":
                    m_SagittalComputeShader[m_ImgPositionInList].GetKernelThreadGroupSizes(kernelID, out threadsX, out threadsY, out _);
                    groupSizeX = Mathf.CeilToInt((float)thisDicomWidth / (float)threadsX);
                    groupSizeY = Mathf.CeilToInt((float)thisDicomHeight / (float)threadsY);
                    m_SagittalComputeShader[m_ImgPositionInList].Dispatch(kernelID, groupSizeX, groupSizeY, 1);
                    break;
                default:
                    break;
            }
        }

        void SetComputeShaderProperties(int m_ImgPositionInList, string m_StudyListType, int m_TextureWidth)
        {
            switch (m_StudyListType)
            {
                case "Axial":
                    m_AxialComputeShader[m_ImgPositionInList].SetFloat("m_CurrentWindowWidth", m_CurrentWindowWidth);
                    m_AxialComputeShader[m_ImgPositionInList].SetFloat("m_CurrentWindowCenter", m_CurrentWindowCenter);
                    m_AxialComputeShader[m_ImgPositionInList].SetFloat("m_MaxColorValue", m_MaxColorValue);
                    m_AxialComputeShader[m_ImgPositionInList].SetInt("m_TextureWidth", m_TextureWidth);
                    break;
                case "Coronal":
                    m_CoronalComputeShader[m_ImgPositionInList].SetFloat("m_CurrentWindowWidth", m_CurrentWindowWidth);
                    m_CoronalComputeShader[m_ImgPositionInList].SetFloat("m_CurrentWindowCenter", m_CurrentWindowCenter);
                    m_CoronalComputeShader[m_ImgPositionInList].SetFloat("m_MaxColorValue", m_MaxColorValue);
                    m_CoronalComputeShader[m_ImgPositionInList].SetInt("m_TextureWidth", m_TextureWidth);
                    break;
                case "Sagittal":
                    m_SagittalComputeShader[m_ImgPositionInList].SetFloat("m_CurrentWindowWidth", m_CurrentWindowWidth);
                    m_SagittalComputeShader[m_ImgPositionInList].SetFloat("m_CurrentWindowCenter", m_CurrentWindowCenter);
                    m_SagittalComputeShader[m_ImgPositionInList].SetFloat("m_MaxColorValue", m_MaxColorValue);
                    m_SagittalComputeShader[m_ImgPositionInList].SetInt("m_TextureWidth", m_TextureWidth);
                    break;
                default:
                    break;
            }

            if (b_DebugMssg) Debug.Log("Dispatch: m_CurrentWindowWidth:" + m_CurrentWindowWidth + " m_CurrentWindowCenter:" + m_CurrentWindowCenter + " m_MaxColorValue:" + m_MaxColorValue + " m_TextureWidth:" + m_TextureWidth);
        }

        //Helper Functions
        void InitComputeBuffers(string studyListType, int listCount, int m_Raw16bitDataArrayLength)
        {
            int dicomStructStride = (3) * sizeof(int) + 2 * sizeof(float);
            int dicomRawDataStride = (1) * sizeof(int) + 0 * sizeof(float);

            switch (studyListType)
            {
                case "Axial":
                    m_AxialSampleBuffer = new ComputeBuffer(listCount, dicomStructStride);
                    
                    m_AxialRawDataBuffer = new ComputeBuffer[listCount];
                    for (int i = 0; i < listCount; i++)
                        m_AxialRawDataBuffer[i] = new ComputeBuffer(m_Raw16bitDataArrayLength, dicomRawDataStride);
                    break;
                case "Coronal":
                    m_CoronalSampleBuffer = new ComputeBuffer(listCount, dicomStructStride);

                    m_CoronalRawDataBuffer = new ComputeBuffer[listCount];
                    for (int i = 0; i < listCount; i++)
                        m_CoronalRawDataBuffer[i] = new ComputeBuffer(m_Raw16bitDataArrayLength, dicomRawDataStride);
                    break;
                case "Sagittal":
                    m_SagittalSampleBuffer = new ComputeBuffer(listCount, dicomStructStride);

                    m_SagittalRawDataBuffer = new ComputeBuffer[listCount];
                    for (int i=0;i<listCount;i++)
                        m_SagittalRawDataBuffer[i] = new ComputeBuffer(m_Raw16bitDataArrayLength, dicomRawDataStride);
                    break;
                default:
                    break;
            }
        }

        public void InitStructs(string studyListType, int thisStudyListCount, int m_RawDataCount)
        {
            switch (studyListType)
            {
                case "Axial":
                    m_AxialDicomPropertiesDataArray = new DicomPropertiesData[thisStudyListCount];
                    m_AxialRawDataArray= new DicomRawData[thisStudyListCount, m_RawDataCount];
                    break;
                case "Coronal":
                    m_CoronalDicomPropertiesDataArray = new DicomPropertiesData[thisStudyListCount];
                    m_CoronalRawDataArray = new DicomRawData[thisStudyListCount, m_RawDataCount];
                    break;
                case "Sagittal":
                    m_SagittalDicomPropertiesDataArray = new DicomPropertiesData[thisStudyListCount];
                    m_SagittalRawDataArray = new DicomRawData[thisStudyListCount, m_RawDataCount];
                    break;
                default:
                    break;
            }
        }

        DicomRawData[] SliceMultiDimArray(int imagePositionInArray, DicomRawData[,] m_StudyRawDataArray)
        {
            int len = m_StudyRawDataArray.GetLength(1);//1 to get length in second dimension

            DicomRawData[] tempArray = new DicomRawData[len];

            for (int i = 0; i < len; i++)
                tempArray[i] = m_StudyRawDataArray[imagePositionInArray,i];

            return tempArray;
        }

        public void SetDataComputeBuffer(string studyListType, int listCount)
        {
            switch (studyListType)
            {
                case "Axial":
                    m_AxialSampleBuffer.SetData(m_AxialDicomPropertiesDataArray);
                    for (int i = 0; i < listCount; i++)
                    {
                        DicomRawData[] thisImageRawData = SliceMultiDimArray(i, m_AxialRawDataArray);
                        m_AxialRawDataBuffer[i].SetData(thisImageRawData);
                    }
                    break;
                case "Coronal":
                    m_CoronalSampleBuffer.SetData(m_CoronalDicomPropertiesDataArray);
                    for (int i = 0; i < listCount; i++)
                        m_CoronalRawDataBuffer[i].SetData(SliceMultiDimArray(i, m_CoronalRawDataArray));
                    break;
                case "Sagittal":
                    m_SagittalSampleBuffer.SetData(m_SagittalDicomPropertiesDataArray);
                    for (int i = 0; i < listCount; i++)
                        m_SagittalRawDataBuffer[i].SetData(SliceMultiDimArray(i, m_SagittalRawDataArray));
                    break;
                default:
                    break;
            }
        }

        public void PopulateComputeBufferRawPixelData(ref DicomParameters thisImgDicomParams, string studyListType, int m_ImgPositionInList)
        {
            switch (studyListType)
            {
                case "Axial":
                    for (int r = 0; r < (thisImgDicomParams.m_DicomTextureHeight); r++)
                        for (int c = 0; c < (thisImgDicomParams.m_DicomTextureWidth); c++)
                            m_AxialRawDataArray[m_ImgPositionInList, thisImgDicomParams.ImageNumberCol * r + c].m_Raw16bitDataPoint = (int)thisImgDicomParams.m_Raw16bitDataArray[thisImgDicomParams.ImageNumberCol * r + c];
                    
                    break;

                case "Coronal":
                    for (int r = 0; r < (thisImgDicomParams.m_DicomTextureHeight); r++)
                        for (int c = 0; c < (thisImgDicomParams.m_DicomTextureWidth); c++)
                            m_CoronalRawDataArray[m_ImgPositionInList, thisImgDicomParams.ImageNumberCol * r + c].m_Raw16bitDataPoint = (int)thisImgDicomParams.m_Raw16bitDataArray[thisImgDicomParams.ImageNumberCol * r + c];
                    
                    break;

                case "Sagittal":
                    for (int r = 0; r < (thisImgDicomParams.m_DicomTextureHeight); r++)
                        for (int c = 0; c < (thisImgDicomParams.m_DicomTextureWidth); c++)
                            m_SagittalRawDataArray[m_ImgPositionInList, thisImgDicomParams.ImageNumberCol * r + c].m_Raw16bitDataPoint = (int)thisImgDicomParams.m_Raw16bitDataArray[thisImgDicomParams.ImageNumberCol * r + c];

                    break;
            }
        }

        public void PopulateComputeBufferDicomPropertiesData(ref DicomParameters thisImgDicomParams, string studyListType, int m_ImgPositionInList)
        {
            switch (studyListType)
            {
                case "Axial":
                    m_AxialDicomPropertiesDataArray[m_ImgPositionInList].m_ImagePositionInList = m_ImgPositionInList;
                    m_AxialDicomPropertiesDataArray[m_ImgPositionInList].ImageNumberRow = thisImgDicomParams.ImageNumberRow;
                    m_AxialDicomPropertiesDataArray[m_ImgPositionInList].ImageNumberColumn = thisImgDicomParams.ImageNumberCol;
                    m_AxialDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowWidth = thisImgDicomParams.ManufacturerWindowWidth;
                    m_AxialDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowCenter = thisImgDicomParams.ManufacturerWindowCenter;
                   
                    break;
                case "Coronal":
                    m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].m_ImagePositionInList = m_ImgPositionInList;
                    m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].ImageNumberRow = thisImgDicomParams.ImageNumberRow;
                    m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].ImageNumberColumn = thisImgDicomParams.ImageNumberCol;
                    m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowWidth = thisImgDicomParams.ManufacturerWindowWidth;
                    m_CoronalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowCenter = thisImgDicomParams.ManufacturerWindowCenter;
                    
                    break;
                case "Sagittal":
                    m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].m_ImagePositionInList = m_ImgPositionInList;
                    m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].ImageNumberRow = thisImgDicomParams.ImageNumberRow;
                    m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].ImageNumberColumn = thisImgDicomParams.ImageNumberCol;
                    m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowWidth = thisImgDicomParams.ManufacturerWindowWidth;
                    m_SagittalDicomPropertiesDataArray[m_ImgPositionInList].ManufacturerWindowCenter = thisImgDicomParams.ManufacturerWindowCenter;
                    
                    break;

                default:
                    break;
            }
        }

        public void DeleteRaw16BitDataFromMainClass(List<DicomParameters> m_StudyDicomParametersList)
        {
            for (int i = 0; i < m_StudyDicomParametersList.Count; i++)
            {
                m_StudyDicomParametersList[i].m_Raw16bitDataArray = null;
            }
        }

        private void OnDestroy()
        {
            if (m_AxialSampleBuffer != null)
                m_AxialSampleBuffer.Release();

            m_DicomManagerScript.OnWindowWidth_LevelChange -= WindowWidthLevelChangeHandler;
        }

        private void OnDisable()
        {
            if (m_AxialSampleBuffer != null)
                m_AxialSampleBuffer.Release();

            if(m_AxialRawDataBuffer!=null)
            {
                for (int i = 0; i < m_AxialRawDataBuffer.Length; i++)
                {
                    if (m_AxialRawDataBuffer[i] != null)
                        m_AxialRawDataBuffer[i].Release();
                }
            }

            if (m_CoronalSampleBuffer != null)
                m_CoronalSampleBuffer.Release();

            if(m_CoronalRawDataBuffer!=null)
            {
                for (int i = 0; i < m_CoronalRawDataBuffer.Length; i++)
                {
                    if (m_CoronalRawDataBuffer[i] != null)
                        m_CoronalRawDataBuffer[i].Release();
                }
            }

            if (m_SagittalSampleBuffer != null)
                m_SagittalSampleBuffer.Release();

            if(m_SagittalRawDataBuffer!=null)
            {
                for (int i = 0; i < m_SagittalRawDataBuffer.Length; i++)
                {
                    if (m_SagittalRawDataBuffer[i] != null)
                        m_SagittalRawDataBuffer[i].Release();
                }
            }

            m_DicomManagerScript.OnWindowWidth_LevelChange -= WindowWidthLevelChangeHandler;
        }
    }
}
