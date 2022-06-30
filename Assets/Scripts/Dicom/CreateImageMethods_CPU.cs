using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

using FellowOakDicom;
using FellowOakDicom.Imaging.Render;
using FellowOakDicom.Imaging;
using FellowOakDicom.Imaging.LUT;

using System.Linq;

using Rendergon.Managers;
using Rendergon.Computer_Graphics;
using Rendergon.CreateNormals;
using Rendergon.Storage;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Rendergon.Utilities;

namespace Rendergon.Dicom
{
    public class CreateImageMethods_CPU : MonoBehaviour
    {
        //[HideInInspector]
        //Pixel_Manipulation_Methods m_Pixel_Manipulation_Methods;
        GameObject Parent_Axial;
        GameObject Parent_Sagittal;
        GameObject Parent_Coronal;

        public enum DataOrigin { Original, Stored };
        [HideInInspector]
        public DataOrigin m_SceneDataOrigin;
        [HideInInspector]
        public double m_TotalCreateImageMethodsSeconds = 0;

        bool b_DebugMssg;

        private void Start()
        {
            b_DebugMssg = false;

            Parent_Axial = GameObject.FindGameObjectWithTag("Parent_Axial");
            Parent_Sagittal = GameObject.FindGameObjectWithTag("Parent_Sagittal");
            Parent_Coronal = GameObject.FindGameObjectWithTag("Parent_Coronal");
        }

        public static TextureFormat ConvertSelfTextureFormatToUnity(DicomManager.SerializedTextureFormat selfFormat)
        {
            switch (selfFormat)
            {
                case DicomManager.SerializedTextureFormat.RGBAFloat:
                    return TextureFormat.RGBAFloat;
                case DicomManager.SerializedTextureFormat.RGBA32:
                    return TextureFormat.RGBA32;
                default:
                    return TextureFormat.RGBA32;
            }
        }

        public short[] ExtractRawPixelData(IPixelData pixelDataRender)
        {
            short[] pixeldouble16bitArray = new short[pixelDataRender.Width * pixelDataRender.Height];
            for (int r = 0; r < (pixelDataRender.Height); r++)
            {
                for (int c = 0; c < (pixelDataRender.Width); c++)
                    pixeldouble16bitArray[pixelDataRender.Height * r + c] = (short)pixelDataRender.GetPixel(c, r);
            }

            return pixeldouble16bitArray;
        }

        public IEnumerator GetOriginal_WindowedPixelLocationAndColor<T>(Action<DicomParameters> thisDicomParametersResult, DicomParameters thisImgDicomParams, DicomDataset dicomFileDataSet, IPixelData pixelDataRender16bit, FrameGeometry thisGeometry, DicomManager.SerializedTextureFormat self_serializableTextureFormat, Dictionary<int, Vector2> GrayscaleRangeDict, bool b_SafeChecks, bool b_UseHounsfield)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("GetOriginal_GrayscalePixelLocationAndColor");

            var pixelDataRender16bitArray = new NativeArray<short>(ExtractRawPixelData(pixelDataRender16bit), Allocator.TempJob);

            thisImgDicomParams.m_DicomTexture = new Texture2D(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, CreateImageMethods_CPU.ConvertSelfTextureFormatToUnity(self_serializableTextureFormat), false);

            Debug.Log("Format:" + self_serializableTextureFormat);

            //Set Direction for Textures
            Vector3 directionColumn = new Vector3((float)thisGeometry.DirectionColumn.X, (float)thisGeometry.DirectionColumn.Y, (float)thisGeometry.DirectionColumn.Z);
            Vector3 directionRow = new Vector3((float)thisGeometry.DirectionRow.X, (float)thisGeometry.DirectionRow.Y, (float)thisGeometry.DirectionRow.Z);
            float pixelSpacingBetweenColumns = (float)thisGeometry.PixelSpacingBetweenColumns;
            float pixelSpacingBetweenRows = (float)thisGeometry.PixelSpacingBetweenRows;

            var outputLocationData = new NativeArray<Vector3>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            NativeArray<int> hounsfieldCounterPerGroup = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);
            NativeArray<int> counter = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            //store all hounsfield ranges
            thisImgDicomParams.All_Groups_PixelDataMultiHashMap = new NativeMultiHashMap<int, PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

