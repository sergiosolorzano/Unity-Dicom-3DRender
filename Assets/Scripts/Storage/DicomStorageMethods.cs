using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using Newtonsoft.Json;
using System.Text;
using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

using Rendergon.Managers;
using Rendergon.Dicom;
using Rendergon.Utilities;
using Unity.Mathematics;
using Unity.Collections;
using System.IO.Compression;
using UnityEngine.Experimental.Rendering;
using FellowOakDicom.IO;

namespace Rendergon.Storage
{ 
    public class DicomStorageMethods : MonoBehaviour
    {
        public static string m_PNG_SavedExtension= "png";
        public static string m_EXR_SavedExtension = "exr";
        public static string m_JsonSavedExtension = "db";

        public static double m_DeserializeSimulationSeconds=0;
        public static double m_ReadStoredSimulationSeconds = 0;
        public static double m_SaveImageSimulationSeconds = 0;

        public static bool b_DebugMssg;

        private void Start()
        {
            m_DeserializeSimulationSeconds = 0;
            m_ReadStoredSimulationSeconds = 0;
            m_SaveImageSimulationSeconds = 0;
            
            b_DebugMssg = false;
        }

        static string GetImageExtension(DicomManager.SerializedTextureFormat selfFormat)
        {
            switch (selfFormat)
            {
                case DicomManager.SerializedTextureFormat.RGBAFloat:
                    return m_EXR_SavedExtension;
                case DicomManager.SerializedTextureFormat.RGBA32:
                    return m_PNG_SavedExtension;
                default:
                    return m_PNG_SavedExtension;
            }

        }

        public static void DeserializeStoredDicomParameter(ref List<DicomParameters> serializedDicomSingleStudyList, ExtractDicomDataMethods.TargetCut SimulationCut)
        {
            Performance_Metrics.Watch watch = new Performance_Metrics.Watch("DeserializeStoredDicomParameter");

            string path = Application.persistentDataPath + "/SerializedData/" + SimulationCut.ToString() + "/";

            var directoryInfoForDicomImagesFolder = new DirectoryInfo(path);

            try
            {
                foreach (var file in directoryInfoForDicomImagesFolder.GetFiles(string.Concat("*", m_JsonSavedExtension), SearchOption.AllDirectories))
                {
                    //if (!Path.GetExtension(file.Name).Equals(m_ImageSavedExtension) && file.Name.Contains(SimulationCut.ToString()))
                    if (file.Name.Contains(SimulationCut.ToString()))
                    {
                        string fileNameAndPath = path + file.Name;

                        if (File.Exists(fileNameAndPath))
                        {
                            using var fileStream = new FileStream(fileNameAndPath, FileMode.Open, FileAccess.Read);
                            using var streamReader = new StreamReader(fileStream, Encoding.UTF8);
                            string temp = String.Empty;
                            string dicomArrayString = String.Empty;

                            while ((temp = streamReader.ReadLine()) != null)
                            {
                                dicomArrayString = temp;
                            }

                            DicomParameters dicomParams = JsonConvert.DeserializeObject<DicomParameters>(dicomArrayString);
                            serializedDicomSingleStudyList.Add(dicomParams);
                            if (b_DebugMssg) Debug.Log("I've Deserialized image " + dicomParams.ImageName);
                            System.Diagnostics.Debug.WriteLine("I've Deserialized image " + dicomParams.ImageName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Exception Deserializing Image Data:" + e.Message);
            }

            m_DeserializeSimulationSeconds += watch.StopWatch();
        }

        public static IEnumerator ReadStoredImageCustomAlgorithm(DicomParameters thisSerilizedDicomParameters, DicomManager.SerializedTextureFormat self_serializableTextureFormat, Action<Texture2D> callback)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("ReadStoredImageCustomAlgorithm");

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", GetImageExtension(self_serializableTextureFormat)));

            thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, CreateImageMethods_CPU.ConvertSelfTextureFormatToUnity(self_serializableTextureFormat), false);

            try
            {
                thisSerilizedDicomParameters.m_DicomTexture.ReadUncompressed(fileNamePath);
                //var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();//To test the pixels we get from reading
                if(b_DebugMssg) Debug.Log("Image Loaded:" + thisSerilizedDicomParameters.m_DicomTexture.format);

            }
            catch (Exception e)
            {
                Debug.Log("Stored Image " + thisSerilizedDicomParameters.ImageName + " failed to load. Exception:" + e.Message);
                yield break;
            }

            var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
            rend.material.SetColor("_BaseColor", Color.white);

            if (b_DebugMssg) Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);


            m_ReadStoredSimulationSeconds += thisWatch.StopWatch();

            callback(thisSerilizedDicomParameters.m_DicomTexture);
            yield return thisSerilizedDicomParameters.m_DicomTexture;
        }

        public static IEnumerator ReadStoredUShortImageCustomAlgorithm(DicomParameters thisSerilizedDicomParameters, DicomManager.SerializedTextureFormat self_serializableTextureFormat, Action<Texture2D> callback)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("ReadStoredImageCustomAlgorithm");

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", GetImageExtension(self_serializableTextureFormat)));

            thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, TextureFormat.R16, false);

