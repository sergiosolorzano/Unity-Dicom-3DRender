using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
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
using System.Threading.Tasks;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

using Rendergon.Computer_Graphics;
using Rendergon.Utilities;

namespace Rendergon.Dicom
{
    public class ExtractDicomDataMethods
    {
        public enum FrameOrientation
        {
            Axial,
            Sagittal,
            Coronal
        }

        public enum TargetCut { All, Axial, Sagittal, Coronal }

        public double m_TotalGenerateDicomDataSeconds = 0;

        //Burst Related Params TODO
        Texture2D m_Texture;
        int cumulativePixelsChanged;
        public Unity.Collections.NativeArray<UnityEngine.Color32>[] m_AxialStudyInitialPixelNativeArray;
        public Unity.Collections.NativeArray<UnityEngine.Color32>[] m_CoronalStudyInitialPixelNativeArray;
        public Unity.Collections.NativeArray<UnityEngine.Color32>[] m_SagittalStudyInitialPixelNativeArray;
        public bool b_InitialAxialPixelDataStorageCompleted;
        public bool b_InitialCoronalPixelDataStorageCompleted;
        public bool b_InitialSagittalPixelDataStorageCompleted;

        bool b_DebugMssg;

        void Start()
        {
            b_DebugMssg = true;
        }

        public IEnumerator GenerateDicomParameterData<T>(Action<DicomParameters> thisDicomParametersResult, FileInfo file, string path, TargetCut simulationCut)
        {
            var dicomFile = DicomFile.Open(string.Concat(path, "/", file.Name));

            DicomParameters thisDicomImageParams = new DicomParameters();

            var sourcegeometry = new FrameGeometry(dicomFile.Dataset);
            
            thisDicomImageParams.FrameOrientation = (FrameOrientation)Enum.Parse(typeof(FrameOrientation), sourcegeometry.Orientation.ToString(), true);

            if (simulationCut == TargetCut.All || thisDicomImageParams.FrameOrientation.ToString() == simulationCut.ToString())
            {
                Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("GenerateDicomParameterData");

                thisDicomImageParams.ImageName = file.Name;

                thisDicomImageParams.m_DicomTextureWidth = sourcegeometry.FrameSize.X;
                thisDicomImageParams.m_DicomTextureHeight = sourcegeometry.FrameSize.Y;

                if (b_DebugMssg) Debug.Log("PointTopLeft:" + (float)sourcegeometry.PointTopLeft.X + "," + (float)sourcegeometry.PointTopLeft.Y + "," + (float)sourcegeometry.PointTopLeft.Z);

                thisDicomImageParams.PointTopLeft = new Vector3((float)sourcegeometry.PointTopLeft.X, (float)sourcegeometry.PointTopLeft.Y, (float)sourcegeometry.PointTopLeft.Z);
                thisDicomImageParams.PointTopRight = new Vector3((float)sourcegeometry.PointTopRight.X, (float)sourcegeometry.PointTopRight.Y, (float)sourcegeometry.PointTopRight.Z);
                thisDicomImageParams.PointBottomLeft = new Vector3((float)sourcegeometry.PointBottomLeft.X, (float)sourcegeometry.PointBottomLeft.Y, (float)sourcegeometry.PointBottomLeft.Z);
                thisDicomImageParams.PointBottomRight = new Vector3((float)sourcegeometry.PointBottomRight.X, (float)sourcegeometry.PointBottomRight.Y, (float)sourcegeometry.PointBottomRight.Z);

                thisDicomImageParams.DirectionNormal = new Vector3((float)sourcegeometry.DirectionNormal.X, (float)sourcegeometry.DirectionNormal.Y, (float)sourcegeometry.DirectionNormal.Z);
                
                //ShowTags(dicomFile.Dataset);

                GetTagParameters(dicomFile.Dataset, ref thisDicomImageParams);

                DicomPixelData pixelData = DicomPixelData.Create(dicomFile.Dataset, false);

                byte[] rawPixelData = pixelData.GetFrame(0).Data;//Buffer

                //In Monocrhome1`or Monochrome2, Color depth is directly defined by Bits Stored
                thisDicomImageParams.BitDepth = new BitDepthSS(pixelData.BitDepth.BitsAllocated,
                                                                            pixelData.BitDepth.BitsStored,
                                                                            pixelData.BitDepth.HighBit,
                                                                            pixelData.BitDepth.IsSigned,
                                                                            pixelData.BitDepth.MaximumValue,
                                                                            pixelData.BitDepth.MinimumValue);

                m_TotalGenerateDicomDataSeconds += thisWatch.StopWatch();
            }

            thisDicomParametersResult(thisDicomImageParams);

            yield return true;
        }
            
