using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
//using System.Diagnostics;
using FellowOakDicom.Log;
using FellowOakDicom.Media;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.LUT;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.Imaging.Mathematics;
using FellowOakDicom.IO;
using FellowOakDicom.IO.Buffer;
using System.Linq;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.UnityConverters;
using Newtonsoft.Json.UnityConverters.Math;
using System.Threading.Tasks;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

using Rendergon.Dicom;
using Rendergon.Storage;
using Rendergon.UI;
using Rendergon.Computer_Graphics;
using Rendergon.Player;
using Rendergon.Utilities;
using UnityEditor;
using UnityEngine.UI;

/*Dicom images data processed with the Fellow Oak DICOM library (https://github.com/fo-dicom/fo-dicom) with its license https://github.com/fo-dicom/fo-dicom/blob/development/License.txt.
The MIT License (MIT) 
Copyright(c) Microsoft Corporation
Permission is hereby granted, free of charge, to any person obtaining a copy  of this software and associated documentation files (the "Software"), to deal  in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell  copies of the Software, and to permit persons to whom the Software is  furnished to do so, subject to the following conditions: */

namespace Rendergon.Managers
{
    public class DicomManager : MonoBehaviour
    {
        [HideInInspector]
        private readonly string PathToDicomImages = Path.Combine(@"G:\New-GoogleDrive\DesktopBackup\RENDERGON\Business\Deals\Pitches\CT-3D\TACS_Tests\TUMOR RENAL\DICOM");

        [HideInInspector]
        UI_Methods UIMethods;
        //[HideInInspector]
        ExtractDicomDataMethods m_ExtractDicomDataMethods;
        [HideInInspector]
        FreeFlyCamera m_FreeFlyCamera;
        [HideInInspector]
        CreateImageMethods_CPU m_CreateImageMethods_CPU;
        [HideInInspector]
        CreateImageMethods_GPU m_CreateImageMethods_GPU;
        [HideInInspector]
        CreateVolumetricRender_GPU m_CreateVolumetricRender_GPU;

        [Header("Dicom Settings:")]
        public ExtractDicomDataMethods.TargetCut SimulationOrientation= ExtractDicomDataMethods.TargetCut.Axial;
        [HideInInspector]
        public bool b_AllImagesMonochrome2_Test;
        public int m_IncludeDicomColsAndRows_Sagittal;
        public int m_IncludeDicomColsAndRows_Coronal;
        public int m_IncludeDicomColsAndRows_Axial;

        public enum AnalysisType { Grayscale, Window_Level}
        [Header("Window Settings:")]
        public AnalysisType m_AnalysisType = AnalysisType.Window_Level;
        public enum Vectorization { CPU, Hybrid_GPU}
        public Vectorization m_Vectorization = Vectorization.Hybrid_GPU;
        public enum WindowWidth_Level { Manufacturer, User, Abdominal_Soft_Tissue, Soft_Tissue,Abdominal_Liver, Bone, Brain, Lung, Mediastinum, Blood_clot, Acute_Stroke }
        private Vector2 currentWindowWidth_LevelValues = new Vector2(0,0);
        public WindowWidth_Level WindowWidth_LevelSetting = WindowWidth_Level.Manufacturer;
        private WindowWidth_Level _m_PriorWindowWidth_LevelSetting = WindowWidth_Level.Manufacturer;

        public delegate void OnWindowWidth_LevelChangeDelegate(string m_StudyListType,List<DicomParameters> m_StudyDicomParametersList, WindowWidth_Level newWindowWidth_Level, Vector2 currentWindowWidth_Level);
        public event OnWindowWidth_LevelChangeDelegate OnWindowWidth_LevelChange;

        [Range(0, 1800)]
        public float m_CustomWindowWidth;
        private float m_PriorCustomWindowWidth;
        [Range(0, 600)]
        public float m_CustomWindowCenter;
        private float m_PriorCustomWindowCenter;

        [Header("Volumetric Render:")]
        public bool b_VolumetricRender=true;
        public bool b_3D_RenderTexture = true;

        public enum SerializedTextureFormat { RGBAFloat, RGBA32 }
        [Header("Save Settings:")]
        [Tooltip("RGBAFloat slower at 32-bit. RGBA32 faster 8bit.")]
        public SerializedTextureFormat m_SerializedDicomTextureFormat = SerializedTextureFormat.RGBAFloat;

        //Performance Metrics
        [Header("Performance Metrics:")]
        public int TotalDicomImagesCreated;
        public int TotalOriginalImagesCompleted;
        public double m_TotalManangerSimulationSeconds;
        public int TotalDicomExcluded;

        [Tooltip("Extract New Dicom Data or used Saved Data:")]
        [Header("Data Selection:")]
        public bool b_UseDicomSubset;
        /*public*/ bool b_UseLocalData=false;//Disabled functionality not totally implemented

        [Header("Hounsfield Analysis:")]
        public bool b_UseHounsfield;
        public bool b_SafeChecks;

        enum PrefabType { Plane, Quad_Grayscale, Quad_Windowing, Cube, Quad_URP }
        PrefabType m_PrefabType;
        string PrefabAddress = null;

        [Tooltip("Root Parent of FrameOrientation")]
        [Header("FrameOrientation Parent:")]
        public GameObject Parent_Axial;
        public GameObject Parent_Coronal;
        public GameObject Parent_Sagittal;
        public GameObject Parent_VolRend;

        [Tooltip("Normal Map Settings:")]
        [Header("Normal Settings:")]
        public bool GenerateNormalMap = false;
        public int normalFilterSize;
        public int normalStrength;

        [HideInInspector]
        public List<DicomParameters> m_AxialStudyDicomParametersList = new List<DicomParameters>();
        [HideInInspector]
        public List<DicomParameters> m_CoronalStudyDicomParametersList = new List<DicomParameters>();
        [HideInInspector]
        public List<DicomParameters> m_SagittalStudyDicomParametersList = new List<DicomParameters>();

        bool b_DebugMssg;
        bool b_FirstMessageCompleted;

        public void SetWindowWidth_Level()
        {
            Vector2 currentWindowWidth_LevelValues = DicomAnalysis.WindowSetting(WindowWidth_LevelSetting, m_CustomWindowWidth, m_CustomWindowCenter);//return value is Vector2(windowWidth,windowCenter)
            m_CustomWindowWidth = currentWindowWidth_LevelValues.x;
            m_CustomWindowCenter = currentWindowWidth_LevelValues.y;

            if (m_AxialStudyDicomParametersList.Count > 0)
                    OnWindowWidth_LevelChange("Axial", m_AxialStudyDicomParametersList, WindowWidth_LevelSetting, currentWindowWidth_LevelValues);

            if (m_CoronalStudyDicomParametersList.Count > 0)
                OnWindowWidth_LevelChange("Coronal", m_CoronalStudyDicomParametersList, WindowWidth_LevelSetting, currentWindowWidth_LevelValues);

            if (m_SagittalStudyDicomParametersList.Count > 0)
                OnWindowWidth_LevelChange("Sagittal", m_SagittalStudyDicomParametersList, WindowWidth_LevelSetting, currentWindowWidth_LevelValues);
        }

        private void OnValidate()
        {
            if(m_AnalysisType==AnalysisType.Window_Level)
            {
                if(_m_PriorWindowWidth_LevelSetting!=WindowWidth_LevelSetting)
                {
                    _m_PriorWindowWidth_LevelSetting = WindowWidth_LevelSetting;
                    SetWindowWidth_Level();
                    return;
                }
                
                if ((m_PriorCustomWindowWidth != m_CustomWindowWidth) || (m_PriorCustomWindowCenter != m_CustomWindowCenter))
                {
                    m_PriorCustomWindowWidth = m_CustomWindowWidth;
                    m_PriorCustomWindowCenter = m_CustomWindowCenter;
                    WindowWidth_LevelSetting = WindowWidth_LevelSetting = WindowWidth_Level.User;
                    SetWindowWidth_Level();
                    return;
                }
            }
        }