            try
            {
                thisSerilizedDicomParameters.m_DicomTexture.ReadUncompressed(fileNamePath);
                //var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();//To test the pixels we get from reading
                if (b_DebugMssg) Debug.Log("Image Loaded:" + thisSerilizedDicomParameters.m_DicomTexture.format);

            }
            catch (Exception e)
            {
                Debug.Log("Stored Image " + thisSerilizedDicomParameters.ImageName + " failed to load. Exception:" + e.Message);
                yield break;
            }

            var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
            rend.material.SetColor("_BaseColor", Color.white);

            if (b_DebugMssg) Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);


            m_ReadStoredSimulationSeconds += thisWatch.StopWatch();

            callback(thisSerilizedDicomParameters.m_DicomTexture);
            yield return thisSerilizedDicomParameters.m_DicomTexture;
        }

        public static IEnumerator ReadStoredBufferImageCustomAlgorithm(double[] inputData,DicomParameters thisSerilizedDicomParameters, DicomManager.SerializedTextureFormat self_serializableTextureFormat, Action<Texture2D> callback)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("ReadStoredImageCustomAlgorithm");

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", GetImageExtension(self_serializableTextureFormat)));

            thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, TextureFormat.R16, false);

            //var shortData = ByteConverter.ToArray<short>(data, bitDepth.BitsAllocated);

            double[] yourData = inputData;// wherever you get this from;
            byte[] byteData = new byte[sizeof(double) * yourData.Length];

            // On memory level copy the bytes from yourData into byteData
            Buffer.BlockCopy(yourData, 0, byteData, 0, byteData.Length);

            thisSerilizedDicomParameters.m_DicomTexture.LoadRawTextureData(byteData);
            thisSerilizedDicomParameters.m_DicomTexture.Apply();

            var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();//To test the pixels we get from reading
            var map2556 = thisSerilizedDicomParameters.m_DicomTexture.GetPixels();
            var map2557 = thisSerilizedDicomParameters.m_DicomTexture.GetPixels32();
            if (b_DebugMssg) Debug.Log("Image Loaded:" + thisSerilizedDicomParameters.m_DicomTexture.format);

            /*var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
            rend.material.SetColor("_BaseColor", Color.white);*/

            if (b_DebugMssg) Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);


            m_ReadStoredSimulationSeconds += thisWatch.StopWatch();

            callback(thisSerilizedDicomParameters.m_DicomTexture);
            yield return thisSerilizedDicomParameters.m_DicomTexture;
        }

        public static IEnumerator ReadStoredImageFast(DicomParameters thisDicomParameters, DicomManager.SerializedTextureFormat self_serializableTextureFormat, Action<Texture2D> callback)
        {
            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName, ".", GetImageExtension(self_serializableTextureFormat)));

            thisDicomParameters.m_DicomTexture = new Texture2D(thisDicomParameters.m_DicomTextureWidth, thisDicomParameters.m_DicomTextureHeight, CreateImageMethods_CPU.ConvertSelfTextureFormatToUnity(self_serializableTextureFormat), false);

            Texture2D tempTex = new Texture2D(thisDicomParameters.m_DicomTextureWidth, thisDicomParameters.m_DicomTextureHeight, CreateImageMethods_CPU.ConvertSelfTextureFormatToUnity(self_serializableTextureFormat), false);

            byte[] imgBytes = File.ReadAllBytes(fileNamePath);
            thisDicomParameters.m_DicomTexture.LoadImage(imgBytes);
            var rend = thisDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.mainTexture = thisDicomParameters.m_DicomTexture;


            Debug.Log("Texture Format" + thisDicomParameters.m_DicomTexture.format);
            Debug.Log("I've created Dicom Texture " + fileNamePath);
            System.Diagnostics.Debug.WriteLine("I've created Dicom Texture " + fileNamePath);

            callback(thisDicomParameters.m_DicomTexture);
            yield return thisDicomParameters.m_DicomTexture;
        }

        public static void SaveImageLocally(DicomParameters thisDicomParameters, bool b_overwrite, string orientation, DicomManager.SerializedTextureFormat selfFormat)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("SaveImageLocallyCustomAlgorithm");

            Texture2D m_Texture = (Texture2D)thisDicomParameters.ImagePlaneGO.GetComponent<Renderer>().material.GetTexture("_BaseMap");

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";

            if (!Directory.Exists(thisDir))
                Directory.CreateDirectory(thisDir);

            var fileNamePath = Path.Combine(thisDir, string.Concat(orientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName + "." + GetImageExtension(selfFormat)));

            if (b_overwrite || !File.Exists(fileNamePath))
            {
                if(selfFormat== DicomManager.SerializedTextureFormat.RGBAFloat)
                {
                    try
                    {
                        m_Texture.SaveUncompressed(fileNamePath, Texture2DExtension.DataFormat.ARGBFloat);
                        if (b_DebugMssg) Debug.Log("Saved file:" + fileNamePath);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Failed to Save Custom Format Image:" + fileNamePath + e.Message);
                        throw;
                    }
                }
                
                else if (selfFormat == DicomManager.SerializedTextureFormat.RGBA32)
                {
                    try
                    {
                        byte[] imageBytes = m_Texture.EncodeToPNG();
                        File.WriteAllBytes(fileNamePath, imageBytes);
                        if (b_DebugMssg) Debug.Log("Saved file:" + fileNamePath + " bytes:" + imageBytes.Length);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Failed to Save PNG Image:" + fileNamePath + e.Message);
                        throw;
                    }
                }
            }

            m_SaveImageSimulationSeconds += thisWatch.StopWatch();

        }
        
        public static void SaveToJsonDicomParametersClass(DicomParameters thisDicomParameters)
        {
            if (thisDicomParameters != null) 
            { 
                Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("SaveToJsonSerializedDicomParametersClass");

                var orientation = thisDicomParameters.FrameOrientation;

                string path = Application.persistentDataPath + "/SerializedData/" + orientation.ToString() + "/";
                System.IO.FileInfo file = new System.IO.FileInfo(path);
                if (!Directory.Exists(path))
                    file.Directory.Create(); // If the directory already exists, this method does nothing.
                string fileNameAndPath = path + orientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName + m_JsonSavedExtension;

                //JSON
                //thisDicomParameters.ImageName = thisDicomParameters.ImageName.Replace('.', '-');//resource folder cant use dots to load correctly
                var dicomJson = JsonConvert.SerializeObject(thisDicomParameters);
                //thisDicomParameters.ImageName = thisDicomParameters.ImageName.Replace('-', '.');
                using (var stream = new FileStream(fileNameAndPath, FileMode.Create, FileAccess.Write, FileShare.Write))
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    sw.Write(dicomJson);
                }

                if (b_DebugMssg) Debug.Log("I've saved Dicom Json Image " + thisDicomParameters.ImageName + " item to folder " + path);
                System.Diagnostics.Debug.WriteLine("I've saved Dicom Json Image " + thisDicomParameters.ImageName + " item to folder " + path);
            }
        }



        #region MAYBE_NOT_NEEDED
        //MAYBE NOT NEEDED
        /*
        public static void SaveImageLocallyCustomAlgorithm(DicomParameters thisDicomParameters, bool b_overwrite, string orientation, CreateImageMethods.SerializedTextureFormat selfFormat)
        {
            if(selfFormat!=CreateImageMethods.SerializedTextureFormat.RGBAFloat)
            {
                Debug.LogWarning("Incorrect Format to Save Images.");
                return;
            }
            else
            {
                Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("SaveImageLocallyCustomAlgorithm");

                Texture2D m_Texture = (Texture2D)thisDicomParameters.ImagePlaneGO.GetComponent<Renderer>().material.GetTexture("_BaseMap");

                var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";

                if (!Directory.Exists(thisDir))
                    Directory.CreateDirectory(thisDir);

                var fileNamePath = Path.Combine(thisDir, string.Concat(orientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName + "." + GetImageExtension(selfFormat)));

                if (b_overwrite || !File.Exists(fileNamePath))
                {
                    try
                    {
                        m_Texture.SaveUncompressed(fileNamePath, Texture2DExtension.DataFormat.ARGBFloat);
                        if (b_DebugMssg) Debug.Log("Saved file:" + fileNamePath);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Failed to Save Custom Format Image:" + fileNamePath + e.Message);
                        throw;
                    }
                }

                m_SaveImageSimulationSeconds += thisWatch.StopWatch();
            }
        }

        public static void SaveImageFastLocally(DicomParameters thisDicomParameters, bool b_overwrite, string orientation, CreateImageMethods.SerializedTextureFormat selfFormat)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("SaveImageLocally");

            Texture2D m_Texture = (Texture2D)thisDicomParameters.ImagePlaneGO.GetComponent<Renderer>().material.GetTexture("_BaseMap");

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";

            if (!Directory.Exists(thisDir))
                Directory.CreateDirectory(thisDir);

            var fileNamePath = Path.Combine(thisDir, string.Concat(orientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName + "." + GetImageExtension(selfFormat)));

            byte[] imageBytes = m_Texture.EncodeToPNG();

            try
            {
                File.WriteAllBytes(fileNamePath, imageBytes);
                if(b_DebugMssg) Debug.Log("Saved file:" + fileNamePath + " bytes:" + imageBytes.Length);
            }
            catch (Exception e)
            {
                Debug.Log("Failed to Save PNG Image:" + fileNamePath + e.Message);
                throw;
            }
            m_SaveImageSimulationSeconds += thisWatch.StopWatch();
        }
        
        public static IEnumerator ReadImageLoadToTexture(DicomParameters thisDicomParameters, TextureFormat thisTextureFormat, Action<Texture2D> callback)
        {
            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName, ".", m_ImageSavedExtension));
            
            byte[] imgBytes= File.ReadAllBytes(fileNamePath);
            thisDicomParameters.m_DicomTexture.LoadImage(imgBytes);

            Debug.Log("Texture Format" + thisDicomParameters.m_DicomTexture.format);
            Debug.Log("I've created Dicom Texture " + fileNamePath);
            System.Diagnostics.Debug.WriteLine("I've created Dicom Texture " + fileNamePath);

            callback(thisDicomParameters.m_DicomTexture);
            yield return thisDicomParameters.m_DicomTexture;
        }

        public static IEnumerator ReadImageGetRawTexture(DicomParameters thisDicomParameters, TextureFormat thisTextureFormat, Action<Texture2D> callback)
        {
            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName, ".", m_ImageSavedExtension));

            byte[] inputByteArray = File.ReadAllBytes(fileNamePath);

            NativeArray<byte> textureOutputByteNativeArray = thisDicomParameters.m_DicomTexture.GetRawTextureData<byte>();

            textureOutputByteNativeArray = new NativeArray<byte>(inputByteArray, Allocator.TempJob);

            //thisSerilizedDicomParameters.m_DicomTexture.LoadRawTextureData(inputByteArray);

            thisDicomParameters.m_DicomTexture.Apply();
            textureOutputByteNativeArray.Dispose();

            Debug.Log("I've created Dicom Texture " + fileNamePath);
            System.Diagnostics.Debug.WriteLine("I've created Dicom Texture " + fileNamePath);

            callback(thisDicomParameters.m_DicomTexture);
            yield return thisDicomParameters.m_DicomTexture;
        }

        static Texture2D DuplicateNonReadableTexture(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height,TextureFormat.RGBAFloat,false);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        public static IEnumerator ReadStoredImageCopyViaRenderTextureAndAttachToTexture(DicomParameters thisSerilizedDicomParameters, TextureFormat serializableTextureFormat, Action<Texture2D> callback)
        {
            //var fileNameLoadPath = Path.Combine("SerializedData", thisSerilizedDicomParameters.FrameOrientation.ToString(), string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName));
            var fileNameLoadPath = Path.Combine("SerializedData", thisSerilizedDicomParameters.FrameOrientation.ToString(), string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName));

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            //var tempName = thisSerilizedDicomParameters.ImageName.Replace('.', '-');
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", m_ImageSavedExtension));

            Texture2D tempTex = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
            try
            {
                //tempTex = Resources.Load<Texture2D>(fileNameLoadPath);
                byte[] byteArray = File.ReadAllBytes(fileNamePath);
                //tempTex.LoadImage(byteArray);
                tempTex.LoadRawTextureData(byteArray);
                var map25551 = tempTex.GetRawTextureData<Color>();
                Debug.Log("Initially Format Loaded:" + tempTex.format);

            }
            catch (Exception e)
            {
                Debug.Log("Stored Image " + thisSerilizedDicomParameters.ImageName + " in Resource Folder " + thisSerilizedDicomParameters.FrameOrientation.ToString() + " cannot be loaded. Exception:"+e.Message);
                yield break;
            }

            thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
            thisSerilizedDicomParameters.m_DicomTexture = DuplicateNonReadableTexture(tempTex);
            //Debug.Log("Final Format Copied:" + thisSerilizedDicomParameters.m_DicomTexture);
            //UnityEngine.Object.Destroy(tempTex);

            var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
            rend.material.SetColor("_BaseColor", Color.white);
            var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();

            Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);

            callback(thisSerilizedDicomParameters.m_DicomTexture);
            yield return thisSerilizedDicomParameters.m_DicomTexture;
        }

        public static IEnumerator ReadStoredImageWebRequestAndAttachToTexture(DicomParameters thisSerilizedDicomParameters, TextureFormat serializableTextureFormat, Action<Texture2D> callback)
        {
            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", m_ImageSavedExtension));

            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(fileNamePath))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(uwr.error);
                }
                else
                {
                    // Get downloaded asset bundle
                    Texture2D tempTex = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
                    tempTex = ((DownloadHandlerTexture)uwr.downloadHandler).texture;
                    //tempTex = DownloadHandlerTexture.GetContent(uwr);
                    Debug.Log("Format Loaded:" + tempTex.format);

                    thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
                    
                    //Only use duplicate if you can't read from uploaded texture and need to
                    thisSerilizedDicomParameters.m_DicomTexture = DuplicateNonReadableTexture(tempTex);
                    Debug.Log("Final Format Copied:" + thisSerilizedDicomParameters.m_DicomTexture);
                    //UnityEngine.Object.Destroy(tempTex);

                    var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
                    rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
                    rend.material.SetColor("_BaseColor", Color.white);
                    var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();

                    //***********************

                    Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);

                    callback(thisSerilizedDicomParameters.m_DicomTexture);
                    yield return thisSerilizedDicomParameters.m_DicomTexture;
                }
            }
        }

        public static void SaveImageLocally(DicomParameters thisDicomParameters, bool b_overwrite, string orientation)
        {
            Performance_Metrics.Watch thisWatch = new Performance_Metrics.Watch("SaveImageLocally");

            //string saveName = thisDicomParameters.ImageName.Replace('.','-');

            Texture2D m_Texture = (Texture2D)thisDicomParameters.ImagePlaneGO.GetComponent<Renderer>().material.GetTexture("_BaseMap");

            //Path.Combine("SerializedData", thisDicomParameters.FrameOrientation.ToString(), string.Concat(thisDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName));

            //var thisDir = Path.Combine(Application.persistentDataPath, "Resources", "SerializedData", orientation.ToString());
            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisDicomParameters.FrameOrientation.ToString() + "/";

            if (!Directory.Exists(thisDir))
                Directory.CreateDirectory(thisDir);

            var fileNamePath = Path.Combine(thisDir, string.Concat(orientation.ToString() + "_Dicom_" + thisDicomParameters.ImageName + "." + m_ImageSavedExtension));
            //var fileNamePath = "abc";

            byte[] imageBytes = m_Texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

           
            try
            {
                File.WriteAllBytes(fileNamePath, imageBytes);
                Debug.Log("Saved file:" + fileNamePath + " bytes:" + imageBytes.Length);
            }
            catch (Exception e)
            {
                Debug.Log("Error:" + e.Message);
                throw;
            }
            m_StorageSimulationSeconds += thisWatch.StopWatch();
        }*/


        /*public static IEnumerator ReadStoredImageCustomAlgorithm(DicomParameters thisSerilizedDicomParameters, TextureFormat serializableTextureFormat, Action<Texture2D> callback)
        {
            var fileNameLoadPath = Path.Combine("SerializedData", thisSerilizedDicomParameters.FrameOrientation.ToString(), string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName));

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", m_ImageSavedExtension));

            Texture2D tempTex = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
            try
            {
                //byte[] byteArray = File.ReadAllBytes(fileNamePath);
                //tempTex.LoadImage(byteArray);
                tempTex.ReadUncompressed(fileNamePath);
                //tempTex.LoadRawTextureData(byteArray);
                var map25551 = tempTex.GetRawTextureData<Color>();
                Debug.Log("Initially Format Loaded:" + tempTex.format);

            }
            catch (Exception e)
            {
                Debug.Log("Stored Image " + thisSerilizedDicomParameters.ImageName + " in Resource Folder " + thisSerilizedDicomParameters.FrameOrientation.ToString() + " cannot be loaded. Exception:" + e.Message);
                yield break;
            }

            thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
            thisSerilizedDicomParameters.m_DicomTexture = DuplicateNonReadableTexture(tempTex);
            //Debug.Log("Final Format Copied:" + thisSerilizedDicomParameters.m_DicomTexture);
            //UnityEngine.Object.Destroy(tempTex);

            var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
            rend.material.SetColor("_BaseColor", Color.white);
            var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();

            Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);

            callback(thisSerilizedDicomParameters.m_DicomTexture);
            yield return thisSerilizedDicomParameters.m_DicomTexture;
        }*/
        /*public static IEnumerator UploadDicomListWebRequest(List<SerializedDicomParams> DicomSerializedList)
        {
            var JsonDicomSerializedList = AddBinaryArrayAndListToJson(DicomSerializedList);

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormDataSection("DicomSerializedList", JsonDicomSerializedList, "application/json"));

            using (UnityWebRequest www = UnityWebRequest.Post("https://artfunctionlogin.azurewebsites.net/api/UserAccessProcessToken3?code=wk7kS9AQEzvWpIKIfLaQyuC4isnWr7sjc132qcH5Y/1XOfZJb/rJSA==", formData))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.Log("Upload to Server error:" + www.error);
                    www.Abort();
                    www.Dispose();

                    yield break;
                }
                else
                {
                    string response = System.Text.Encoding.UTF8.GetString(www.downloadHandler.data);
                    if (response.Contains("Success"))
                        Debug.Log("Upload Dicom List Success: " + response);
                    else
                        Debug.Log("Upload Dicom List Failed: " + response);
                }

                yield return null;
            }
        }
        static string AddBinaryArrayAndListToJson(List<SerializedDicomParams> DicomSerializedList)
        {
            if (DicomSerializedList.Count > 0)
            {
                for (int i = 0; i < DicomSerializedList.Count; i++)
                {
                    var thisTex = (Texture2D)DicomSerializedList[i].ImagePlaneGO.GetComponent<Renderer>().material.GetTexture("_BaseMap");
                    DicomSerializedList[i].m_DicomTexByteArray = thisTex.EncodeToPNG();
                    //DicomSerializedList[i].m_Dicom_PixelDataFloat4NativeArray = new NativeArray<float4>(DicomSerializedList[i].m_DicomTextureWidth * DicomSerializedList[i].m_DicomTextureHeight, Unity.Collections.Allocator.Persistent);
                    //DicomSerializedList[i].m_Dicom_PixelDataFloat4NativeArray = thisTex.GetRawTextureData<UnityEngine.Color>();
                }
            }

            var JsonDicomSerializedList = JsonConvert.SerializeObject(DicomSerializedList);
            return JsonDicomSerializedList;
        }*/

        /*public static IEnumerator ReadStoredImageCustomized(DicomParameters thisSerilizedDicomParameters, TextureFormat serializableTextureFormat, Action<Texture2D> callback)
        {
            var fileNameLoadPath = Path.Combine("SerializedData", thisSerilizedDicomParameters.FrameOrientation.ToString(), string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName));

            var thisDir = Application.persistentDataPath + "/SerializedData/" + thisSerilizedDicomParameters.FrameOrientation.ToString() + "/";
            var fileNamePath = Path.Combine(thisDir, string.Concat(thisSerilizedDicomParameters.FrameOrientation.ToString() + "_Dicom_" + thisSerilizedDicomParameters.ImageName, ".", m_ImageSavedExtension));

            Texture2D tempTex = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
            try
            {
                //tempTex = Resources.Load<Texture2D>(fileNameLoadPath);
                //byte[] byteArray = File.ReadAllBytes(fileNamePath);
                //tempTex.LoadImage(byteArray);
                tempTex.ReadUncompressed(fileNamePath);
                //tempTex.LoadRawTextureData(byteArray);
                var map25551 = tempTex.GetRawTextureData<Color>();
                Debug.Log("Initially Format Loaded:" + tempTex.format);

            }
            catch (Exception e)
            {
                Debug.Log("Stored Image " + thisSerilizedDicomParameters.ImageName + " in Resource Folder " + thisSerilizedDicomParameters.FrameOrientation.ToString() + " cannot be loaded. Exception:" + e.Message);
                yield break;
            }

            thisSerilizedDicomParameters.m_DicomTexture = new Texture2D(thisSerilizedDicomParameters.m_DicomTextureWidth, thisSerilizedDicomParameters.m_DicomTextureHeight, serializableTextureFormat, false);
            thisSerilizedDicomParameters.m_DicomTexture = DuplicateNonReadableTexture(tempTex);
            //Debug.Log("Final Format Copied:" + thisSerilizedDicomParameters.m_DicomTexture);
            //UnityEngine.Object.Destroy(tempTex);

            var rend = thisSerilizedDicomParameters.ImagePlaneGO.GetComponent<Renderer>();
            rend.material.SetTexture("_BaseMap", thisSerilizedDicomParameters.m_DicomTexture);
            rend.material.SetColor("_BaseColor", Color.white);
            var map2555 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<Color>();

            Debug.Log("Deserialized Format:" + thisSerilizedDicomParameters.m_DicomTexture.format);

            callback(thisSerilizedDicomParameters.m_DicomTexture);
            yield return thisSerilizedDicomParameters.m_DicomTexture;
        }*/

        /*public static IEnumerator DownloadDicomListWebRequest(List<DicomParameters> DicomParamsList)
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            formData.Add(new MultipartFormDataSection("frameOrientation", DicomParamsList[0].FrameOrientation.ToString(), "text/plain"));

            using (UnityWebRequest www = UnityWebRequest.Post("https://artfunctionlogin.azurewebsites.net/api/UserAccessProcessToken3?code=wk7kS9AQEzvWpIKIfLaQyuC4isnWr7sjc132qcH5Y/1XOfZJb/rJSA==", formData))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.Log("Dicom Orientation Download Server error:" + www.error);
                    www.Abort();
                    www.Dispose();
                    yield break;
                }
                else
                {
                    string response = System.Text.Encoding.UTF8.GetString(www.downloadHandler.data);
                    try
                    {
                        JsonConvert.PopulateObject(response, DicomParamsList);
                        foreach (var img in DicomParamsList)
                        {
                            Debug.Log("Downloaded Dicom Image: " + img.ImageName);
                        }

                        Debug.Log("Upload Dicom List Success: " + response);
                    }
                    catch (Exception)
                    {
                        Debug.Log("Upload Dicom List Failed: " + response);
                    }
                }
            }
            yield return DicomParamsList;
        }*/
        #endregion

    }
}



/*var colors3 = thisSerilizedDicomParameters.m_DicomTexture.GetRawTextureData<ColorARGB32>();

Debug.Log("Color " + colors[0].r + ","+colors[152000].r);
Debug.Log("Color2 size " + colors2.Length);
float2 col3Tran = new float2((float)colors3[0].r / 255, (float)colors3[152000].r / 255);
Debug.Log("Color3 len " +colors3.Length +" Color3 " + col3Tran[0] + "," + (float)colors3[152000].r/255);

var colorArray = new Color32[colors4.Length / 4];
for (var i = 0; i < colors4.Length; i += 4)
{
    var color = new Color32(colors4[i + 0], colors4[i + 1], colors4[i + 2], colors4[i + 3]);
    colorArray[i / 4] = color;
}

Debug.Log("Color4 len:" + colors4.Length + " colorArray len:" + colorArray.Length);
Debug.Log("Color4 " + colorArray[0].r + "," + colorArray[152000].r);*/