        private double[] GetImageOrientationPatientVector(DicomDataset thisFileDataset, DicomItem tag)
        {
            int value_multiplicity = thisFileDataset.GetValueCount(tag.Tag);
            double[] thisImageOrientation = new double[value_multiplicity];

            for (int i = 0; i < value_multiplicity; i++)
            {
                thisImageOrientation[i] = thisFileDataset.GetValue<double>(tag.Tag, i);
            }

            return thisImageOrientation;
        }

        public void GetTagParameters(DicomDataset dicomFileDataSet, ref DicomParameters thisImageLocatParams)
        {
            bool b_found_AnatomicalOrientation = false;

            foreach (var tag in dicomFileDataSet)
            {
                if (tag.Tag.ToString().Equals("(0028,2002)"))//
                {
                    if (b_DebugMssg) Debug.Log("Color Space:" + dicomFileDataSet.GetString(tag.Tag));
                }
                if (tag.Tag.ToString().Equals("(0028,2000)"))//
                {
                    if (b_DebugMssg) Debug.Log("ICC Profile:" + dicomFileDataSet.GetString(tag.Tag));
                }
                if (tag.Tag.ToString().Equals("(0028,3010)"))//
                {
                    if (b_DebugMssg) Debug.Log(" Lookup Tables :" + dicomFileDataSet.GetString(tag.Tag));
                }
                if (tag.Tag.ToString().Equals("(0028,1051)"))//
                {
                    //Debug.Log("Win Width:"+ dicomFileDataSet.GetString(tag.Tag));
                }
                if (tag.Tag.ToString().Equals("(0028,1050)"))//
                {
                    //Debug.Log("Win Center"+ dicomFileDataSet.GetString(tag.Tag));
                }
                if (tag.Tag.ToString().Equals("(0028,1056)"))//
                {
                    if (b_DebugMssg) Debug.Log("VOILUT PRESENT");
                }
                if (tag.Tag.ToString().Equals("(0028,1054)"))//
                {
                    if (b_DebugMssg) Debug.Log("RESCALE TYPE ATTRIBUTE");
                }
                if (tag.Tag.ToString().Equals("(0028,3000)"))//
                {
                    if (b_DebugMssg) Debug.Log("Modality LUT Sequence");
                }
                if (tag.Tag.ToString().Equals("(2050,0010)"))//
                {
                    if (b_DebugMssg) Debug.Log("Presentation LUT Sequence");
                }
                if (tag.Tag.ToString().Equals("(0028,3002)"))//
                {
                    if (b_DebugMssg) Debug.Log("LUT Descriptor");
                }
                if (tag.Tag.ToString().Equals("(0020,0037)"))//Image Orientation: https://dicom.innolitics.com/ciods/nm-image/nm-detector/00540022/00200037
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    double[] ImgOrient = GetImageOrientationPatientVector(dicomFileDataSet, tag);

                    thisImageLocatParams.ImageOrientationRow = new double[ImgOrient.Length / 2];
                    thisImageLocatParams.ImageOrientationColumn = new double[ImgOrient.Length / 2];

                    for (int i = 0; i < ImgOrient.Length; i++)
                    {
                        if (i < 3)
                            thisImageLocatParams.ImageOrientationRow[i] = ImgOrient[i];
                        else
                            thisImageLocatParams.ImageOrientationColumn[i - 3] = ImgOrient[i];
                    }
                    if (b_DebugMssg) Debug.Log("ImgOrientRow x:" + thisImageLocatParams.ImageOrientationRow[0] + " y:" + thisImageLocatParams.ImageOrientationRow[1] + " z:" + thisImageLocatParams.ImageOrientationRow[2] +
                        " ImgOrientCol x:" + thisImageLocatParams.ImageOrientationColumn[0] + " y:" + thisImageLocatParams.ImageOrientationRow[1] + " z:" + thisImageLocatParams.ImageOrientationRow[2]);
                }

                if (tag.Tag.ToString().Equals("(0020,0032)"))//Image Position (Patient) https://dicom.innolitics.com/ciods/nm-image/nm-detector/00540022/00200032
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.ImagePositionPatient = new double[3];

                    for (int i = 0; i < 3; i++)
                        thisImageLocatParams.ImagePositionPatient[i] = dicomFileDataSet.GetValue<double>(tag.Tag, i);

                    if (b_DebugMssg) Debug.Log("Image Position x:" + thisImageLocatParams.ImagePositionPatient[0] + " y:" + thisImageLocatParams.ImagePositionPatient[1] + " z:" + thisImageLocatParams.ImagePositionPatient[2]);
                }