        private void Start()
        {
            b_DebugMssg = false;

            var settings = new JsonSerializerSettings
            {
                Converters = new[] {
                    new Vector4Converter()
                },
                ContractResolver = new UnityTypeContractResolver(),
            };
            
            InitialAssignmentOfObjects();

            StartCoroutine(CreateOriginalOrStoredImageProcess());
        }

        void SaveDicomImageFunction(bool overwrite, List<DicomParameters> m_StudyDicomParametersList, int i)
        {
            DicomStorageMethods.SaveImageLocally(m_StudyDicomParametersList[i], 
                                                            true, 
                                                            m_StudyDicomParametersList[i].FrameOrientation.ToString(),
                                                            m_SerializedDicomTextureFormat);
        }

        void SaveDicomImageFunction(bool overwrite, DicomParameters thisDicomImageParams)
        {
            DicomStorageMethods.SaveImageLocally(thisDicomImageParams,
                                                            true,
                                                            thisDicomImageParams.FrameOrientation.ToString(),
                                                            m_SerializedDicomTextureFormat);
        }

        private void InitialAssignmentOfObjects()
        {
            m_IncludeDicomColsAndRows_Sagittal = 656;
            m_IncludeDicomColsAndRows_Coronal = 656;
            m_IncludeDicomColsAndRows_Axial = 512;

            m_CustomWindowWidth = 400;
            m_PriorCustomWindowWidth = 400;
            m_CustomWindowCenter = 40;
            m_PriorCustomWindowCenter = 40;

            m_TotalManangerSimulationSeconds = 0;

            UIMethods = GameObject.FindGameObjectWithTag("UI_Methods").GetComponent<UI_Methods>();
            m_ExtractDicomDataMethods = new ExtractDicomDataMethods();
            m_FreeFlyCamera = GameObject.FindGameObjectWithTag("Player").GetComponent<FreeFlyCamera>();
            m_CreateImageMethods_CPU = GameObject.FindGameObjectWithTag("Image_Methods").GetComponent<CreateImageMethods_CPU>();
            m_CreateImageMethods_GPU = GameObject.FindGameObjectWithTag("Image_Methods").GetComponent<CreateImageMethods_GPU>();
            m_CreateVolumetricRender_GPU = GameObject.FindGameObjectWithTag("Parent_VolRend").GetComponent<CreateVolumetricRender_GPU>();

            if (!b_UseHounsfield)
                b_SafeChecks = false;

            if(b_VolumetricRender)
            {
                if(!b_3D_RenderTexture && !b_FirstMessageCompleted)
                {
                    Debug.LogWarning("Volumetric Render using 3D Texture.");
                    Debug.LogWarning("Hounsfield windowding not built for volumetric render with 3D Textures (only for 3D Render Textures).");
                }
                else if (!b_FirstMessageCompleted)
                    Debug.LogWarning("Volumetric Render using 3D Render Textures.");

                b_FirstMessageCompleted = true;

                if (SimulationOrientation== ExtractDicomDataMethods.TargetCut.All)
                {
                    Debug.LogWarning("Volumetric render built for a single study, please re-run and choose Axial, Coronal or Sagittal. Defaulted to Axial.");
                    SimulationOrientation = ExtractDicomDataMethods.TargetCut.Axial;
                }
            }

            if (m_AnalysisType == AnalysisType.Window_Level)
            {
                if (!b_VolumetricRender)
                    Debug.LogWarning("Grayscale Window Slider Analysis run on GPU.");
                
                b_UseHounsfield = false;
                b_SafeChecks = false;
            }
            else//Grayscale
            {
                if (b_VolumetricRender)
                {
                    b_VolumetricRender = false;
                    Debug.Log("Grayscale Range Analysis not available for Volumetric Render (either 3D Textures or 3D Render Textures).");
                }

                if (b_3D_RenderTexture)
                {
                    b_3D_RenderTexture= false;
                    Debug.Log("Grayscale Range Analysis not available for Volumetric Render using 3D Render Textures.");
                }

                if (m_Vectorization == Vectorization.Hybrid_GPU)
                {
                    m_Vectorization = Vectorization.CPU;
                    Debug.LogWarning("Grayscale Range Analysis currently only run on Multi-thread CPU. Defaulted to Vectorization CPU. Running Hounsfield Grayscale Picker.");
                 
                    if(!b_UseHounsfield)
                    {
                        Debug.LogWarning("We've activated Hounsfield Analysis, required for this Scenario.");
                        b_UseHounsfield = true;
                    }
                }

                //enable Grayscale UI
                UIMethods.Init(true);
            }

            if (b_UseLocalData)
            {
                m_CreateImageMethods_CPU.m_SceneDataOrigin = CreateImageMethods_CPU.DataOrigin.Stored;

                if (b_UseDicomSubset)
                {
                    b_UseDicomSubset = false;
                    Debug.LogWarning("b_UseDicomSubset skipped, existing stored local data will be used instead.");
                }
            }
            else
                m_CreateImageMethods_CPU.m_SceneDataOrigin = CreateImageMethods_CPU.DataOrigin.Original;

            Parent_Axial.name = "Parent_Axial";
            Parent_Sagittal.name = "Parent_Sagittal";
            Parent_Coronal.name = "Parent_Coronal";

            if (m_AnalysisType == AnalysisType.Window_Level)
                m_PrefabType = PrefabType.Quad_Windowing;
            else if (m_AnalysisType == AnalysisType.Grayscale)
                m_PrefabType = PrefabType.Quad_Grayscale;

            switch (m_PrefabType)
            {
                case PrefabType.Plane:
                    PrefabAddress = "Prefab/Plane";
                    break;
                case PrefabType.Quad_Grayscale:
                    PrefabAddress = "Prefab/QuadGrayscale";
                    break;
                case PrefabType.Quad_Windowing:
                    PrefabAddress = "Prefab/QuadWindowing";
                    break;
                case PrefabType.Cube:
                    PrefabAddress = "Prefab/Cube";
                    break;
                case PrefabType.Quad_URP:
                    PrefabAddress = "Prefab/Quad_URP";
                    break;
                default:
                    break;
            }
        }

        bool CleanUpData(DicomParameters thisDicomParams)
        {
            switch (thisDicomParams.FrameOrientation)
            {
                case ExtractDicomDataMethods.FrameOrientation.Axial:
                    if (thisDicomParams.ImageNumberRow == m_IncludeDicomColsAndRows_Axial && 
                        thisDicomParams.ImageNumberCol == m_IncludeDicomColsAndRows_Axial && 
                        !string.IsNullOrEmpty(thisDicomParams.ImageName))
                        return true;
                    break;

                case ExtractDicomDataMethods.FrameOrientation.Sagittal:
                    if (thisDicomParams.ImageNumberRow == m_IncludeDicomColsAndRows_Sagittal && 
                        thisDicomParams.ImageNumberCol == m_IncludeDicomColsAndRows_Sagittal && 
                        !string.IsNullOrEmpty(thisDicomParams.ImageName))
                        return true;
                    break;

                case ExtractDicomDataMethods.FrameOrientation.Coronal:
                    if (thisDicomParams.ImageNumberRow == m_IncludeDicomColsAndRows_Coronal && 
                        thisDicomParams.ImageNumberCol == m_IncludeDicomColsAndRows_Coronal && 
                        !string.IsNullOrEmpty(thisDicomParams.ImageName))
                        return true;
                    break;

                default:
                    TotalDicomExcluded++;
                    return false;
            }

            TotalDicomExcluded++;
            return false;
        }
        