            #region Color Job
            if (self_serializableTextureFormat == DicomManager.SerializedTextureFormat.RGBAFloat)
            {
                //Set Hounsfield color ranges
                NativeHashMap<int, Vector2> grayscaleRangeHashArray = new NativeHashMap<int, Vector2>(GrayscaleRangeDict.Count, Allocator.TempJob);
                for (int i = 0; i < GrayscaleRangeDict.Count; i++)
                    grayscaleRangeHashArray.TryAdd(i, new Vector2(GrayscaleRangeDict[i].x, GrayscaleRangeDict[i].y));

                thisImgDicomParams.m_CurrentTextureGrayscaleData= new NativeArray<Color>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

                //Call Job
                var getOriginalDicomImagePixelLocationAndColorHandle = new Pixel_Manipulation_Methods.GetOriginal_DicomImageWindowed_ColorParallelJob()
                {
                    _b_UseHounsfield = b_UseHounsfield,
                    _inputPixelDataWithLUT = pixelDataRender16bitArray,//input texture data for original texture pixels
                    _pixelDataRender16bit_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisGeometry_DirectionColumn = directionColumn,
                    _thisGeometry_DirectionRow = directionRow,
                    _thisGeometry_PixelSpacingBetweenColumns = pixelSpacingBetweenColumns,
                    _thisGeometry_PixelSpacingBetweenRows = pixelSpacingBetweenRows,
                    _thisDicomImage_PointTopLeft = thisImgDicomParams.PointTopLeft,
                    _textureSingleChannelPixels = thisImgDicomParams.m_CurrentTextureGrayscaleData,
                    _outputLocationData = outputLocationData,
                    _rangeGroupCounter = hounsfieldCounterPerGroup,
                    _counter = counter,
                    _grayScaleRangeHashArray = grayscaleRangeHashArray,
                    _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
                };
                getOriginalDicomImagePixelLocationAndColorHandle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

                thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureGrayscaleData, 0, 0);

                if (b_UseHounsfield && b_SafeChecks)
                    CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