                if (tag.Tag.ToString().Equals("(0028,0030)"))//Pixel Spacing https://dicom.innolitics.com/ciods/nm-image/nm-image-pixel/00280030
                {
                    thisImageLocatParams.PixelSpacingRow = new double();
                    thisImageLocatParams.PixelSpacingCol = new double();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.PixelSpacingRow = dicomFileDataSet.GetValue<double>(tag.Tag, 0);
                    thisImageLocatParams.PixelSpacingCol = dicomFileDataSet.GetValue<double>(tag.Tag, 1);

                    if (b_DebugMssg) Debug.Log("Pixel Spacing Col (X):" + thisImageLocatParams.PixelSpacingCol + "Pixel Spacing Row (Y):" + thisImageLocatParams.PixelSpacingRow);
                }

                /*if (tag.Tag.ToString().Equals("(0018,1164)"))//Imager Pixel Spacing https://dicom.innolitics.com/ciods/cr-image/cr-image/00181164
                {
                    thisImageLocatParams.ImagerPixelSpacingRow = new double();
                    thisImageLocatParams.ImagerPixelSpacingCol = new double();

                    if (b_DebugEnabled) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.ImagerPixelSpacingRow = dicomFileDataSet.GetValue<double>(tag.Tag, 0);
                    thisImageLocatParams.ImagerPixelSpacingCol = dicomFileDataSet.GetValue<double>(tag.Tag, 1);

                    if (b_DebugEnabled) Debug.Log("Imager Pixel Spacing Col (X):" + thisImageLocatParams.ImagerPixelSpacingCol + "Imager Pixel Spacing Row (Y):" + thisImageLocatParams.ImagerPixelSpacingRow);
                }*/

                if (tag.Tag.ToString().Equals("(0010,2210)"))//Anatomical Orientation https://dicom.innolitics.com/ciods/mr-image/general-series/00102210
                {
                    b_found_AnatomicalOrientation = true;

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.AnatomicalOrientation = dicomFileDataSet.GetString(tag.Tag);

                    if (b_DebugMssg) Debug.Log("Anatomical Orientation: " + thisImageLocatParams.AnatomicalOrientation);
                }

                if (tag.Tag.ToString().Equals("(0028,1051)"))//Number Row per image
                {
                    thisImageLocatParams.ManufacturerWindowWidth = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.ManufacturerWindowWidth = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Window Width: " + thisImageLocatParams.ManufacturerWindowWidth);
                }