        IEnumerator ExtractDicomData()
        {
            if(b_DebugMssg) Debug.Log("Creating Dicom directory...");

            string path = null;
            if (!b_UseDicomSubset)
                path = @"Assets/DicomImgs";
            else
                path = @"Assets/DicomImgsSubset";

            var directoryInfoForDicomImagesFolder = new DirectoryInfo(path);
            var dicomCount = (from file in Directory.EnumerateFiles(path, "*.DCM", SearchOption.AllDirectories)
                              select file).Count();
            if (b_DebugMssg) Debug.Log("Processing " + dicomCount + " Dicom Images");

            //Variables to Calculate the Center by Group Orientation of Images
            Vector3 axialGroupVectors = new Vector3(0, 0, 0);
            Vector3 axialGroupCenter = new Vector3(0, 0, 0);
            Vector3 coronalGroupVectors = new Vector3(0, 0, 0);
            Vector3 coronalGroupCenter = new Vector3(0, 0, 0);
            Vector3 sagittalGroupVectors = new Vector3(0, 0, 0);
            Vector3 sagittalGroupCenter = new Vector3(0, 0, 0);

            foreach (var file in directoryInfoForDicomImagesFolder.GetFiles("*.*", SearchOption.AllDirectories))
            {
                if (!file.Name.Contains("SC") && !file.Name.Contains("desktop") && !file.Name.Contains("meta"))
                {
                    DicomParameters thisDicomParameters = new DicomParameters();

                    IEnumerator ExtractDicomData = m_ExtractDicomDataMethods.GenerateDicomParameterData<bool>(result => thisDicomParameters = result, file, path, SimulationOrientation);
                    yield return (ExtractDicomData);

                    if (CleanUpData(thisDicomParameters))
                    {
                        if (b_DebugMssg) Debug.Log("ThisDicomParamsName " + thisDicomParameters.ImageName);

                        //Init Groups of Colors for this Image
                        //InitializeDicomImageGroupColors(ref thisDicomParameters);

                        var dicomFile = DicomFile.Open(string.Concat(path, "/", file.Name));
                        //dicomFile.Dataset.AddOrUpdate(DicomTag.WindowCenter, "5.00");

                        DicomPixelData pixelData = DicomPixelData.Create(dicomFile.Dataset, false);
                        var pixelDataRender = PixelDataFactory.Create(pixelData, 0);//16 Bit pixel data for color

                        thisDicomParameters.m_Raw16bitDataArray = new int[thisDicomParameters.m_DicomTextureWidth * thisDicomParameters.m_DicomTextureHeight];
                        for (int r = 0; r < (pixelDataRender.Height); r++)
                            for (int c = 0; c < (pixelDataRender.Width); c++)
                                thisDicomParameters.m_Raw16bitDataArray[pixelDataRender.Height * r + c] = (int)pixelDataRender.GetPixel(c, r);

                        var sourcegeometry = new FrameGeometry(dicomFile.Dataset);

                        //Generate Image
                        if(m_Vectorization==Vectorization.CPU)
                        {
                            if (m_AnalysisType == AnalysisType.Window_Level)
                            {
                                IEnumerator GetPixelLocatAndColorCoroutine = m_CreateImageMethods_CPU.GetOriginal_WindowedPixelLocationAndColor<bool>(result => thisDicomParameters = result, thisDicomParameters, dicomFile.Dataset, pixelDataRender, sourcegeometry, m_SerializedDicomTextureFormat, UIMethods.GrayscaleRangeDict, b_SafeChecks, b_UseHounsfield);
                                yield return (GetPixelLocatAndColorCoroutine);
                            }
                            else
                            {
                                IEnumerator GetPixelLocatAndColorCoroutine = m_CreateImageMethods_CPU.GetOriginal_DicomImagePixelLocationAndColor<bool>(result => thisDicomParameters = result, thisDicomParameters, dicomFile.Dataset, pixelDataRender, sourcegeometry, m_SerializedDicomTextureFormat, UIMethods.GrayscaleRangeDict, b_SafeChecks, b_UseHounsfield);
                                yield return (GetPixelLocatAndColorCoroutine);
                            }
                        }
                        else
                        {
                            IEnumerator GetPixelLocatAndColorCoroutine = m_CreateImageMethods_CPU.GetOriginal_WindowedPixelLocation<bool>(result => thisDicomParameters = result, thisDicomParameters, sourcegeometry);
                            yield return (GetPixelLocatAndColorCoroutine);
                        }

                        Vector3 imageNormal = new Vector3((float)thisDicomParameters.DirectionNormal[0], (float)thisDicomParameters.DirectionNormal[1], (float)thisDicomParameters.DirectionNormal[2]);
                        Quaternion imgRotation = Quaternion.FromToRotation(-Vector3.forward, imageNormal);

                        int thisImageCenterLocation = thisDicomParameters.ImageNumberCol / 2 + (thisDicomParameters.ImageNumberRow / 2 * thisDicomParameters.m_DicomTextureWidth);
                        thisDicomParameters.ImagePlanePosition = thisDicomParameters.PixelLocationArray[thisImageCenterLocation];
                        
                        //RotatePoints(ref thisDicomParameters);

                        if (!b_VolumetricRender)
                        {
                            thisDicomParameters.ImagePlaneGO = Instantiate(
                                        Resources.Load<GameObject>(PrefabAddress),
                                        thisDicomParameters.ImagePlanePosition,
                                        imgRotation);
                        }

                        //If GPU we don't create texture yet
                        if(m_Vectorization == Vectorization.CPU)
                        {
                            var rend = thisDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
                            rend.material.SetColor("_BaseColor", Color.white);
                            rend.material.SetTexture("_BaseMap", thisDicomParameters.m_DicomTexture);
                            rend.material.mainTexture = thisDicomParameters.m_DicomTexture;

                            //DicomStorageMethods.SaveToJsonDicomParametersClass(thisDicomParameters);
                            //SaveDicomImageFunction(true, thisDicomParameters);
                        }
                        
                        //thisDicomParameters.PixelLocationArray = null;//release locationArray, no longer needed

                        Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("AddImageToStudyList");

                        AddImageToStudyList(ref thisDicomParameters);
                        if (!b_VolumetricRender)
                            CalculateCenterForEachOrientationGroup(ref thisDicomParameters, ref axialGroupVectors, ref coronalGroupVectors, ref sagittalGroupVectors);

                        m_TotalManangerSimulationSeconds += thisWatch.StopWatch();

                        //Set black pixels transparent from the start
                        if(b_UseHounsfield && m_AnalysisType==AnalysisType.Grayscale)//if not running Hounsfield we set black pixel to zero when creating the image
                        {
                            if (GameObject.FindGameObjectWithTag("Hounsfield_Toggle_0") != null)
                            {
                                GameObject.FindGameObjectWithTag("Hounsfield_Toggle_0").GetComponent<UI_HounsfieldController>().m_UI_Hounsfield.UpdateColorRangeTransparencyButtonPressedBurst(false, UI_Methods.GrayscaleRange.R_000, UIMethods.GrayscaleRangeDict, thisDicomParameters);
                                GameObject.FindGameObjectWithTag("Hounsfield_Toggle_0").GetComponent<Toggle>().isOn = false;
                            }
                        }

                        TotalDicomImagesCreated++;
                        //Debug.Log("Total Original Images:" + TotalDicomImagesCreated);

                        if (!thisDicomParameters.PhotometricInterpretation.Equals("MONOCHROME2"))
                            Debug.LogWarning("Image " + thisDicomParameters.ImageName + " not MONOCHROME2");

                        if(b_DebugMssg) Debug.Log("End of image " + thisDicomParameters.ImageName);
                    }
                }
            }

            //Create3DVolume();

            if (!b_VolumetricRender)
            {
                if (m_AxialStudyDicomParametersList.Count > 0)
                {
                    if (m_Vectorization == Vectorization.Hybrid_GPU)
                    {
                        if (m_AxialStudyDicomParametersList.Count > 0)
                        {
                            m_CreateImageMethods_GPU.ComputeBufferInitManager("Axial", m_AxialStudyDicomParametersList, m_SerializedDicomTextureFormat);
                        }
                    }

                    axialGroupCenter = axialGroupVectors / m_AxialStudyDicomParametersList.Count;
                    m_AxialStudyDicomParametersList[0].ParentCenterPosition = Parent_Axial.transform.position = axialGroupCenter;
                    Parent_Axial.name += "" + m_AxialStudyDicomParametersList.Count + ")";

                    Parent_VolRend.transform.position = m_AxialStudyDicomParametersList[0].ParentCenterPosition;
                }

                if (m_CoronalStudyDicomParametersList.Count > 0)
                {
                    if (m_Vectorization == Vectorization.Hybrid_GPU)
                    {
                        if (m_CoronalStudyDicomParametersList.Count > 0)
                        {
                            m_CreateImageMethods_GPU.ComputeBufferInitManager("Coronal", m_CoronalStudyDicomParametersList, m_SerializedDicomTextureFormat);
                        }
                    }

                    coronalGroupCenter = coronalGroupVectors / m_CoronalStudyDicomParametersList.Count;
                    m_CoronalStudyDicomParametersList[0].ParentCenterPosition = Parent_Coronal.transform.position = coronalGroupCenter;
                    Parent_Coronal.name += "(" + m_CoronalStudyDicomParametersList.Count + ")";

                    Parent_VolRend.transform.position = m_CoronalStudyDicomParametersList[0].ParentCenterPosition;
                }

                if (m_SagittalStudyDicomParametersList.Count > 0)
                {
                    if (m_Vectorization == Vectorization.Hybrid_GPU)
                    {
                        if (m_SagittalStudyDicomParametersList.Count > 0)
                        {
                            m_CreateImageMethods_GPU.ComputeBufferInitManager("Sagittal", m_SagittalStudyDicomParametersList, m_SerializedDicomTextureFormat);
                        }
                    }

                    sagittalGroupCenter = sagittalGroupVectors / m_SagittalStudyDicomParametersList.Count;
                    m_SagittalStudyDicomParametersList[0].ParentCenterPosition = Parent_Sagittal.transform.position = sagittalGroupCenter;
                    Parent_Sagittal.name += "(" + m_SagittalStudyDicomParametersList.Count + ")";

                    Parent_VolRend.transform.position = m_SagittalStudyDicomParametersList[0].ParentCenterPosition;
                }
            }
        }

