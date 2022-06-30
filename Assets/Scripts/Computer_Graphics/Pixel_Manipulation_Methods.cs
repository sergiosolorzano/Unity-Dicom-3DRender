using Rendergon.Dicom;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Rendergon.Computer_Graphics
{
    public class Pixel_Manipulation_Methods
    {
        [BurstCompile]
        public struct ApplyHounsfieldAlphaToColorParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_AlphaOn;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;
            [NativeDisableParallelForRestriction] public NativeArray<Color> _textureData;
            [ReadOnly]public NativeArray<PixelData> _all_Groups_PixelData_1DArray;
            [ReadOnly] public int _pixelData_1DArray_StartIndex;
            public void Execute(int elementPositionWithinRangeSubset)
            {
                int elementPosition_1DArray = _pixelData_1DArray_StartIndex + elementPositionWithinRangeSubset;

                if (_b_AlphaOn)
                    _textureData[_all_Groups_PixelData_1DArray[elementPosition_1DArray].pixelIndexInTexture] = _all_Groups_PixelData_1DArray[elementPosition_1DArray].pixelColor;
                else
                    _textureData[_all_Groups_PixelData_1DArray[_pixelData_1DArray_StartIndex + elementPositionWithinRangeSubset].pixelIndexInTexture] = new Color(0, 0, 0, 0);

                _counter[_wrokerThreadIndex]++;
            }
        }

        [BurstCompile]
        public struct ApplyHounsfieldAlphaToColor32ParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_AlphaOn;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;
            [NativeDisableParallelForRestriction] public NativeArray<ColorARGB32> _textureData;
            [ReadOnly] public NativeArray<PixelData> _all_Groups_PixelData_1DArray;
            [ReadOnly] public int _pixelData_1DArray_StartIndex;
            public void Execute(int elementPositionWithinRangeSubset)
            {
                int elementPosition_1DArray = _pixelData_1DArray_StartIndex + elementPositionWithinRangeSubset;

                if (_b_AlphaOn)
                    _textureData[_all_Groups_PixelData_1DArray[elementPosition_1DArray].pixelIndexInTexture] = _all_Groups_PixelData_1DArray[elementPosition_1DArray].pixelColor32;
                else
                    _textureData[_all_Groups_PixelData_1DArray[_pixelData_1DArray_StartIndex + elementPositionWithinRangeSubset].pixelIndexInTexture] = new ColorARGB32(0, 0, 0, 0);

                _counter[_wrokerThreadIndex]++;
            }
        }

        [BurstCompile]
        public struct GetOriginal_DicomImageWindowed_ColorParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_UseHounsfield;
            [ReadOnly] public NativeArray<short> _inputPixelDataWithLUT;//input texture data for original images
            public int _pixelDataRender16bit_Width;
            public int _thisImgDicomParam_Width;
            public Vector3 _thisGeometry_DirectionColumn;
            public Vector3 _thisGeometry_DirectionRow;
            public float _thisGeometry_PixelSpacingBetweenColumns;
            public float _thisGeometry_PixelSpacingBetweenRows;
            public Vector3 _thisDicomImage_PointTopLeft;
            [NativeDisableParallelForRestriction] public NativeArray<Color> _textureSingleChannelPixels;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> _outputLocationData;
            [NativeDisableParallelForRestriction] public NativeArray<int> _rangeGroupCounter;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            [ReadOnly] public NativeHashMap<int, Vector2> _grayScaleRangeHashArray;
            //public NativeHashMap<int, PixelData>.ParallelWriter _fullPixelHashMap;
            public NativeMultiHashMap<int, PixelData>.ParallelWriter _all_Groups_PixelDataHashMap;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    /*short value = _inputPixelDataWithLUT[c + y * _pixelDataRender16bit_Width];// something between –32,768 and 32,767
                    float value1 = (float)value;

                    float min = 0;
                    float max = 1;
                    float WindowCenterMin05 =40-0.5f;
                    float WindowWidthMin1 = 400-1;
                    float OutputRange = 1;
                    float MinimumOutputValue = 0;
                    float valFloat = (float)Math.Min(max,
                            Math.Max(min,
                            (((value1 - WindowCenterMin05) / WindowWidthMin1) + 0.5) * OutputRange + MinimumOutputValue
                            ));
                    
                    if (valFloat == 0)
                        _textureSingleChannelPixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(0, 0, 0, 0);
                    else
                        _textureSingleChannelPixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(valFloat, valFloat, valFloat, 1);*/

                    //Get Pixel Location
                    Vector3 DirColPixelSpace = new Vector3(_thisGeometry_DirectionColumn.x * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.y * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.z * _thisGeometry_PixelSpacingBetweenColumns * y);
                    Vector3 DirRowPixelSpace = new Vector3(_thisGeometry_DirectionRow.x * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.y * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.z * _thisGeometry_PixelSpacingBetweenRows * c);
                    //get pixel location value
                    _outputLocationData[c + y * _thisImgDicomParam_Width] = new Vector3(_thisDicomImage_PointTopLeft.x + DirColPixelSpace.x + DirRowPixelSpace.x,
                                                                                        _thisDicomImage_PointTopLeft.y + DirColPixelSpace.y + DirRowPixelSpace.y,
                                                                                        _thisDicomImage_PointTopLeft.z + DirColPixelSpace.z + DirRowPixelSpace.z);

                    _counter[_wrokerThreadIndex]++;
                }
            }
        }

        public struct GetOriginal_DicomImageWindowed_Color32ParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_UseHounsfield;
            [ReadOnly] public NativeArray<short> _inputPixelDataWithLUT;//input texture data for original images
            public int _pixelDataRender16bit_Width;
            public int _thisImgDicomParam_Width;
            public Vector3 _thisGeometry_DirectionColumn;
            public Vector3 _thisGeometry_DirectionRow;
            public float _thisGeometry_PixelSpacingBetweenColumns;
            public float _thisGeometry_PixelSpacingBetweenRows;
            public Vector3 _thisDicomImage_PointTopLeft;
            [NativeDisableParallelForRestriction] public NativeArray<ColorARGB32> _textureSingleChannelPixels;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> _outputLocationData;
            [NativeDisableParallelForRestriction] public NativeArray<int> _rangeGroupCounter;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            [ReadOnly] public NativeHashMap<int, Vector2Int> _grayScaleRangeHashArray;
            //public NativeHashMap<int, PixelData>.ParallelWriter _fullPixelHashMap;
            public NativeMultiHashMap<int, PixelData>.ParallelWriter _all_Groups_PixelDataHashMap;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    /*short value = _inputPixelDataWithLUT[c + y * _pixelDataRender16bit_Width];// something between –32,768 and 32,767
                    float value1 = (float)value;

                    float min = 0;
                    float max = 255;//32768
                    float WindowCenterMin05 = 40 - 0.5f;
                    float WindowWidthMin1 = 400 - 1;
                    float OutputRange = 255;
                    float MinimumOutputValue = 0;
                    float valFloat = (float)Math.Min(max,
                            Math.Max(min,
                            (((value1 - WindowCenterMin05) / WindowWidthMin1) + 0.5) * OutputRange + MinimumOutputValue
                            ));

                    if (valFloat == 0)
                        _textureSingleChannelPixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32((byte)valFloat, (byte)valFloat, (byte)valFloat, 0);
                    else
                        _textureSingleChannelPixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32((byte)valFloat, (byte)valFloat, (byte)valFloat, (byte)255);*/

                    //Get Pixel Location
                    Vector3 DirColPixelSpace = new Vector3(_thisGeometry_DirectionColumn.x * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.y * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.z * _thisGeometry_PixelSpacingBetweenColumns * y);
                    Vector3 DirRowPixelSpace = new Vector3(_thisGeometry_DirectionRow.x * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.y * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.z * _thisGeometry_PixelSpacingBetweenRows * c);
                    //get pixel location value
                    _outputLocationData[c + y * _thisImgDicomParam_Width] = new Vector3(_thisDicomImage_PointTopLeft.x + DirColPixelSpace.x + DirRowPixelSpace.x,
                                                                                        _thisDicomImage_PointTopLeft.y + DirColPixelSpace.y + DirRowPixelSpace.y,
                                                                                        _thisDicomImage_PointTopLeft.z + DirColPixelSpace.z + DirRowPixelSpace.z);

                    _counter[_wrokerThreadIndex]++;
                }
            }
        }

        public struct GetOriginal_DicomImageWindowed_LocationParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            public int _thisImgDicomParam_Width;
            public Vector3 _thisGeometry_DirectionColumn;
            public Vector3 _thisGeometry_DirectionRow;
            public float _thisGeometry_PixelSpacingBetweenColumns;
            public float _thisGeometry_PixelSpacingBetweenRows;
            public Vector3 _thisDicomImage_PointTopLeft;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> _outputLocationData;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    //Get Pixel Location
                    Vector3 DirColPixelSpace = new Vector3(_thisGeometry_DirectionColumn.x * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.y * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.z * _thisGeometry_PixelSpacingBetweenColumns * y);
                    Vector3 DirRowPixelSpace = new Vector3(_thisGeometry_DirectionRow.x * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.y * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.z * _thisGeometry_PixelSpacingBetweenRows * c);
                    //get pixel location value
                    _outputLocationData[c + y * _thisImgDicomParam_Width] = new Vector3(_thisDicomImage_PointTopLeft.x + DirColPixelSpace.x + DirRowPixelSpace.x,
                                                                                        _thisDicomImage_PointTopLeft.y + DirColPixelSpace.y + DirRowPixelSpace.y,
                                                                                        _thisDicomImage_PointTopLeft.z + DirColPixelSpace.z + DirRowPixelSpace.z);

                    _counter[_wrokerThreadIndex]++;
                }
            }
        }

        [BurstCompile]
        public struct GetOriginal_DicomImagePixelColorAndLocation_AndHounsfieldRangesParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_UseHounsfield;
            [ReadOnly] public NativeArray<int> _inputPixelDataWithLUT;//input texture data for original images
            public int _pixelDataRender16bit_Width;
            public int _thisImgDicomParam_Width;
            public Vector3 _thisGeometry_DirectionColumn;
            public Vector3 _thisGeometry_DirectionRow;
            public float _thisGeometry_PixelSpacingBetweenColumns;
            public float _thisGeometry_PixelSpacingBetweenRows;
            public Vector3 _thisDicomImage_PointTopLeft;
            [NativeDisableParallelForRestriction] public NativeArray<Color> _texturePixels;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> _outputLocationData;
            [NativeDisableParallelForRestriction] public NativeArray<int> _rangeGroupCounter;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            [ReadOnly] public NativeHashMap<int, Vector2> _grayScaleRangeHashArray;
            //public NativeHashMap<int, PixelData>.ParallelWriter _fullPixelHashMap;
            public NativeMultiHashMap<int, PixelData>.ParallelWriter _all_Groups_PixelDataHashMap;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    float grayscale = 0f;
                    int value = _inputPixelDataWithLUT[c + y * _pixelDataRender16bit_Width];

                    grayscale = value / 255f;

                    if (value == 0)
                    {
                        if (_b_UseHounsfield)
                            _texturePixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(1 * grayscale, 1 * grayscale, 1 * grayscale, 1);//ensure a black pixel is not alpha=0
                        else
                            _texturePixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(1 * grayscale, 1 * grayscale, 1 * grayscale, 0);//black pixel alpha=0 if not running Hounsfield
                    }
                    else
                        _texturePixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(1 * grayscale, 1 * grayscale, 1 * grayscale, 1);

                    //Get Pixel Location
                    Vector3 DirColPixelSpace = new Vector3(_thisGeometry_DirectionColumn.x * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.y * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.z * _thisGeometry_PixelSpacingBetweenColumns * y);
                    Vector3 DirRowPixelSpace = new Vector3(_thisGeometry_DirectionRow.x * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.y * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.z * _thisGeometry_PixelSpacingBetweenRows * c);
                    //get pixel location value
                    _outputLocationData[c + y * _thisImgDicomParam_Width] = new Vector3(_thisDicomImage_PointTopLeft.x + DirColPixelSpace.x + DirRowPixelSpace.x,
                                                                                        _thisDicomImage_PointTopLeft.y + DirColPixelSpace.y + DirRowPixelSpace.y,
                                                                                        _thisDicomImage_PointTopLeft.z + DirColPixelSpace.z + DirRowPixelSpace.z);

                    _counter[_wrokerThreadIndex]++;

                    //Create Hounsfield Group ranges by color
                    if (_b_UseHounsfield)
                    {
                        for (int rangeGroup = 0; rangeGroup < _grayScaleRangeHashArray.Count(); rangeGroup++)
                        {
                            if ((_texturePixels[c + y * _thisImgDicomParam_Width].r >= _grayScaleRangeHashArray[rangeGroup].x
                                && _texturePixels[c + y * _thisImgDicomParam_Width].r < _grayScaleRangeHashArray[rangeGroup].y))
                            {
                                PixelData thisPixel = new PixelData(_texturePixels[c + y * _thisImgDicomParam_Width], new ColorARGB32 { }, c + y * _thisImgDicomParam_Width);
                                _all_Groups_PixelDataHashMap.Add(rangeGroup, thisPixel);
                                _rangeGroupCounter[_wrokerThreadIndex]++;
                                break;
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public struct GetOriginal_DicomImagePixelColor32AndLocation_AndHounsfieldRangesParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_UseHounsfield;
            [ReadOnly] public NativeArray<int> _inputPixelDataWithLUT;//input texture data for original images
            public int _pixelDataRender16bit_Width;
            public int _thisImgDicomParam_Width;
            public Vector3 _thisGeometry_DirectionColumn;
            public Vector3 _thisGeometry_DirectionRow;
            public float _thisGeometry_PixelSpacingBetweenColumns;
            public float _thisGeometry_PixelSpacingBetweenRows;
            public Vector3 _thisDicomImage_PointTopLeft;
            [NativeDisableParallelForRestriction] public NativeArray<ColorARGB32> _texturePixels;
            [NativeDisableParallelForRestriction] public NativeArray<Vector3> _outputLocationData;
            [NativeDisableParallelForRestriction] public NativeArray<int> _rangeGroupCounter;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            [ReadOnly]public NativeHashMap<int, Vector2Int> _grayScaleRangeHashArray;
            public NativeMultiHashMap<int, PixelData>.ParallelWriter _all_Groups_PixelDataHashMap;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    byte grayscale = 0;
                    int value = _inputPixelDataWithLUT[c + y * _pixelDataRender16bit_Width];

                    grayscale = (byte)value;

                    if (value == 0)
                    {
                        if (_b_UseHounsfield)
                            _texturePixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32(grayscale, grayscale, grayscale, 255);//ensure a black pixel is not alpha=0
                        else
                            _texturePixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32(grayscale, grayscale, grayscale,0);//black pixel alpha=0 if not running Hounsfield
                    }
                    else
                        _texturePixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32(grayscale, grayscale, grayscale, 255);

                    //Get Pixel Location
                    Vector3 DirColPixelSpace = new Vector3(_thisGeometry_DirectionColumn.x * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.y * _thisGeometry_PixelSpacingBetweenColumns * y,
                                                            _thisGeometry_DirectionColumn.z * _thisGeometry_PixelSpacingBetweenColumns * y);
                    Vector3 DirRowPixelSpace = new Vector3(_thisGeometry_DirectionRow.x * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.y * _thisGeometry_PixelSpacingBetweenRows * c,
                                                            _thisGeometry_DirectionRow.z * _thisGeometry_PixelSpacingBetweenRows * c);
                    //get pixel location value
                    _outputLocationData[c + y * _thisImgDicomParam_Width] = new Vector3(_thisDicomImage_PointTopLeft.x + DirColPixelSpace.x + DirRowPixelSpace.x,
                                                                                        _thisDicomImage_PointTopLeft.y + DirColPixelSpace.y + DirRowPixelSpace.y,
                                                                                        _thisDicomImage_PointTopLeft.z + DirColPixelSpace.z + DirRowPixelSpace.z);

                    _counter[_wrokerThreadIndex]++;

                    //Create Hounsfield Group ranges by color
                    if (_b_UseHounsfield)
                    {
                        for (int rangeGroup = 0; rangeGroup < _grayScaleRangeHashArray.Count(); rangeGroup++)
                        {
                            int pix_r = _texturePixels[c + y * _thisImgDicomParam_Width].r;
                            
                            if (((pix_r) >= _grayScaleRangeHashArray[rangeGroup].x
                                && (pix_r) < _grayScaleRangeHashArray[rangeGroup].y))
                            {
                                PixelData thisPixel = new PixelData(new Color { }, _texturePixels[c + y * _thisImgDicomParam_Width], c + y * _thisImgDicomParam_Width);
                                _all_Groups_PixelDataHashMap.Add(rangeGroup, thisPixel);
                                _rangeGroupCounter[_wrokerThreadIndex]++;
                                break;
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public struct GetStored_DicomImagePixelColorAndLocation_AndHounsfieldRangesParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_UseHounsfield;
            [ReadOnly] public NativeArray<UnityEngine.Color> _inputTextureData;//input texture data for stored images
            public int _thisImgDicomParam_Width;
            [NativeDisableParallelForRestriction] public NativeArray<Color> _texturePixels;
            [NativeDisableParallelForRestriction] public NativeArray<int> _rangeGroupCounter;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            [ReadOnly] public NativeHashMap<int, Vector2> _grayScaleRangeHashArray;
            public NativeMultiHashMap<int, PixelData>.ParallelWriter _all_Groups_PixelDataHashMap;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    var _grayscale = (float)(_inputTextureData[c + y * _thisImgDicomParam_Width][0]);

                    if (_grayscale == 0)
                    {
                        if (_b_UseHounsfield)
                            _texturePixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(1 * _grayscale, 1 * _grayscale, 1 * _grayscale, 1);//ensure a black pixel is not alpha=0
                        else
                            _texturePixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(1 * _grayscale, 1 * _grayscale, 1 * _grayscale, 0);//ensure a black pixel is not alpha=0
                    }
                    else
                        _texturePixels[c + y * _thisImgDicomParam_Width] = new UnityEngine.Color(1 * _grayscale, 1 * _grayscale, 1 * _grayscale, 1);//ensure a black pixel is not alpha=0
                    

                    _counter[_wrokerThreadIndex]++;

                    //Create Hounsfield Group ranges by color
                    if (_b_UseHounsfield)
                    {
                        for (int rangeGroup = 0; rangeGroup < _grayScaleRangeHashArray.Count(); rangeGroup++)
                        {
                            if (_texturePixels[c + y * _thisImgDicomParam_Width].r >= _grayScaleRangeHashArray[rangeGroup].x
                                && _texturePixels[c + y * _thisImgDicomParam_Width].r < _grayScaleRangeHashArray[rangeGroup].y)
                            {
                                PixelData thisPixel = new PixelData(_texturePixels[c + y * _thisImgDicomParam_Width], new ColorARGB32 { },c + y * _thisImgDicomParam_Width);
                                _all_Groups_PixelDataHashMap.Add(rangeGroup, thisPixel);
                                _rangeGroupCounter[_wrokerThreadIndex]++;
                                break;
                            }
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public struct GetStored_DicomImagePixelColor32AndLocation_AndHounsfieldRangesParallelJob : IJobParallelFor
        {
            [NativeSetThreadIndex] public int _wrokerThreadIndex;
            [ReadOnly] public bool _b_UseHounsfield;
            [ReadOnly] public NativeArray<ColorARGB32> _inputTextureData;//input texture data for stored images
            public int _thisImgDicomParam_Width;
            [NativeDisableParallelForRestriction] public NativeArray<ColorARGB32> _texturePixels;
            [NativeDisableParallelForRestriction] public NativeArray<int> _rangeGroupCounter;
            [NativeDisableParallelForRestriction] public NativeArray<int> _counter;

            [ReadOnly] public NativeHashMap<int, Vector2Int> _grayScaleRangeHashArray;
            public NativeMultiHashMap<int, PixelData>.ParallelWriter _all_Groups_PixelDataHashMap;

            public void Execute(int y)
            {
                for (int c = 0; c < _thisImgDicomParam_Width; c++)
                {
                    byte _grayscale = (_inputTextureData[c + y * _thisImgDicomParam_Width].r);

                    if (!_b_UseHounsfield && _grayscale == 0)
                        //_texturePixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32(_inputTextureData[c + y * _thisImgDicomParam_Width].a, _inputTextureData[c + y * _thisImgDicomParam_Width].r, _inputTextureData[c + y * _thisImgDicomParam_Width].g, _inputTextureData[c + y * _thisImgDicomParam_Width].b);//ensure a black pixel is transparent
                        _texturePixels[c + y * _thisImgDicomParam_Width] = new ColorARGB32(0, _inputTextureData[c + y * _thisImgDicomParam_Width].r, _inputTextureData[c + y * _thisImgDicomParam_Width].g, _inputTextureData[c + y * _thisImgDicomParam_Width].b);//ensure a black pixel is transparent

                    else
                        _texturePixels[c + y * _thisImgDicomParam_Width] = _inputTextureData[c + y * _thisImgDicomParam_Width];// new UnityEngine.Color32(_inputTextureData[c + y * _thisImgDicomParam_Width][0], _inputTextureData[c + y * _thisImgDicomParam_Width][1], _inputTextureData[c + y * _thisImgDicomParam_Width][2], _inputTextureData[c + y * _thisImgDicomParam_Width][3]);//ensure a black pixel is transparent

                    _counter[_wrokerThreadIndex]++;

                    //Create Hounsfield Group ranges by color
                    if (_b_UseHounsfield)
                    {
                        for (int rangeGroup = 0; rangeGroup < _grayScaleRangeHashArray.Count(); rangeGroup++)
                        {
                            int pix_r = _inputTextureData[c + y * _thisImgDicomParam_Width].r;

                            if ((pix_r) >= _grayScaleRangeHashArray[rangeGroup].x
                                && (pix_r) < _grayScaleRangeHashArray[rangeGroup].y)
                            {
                                PixelData thisPixel = new PixelData(new Color { }, _texturePixels[c + y * _thisImgDicomParam_Width], c + y * _thisImgDicomParam_Width);
                                _all_Groups_PixelDataHashMap.Add(rangeGroup, thisPixel);
                                _rangeGroupCounter[_wrokerThreadIndex]++;
                                break;
                            }
                        }
                    }
                }
            }
        }

        public static int ReportJobCounter(NativeArray<int> counters, bool b_ShowJobsPerThread)
        {
            var total = 0;

            for (var i = 0; i < JobsUtility.MaxJobThreadCount; i++)
            {
                total += counters[i];

                if (counters[i] > 0)
                {
                    //Debug.Log($"JobThread=[{i}] Count=[{counters[i]}]");
                }
            }

            return total;
        }

        
    }
}