                if (tag.Tag.ToString().Equals("(0028,1050)"))//Number Row per image
                {
                    thisImageLocatParams.ManufacturerWindowCenter = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.ManufacturerWindowCenter = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Window Center: " + thisImageLocatParams.ManufacturerWindowCenter);
                }

                if (tag.Tag.ToString().Equals("(0028,0010)"))//Number Row per image
                {
                    thisImageLocatParams.ImageNumberRow = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.ImageNumberRow = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Image Number Rows: " + thisImageLocatParams.ImageNumberRow);
                }

                if (tag.Tag.ToString().Equals("(0028,0011)"))//Number Col per image
                {
                    thisImageLocatParams.ImageNumberCol = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.ImageNumberCol = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Image Number Col: " + thisImageLocatParams.ImageNumberCol);
                }

                if (tag.Tag.ToString().Equals("(0028,0100)"))//Bits Allocated http://dicomiseasy.blogspot.com/2012/08/chapter-12-pixel-data.html
                {
                    thisImageLocatParams.BitsAllocated = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.BitsAllocated = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Bits Allocated: " + thisImageLocatParams.BitsAllocated);
                }

                if (tag.Tag.ToString().Equals("(0028,0101)"))//Bits Stored http://dicomiseasy.blogspot.com/2012/08/chapter-12-pixel-data.html
                {
                    thisImageLocatParams.BitsStored = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.BitsStored = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Bits Stored: " + thisImageLocatParams.BitsStored);
                }

                if (tag.Tag.ToString().Equals("(0028,0002)"))//Samples Per Pixel http://dicomiseasy.blogspot.com/2012/08/chapter-12-pixel-data.html
                {
                    thisImageLocatParams.SamplesPerPixel = new int();

                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.SamplesPerPixel = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Samples Per Pixel: " + thisImageLocatParams.SamplesPerPixel);
                }

                if (tag.Tag.ToString().Equals("(0028,0004)"))//Photometric Interpretation https://stackoverflow.com/questions/69446381/how-to-get-color-depth-of-dicom-pixel-data-in-reliable-way
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.PhotometricInterpretation = dicomFileDataSet.GetString(tag.Tag);

                    if (b_DebugMssg) Debug.Log("Photometric Interpretation: " + thisImageLocatParams.PhotometricInterpretation);
                }

                if (tag.Tag.ToString().Equals("(0018,0050)"))//Slice Thickness
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.SliceThickness = dicomFileDataSet.GetValue<int>(tag.Tag, 0);