        #region Merge_Studies_Into_3DVolume
        struct VolumeData
        {
            public int slice;
            public float3 m_Axial_Location;
            public float3 m_Coronal_Location;
            public float3 m_Sagittal_Location;
        }

        void Create3DVolume()
        {
            float minZ = 0;
            float maxZ = 0;

            List<VolumeData> myVolume = new List<VolumeData>();

            for(int i = 0; i < m_AxialStudyDicomParametersList.Count; i++)
            {
                for(int y=0;y<m_AxialStudyDicomParametersList.Count;y++)
                {
                    minZ = m_AxialStudyDicomParametersList[i].PixelLocationArray.Min(v => v.z);
                    maxZ = m_AxialStudyDicomParametersList[i].PixelLocationArray.Max(v => v.z);
                }
            }

            for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
            {
                for (int y = 0; y < m_CoronalStudyDicomParametersList.Count; y++)
                {
                    if (m_CoronalStudyDicomParametersList[i].PixelLocationArray[y].z < minZ)
                        minZ = m_CoronalStudyDicomParametersList[i].PixelLocationArray[y].z;

                    if (m_CoronalStudyDicomParametersList[i].PixelLocationArray[y].z > maxZ)
                        maxZ = m_CoronalStudyDicomParametersList[i].PixelLocationArray[y].z;
                }
            }

            for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
            {
                for (int y = 0; y < m_SagittalStudyDicomParametersList.Count; y++)
                {
                    if (m_SagittalStudyDicomParametersList[i].PixelLocationArray[y].z < minZ)
                        minZ = m_SagittalStudyDicomParametersList[i].PixelLocationArray[y].z;

                    if (m_SagittalStudyDicomParametersList[i].PixelLocationArray[y].z > maxZ)
                        maxZ = m_SagittalStudyDicomParametersList[i].PixelLocationArray[y].z;
                }
            }

            float currZ = minZ;
            float epsilon = 0.1f;
            int currentSlice = 0;
            bool b_AddPixel = false;

            Debug.Log("MaxZ is " + maxZ + " currZ:" + currZ);
            do
            {
                //thisPixel.slice = currentSlice;
                b_AddPixel = false;

                for (int i = 0; i < m_AxialStudyDicomParametersList.Count; i++)
                {
                    for(int y=0;y<m_AxialStudyDicomParametersList[i].PixelLocationArray.Length;y++)
                    {
                        if ((m_AxialStudyDicomParametersList[i].PixelLocationArray[y].z <= (currZ + epsilon)) && (m_AxialStudyDicomParametersList[i].PixelLocationArray[y].z >= (currZ - epsilon)))
                        {
                  //          thisPixel.m_Axial_Location = m_AxialStudyDicomParametersList[i].PixelLocationArray[y];
                            b_AddPixel = true;
                        }
                    }
                }

                for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
                {
                    for (int y = 0; y < m_CoronalStudyDicomParametersList[i].PixelLocationArray.Length; y++)
                    {
                        if ((m_CoronalStudyDicomParametersList[i].PixelLocationArray[y].z <= (currZ + epsilon)) && (m_CoronalStudyDicomParametersList[i].PixelLocationArray[y].z >= (currZ - epsilon)))
                        {
                    //        thisPixel.m_Coronal_Location = m_CoronalStudyDicomParametersList[i].PixelLocationArray[y];
                            b_AddPixel = true;
                        }
                    }
                }

                for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
                {
                    for (int y = 0; y < m_SagittalStudyDicomParametersList[i].PixelLocationArray.Length; y++)
                    {
                        if ((m_SagittalStudyDicomParametersList[i].PixelLocationArray[y].z <= (currZ + epsilon)) && (m_SagittalStudyDicomParametersList[i].PixelLocationArray[y].z >= (currZ - epsilon)))
                        {
                      //      thisPixel.m_Sagittal_Location = m_SagittalStudyDicomParametersList[i].PixelLocationArray[y];
                            b_AddPixel = true;
                        }
                    }
                }

                if(b_AddPixel)
                {
                    //myVolume.Add(thisPixel);
                    currentSlice++;
                }

                currZ += epsilon;

            }while (currZ <= maxZ);
            Debug.Log("Ended");
        }
        #endregion

        void CalculateCenterForEachOrientationGroup(ref DicomParameters thisDicomParameters,ref Vector3 axialGroupVectors, ref Vector3 coronalGroupVectors, ref Vector3 sagittalGroupVectors)
        {
            switch (thisDicomParameters.FrameOrientation)
            {
                case ExtractDicomDataMethods.FrameOrientation.Axial:
                    axialGroupVectors += thisDicomParameters.ImagePlaneGO.transform.position;

                    break;
                case ExtractDicomDataMethods.FrameOrientation.Sagittal:
                    sagittalGroupVectors += thisDicomParameters.ImagePlaneGO.transform.position;

                    break;
                case ExtractDicomDataMethods.FrameOrientation.Coronal:
                    coronalGroupVectors += thisDicomParameters.ImagePlaneGO.transform.position;

                    break;
                default:
                    break;
            }
        }