                grayscaleRangeHashArray.Dispose();

            }
            #endregion

            #region Color32 Job
            else if (self_serializableTextureFormat == DicomManager.SerializedTextureFormat.RGBA32)
            {
                //Set Hounsfield color ranges
                NativeHashMap<int, Vector2Int> grayscaleRangeHashArray = new NativeHashMap<int, Vector2Int>(GrayscaleRangeDict.Count, Allocator.TempJob);
                for (int i = 0; i < GrayscaleRangeDict.Count; i++)
                {
                    if (i == 0)
                    {
                        grayscaleRangeHashArray.TryAdd(i, new Vector2Int(0, 1));
                    }
                    else
                        grayscaleRangeHashArray.TryAdd(i, new Vector2Int((int)Math.Round((double)(GrayscaleRangeDict[i].x * 255), 0), (int)Math.Round((double)(GrayscaleRangeDict[i].y * 255))));
                }

                thisImgDicomParams.m_CurrentTextureColor32Data = new NativeArray<ColorARGB32>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

                //Call Job
                var getOriginalDicomImagePixelLocationAndColor32Handle = new Pixel_Manipulation_Methods.GetOriginal_DicomImageWindowed_Color32ParallelJob()
                {
                    _b_UseHounsfield = b_UseHounsfield,
                    _inputPixelDataWithLUT = pixelDataRender16bitArray,
                    _pixelDataRender16bit_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisGeometry_DirectionColumn = directionColumn,
                    _thisGeometry_DirectionRow = directionRow,
                    _thisGeometry_PixelSpacingBetweenColumns = pixelSpacingBetweenColumns,
                    _thisGeometry_PixelSpacingBetweenRows = pixelSpacingBetweenRows,
                    _thisDicomImage_PointTopLeft = thisImgDicomParams.PointTopLeft,
                    _textureSingleChannelPixels = thisImgDicomParams.m_CurrentTextureColor32Data,
                    _outputLocationData = outputLocationData,
                    _rangeGroupCounter = hounsfieldCounterPerGroup,
                    _counter = counter,
                    _grayScaleRangeHashArray = grayscaleRangeHashArray,
                    _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
                };
                getOriginalDicomImagePixelLocationAndColor32Handle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

                thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureColor32Data, 0, 0);

                if (b_UseHounsfield && b_SafeChecks)
                    CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

                grayscaleRangeHashArray.Dispose();

            }
            #endregion

            thisImgDicomParams.m_DicomTexture.Apply();

            //Debug.Log("Current:" + " value=" + thisImgDicomParams.m_CurrentTextureColorData[151309].r);
            if (b_DebugMssg) Debug.Log("Total texture pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(counter, false));
            if (b_DebugMssg) Debug.Log("Total hounsfield pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(hounsfieldCounterPerGroup, false));

            thisImgDicomParams.PixelLocationArray = new Vector3[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight];
            outputLocationData.CopyTo(thisImgDicomParams.PixelLocationArray);

            //Create Pixel color Quadrants
            if (b_UseHounsfield)
            {
                thisImgDicomParams.All_Groups_PixelData_1DArray = new NativeArray<PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);
                IEnumerator createPixelDataNativeArraysCoroutine = CreatePixelDataArrays(GrayscaleRangeDict, thisImgDicomParams, result => thisImgDicomParams = result);
                yield return (createPixelDataNativeArraysCoroutine);
            }

            pixelDataRender16bitArray.Dispose();
            outputLocationData.Dispose();
            hounsfieldCounterPerGroup.Dispose();
            counter.Dispose();
            thisImgDicomParams.All_Groups_PixelDataMultiHashMap.Dispose();

            m_TotalCreateImageMethodsSeconds += thisWatch.StopWatch();

            thisDicomParametersResult(thisImgDicomParams);
            yield return (thisImgDicomParams);
        }

        public IEnumerator GetOriginal_DicomImagePixelLocationAndColor<T>(Action<DicomParameters> thisDicomParametersResult, DicomParameters thisImgDicomParams, DicomDataset dicomFileDataSet, IPixelData pixelDataRender16bit, FrameGeometry thisGeometry, DicomManager.SerializedTextureFormat self_serializableTextureFormat, Dictionary<int, Vector2> GrayscaleRangeDict, bool b_SafeChecks, bool b_UseHounsfield)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("GetOriginal_DicomImagePixelLocationAndColor");

            //Tutorial: https://gitter.im/fo-dicom/fo-dicom?at=5e28366b258edf397bc913a7
            int[] pixelDataWithLUT = new int[pixelDataRender16bit.Width * pixelDataRender16bit.Height];//thisImgLocalizationiParams.ImageNumberRow * thisImgLocalizationiParams.PixelSpacingRow

            var voilutOptions = GrayscaleRenderOptions.FromDataset(dicomFileDataSet);
             var grayScaleLUT = VOILUT.Create(voilutOptions);

            pixelDataRender16bit.Render(grayScaleLUT, pixelDataWithLUT);

            if(b_DebugMssg) Debug.Log("Max:" + pixelDataWithLUT.Max());
            if (b_DebugMssg) Debug.Log("Min:" + pixelDataWithLUT.Min());

            var pixelDataWithLUTNativeArray = new NativeArray<int>(pixelDataWithLUT, Allocator.TempJob);
            
            thisImgDicomParams.m_DicomTexture = new Texture2D(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, CreateImageMethods_CPU.ConvertSelfTextureFormatToUnity(self_serializableTextureFormat), false);

            //Set Direction for Textures
            Vector3 directionColumn = new Vector3((float)thisGeometry.DirectionColumn.X, (float)thisGeometry.DirectionColumn.Y, (float)thisGeometry.DirectionColumn.Z);
            Vector3 directionRow = new Vector3((float)thisGeometry.DirectionRow.X, (float)thisGeometry.DirectionRow.Y, (float)thisGeometry.DirectionRow.Z);
            float pixelSpacingBetweenColumns = (float)thisGeometry.PixelSpacingBetweenColumns;
            float pixelSpacingBetweenRows = (float)thisGeometry.PixelSpacingBetweenRows;

            var outputLocationData = new NativeArray<Vector3>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            NativeArray<int> hounsfieldCounterPerGroup = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);
            NativeArray<int> counter = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            //store all hounsfield ranges
            thisImgDicomParams.All_Groups_PixelDataMultiHashMap = new NativeMultiHashMap<int, PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

            #region Color Job
            if (self_serializableTextureFormat == DicomManager.SerializedTextureFormat.RGBAFloat)
            {
                //Set Hounsfield color ranges
                NativeHashMap<int, Vector2> grayscaleRangeHashArray = new NativeHashMap<int, Vector2>(GrayscaleRangeDict.Count, Allocator.TempJob);
                for (int i = 0; i < GrayscaleRangeDict.Count; i++)
                    grayscaleRangeHashArray.TryAdd(i, new Vector2(GrayscaleRangeDict[i].x, GrayscaleRangeDict[i].y));

                thisImgDicomParams.m_CurrentTextureColorData = new NativeArray<Color>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

                //Call Job
                var getOriginalDicomImagePixelLocationAndColorHandle = new Pixel_Manipulation_Methods.GetOriginal_DicomImagePixelColorAndLocation_AndHounsfieldRangesParallelJob()
                {
                    _b_UseHounsfield = b_UseHounsfield,
                    _inputPixelDataWithLUT = pixelDataWithLUTNativeArray,//input texture data for original texture pixels
                    _pixelDataRender16bit_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisGeometry_DirectionColumn = directionColumn,
                    _thisGeometry_DirectionRow = directionRow,
                    _thisGeometry_PixelSpacingBetweenColumns = pixelSpacingBetweenColumns,
                    _thisGeometry_PixelSpacingBetweenRows = pixelSpacingBetweenRows,
                    _thisDicomImage_PointTopLeft = thisImgDicomParams.PointTopLeft,
                    _texturePixels = thisImgDicomParams.m_CurrentTextureColorData,
                    _outputLocationData = outputLocationData,
                    _rangeGroupCounter = hounsfieldCounterPerGroup,
                    _counter = counter,
                    _grayScaleRangeHashArray = grayscaleRangeHashArray,
                    _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
                };
                getOriginalDicomImagePixelLocationAndColorHandle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

                thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureColorData, 0, 0);

                if (b_UseHounsfield && b_SafeChecks)
                    CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

                grayscaleRangeHashArray.Dispose();

            }
            #endregion

            #region Color32 Job
            else if (self_serializableTextureFormat == DicomManager.SerializedTextureFormat.RGBA32)
            {
                //Set Hounsfield color ranges
                NativeHashMap<int, Vector2Int> grayscaleRangeHashArray = new NativeHashMap<int, Vector2Int>(GrayscaleRangeDict.Count, Allocator.TempJob);
                for (int i = 0; i < GrayscaleRangeDict.Count; i++)
                {
                    if(i==0)
                    {
                        grayscaleRangeHashArray.TryAdd(i, new Vector2Int(0, 1));
                    }
                    else
                        grayscaleRangeHashArray.TryAdd(i, new Vector2Int((int)Math.Round((double)(GrayscaleRangeDict[i].x * 255), 0), (int)Math.Round((double)(GrayscaleRangeDict[i].y * 255))));
                }

                thisImgDicomParams.m_CurrentTextureColor32Data= new NativeArray<ColorARGB32>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

                //Call Job
                var getOriginalDicomImagePixelLocationAndColor32Handle = new Pixel_Manipulation_Methods.GetOriginal_DicomImagePixelColor32AndLocation_AndHounsfieldRangesParallelJob()
                {
                    _b_UseHounsfield = b_UseHounsfield,
                    _inputPixelDataWithLUT = pixelDataWithLUTNativeArray,//input texture data for original texture pixels
                    _pixelDataRender16bit_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _thisGeometry_DirectionColumn = directionColumn,
                    _thisGeometry_DirectionRow = directionRow,
                    _thisGeometry_PixelSpacingBetweenColumns = pixelSpacingBetweenColumns,
                    _thisGeometry_PixelSpacingBetweenRows = pixelSpacingBetweenRows,
                    _thisDicomImage_PointTopLeft = thisImgDicomParams.PointTopLeft,
                    _texturePixels = thisImgDicomParams.m_CurrentTextureColor32Data,
                    _outputLocationData = outputLocationData,
                    _rangeGroupCounter = hounsfieldCounterPerGroup,
                    _counter = counter,
                    _grayScaleRangeHashArray = grayscaleRangeHashArray,
                    _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
                };
                getOriginalDicomImagePixelLocationAndColor32Handle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

                thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureColor32Data, 0, 0);

                if (b_UseHounsfield && b_SafeChecks)
                    CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

                grayscaleRangeHashArray.Dispose();

            }
            #endregion

            thisImgDicomParams.m_DicomTexture.Apply();
            
            //Debug.Log("Current:" + " value=" + thisImgDicomParams.m_CurrentTextureColorData[151309].r);
            if (b_DebugMssg) Debug.Log("Total texture pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(counter, false));
            if (b_DebugMssg) Debug.Log("Total hounsfield pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(hounsfieldCounterPerGroup, false));

            thisImgDicomParams.PixelLocationArray = new Vector3[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight];
            outputLocationData.CopyTo(thisImgDicomParams.PixelLocationArray);

            //Create Pixel color Quadrants
            if (b_UseHounsfield)
            {
                thisImgDicomParams.All_Groups_PixelData_1DArray = new NativeArray<PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);
                IEnumerator createPixelDataNativeArraysCoroutine = CreatePixelDataArrays(GrayscaleRangeDict, thisImgDicomParams, result => thisImgDicomParams = result);
                yield return (createPixelDataNativeArraysCoroutine);
            }

            pixelDataWithLUTNativeArray.Dispose();
            outputLocationData.Dispose();
            hounsfieldCounterPerGroup.Dispose();
            counter.Dispose();
            thisImgDicomParams.All_Groups_PixelDataMultiHashMap.Dispose();

            m_TotalCreateImageMethodsSeconds += thisWatch.StopWatch();

            thisDicomParametersResult(thisImgDicomParams);
            yield return (thisImgDicomParams);
        }

        public IEnumerator GetStored_DicomImagePixelLocationAndColor<T>(Action<DicomParameters> thisDicomParametersResult, DicomParameters thisImgDicomParams, Dictionary<int, Vector2> GrayscaleRangeDict, bool b_SafeChecks, bool b_UseHounsfield, DicomManager.SerializedTextureFormat self_format)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("GetOStored_DicomImagePixelLocationAndColor");

            NativeArray<int> hounsfieldCounterPerGroup = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);
            NativeArray<int> counter = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            //store all hounsfield ranges
            thisImgDicomParams.All_Groups_PixelDataMultiHashMap = new NativeMultiHashMap<int, PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

            #region Color Job
            if (self_format== DicomManager.SerializedTextureFormat.RGBAFloat)
            {
                //Set Hounsfield color ranges
                NativeHashMap<int, Vector2> grayscaleRangeHashArray = new NativeHashMap<int, Vector2>(GrayscaleRangeDict.Count, Allocator.TempJob);
                for (int i = 0; i < GrayscaleRangeDict.Count; i++)
                    grayscaleRangeHashArray.TryAdd(i, new Vector2(GrayscaleRangeDict[i].x, GrayscaleRangeDict[i].y));

                NativeArray<Color> inputTextureData = new NativeArray<UnityEngine.Color>(thisImgDicomParams.m_DicomTexture.GetRawTextureData<UnityEngine.Color>(), Allocator.TempJob);
                thisImgDicomParams.m_CurrentTextureColorData = new NativeArray<Color>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

                //Call Job
                var getOriginalDicomImagePixelLocationAndColorHandle = new Pixel_Manipulation_Methods.GetStored_DicomImagePixelColorAndLocation_AndHounsfieldRangesParallelJob()
                {
                    _b_UseHounsfield = b_UseHounsfield,
                    _inputTextureData = inputTextureData,
                    _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _texturePixels = thisImgDicomParams.m_CurrentTextureColorData,
                    _rangeGroupCounter = hounsfieldCounterPerGroup,
                    _counter = counter,
                    _grayScaleRangeHashArray = grayscaleRangeHashArray,
                    _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
                };
                getOriginalDicomImagePixelLocationAndColorHandle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

                if (b_UseHounsfield && b_SafeChecks)
                    CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

                inputTextureData.Dispose();
                grayscaleRangeHashArray.Dispose();
            }
            #endregion Color

            #region Color32 Job
            else if (self_format == DicomManager.SerializedTextureFormat.RGBA32)
            {
                //Set Hounsfield color ranges
                NativeHashMap<int, Vector2Int> grayscaleRangeHashArray = new NativeHashMap<int, Vector2Int>(GrayscaleRangeDict.Count, Allocator.TempJob);
                for (int i = 0; i < GrayscaleRangeDict.Count; i++)
                {
                    if (i == 0)
                    {
                        grayscaleRangeHashArray.TryAdd(i, new Vector2Int(0, 1));
                    }
                    else
                        grayscaleRangeHashArray.TryAdd(i, new Vector2Int((int)Math.Round((double)(GrayscaleRangeDict[i].x * 255), 0), (int)Math.Round((double)(GrayscaleRangeDict[i].y * 255))));
                }

                NativeArray<ColorARGB32> inputTextureData = new NativeArray<ColorARGB32>(thisImgDicomParams.m_DicomTexture.GetPixelData<ColorARGB32>(0), Allocator.TempJob);
                thisImgDicomParams.m_CurrentTextureColor32Data= new NativeArray<ColorARGB32>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);
                
                //Call Job
                var getOriginalDicomImagePixelLocationAndColorHandle = new Pixel_Manipulation_Methods.GetStored_DicomImagePixelColor32AndLocation_AndHounsfieldRangesParallelJob()
                {
                    _b_UseHounsfield = b_UseHounsfield,
                    _inputTextureData = inputTextureData,
                    _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                    _texturePixels = thisImgDicomParams.m_CurrentTextureColor32Data,
                    _rangeGroupCounter = hounsfieldCounterPerGroup,
                    _counter = counter,
                    _grayScaleRangeHashArray = grayscaleRangeHashArray,
                    _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
                };
                getOriginalDicomImagePixelLocationAndColorHandle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

                if (b_UseHounsfield && b_SafeChecks)
                    CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

                inputTextureData.Dispose();
                grayscaleRangeHashArray.Dispose();
            }
            #endregion Color32

            if(self_format== DicomManager.SerializedTextureFormat.RGBAFloat)
                thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureColorData, 0, 0);
            else if (self_format == DicomManager.SerializedTextureFormat.RGBA32)
                thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureColor32Data, 0, 0);   

            thisImgDicomParams.m_DicomTexture.Apply();

            if (b_DebugMssg) Debug.Log("Total texture pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(counter, false));

            //Create Pixel color Quadrants
            if (b_UseHounsfield)
            {
                thisImgDicomParams.All_Groups_PixelData_1DArray = new NativeArray<PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);
                IEnumerator createPixelDataNativeArraysCoroutine = CreatePixelDataArrays(GrayscaleRangeDict, thisImgDicomParams, result => thisImgDicomParams = result);
                yield return (createPixelDataNativeArraysCoroutine);

                if (b_DebugMssg) Debug.Log("Total hounsfield pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(hounsfieldCounterPerGroup, false));

            }

            hounsfieldCounterPerGroup.Dispose();
            counter.Dispose();
            thisImgDicomParams.All_Groups_PixelDataMultiHashMap.Dispose();

            m_TotalCreateImageMethodsSeconds += thisWatch.StopWatch();

            thisDicomParametersResult(thisImgDicomParams);
            yield return null;
        }

        public IEnumerator CreatePixelDataArrays(Dictionary<int, Vector2> GrayscaleRangeDict, DicomParameters thisImgDicomParams, Action<DicomParameters> callback)
        {
            int totalCount = 0;
            int thisRangeCount = 0;
            int thisRangePixelDataSize = 0;
            PixelData item;
            NativeMultiHashMapIterator<int> nativeIt;
            thisImgDicomParams.All_Groups_ArrayStartIndex = new int[GrayscaleRangeDict.Count];

            //Create Pixel Data Native Arrays

            foreach (var range in GrayscaleRangeDict)
            {
                thisRangeCount = 0;
                thisRangePixelDataSize = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.CountValuesForKey(range.Key);

                //Create 1D array with a starting index position for each range group within this 1D array
                if (range.Key == 0)
                {
                    thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key] = 0;
                    if (b_DebugMssg) Debug.Log("Start index " + range.Key + " " + thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key]);
                }
                else
                {
                    thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key] = totalCount;
                    if (b_DebugMssg) Debug.Log("Start index " + range.Key + " " + thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key]);
                }

                if (thisImgDicomParams.All_Groups_PixelDataMultiHashMap.TryGetFirstValue(range.Key, out item, out nativeIt))
                {
                    int _index = thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key];
                    do
                    {
                        thisImgDicomParams.All_Groups_PixelData_1DArray[_index] = item;
                        //if (range.Key == 9 && thisRangeCount < 500)
                          //  Debug.Log("Pix1D Array Col32: " + item.pixelColor32);
                        thisRangeCount++;
                        _index++;

                    } while (thisImgDicomParams.All_Groups_PixelDataMultiHashMap.TryGetNextValue(out item, ref nativeIt));
                }

                totalCount += thisRangeCount;

                //Debug.Log("Array Range " + range.Key + " holds " + thisRangeCount + " start Index:"+ thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key] + " end Index:"+ (thisImgDicomParams.All_Groups_ArrayStartIndex[range.Key]+ thisRangeCount));
            }

            callback(thisImgDicomParams);
            yield return (thisImgDicomParams);
        }

        void CheckPixelGroupsCount(NativeHashMap<int,Vector2> grayscaleRangeHashArray, DicomParameters thisImgDicomParams)
        {
            if (b_DebugMssg) Debug.Log("***Image: " + thisImgDicomParams.ImageName + " Pix Added to Map:" + thisImgDicomParams.All_Groups_PixelDataMultiHashMap.Count());

            PixelData item;
            NativeMultiHashMapIterator<int> nativeIt;
            int totalCount = 0;
            for (int rangeColor = 0; rangeColor < grayscaleRangeHashArray.Count(); rangeColor++)
            {
                int count = 0;
                if (thisImgDicomParams.All_Groups_PixelDataMultiHashMap.TryGetFirstValue(rangeColor, out item, out nativeIt))
                {
                    do
                    {
                        count++;
                        //Debug.Log(count + "item color:" + item.pixelColor.r + "," + item.pixelColor.g + "," + item.pixelColor.b + "," + item.pixelColor.a + " item location:" + item.pixelLocat.x + "," + item.pixelLocat.y + "," + item.pixelLocat);
                    } while (thisImgDicomParams.All_Groups_PixelDataMultiHashMap.TryGetNextValue(out item, ref nativeIt));
                }
                totalCount += count;
                if(b_DebugMssg) Debug.Log("Range:" + rangeColor + " total Pixels:" + count);
            }
            if (b_DebugMssg) Debug.Log("***Total Count:" + totalCount);
        }

        void CheckPixelGroupsCount(NativeHashMap<int, Vector2Int> grayscaleRangeHashArray, DicomParameters thisImgDicomParams)
        {
            if (b_DebugMssg) Debug.Log("***Image: " + thisImgDicomParams.ImageName + " Pix Added to Map:" + thisImgDicomParams.All_Groups_PixelDataMultiHashMap.Count());

            PixelData item;
            NativeMultiHashMapIterator<int> nativeIt;
            int totalCount = 0;
            for (int rangeColor = 0; rangeColor < grayscaleRangeHashArray.Count(); rangeColor++)
            {
                int count = 0;
                if (thisImgDicomParams.All_Groups_PixelDataMultiHashMap.TryGetFirstValue(rangeColor, out item, out nativeIt))
                {
                    do
                    {
                        count++;
                        //Debug.Log(count + "item color:" + item.pixelColor.r + "," + item.pixelColor.g + "," + item.pixelColor.b + "," + item.pixelColor.a + " item location:" + item.pixelLocat.x + "," + item.pixelLocat.y + "," + item.pixelLocat);
                    } while (thisImgDicomParams.All_Groups_PixelDataMultiHashMap.TryGetNextValue(out item, ref nativeIt));
                }
                totalCount += count;
                if (b_DebugMssg) Debug.Log("Range:" + rangeColor + " total Pixels:" + count);
            }
            if (b_DebugMssg) Debug.Log("***Total Count:" + totalCount);
        }

        public IEnumerator InstantiateStoredImageGeometry(DicomParameters thisdDicomParams, string PrefabAddress, Action<DicomParameters> thisDicomParamsResult)
        {
            GameObject thisParent = (thisdDicomParams.FrameOrientation == ExtractDicomDataMethods.FrameOrientation.Axial) ? Parent_Axial :
                                (thisdDicomParams.FrameOrientation== ExtractDicomDataMethods.FrameOrientation.Sagittal) ? Parent_Sagittal :
                                Parent_Coronal;

            Vector3 imageNormal = thisdDicomParams.DirectionNormal;
            Quaternion imgRotation = Quaternion.FromToRotation(-Vector3.forward, imageNormal);

            thisdDicomParams.ImagePlaneGO = Instantiate(
                                        Resources.Load<GameObject>(PrefabAddress),
                                        thisdDicomParams.ImagePlanePosition,
                                        imgRotation,
                                        thisParent.transform);

            float minThickness = 2;
            thisdDicomParams.ImagePlaneGO.transform.localScale = new Vector3(
                    (float)thisdDicomParams.m_DicomTextureWidth * (float)thisdDicomParams.PixelSpacingCol,
                    (float)thisdDicomParams.m_DicomTextureHeight * (float)thisdDicomParams.PixelSpacingRow,
                    Math.Max((float)(thisdDicomParams.SliceThickness), minThickness));

            thisdDicomParams.ImagePlaneGO.name = thisdDicomParams.ImageName + "-" + thisdDicomParams.FrameOrientation;

            thisDicomParamsResult(thisdDicomParams);

            yield return thisdDicomParams;
        }

        public void ScaleOriginalImageGeometry(ref DicomParameters thisImageLocationParams, string PrefabAddress)
        {
            float minThickness = 2;
            float minSpacing = 2;
            thisImageLocationParams.ImagePlaneGO.transform.localScale = new Vector3(
                    Math.Max((float)thisImageLocationParams.m_DicomTextureWidth * (float)thisImageLocationParams.PixelSpacingCol, minSpacing),
                    Math.Max((float)thisImageLocationParams.m_DicomTextureHeight * (float)thisImageLocationParams.PixelSpacingRow, minSpacing),
                    Math.Max((float)thisImageLocationParams.SliceThickness, minThickness));

            thisImageLocationParams.ImagePlaneGO.name = thisImageLocationParams.ImageName + "-" + thisImageLocationParams.FrameOrientation;
        }

        //GPU RELATED JOBS
        public IEnumerator GetOriginal_WindowedPixelLocation<T>(Action<DicomParameters> thisDicomParametersResult, DicomParameters thisImgDicomParams, FrameGeometry thisGeometry)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("GetOriginal_WindowedPixelLocation");

            //Set Direction for Textures
            Vector3 directionColumn = new Vector3((float)thisGeometry.DirectionColumn.X, (float)thisGeometry.DirectionColumn.Y, (float)thisGeometry.DirectionColumn.Z);
            Vector3 directionRow = new Vector3((float)thisGeometry.DirectionRow.X, (float)thisGeometry.DirectionRow.Y, (float)thisGeometry.DirectionRow.Z);
            float pixelSpacingBetweenColumns = (float)thisGeometry.PixelSpacingBetweenColumns;
            float pixelSpacingBetweenRows = (float)thisGeometry.PixelSpacingBetweenRows;

            var outputLocationData = new NativeArray<Vector3>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            NativeArray<int> counter = new NativeArray<int>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

            //Call Job
            var getOriginalDicomImagePixelLocationAndColorHandle = new Pixel_Manipulation_Methods.GetOriginal_DicomImageWindowed_LocationParallelJob()
            {
                _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
                _thisGeometry_DirectionColumn = directionColumn,
                _thisGeometry_DirectionRow = directionRow,
                _thisGeometry_PixelSpacingBetweenColumns = pixelSpacingBetweenColumns,
                _thisGeometry_PixelSpacingBetweenRows = pixelSpacingBetweenRows,
                _thisDicomImage_PointTopLeft = thisImgDicomParams.PointTopLeft,
                _outputLocationData = outputLocationData,
                _counter = counter,
            };
            getOriginalDicomImagePixelLocationAndColorHandle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

            if (b_DebugMssg) Debug.Log("Total texture pixel locations gathered:" + Pixel_Manipulation_Methods.ReportJobCounter(counter, false));

            thisImgDicomParams.PixelLocationArray = new Vector3[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight];
            outputLocationData.CopyTo(thisImgDicomParams.PixelLocationArray);
           
            outputLocationData.Dispose();
            counter.Dispose();

            m_TotalCreateImageMethodsSeconds += thisWatch.StopWatch();

            thisDicomParametersResult(thisImgDicomParams);
            yield return (thisImgDicomParams);
        }

    }
}







