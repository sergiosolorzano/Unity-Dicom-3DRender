using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Jobs;

using Rendergon.Dicom;
using Rendergon.Computer_Graphics;
using Rendergon.Managers;
using System;
using Unity.Jobs.LowLevel.Unsafe;
using Rendergon.Utilities;

namespace Rendergon.UI
{
    public class UI_Hounsfield : MonoBehaviour
    {
        DicomManager m_DicomManager;
        CreateImageMethods_CPU m_CreateImageMethods;

        public bool b_DebugMssg;

        private void OnEnable()
        {
            b_DebugMssg = false;

            Debug.Log(Application.persistentDataPath);
            m_DicomManager = GameObject.FindGameObjectWithTag("Managers").GetComponent<DicomManager>();
            m_CreateImageMethods = GameObject.FindGameObjectWithTag("Image_Methods").GetComponent<CreateImageMethods_CPU>();
        }

        public void UpdateColorRangeTransparencyButtonPressedBurst(bool b_AlphaOn, UI_Methods.GrayscaleRange thisHounsfieldRangeIndex, 
            Dictionary<int, Vector2> GrayscaleRangeDict)
        {
            UpdateColorRangeTransparency(b_AlphaOn, ref m_DicomManager.m_AxialStudyDicomParametersList, thisHounsfieldRangeIndex, GrayscaleRangeDict, m_CreateImageMethods.m_SceneDataOrigin);
            UpdateColorRangeTransparency(b_AlphaOn, ref m_DicomManager.m_CoronalStudyDicomParametersList, thisHounsfieldRangeIndex, GrayscaleRangeDict, m_CreateImageMethods.m_SceneDataOrigin);
            UpdateColorRangeTransparency(b_AlphaOn, ref m_DicomManager.m_SagittalStudyDicomParametersList, thisHounsfieldRangeIndex, GrayscaleRangeDict, m_CreateImageMethods.m_SceneDataOrigin);
        }

        public void UpdateColorRangeTransparencyButtonPressedBurst(bool b_AlphaOn, UI_Methods.GrayscaleRange thisHounsfieldRangeIndex, 
            Dictionary<int, Vector2> GrayscaleRangeDict, DicomParameters thisDicomImageParams)
        {
            UpdateColorRangeTransparency(b_AlphaOn, ref thisDicomImageParams, thisHounsfieldRangeIndex, GrayscaleRangeDict, m_CreateImageMethods.m_SceneDataOrigin);
        }