                    if (b_DebugMssg) Debug.Log("Slice Thickness: " + thisImageLocatParams.SliceThickness);
                }

                if (tag.Tag.ToString().Equals("(0028,1053)"))//Rescale Slope
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.RescaleSlope = dicomFileDataSet.GetValue<string>(tag.Tag, 0);

                    //Debug.Log("Rescale Slope: " + thisImageLocatParams.RescaleSlope + " and Tag.Tag " + tag.Tag.ToString() + " tag " + tag.ToString()+ " Image:" + thisImageLocatParams.ImageName);
                }

                if (tag.Tag.ToString().Equals("(0028,1052)"))//Rescale Intercept
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.RescaleIntercept = dicomFileDataSet.GetValue<string>(tag.Tag, 0);

                    //Debug.Log("Rescale Intercept: " + thisImageLocatParams.RescaleIntercept + " and Tag.Tag " + tag.Tag.ToString() + " tag " + tag.ToString() + " Image:" + thisImageLocatParams.ImageName);
                }

                if (tag.Tag.ToString().Equals("(0008,0016)"))// SOP Class UID 
                {
                    if (b_DebugMssg) Debug.Log(tag + " " + dicomFileDataSet.GetString(tag.Tag));

                    thisImageLocatParams.RescaleIntercept = dicomFileDataSet.GetValue<string>(tag.Tag, 0);

                    //Debug.Log("SOP Class UID : " + thisImageLocatParams.RescaleIntercept + " and Tag.Tag " + tag.Tag.ToString() + " tag " + tag.ToString() + " Image:" + thisImageLocatParams.ImageName);
                }
            }

            if (!b_found_AnatomicalOrientation)
                thisImageLocatParams.AnatomicalOrientation = "Not Found";
        }

        private void ShowTags(DicomDataset dicomFileDataSet)
        {
            foreach (var tag in dicomFileDataSet)
            {
                Debug.Log($"Tag  {tag} '{dicomFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");

                /*if (tag.Tag.ToString().Contains("(0028,"))//Image Position//0028
                {
                    if (tag.Tag.ToString().Contains("(0028,0030)"))
                        Debug.Log("PIXEL vertical spacing:" + tag + " " + dicomFileDataSet.GetValueOrDefault(tag.Tag, 0, "")[0] + " Horizontal Spacing:" + dicomFileDataSet.GetValueOrDefault(tag.Tag, 0, "")[1]);
                    else
                        Debug.Log($" {tag} '{dicomFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");
                }*/
            }

            //Pixel Data

            //Misc Data
            /*if (tag.Tag.ToString().Equals("(3002,0011)"))//Image Position
            {
                Debug.Log("**********************Image Plane Pixel Spacing *******************");
            }
            if (tag.Tag.ToString().Equals("(0020,0052)"))//FrameOfReferenceUID
            {
                Debug.Log("**********************FrameOfReferenceUID  *******************");
                Debug.Log($"FRAMEOFREF {tag} '{dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");
            }
            if (tag.Tag.ToString().Equals("(0008,1140)"))//ReferencedImageSequence
            {
                Debug.Log("**********************ReferencedImageSequence *******************");
            }*/


            //Test
            /*ImagePositionPatient
                PixelSpacing_x
                PixelSpacing_y
                ImageOrientationColumn
                ImageOrientationRow
            AnatomicalOrientation*/

            /*if (tag.Tag.ToString().Equals("(0010,2210)"))//Anatomical Orientation
            {
                Debug.Log("Anatomical Orientation: " + tag + " " + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, ""));

            }
            if (tag.Tag.ToString().Equals("(0020,0032)"))//Image Position
            {
                Debug.Log("Image Position (Patient): " + tag + " " + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, ""));

            }
            if (tag.Tag.ToString().Equals("(0020,0037)"))//Image Orientation
            {
                Debug.Log("Orientation :" + tag + " " + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")[0] + " Orientation 2:" + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")[1]);
                //Debug.Log("Image Orientation (Patient): " + tag + " " + tag.Tag.DictionaryEntry.ValueRepresentations + " " + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, ""));


            }
            if (tag.Tag.ToString().Contains("(0018,0088)"))//Spacing Between Slices
            {
                Debug.Log("Spacing Between Slices:" + tag + " " + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, ""));

            }
            if (tag.Tag.ToString().Contains("(0028,0030)"))//Pixel Spacing
            {
                Debug.Log("PIXEL vertical spacing:" + tag + " " + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")[0] + " Horizontal Spacing:" + dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")[1]);

            }

            /*if (tag.Tag.ToString().Equals("(0020,0032)"))//Image Position
                Debug.Log($" {tag} '{dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");

            if (tag.Tag.ToString().Equals("(0018,1100)"))//Reconstruction Diameter
                Debug.Log($" {tag} '{dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");

            if (tag.Tag.ToString().Equals("(0010,2210)"))//Anatomical Orientation Type
                Debug.Log($" {tag} '{dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");

            //How axis change with orientation: https://dicom.innolitics.com/ciods/rt-dose/image-plane/00200037

            if (tag.Tag.ToString().Equals("(0020,0037)"))//ImageOrientationPatient 
                Debug.Log($" {tag} '{dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");*/

            //Debug.Log($" {tag} '{dicomeFileDataSet.GetValueOrDefault(tag.Tag, 0, "")}'");
        }
    }
  
    [Serializable]
    public class BitDepthSS
    {
        public int BitsAllocated;
        public int BitsStored;
        public int HighBit;
        public bool IsSigned;
        public int MaximumValue;
        public int MinimumValue;

        public BitDepthSS(int a, int b, int c, bool d, int e, int f)
        {
            BitsAllocated = a; BitsStored = b; HighBit = c; IsSigned = d; MaximumValue = e; MinimumValue = f;
        }
    }

    [Serializable]
    public class DicomParameters 
    {
        public string ImageName;
        public ExtractDicomDataMethods.FrameOrientation FrameOrientation;
        [System.NonSerialized] public double[] ImageOrientationRow;
        [System.NonSerialized] public double[] ImageOrientationColumn;
        [System.NonSerialized] public double[] ImagePositionPatient;
        public int ManufacturerWindowWidth;
        public int ManufacturerWindowCenter;
        public double PixelSpacingRow;//y
        public double PixelSpacingCol;//x
        public int m_DicomTextureWidth;
        public int m_DicomTextureHeight;
        [System.NonSerialized] public string AnatomicalOrientation;
        public Vector3 DirectionNormal;
        public string RescaleSlope;
        public string RescaleIntercept;
        [System.NonSerialized] public Vector3 PointTopLeft;
        [System.NonSerialized] public Vector3 PointTopRight;
        [System.NonSerialized] public Vector3 PointBottomLeft;
        [System.NonSerialized] public Vector3 PointBottomRight;
        [System.NonSerialized] public int ImageNumberRow;
        [System.NonSerialized] public int ImageNumberCol;
        [System.NonSerialized] public int BitsAllocated;
        [System.NonSerialized] public int BitsStored;
        [System.NonSerialized] public int SamplesPerPixel;
        [System.NonSerialized] public string PhotometricInterpretation;
        [System.NonSerialized] public BitDepthSS BitDepth;
        [System.NonSerialized] public Vector3[] PixelLocationArray;
        [System.NonSerialized] public double[] Distance;
        public double SliceThickness;
        [System.NonSerialized] public GameObject ImagePlaneGO;
        public Vector3 ImagePlanePosition;
        public Vector3 ParentCenterPosition;
        [System.NonSerialized] public Texture2D m_DicomTexture;
        [System.NonSerialized] public int[] All_Groups_ArrayStartIndex;
        [System.NonSerialized] public int[] m_Raw16bitDataArray;
        [System.NonSerialized] public NativeArray<Color> m_CurrentTextureColorData;
        [System.NonSerialized] public NativeArray<Color> m_CurrentTextureGrayscaleData;
        [System.NonSerialized] public NativeArray<ColorARGB32> m_CurrentTextureColor32Data;
        [System.NonSerialized] public NativeArray<PixelData> All_Groups_PixelData_1DArray;
        [System.NonSerialized] public NativeMultiHashMap<int, PixelData> All_Groups_PixelDataMultiHashMap;
    }

    public struct PixelData
    {
        public Color pixelColor;
        public ColorARGB32 pixelColor32;
        //public UnityEngine.Color32 pixelColor32;
        public int pixelIndexInTexture;

        public PixelData(Color thisColor, ColorARGB32 thisColor32, int thisPixelIndexInTexture)
        //public PixelData(Color thisColor, UnityEngine.Color32 thisColor32, int thisPixelIndexInTexture)
        {
            pixelColor = thisColor;
            pixelColor32 = thisColor32;
            pixelIndexInTexture = thisPixelIndexInTexture;
        }
    }

    public struct ColorARGB32
    {
        public byte a;
        public byte r;
        public byte g;
        public byte b;

        public ColorARGB32(byte _a, byte _r, byte _g, byte _b)
        {
            a = _a;
            r = _r;
            g = _g;
            b = _b;
        }
    }

    public struct SampleGrayscaleData
    {
        Vector3 position;
        double grayscaleValue;
        float size;
    }
}