//Pixel Groups
/*thisImgDicomParams.All_Groups_PixelsHashMap = new NativeHashMap<int, Dicom_PixelColorGroups>(GrayscaleRangeDict.Count, Allocator.Persistent);

NativeHashMap<int, Vector2> grayscaleRangeHashArray = new NativeHashMap<int, Vector2>(GrayscaleRangeDict.Count, Allocator.TempJob);
for(int i=0;i< GrayscaleRangeDict.Count;i++)
    grayscaleRangeHashArray.TryAdd(i, new Vector2(GrayscaleRangeDict[i].x, GrayscaleRangeDict[i].y));


for (int rangeColor = 0; rangeColor < grayscaleRangeHashArray.Count(); rangeColor++)
{
    //DicomParameters.A_Groups_PixelDataHashMap = new NativeHashMap<float3, float4>[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight];
}

for (int x = 0; x < grayscaleRangeHashArray.Count(); x++)
{
    thisImgDicomParams.All_Groups_PixelsHashMap = new NativeHashMap<int, Dicom_PixelColorGroups>(GrayscaleRangeDict.Count, Allocator.Persistent);

    Dicom_PixelColorGroups a_Group = new Dicom_PixelColorGroups();
    a_Group.Group_PixelLocation = new Vector3[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight / grayscaleRangeHashArray.Count()];
    a_Group.Group_PixelColor= new Vector4[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight / grayscaleRangeHashArray.Count()];

    thisImgDicomParams.All_Groups_PixelsHashMap.Add(x, a_Group);
}*/