        void UpdateColorRangeTransparency(bool b_AlphaOn, ref List<DicomParameters> m_StudyTypeDicomParametersList, UI_Methods.GrayscaleRange thisHounsfieldRangeIndex, 
            Dictionary<int, Vector2> GrayscaleRangeDict, CreateImageMethods_CPU.DataOrigin thisSceneDataOrigin)
        {
            int hounsfieldEnumIndex = Math.Max(0, Array.IndexOf(Enum.GetValues((thisHounsfieldRangeIndex).GetType()), thisHounsfieldRangeIndex) - 1);

            for (int x = 0; x < m_StudyTypeDicomParametersList.Count; x++)
            {
                NativeArray<int> counter = new NativeArray<int>(m_StudyTypeDicomParametersList[x].m_DicomTextureWidth * m_StudyTypeDicomParametersList[x].m_DicomTextureHeight, Allocator.TempJob);

                //Get Size of this range in 1D Array
                int rangeSize = 0;
                if (hounsfieldEnumIndex == GrayscaleRangeDict.Count - 1)
                    rangeSize = m_StudyTypeDicomParametersList[x].All_Groups_PixelData_1DArray.Length - m_StudyTypeDicomParametersList[x].All_Groups_ArrayStartIndex[hounsfieldEnumIndex];
                else
                    rangeSize = m_StudyTypeDicomParametersList[x].All_Groups_ArrayStartIndex[hounsfieldEnumIndex + 1] - m_StudyTypeDicomParametersList[x].All_Groups_ArrayStartIndex[hounsfieldEnumIndex];

                if(b_DebugMssg) Debug.Log("Request change rangeSize:" + rangeSize + " for index:" + hounsfieldEnumIndex);
                
                if(m_DicomManager.m_SerializedDicomTextureFormat==DicomManager.SerializedTextureFormat.RGBAFloat)
                {
                    var changeColorHandle = new Pixel_Manipulation_Methods.ApplyHounsfieldAlphaToColorParallelJob()
                    {
                        _b_AlphaOn = b_AlphaOn,
                        _counter = counter,
                        _textureData = m_StudyTypeDicomParametersList[x].m_CurrentTextureColorData,
                        _all_Groups_PixelData_1DArray = m_StudyTypeDicomParametersList[x].All_Groups_PixelData_1DArray,
                        _pixelData_1DArray_StartIndex = m_StudyTypeDicomParametersList[x].All_Groups_ArrayStartIndex[hounsfieldEnumIndex],
                    };
                    changeColorHandle.Schedule(rangeSize, 1).Complete();

                    m_StudyTypeDicomParametersList[x].m_DicomTexture.SetPixelData(m_StudyTypeDicomParametersList[x].m_CurrentTextureColorData, 0, 0);

                }
                else if (m_DicomManager.m_SerializedDicomTextureFormat == DicomManager.SerializedTextureFormat.RGBA32)
                {
                    var changeColor32Handle = new Pixel_Manipulation_Methods.ApplyHounsfieldAlphaToColor32ParallelJob()
                    {
                        _b_AlphaOn = b_AlphaOn,
                        _counter = counter,
                        _textureData = m_StudyTypeDicomParametersList[x].m_CurrentTextureColor32Data,
                        _all_Groups_PixelData_1DArray = m_StudyTypeDicomParametersList[x].All_Groups_PixelData_1DArray,
                        _pixelData_1DArray_StartIndex = m_StudyTypeDicomParametersList[x].All_Groups_ArrayStartIndex[hounsfieldEnumIndex],
                    };
                    changeColor32Handle.Schedule(rangeSize, 1).Complete();

                    m_StudyTypeDicomParametersList[x].m_DicomTexture.SetPixelData(m_StudyTypeDicomParametersList[x].m_CurrentTextureColor32Data, 0, 0);

                }

                if (b_DebugMssg) Debug.Log("Finished Texture Pos " + x + " pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(counter,false));

                m_StudyTypeDicomParametersList[x].m_DicomTexture.Apply();

                counter.Dispose();
            }
        }

        void UpdateColorRangeTransparency(bool b_AlphaOn, ref DicomParameters thisDicomImageParams, UI_Methods.GrayscaleRange thisHounsfieldRangeIndex, 
            Dictionary<int, Vector2> GrayscaleRangeDict, CreateImageMethods_CPU.DataOrigin thisSceneDataOrigin)
        {
            int hounsfieldEnumIndex = Math.Max(0, Array.IndexOf(Enum.GetValues((thisHounsfieldRangeIndex).GetType()), thisHounsfieldRangeIndex) - 1);

            NativeArray<int> counter = new NativeArray<int>(thisDicomImageParams.m_DicomTextureWidth * thisDicomImageParams.m_DicomTextureHeight, Allocator.TempJob);

            //Get Size of this range in 1D Array
            int rangeSize = 0;
            if (hounsfieldEnumIndex == GrayscaleRangeDict.Count - 1)
                rangeSize = thisDicomImageParams.All_Groups_PixelData_1DArray.Length - thisDicomImageParams.All_Groups_ArrayStartIndex[hounsfieldEnumIndex];
            else
                rangeSize = thisDicomImageParams.All_Groups_ArrayStartIndex[hounsfieldEnumIndex + 1] - thisDicomImageParams.All_Groups_ArrayStartIndex[hounsfieldEnumIndex];

            if (b_DebugMssg) Debug.Log("Request change rangeSize:" + rangeSize + " for index:" + hounsfieldEnumIndex);

            if (m_DicomManager.m_SerializedDicomTextureFormat == DicomManager.SerializedTextureFormat.RGBAFloat)
            {
                var changeColorHandle = new Pixel_Manipulation_Methods.ApplyHounsfieldAlphaToColorParallelJob()
                {
                    _b_AlphaOn = b_AlphaOn,
                    _counter = counter,
                    _textureData = thisDicomImageParams.m_CurrentTextureColorData,
                    _all_Groups_PixelData_1DArray = thisDicomImageParams.All_Groups_PixelData_1DArray,
                    _pixelData_1DArray_StartIndex = thisDicomImageParams.All_Groups_ArrayStartIndex[hounsfieldEnumIndex],
                };
                changeColorHandle.Schedule(rangeSize, 1).Complete();

                thisDicomImageParams.m_DicomTexture.SetPixelData(thisDicomImageParams.m_CurrentTextureColorData, 0, 0);

            }

            else if(m_DicomManager.m_SerializedDicomTextureFormat == DicomManager.SerializedTextureFormat.RGBA32)
            {
                var changeColor32Handle = new Pixel_Manipulation_Methods.ApplyHounsfieldAlphaToColor32ParallelJob()
                {
                    _b_AlphaOn = b_AlphaOn,
                    _counter = counter,
                    _textureData = thisDicomImageParams.m_CurrentTextureColor32Data,
                    _all_Groups_PixelData_1DArray = thisDicomImageParams.All_Groups_PixelData_1DArray,
                    _pixelData_1DArray_StartIndex = thisDicomImageParams.All_Groups_ArrayStartIndex[hounsfieldEnumIndex],
                };
                changeColor32Handle.Schedule(rangeSize, 64).Complete();

                thisDicomImageParams.m_DicomTexture.SetPixelData(thisDicomImageParams.m_CurrentTextureColor32Data, 0, 0);

            }

            if (b_DebugMssg) Debug.Log("Finished Texture Image " + thisDicomImageParams.ImageName + " pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(counter, false));
            
            thisDicomImageParams.m_DicomTexture.Apply();

            counter.Dispose();
        }
    }
}