        void RotatePoints(ref DicomParameters thisImgDicomParams)
        {
            Vector3 dir = new Vector3(0, 0, 0);

            if(thisImgDicomParams.FrameOrientation == ExtractDicomDataMethods.FrameOrientation.Coronal)
            {
                Vector3 angles = new Vector3(90, 0, 180);

                for (int y = 0; y < thisImgDicomParams.PixelLocationArray.Length; y++)
                {
                    dir = thisImgDicomParams.PixelLocationArray[y] - thisImgDicomParams.ImagePlanePosition;
                    dir = Quaternion.Euler(angles) * dir; // rotate it
                    thisImgDicomParams.PixelLocationArray[y] = dir + thisImgDicomParams.ImagePlanePosition;
                }
            }

            if (thisImgDicomParams.FrameOrientation == ExtractDicomDataMethods.FrameOrientation.Sagittal)
            {
                Vector3 angles = new Vector3(-90, -90, 0);

                for (int y = 0; y < thisImgDicomParams.PixelLocationArray.Length; y++)
                {
                    dir = thisImgDicomParams.PixelLocationArray[y] - thisImgDicomParams.ImagePlanePosition;
                    dir = Quaternion.Euler(angles) * dir; // rotate it
                    thisImgDicomParams.PixelLocationArray[y] = dir + thisImgDicomParams.ImagePlanePosition;
                }
            }
        }

        IEnumerator CreateOriginalOrStoredImageProcess()
        {
            if (!b_UseLocalData)
            {
                IEnumerator generateData = ExtractDicomData();
                yield return (generateData);

                GC.Collect();

                if (b_VolumetricRender)
                    CreateVolumetricRender();
                else
                    ProcessOriginalImages();
            }
            else
            {
                var startTime = Time.deltaTime;

                //Load json Data and jpg
                //IEnumerator coroutineLoadImgToTexture = null;
                IEnumerator coroutineInstantiateImageGOForLists = null;

                switch (SimulationOrientation)
                {
                    case ExtractDicomDataMethods.TargetCut.All:
                        DicomStorageMethods.DeserializeStoredDicomParameter(ref m_CoronalStudyDicomParametersList, ExtractDicomDataMethods.TargetCut.Coronal);
                        DicomStorageMethods.DeserializeStoredDicomParameter(ref m_AxialStudyDicomParametersList, ExtractDicomDataMethods.TargetCut.Axial);
                        DicomStorageMethods.DeserializeStoredDicomParameter(ref m_SagittalStudyDicomParametersList, ExtractDicomDataMethods.TargetCut.Sagittal);

                        if(m_CoronalStudyDicomParametersList.Count>0)
                            Parent_Coronal.transform.position = m_CoronalStudyDicomParametersList[0].ParentCenterPosition;
                        if (m_AxialStudyDicomParametersList.Count > 0)
                            Parent_Axial.transform.position = m_AxialStudyDicomParametersList[0].ParentCenterPosition;
                        if (m_SagittalStudyDicomParametersList.Count > 0)
                            Parent_Sagittal.transform.position = m_SagittalStudyDicomParametersList[0].ParentCenterPosition;

                        Parent_VolRend.transform.position = Parent_Coronal.transform.position;

                        coroutineInstantiateImageGOForLists = InstantiateStoredDicomImageReadStoredImageAttachToTextureAndNormalManager(SimulationOrientation);
                        yield return (coroutineInstantiateImageGOForLists);

                        break;
                    case ExtractDicomDataMethods.TargetCut.Axial:
                        DicomStorageMethods.DeserializeStoredDicomParameter(ref m_AxialStudyDicomParametersList, ExtractDicomDataMethods.TargetCut.Axial);

                        if (m_AxialStudyDicomParametersList.Count > 0)
                        {
                            Parent_Axial.transform.position = m_AxialStudyDicomParametersList[0].ParentCenterPosition;

                            Parent_VolRend.transform.position = Parent_Coronal.transform.position;

                            coroutineInstantiateImageGOForLists = InstantiateStoredDicomImageReadStoredImageAttachToTextureAndNormalManager(SimulationOrientation);
                            yield return (coroutineInstantiateImageGOForLists);
                        }

                        break;
                    case ExtractDicomDataMethods.TargetCut.Sagittal:
                        DicomStorageMethods.DeserializeStoredDicomParameter(ref m_SagittalStudyDicomParametersList, ExtractDicomDataMethods.TargetCut.Sagittal);

                        if (m_SagittalStudyDicomParametersList.Count > 0)
                        {
                            Parent_Sagittal.transform.position = m_SagittalStudyDicomParametersList[0].ParentCenterPosition;

                            Parent_VolRend.transform.position = Parent_Sagittal.transform.position;

                            coroutineInstantiateImageGOForLists = InstantiateStoredDicomImageReadStoredImageAttachToTextureAndNormalManager(SimulationOrientation);
                            yield return (coroutineInstantiateImageGOForLists);
                        }

                        break;
                    case ExtractDicomDataMethods.TargetCut.Coronal:
                        DicomStorageMethods.DeserializeStoredDicomParameter(ref m_CoronalStudyDicomParametersList, ExtractDicomDataMethods.TargetCut.Coronal);

                        if (m_CoronalStudyDicomParametersList.Count > 0)
                        {
                            Parent_Coronal.transform.position = m_CoronalStudyDicomParametersList[0].ParentCenterPosition;

                            Parent_VolRend.transform.position = Parent_Coronal.transform.position;

                            coroutineInstantiateImageGOForLists = InstantiateStoredDicomImageReadStoredImageAttachToTextureAndNormalManager(SimulationOrientation);
                            yield return (coroutineInstantiateImageGOForLists);
                        }

                        break;
                    default:
                        break;
                }

                if (m_SagittalStudyDicomParametersList.Count == 0 && m_CoronalStudyDicomParametersList.Count == 0 && m_AxialStudyDicomParametersList.Count == 0)
                    Debug.LogWarning("No Stored Images found.");

                if (b_DebugMssg) Debug.Log("Completed Creating Stored Images:" + " Axial Images:" + m_AxialStudyDicomParametersList.Count + " Coronal:" + m_CoronalStudyDicomParametersList.Count + " Sagittal:" + m_SagittalStudyDicomParametersList.Count);
            }
        }
        
        IEnumerator InstantiateStoredDicomImageReadStoredImageAttachToTextureAndNormalManager(ExtractDicomDataMethods.TargetCut SimulationOrientation)
        {
            IEnumerator coroutineInstantiateImage = null;

            switch (SimulationOrientation)
            {
                case ExtractDicomDataMethods.TargetCut.All:
                    coroutineInstantiateImage=InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(m_CoronalStudyDicomParametersList);
                    yield return (coroutineInstantiateImage);
                    coroutineInstantiateImage = InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(m_AxialStudyDicomParametersList);
                    yield return (coroutineInstantiateImage);
                    coroutineInstantiateImage = InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(m_SagittalStudyDicomParametersList);
                    yield return (coroutineInstantiateImage);

                    break;

                case ExtractDicomDataMethods.TargetCut.Axial:
                    coroutineInstantiateImage = InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(m_AxialStudyDicomParametersList);
                    yield return (coroutineInstantiateImage);

                    break;
                case ExtractDicomDataMethods.TargetCut.Sagittal:
                    coroutineInstantiateImage = InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(m_SagittalStudyDicomParametersList);
                    yield return (coroutineInstantiateImage);

                    break;

                case ExtractDicomDataMethods.TargetCut.Coronal:
                    coroutineInstantiateImage = InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(m_CoronalStudyDicomParametersList);
                    yield return (coroutineInstantiateImage);

                    break;

                default:
                    break;
            }

            RotateParentGOAndPositionPlayer();

            yield return null;
        }

        void ProcessOriginalImageDictionary(int ImgPosInList ,ExtractDicomDataMethods.TargetCut SimulationOrientation, ref List<DicomParameters> thisStudyDicomParametersList, string PrefabAddress, bool GenerateNormalMap, int normalFilterSize, int normalStrength, bool b_UseLocalData)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("ProcessOriginalImageDictionary");

            DicomParameters tempDicomParams = thisStudyDicomParametersList[ImgPosInList];