/*public IEnumerator GetOriginalDicomImagePixelLocationAndColor<T>(Action<DicomParameters> thisDicomParametersResult, DicomParameters thisImgDicomParams, DicomDataset dicomFileDataSet, IPixelData pixelDataRender16bit, FrameGeometry thisGeometry, TextureFormat texFormat, Dictionary<int, Vector2> GrayscaleRangeDict, bool b_SafeChecks, bool b_UseHounsfield)
    {
        //Tutorial: https://gitter.im/fo-dicom/fo-dicom?at=5e28366b258edf397bc913a7
        int[] pixelDataWithLUT = new int[pixelDataRender16bit.Width * pixelDataRender16bit.Height];//thisImgLocalizationiParams.ImageNumberRow * thisImgLocalizationiParams.PixelSpacingRow

        var grayScaleLUT = VOILUT.Create(GrayscaleRenderOptions.FromDataset(dicomFileDataSet));

        pixelDataRender16bit.Render(grayScaleLUT, pixelDataWithLUT);

        var pixelDataWithLUTNativeArray = new NativeArray<int>(pixelDataWithLUT, Allocator.TempJob);
        var outputLocationData = new NativeArray<Vector3>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.TempJob);

        NativeArray<int> RangeGroupCounter = new NativeArray<int>(pixelDataRender16bit.Width * pixelDataRender16bit.Height, Allocator.TempJob);
        NativeArray<int> counter = new NativeArray<int>(pixelDataRender16bit.Width * pixelDataRender16bit.Height, Allocator.TempJob);

        //Attach Texture to GO
        thisImgDicomParams.m_DicomTexture = new Texture2D(thisImgDicomParams.m_DicomTextureWidth, thisImgDicomParams.m_DicomTextureHeight, texFormat, false);
        thisImgDicomParams.m_CurrentTextureColorData = new NativeArray<Color>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

        thisImgDicomParams.All_Groups_PixelDataMultiHashMap = new NativeMultiHashMap<int, PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);

        NativeHashMap<int, Vector2> grayscaleRangeHashArray = new NativeHashMap<int, Vector2>(GrayscaleRangeDict.Count, Allocator.TempJob);
        for (int i = 0; i < GrayscaleRangeDict.Count; i++)
            grayscaleRangeHashArray.TryAdd(i, new Vector2(GrayscaleRangeDict[i].x, GrayscaleRangeDict[i].y));

        var getOriginalDicomImagePixelLocationAndColorHandle = new Pixel_Manipulation_Methods.GetOriginalDicomImagePixelLocationAndColorAndPixelateImageParallelJob()
        {
            _b_UseHounsfield = b_UseHounsfield,
            _inputPixelDataWithLUT = pixelDataWithLUTNativeArray,
            _pixelDataRender16bit_Width = pixelDataRender16bit.Width,
            _thisImgDicomParam_Width = thisImgDicomParams.m_DicomTextureWidth,
            _thisGeometry_DirectionColumn = new Vector3((float)thisGeometry.DirectionColumn.X, (float)thisGeometry.DirectionColumn.Y, (float)thisGeometry.DirectionColumn.Z),
            _thisGeometry_DirectionRow = new Vector3((float)thisGeometry.DirectionRow.X, (float)thisGeometry.DirectionRow.Y, (float)thisGeometry.DirectionRow.Z),
            _thisGeometry_PixelSpacingBetweenColumns = (float)thisGeometry.PixelSpacingBetweenColumns,
            _thisGeometry_PixelSpacingBetweenRows = (float)thisGeometry.PixelSpacingBetweenRows,
            _thisDicomImage_PointTopLeft = thisImgDicomParams.PointTopLeft,
            _texturePixels = thisImgDicomParams.m_CurrentTextureColorData,
            _outputLocationData = outputLocationData,
            _rangeGroupCounter = RangeGroupCounter,
            _counter = counter,
            _grayScaleRangeHashArray = grayscaleRangeHashArray,
            _all_Groups_PixelDataHashMap = thisImgDicomParams.All_Groups_PixelDataMultiHashMap.AsParallelWriter(),
        };
        getOriginalDicomImagePixelLocationAndColorHandle.Schedule(thisImgDicomParams.m_DicomTextureWidth, 64).Complete();

        thisImgDicomParams.m_DicomTexture.SetPixelData(thisImgDicomParams.m_CurrentTextureColorData, 0, 0);
        thisImgDicomParams.m_DicomTexture.Apply();

        Debug.Log("Total pixels changed:" + Pixel_Manipulation_Methods.ReportJobCounter(counter,false));

        if (b_SafeChecks)
            CheckPixelGroupsCount(grayscaleRangeHashArray, thisImgDicomParams);

        thisImgDicomParams.PixelLocationArray = new Vector3[thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight];
        outputLocationData.CopyTo(thisImgDicomParams.PixelLocationArray);

        //Create Pixel color Quadrants
        thisImgDicomParams.All_Groups_PixelData_1DArray = new NativeArray<PixelData>(thisImgDicomParams.m_DicomTextureWidth * thisImgDicomParams.m_DicomTextureHeight, Allocator.Persistent);
        IEnumerator createPixelDataNativeArraysCoroutine = CreatePixelDataArrays(GrayscaleRangeDict, thisImgDicomParams, result => thisImgDicomParams = result);
        yield return (createPixelDataNativeArraysCoroutine);

        pixelDataWithLUTNativeArray.Dispose();
        outputLocationData.Dispose();
        RangeGroupCounter.Dispose();
        counter.Dispose();
        thisImgDicomParams.All_Groups_PixelDataMultiHashMap.Dispose();
        grayscaleRangeHashArray.Dispose();

        thisDicomParametersResult(thisImgDicomParams);
        yield return null;
    }*/