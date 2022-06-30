using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

namespace Rendergon.Particle_Scripts
{
    public static class My_Serializer
    {
        public static IEnumerator DeserializePixelDictionary(string imageName)
        {
            Dictionary<int, PixelData> thisPixelDataDict = new Dictionary<int, PixelData>();

            string pathWithFileNameSaved = Path.Combine(Application.persistentDataPath, "Calculations", string.Concat(imageName, ".txt"));

            if (File.Exists(pathWithFileNameSaved))
            {
                using (StreamReader r = new StreamReader(pathWithFileNameSaved))
                {
                    try
                    {
                        string json = r.ReadToEnd();
                        JsonConvert.PopulateObject(json, thisPixelDataDict);
                        Debug.Log("Success Deserialize dictionary from file Image " + imageName);
                    }
                    catch (Exception e)
                    {
                        Debug.Log("Failed-Populate Image for " + imageName + " " + e.Message);
                    }
                }
            }
            yield return thisPixelDataDict;
        }


        public static void SaveImageCalculations(Dictionary<int, PixelData> thisPixelDataDict, string imageName)
        {
            var serializedPixelDict = JsonConvert.SerializeObject(thisPixelDataDict);

            BinaryFormatter bf = new BinaryFormatter();

            string path = Application.persistentDataPath + "/Calculations/" + imageName + ".txt";

            System.IO.FileInfo file = new System.IO.FileInfo(path);
            if (!Directory.Exists(path))
                file.Directory.Create(); // If the directory already exists, this method does nothing.

            System.IO.File.WriteAllText(path, serializedPixelDict);

            //createFile.Close();
            Debug.Log("Success saving Image Calculations at " + path);
        }
    }
}