            //Scale Image
            m_CreateImageMethods_CPU.ScaleOriginalImageGeometry(ref tempDicomParams, PrefabAddress);

            //Convert Normals
            if (GenerateNormalMap)
                tempDicomParams.ImagePlaneGO.GetComponent<CreateNormals.NormalMapMakerRuntime>().ConvertToNormalMap(normalFilterSize, normalStrength);

            thisStudyDicomParametersList[ImgPosInList] = tempDicomParams;

            //Parent Image
            switch (thisStudyDicomParametersList[ImgPosInList].FrameOrientation)
            {
                case ExtractDicomDataMethods.FrameOrientation.Axial:
                    thisStudyDicomParametersList[ImgPosInList].ImagePlaneGO.transform.SetParent(Parent_Axial.transform);

                    break;
                case ExtractDicomDataMethods.FrameOrientation.Sagittal:
                    thisStudyDicomParametersList[ImgPosInList].ImagePlaneGO.transform.SetParent(Parent_Sagittal.transform);

                    break;
                case ExtractDicomDataMethods.FrameOrientation.Coronal:
                    thisStudyDicomParametersList[ImgPosInList].ImagePlaneGO.transform.SetParent(Parent_Coronal.transform);
                    
                    break;
                default:
                    break;
            }

            //Test to Save Original Dicom
            TotalOriginalImagesCompleted++;

            m_TotalManangerSimulationSeconds += thisWatch.StopWatch();
        }

        void ProcessOriginalImages()
        {
            UIMethods.showFPS = true;

            switch (SimulationOrientation)
            {
                case ExtractDicomDataMethods.TargetCut.All:
                    
                    for (int i = 0; i < m_AxialStudyDicomParametersList.Count; i++)
                        ProcessOriginalImageDictionary(i, SimulationOrientation, ref m_AxialStudyDicomParametersList, PrefabAddress, GenerateNormalMap, normalFilterSize, normalStrength, b_UseLocalData);

                    for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
                        ProcessOriginalImageDictionary(i, SimulationOrientation, ref m_CoronalStudyDicomParametersList, PrefabAddress, GenerateNormalMap, normalFilterSize, normalStrength, b_UseLocalData);

                    for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
                        ProcessOriginalImageDictionary(i, SimulationOrientation, ref m_SagittalStudyDicomParametersList, PrefabAddress, GenerateNormalMap, normalFilterSize, normalStrength, b_UseLocalData);

                    break;

                case ExtractDicomDataMethods.TargetCut.Axial:
                    for (int i = 0; i < m_AxialStudyDicomParametersList.Count; i++)
                        ProcessOriginalImageDictionary(i, SimulationOrientation, ref m_AxialStudyDicomParametersList, PrefabAddress, GenerateNormalMap, normalFilterSize, normalStrength, b_UseLocalData);

                    break;
                case ExtractDicomDataMethods.TargetCut.Sagittal:
                    for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
                        ProcessOriginalImageDictionary(i, SimulationOrientation, ref m_SagittalStudyDicomParametersList, PrefabAddress, GenerateNormalMap, normalFilterSize, normalStrength, b_UseLocalData);

                    break;

                case ExtractDicomDataMethods.TargetCut.Coronal:
                    for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
                        ProcessOriginalImageDictionary(i, SimulationOrientation, ref m_CoronalStudyDicomParametersList, PrefabAddress, GenerateNormalMap, normalFilterSize, normalStrength, b_UseLocalData);
                    
                    break;

                default:
                    break;
            }

            RotateParentGOAndPositionPlayer();
            /*
            for (int i = 0; i < m_AxialStudyDicomParametersList.Count; i++)
            {
                var rot = Quaternion.AngleAxis(-180, Vector3.right) *
                        Quaternion.AngleAxis(-180, Vector3.up) *
                        Quaternion.AngleAxis(-180, Vector3.forward);
                m_AxialStudyDicomParametersList[i].ImagePlaneGO.transform.rotation = rot;
            }
            for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
            {
                var rot = Quaternion.AngleAxis(90, Vector3.right) *
                        Quaternion.AngleAxis(0, Vector3.up) *
                        Quaternion.AngleAxis(180, Vector3.forward);
                m_CoronalStudyDicomParametersList[i].ImagePlaneGO.transform.rotation = rot;

            }
            for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
            {
                var rot = Quaternion.AngleAxis(-90, Vector3.right) *
                        Quaternion.AngleAxis(0, Vector3.up) *
                        Quaternion.AngleAxis(0, Vector3.forward);
                m_SagittalStudyDicomParametersList[i].ImagePlaneGO.transform.rotation = rot;
            }


            for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
            {
                var rot = Quaternion.AngleAxis(-90, Vector3.right) *
                        Quaternion.AngleAxis(-90, Vector3.up) *
                        Quaternion.AngleAxis(0, Vector3.forward);

                m_SagittalStudyDicomParametersList[i].ImagePlaneGO.transform.rotation = rot;
            }*/

        }

        void CreateVolumetricRender()
        {
            Debug.Log("Now we start VolumetricRender");
            //calculate cube sizes
            int minSize = 512;
            Vector3 m_AxialCubeSize = new Vector3(Mathf.Min(minSize, 0), Mathf.Min(minSize, 0), Mathf.Min(minSize, 0));
            Vector3 m_CoronalCubeSize = new Vector3(Mathf.Min(minSize, 0), Mathf.Min(minSize, 0), Mathf.Min(minSize, 0));
            Vector3 m_SagittalCubeSize = new Vector3(Mathf.Min(minSize, 0), Mathf.Min(minSize, 0), Mathf.Min(minSize, 0));

            if (m_AxialStudyDicomParametersList.Count > 0)
                m_AxialCubeSize = new Vector3(m_AxialStudyDicomParametersList[0].m_DicomTextureWidth,
                                                    m_AxialStudyDicomParametersList[0].m_DicomTextureWidth,
                                                    //Mathf.Abs(m_AxialStudyDicomParametersList[m_AxialStudyDicomParametersList.Count-1].PixelLocationArray[0].z - m_AxialStudyDicomParametersList[0].PixelLocationArray[0].z));
                                                    m_AxialStudyDicomParametersList.Count);

            if (m_CoronalStudyDicomParametersList.Count > 0)
                m_CoronalCubeSize = new Vector3(m_CoronalStudyDicomParametersList[0].m_DicomTextureWidth,
                                                  Mathf.Abs(m_CoronalStudyDicomParametersList[m_CoronalStudyDicomParametersList.Count-1].PixelLocationArray[0].y - m_CoronalStudyDicomParametersList[0].PixelLocationArray[0].y), 
                                                m_CoronalStudyDicomParametersList.Count);

            if (m_SagittalStudyDicomParametersList.Count > 0)
                m_SagittalCubeSize = new Vector3(Mathf.Abs(m_SagittalStudyDicomParametersList[m_SagittalStudyDicomParametersList.Count-1].PixelLocationArray[0].x - m_SagittalStudyDicomParametersList[0].PixelLocationArray[0].x),
                                                    m_SagittalStudyDicomParametersList[0].m_DicomTextureWidth, 
                                                    m_SagittalStudyDicomParametersList.Count);

            Vector3 m_CubeDimensions = new Vector3(Mathf.Max(m_AxialCubeSize.x, m_CoronalCubeSize.x, m_SagittalCubeSize.x),
                                                    Mathf.Max(m_AxialCubeSize.y, m_CoronalCubeSize.y, m_SagittalCubeSize.y),
                                                    Mathf.Max(m_AxialCubeSize.z, m_CoronalCubeSize.z, m_SagittalCubeSize.z));

            //create mesh
            //if(b_3D_RenderTexture)
            //{
                //Parent_VolRend.GetComponent<MeshFilter>().mesh = new Mesh();
                //Parent_VolRend.GetComponent<MeshFilter>().sharedMesh.recalculateMeshByBounds(m_CubeDimensions);
            //}

            //Parent all orientations
            Parent_Axial.transform.SetParent(Parent_VolRend.transform);
            Parent_Coronal.transform.SetParent(Parent_VolRend.transform);
            Parent_Sagittal.transform.SetParent(Parent_VolRend.transform);

            //Material shaderMat = Parent_VolRend.GetComponent<Renderer>().material;

            m_CreateVolumetricRender_GPU.InitManager(m_SerializedDicomTextureFormat, m_CubeDimensions);
        }

        IEnumerator CreateSingleStoredDicomImage(Action<DicomParameters> thisDicomParametersResult, DicomParameters thisDicomParameters)
        {
            IEnumerator instantiateGO = m_CreateImageMethods_CPU.InstantiateStoredImageGeometry(thisDicomParameters, PrefabAddress, (result) => { thisDicomParameters = result;});
            yield return (instantiateGO);

            thisDicomParametersResult(thisDicomParameters);
        }

        IEnumerator InstantiateStoredDicomImage_ReadStoredImage_CreateHounsfieldRanges_AttachToTextureAndNormalForThisList(List<DicomParameters> thisDicomParamsList)
        {
            IEnumerator createDicomImageGOCoroutine = null;
            IEnumerator readAndAttachTextureCoroutine = null;

            for (int i = 0; i < thisDicomParamsList.Count; i++)
            {
                createDicomImageGOCoroutine = CreateSingleStoredDicomImage(result => thisDicomParamsList[i] = result, thisDicomParamsList[i]);
                yield return (createDicomImageGOCoroutine);

                if(m_SerializedDicomTextureFormat==SerializedTextureFormat.RGBAFloat)
                {
                    readAndAttachTextureCoroutine = DicomStorageMethods.ReadStoredImageCustomAlgorithm(thisDicomParamsList[i], m_SerializedDicomTextureFormat, (returnValue) =>
                    { thisDicomParamsList[i].m_DicomTexture = returnValue; });
                    yield return (readAndAttachTextureCoroutine);
                }
                else if (m_SerializedDicomTextureFormat == SerializedTextureFormat.RGBA32)
                {
                    readAndAttachTextureCoroutine = DicomStorageMethods.ReadStoredImageFast(thisDicomParamsList[i], m_SerializedDicomTextureFormat, (returnValue) =>
                    { thisDicomParamsList[i].m_DicomTexture = returnValue; });
                    yield return (readAndAttachTextureCoroutine);
                }

                //Create Pixel color Quadrants
                NativeArray<int> dummyNativeArray = new NativeArray<int>();
                DicomParameters thisDicomImage = thisDicomParamsList[i];
                IEnumerator GetPixelLocatAndColorCoroutine = m_CreateImageMethods_CPU.GetStored_DicomImagePixelLocationAndColor<bool>(result => thisDicomImage = result,
                thisDicomImage, UIMethods.GrayscaleRangeDict, b_SafeChecks, b_UseHounsfield, m_SerializedDicomTextureFormat);
                yield return (GetPixelLocatAndColorCoroutine);
                
                if (dummyNativeArray.IsCreated)
                dummyNativeArray.Dispose();

                if (b_UseHounsfield)
                {
                    //Set black pixels transparent from the start
                    if (GameObject.FindGameObjectWithTag("Hounsfield_Toggle_0") != null)
                    {
                        GameObject.FindGameObjectWithTag("Hounsfield_Toggle_0").GetComponent<UI_HounsfieldController>().m_UI_Hounsfield.UpdateColorRangeTransparencyButtonPressedBurst(false, UI_Methods.GrayscaleRange.R_000, UIMethods.GrayscaleRangeDict, thisDicomImage);
                        GameObject.FindGameObjectWithTag("Hounsfield_Toggle_0").GetComponent<Toggle>().SetIsOnWithoutNotify(false);
                    }
                }
                
                if (GenerateNormalMap)
                    thisDicomParamsList[i].ImagePlaneGO.GetComponent<CreateNormals.NormalMapMakerRuntime>().ConvertToNormalMap(normalFilterSize, normalStrength);

                TotalDicomImagesCreated++;
            }

            yield return null;
        }

        public void AddImageToStudyList(ref DicomParameters thisDicomParams)
        {
            switch (thisDicomParams.FrameOrientation)
            {
                case ExtractDicomDataMethods.FrameOrientation.Axial:
                    if (thisDicomParams != null) m_AxialStudyDicomParametersList.Add(thisDicomParams);
                    break;
                case ExtractDicomDataMethods.FrameOrientation.Sagittal:
                    if (thisDicomParams != null) m_SagittalStudyDicomParametersList.Add(thisDicomParams);
                    break;
                case ExtractDicomDataMethods.FrameOrientation.Coronal:
                    if (thisDicomParams != null) m_CoronalStudyDicomParametersList.Add(thisDicomParams);
                    break;
                default:
                    break;
            }
        }

        public void RotateParentGO_ByOrientation(GameObject parentGO)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("RotateSingleGO_ByOrientation");

            Quaternion rot = new Quaternion();

            if (parentGO.name.Contains("Axial"))
            {
                rot = Quaternion.AngleAxis(-180, Vector3.right) *
                        Quaternion.AngleAxis(-180, Vector3.up) *
                        Quaternion.AngleAxis(-180, Vector3.forward);
            }
            else if (parentGO.name.Contains("Sagittal"))
            {
                rot = Quaternion.AngleAxis(-90, Vector3.right) *
                        Quaternion.AngleAxis(0, Vector3.up) *
                        Quaternion.AngleAxis(0, Vector3.forward);
            }
            else //Coronal
            {
                rot = Quaternion.AngleAxis(180, Vector3.right) *
                        Quaternion.AngleAxis(0, Vector3.up) *
                        Quaternion.AngleAxis(0, Vector3.forward);
            }

            parentGO.transform.rotation = rot;

            m_TotalManangerSimulationSeconds += thisWatch.StopWatch();
        }

        public void RotateParentGOAndPositionPlayer()
        {
            //Transpose Images to Parent Pivot and rotate

            DicomParameters dummyImageLocationParams = new DicomParameters();
            if (m_AxialStudyDicomParametersList.Count > 0 && ((FrameOrientation)SimulationOrientation == FrameOrientation.Axial || SimulationOrientation == ExtractDicomDataMethods.TargetCut.All))
            {
                RotateParentGO_ByOrientation(Parent_Axial);
                //m_FreeFlyCamera.PositionThirdPersonInFrontOfImageOrPixel(ref m_AxialStudyDicomParametersList);
            }
            if (m_SagittalStudyDicomParametersList.Count > 0 && ((FrameOrientation)SimulationOrientation == FrameOrientation.Sagittal || SimulationOrientation == ExtractDicomDataMethods.TargetCut.All))
            {
                RotateParentGO_ByOrientation(Parent_Sagittal);
                //m_FreeFlyCamera.PositionThirdPersonInFrontOfImageOrPixel(ref m_SagittalStudyDicomParametersList);
            }
            if (m_CoronalStudyDicomParametersList.Count > 0 && ((FrameOrientation)SimulationOrientation == FrameOrientation.Coronal || SimulationOrientation == ExtractDicomDataMethods.TargetCut.All))
            {
                RotateParentGO_ByOrientation(Parent_Coronal);
                //m_FreeFlyCamera.PositionThirdPersonInFrontOfImageOrPixel(ref m_CoronalStudyDicomParametersList);
            }
        }

        public void SaveStudyInitialPixelNativeArray(ref List<DicomParameters> m_StudyTypeDicomParametersList,
                                                    NativeArray<UnityEngine.Color32> thisImagePixelData,
                                                    int positionInArray,
                                                    ref NativeArray<UnityEngine.Color32>[] m_AxialStudyInitialPixelNativeArray,
                                                    ref NativeArray<UnityEngine.Color32>[] m_CoronalStudyInitialPixelNativeArray,
                                                    ref NativeArray<UnityEngine.Color32>[] m_SagittalStudyInitialPixelNativeArray)
        {
            //Save original pixel data for each dicom image
            switch (m_StudyTypeDicomParametersList[0].FrameOrientation)
            {
                case ExtractDicomDataMethods.FrameOrientation.Axial:
                    if (m_AxialStudyInitialPixelNativeArray == null)
                    {
                        m_AxialStudyInitialPixelNativeArray = new NativeArray<UnityEngine.Color32>[m_StudyTypeDicomParametersList.Count];
                    }

                    m_AxialStudyInitialPixelNativeArray[positionInArray] = thisImagePixelData;
                    break;

                case ExtractDicomDataMethods.FrameOrientation.Sagittal:
                    if (m_CoronalStudyInitialPixelNativeArray == null)
                    {
                        m_CoronalStudyInitialPixelNativeArray = new NativeArray<UnityEngine.Color32>[m_StudyTypeDicomParametersList.Count];
                    }

                    m_CoronalStudyInitialPixelNativeArray[positionInArray] = thisImagePixelData;
                    break;

                case ExtractDicomDataMethods.FrameOrientation.Coronal:
                    if (m_SagittalStudyInitialPixelNativeArray == null)
                    {
                        m_SagittalStudyInitialPixelNativeArray = new NativeArray<UnityEngine.Color32>[m_StudyTypeDicomParametersList.Count];
                    }

                    m_SagittalStudyInitialPixelNativeArray[positionInArray] = thisImagePixelData;
                    break;
            }
        }

        private void OnDisable()
        {
            int counter = 0;

            for (int i=0;i<m_AxialStudyDicomParametersList.Count;i++)
            {
                if (m_AxialStudyDicomParametersList[i].m_CurrentTextureColorData.IsCreated)
                {
                    m_AxialStudyDicomParametersList[i].m_CurrentTextureColorData.Dispose();
                    counter++;
                }
                if (m_AxialStudyDicomParametersList[i].m_CurrentTextureColor32Data.IsCreated)
                {
                    m_AxialStudyDicomParametersList[i].m_CurrentTextureColor32Data.Dispose();
                    counter++;
                }

                if (m_AxialStudyDicomParametersList[i].All_Groups_PixelData_1DArray.IsCreated)
                {
                    m_AxialStudyDicomParametersList[i].All_Groups_PixelData_1DArray.Dispose();
                    counter++;
                }

                WindowWidth_LevelSetting = WindowWidth_Level.Manufacturer;
            }
            if(b_DebugMssg) Debug.Log("Dipose Native Containers for m_AxialStudyDicomParametersList: " + counter + " NativeContainers");

            counter = 0;
            for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
            {
                if (m_CoronalStudyDicomParametersList[i].m_CurrentTextureColorData.IsCreated)
                {
                    m_CoronalStudyDicomParametersList[i].m_CurrentTextureColorData.Dispose();
                    counter++;
                }
                if (m_CoronalStudyDicomParametersList[i].m_CurrentTextureColor32Data.IsCreated)
                {
                    m_CoronalStudyDicomParametersList[i].m_CurrentTextureColor32Data.Dispose();
                    counter++;
                }
                if (m_CoronalStudyDicomParametersList[i].All_Groups_PixelData_1DArray.IsCreated)
                {
                    m_CoronalStudyDicomParametersList[i].All_Groups_PixelData_1DArray.Dispose();
                    counter++;
                }
            }
            if (b_DebugMssg) Debug.Log("Dipose Native Containers for m_CoronalStudyDicomParametersList: " + counter + " NativeContainers");


            counter = 0;
            for (int i = 0; i < m_SagittalStudyDicomParametersList.Count; i++)
            {
                if (m_SagittalStudyDicomParametersList[i].m_CurrentTextureColorData.IsCreated)
                {
                    m_SagittalStudyDicomParametersList[i].m_CurrentTextureColorData.Dispose();
                    counter++;
                }
                if (m_SagittalStudyDicomParametersList[i].m_CurrentTextureColor32Data.IsCreated)
                {
                    m_SagittalStudyDicomParametersList[i].m_CurrentTextureColor32Data.Dispose();
                    counter++;
                }
                if (m_SagittalStudyDicomParametersList[i].All_Groups_PixelData_1DArray.IsCreated)
                {
                    m_SagittalStudyDicomParametersList[i].All_Groups_PixelData_1DArray.Dispose();
                    counter++;
                }
            }

            if (b_DebugMssg) Debug.Log("Dipose Native Containers for m_SagittalStudyDicomParametersList: " + counter + " NativeContainers");

            double totalSimSecs =  m_TotalManangerSimulationSeconds + 
                                    DicomStorageMethods.m_DeserializeSimulationSeconds +
                                    DicomStorageMethods.m_ReadStoredSimulationSeconds +
                                    DicomStorageMethods.m_SaveImageSimulationSeconds +
                                    m_ExtractDicomDataMethods.m_TotalGenerateDicomDataSeconds + 
                                    m_CreateImageMethods_CPU.m_TotalCreateImageMethodsSeconds;

            Debug.Log("Total Images: " + TotalDicomImagesCreated+ ". Total Images Excluded:"+TotalDicomExcluded+". Simulation Secs:" + totalSimSecs +
                        ". m_TotalManangerSimulationSeconds: " + m_TotalManangerSimulationSeconds +
                        ". m_DeserializeSimulationSeconds: " + DicomStorageMethods.m_DeserializeSimulationSeconds +
                        ". m_ReadStoredSimulationSeconds: " + DicomStorageMethods.m_ReadStoredSimulationSeconds +
                        ". m_SaveImageSimulationSeconds: " + DicomStorageMethods.m_SaveImageSimulationSeconds +
                        ". m_TotalGenerateDicomDataSeconds: " + m_ExtractDicomDataMethods.m_TotalGenerateDicomDataSeconds +
                        ". m_TotalCreateImageMethodsSeconds: " + m_CreateImageMethods_CPU.m_TotalCreateImageMethodsSeconds);
        }

        //MAYBE NOT NEEDED
        /*public void ReOrderSlicesTopAndBottom(FrameOrientation orientation)
        {
            List<DicomParameters> tempOrderedLocationParams = new List<DicomParameters>();

            int imageCount = m_CoronalStudyDicomParametersList.Count;

            switch (orientation)
            {
                case FrameOrientation.Axial:
                    break;
                case FrameOrientation.Sagittal:
                    break;
                case FrameOrientation.Coronal:
                    m_CoronalStudyDicomParametersList = m_CoronalStudyDicomParametersList.OrderBy(x => x.ImagePlaneGO.transform.position.y).ToList();

                    tempOrderedLocationParams = m_CoronalStudyDicomParametersList;

                    for (int i = 0; i < m_CoronalStudyDicomParametersList.Count; i++)
                    {
                        m_CoronalStudyDicomParametersList[i].ImagePlaneGO.transform.position = new Vector3(m_CoronalStudyDicomParametersList[i].ImagePlaneGO.transform.position.x,
                                                                                                        tempOrderedLocationParams.ElementAt(imageCount - i - 1).ImagePlaneGO.transform.position.y,
                                                                                                        m_CoronalStudyDicomParametersList[i].ImagePlaneGO.transform.position.z);
                    }

                    //tempOrderedLocationParams.Clear();

                    //for (int i = 0; i < m_CoronalStudyLocalizationParametersList.Count; i++)
                    //  Debug.Log("ORDER: i:" + i +" image " + m_CoronalStudyLocalizationParametersList[i].ImagePlaneGO.name + " Y:"+ m_CoronalStudyLocalizationParametersList[i].ImagePlaneGO.transform.position.y);
                    break;
                default:
                    break;
            }
        }*/
    }
}
