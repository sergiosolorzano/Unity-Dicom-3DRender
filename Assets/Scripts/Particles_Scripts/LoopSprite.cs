using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Rendergon.Particle_Scripts
{
    [RequireComponent(typeof(ParticleSystem))]
    public class LoopSprite : MonoBehaviour
    {
        [HideInInspector]
        float PPU;
        Vector2 spriteDimensions;
        Vector3 spriteObjScale;
        Vector2 spriteObjBounds;
        Dictionary<int, PixelData> thisPixelDataDict = new Dictionary<int, PixelData>();

        [HideInInspector]
        Sprite thisSprite;
        [HideInInspector]
        Texture2D m_SpriteTexture;
        [HideInInspector]
        String m_ImageName;

        //Pixel Params
        int index = 0;
        float pixelX, pixelY, pixelZ;

        //Particles
        ParticleSystem m_ParticleSystem;
        public ParticleSystem.MainModule m_MainParticleSystem;
        ParticleSystem.Particle[] m_Particles;
        DateTime m_StartGetDataTime, m_EndGetDataTime, m_StartColourAndPlaceParticles, m_EndColourAndPlaceParticles;

        bool m_ParticlesReady = true;
        public delegate void ParticlesInstantiationComplete();
        public ParticlesInstantiationComplete ParticlesInstantiationCompleteDelegate;

        private void OnEnable()
        {
            ParticlesInstantiationCompleteDelegate += PositionParticles;
        }

        public void Init()
        {
            //Sprite
            thisSprite = GetComponent<SpriteRenderer>().sprite;
            m_ImageName = thisSprite.name;

            m_ParticleSystem = GetComponent<ParticleSystem>();
            m_ParticleSystem.Stop();
            m_MainParticleSystem = m_ParticleSystem.main;

            m_SpriteTexture = thisSprite.texture;
            PPU = thisSprite.pixelsPerUnit;
            spriteDimensions = new Vector2(thisSprite.texture.width, thisSprite.texture.height);
            spriteObjScale = new Vector3(gameObject.transform.localScale.x, gameObject.transform.localScale.y, gameObject.transform.localScale.z);
            spriteObjBounds = new Vector2(spriteDimensions.x / PPU, spriteDimensions.y / PPU);
            pixelZ = gameObject.transform.position.z;
        }

        public void PositionParticles()
        {
            int numParticlesAlive = m_ParticleSystem.GetParticles(m_Particles);

            for (int i = 0; i < numParticlesAlive; i++)
            {
                Vector3 tempPos = transform.InverseTransformPoint(new Vector3(thisPixelDataDict[i].pixelX, thisPixelDataDict[i].pixelY, pixelZ));
                m_Particles[i].position = new Vector3(tempPos.x, tempPos.y, pixelZ);
                //if (i == 0 || i == 1)
                //  Debug.Log("Particle " + i + " posZ " + pixelZ + " image " + m_ImageName);
                m_Particles[i].startColor = new Color(thisPixelDataDict[i].pixelColor.r, thisPixelDataDict[i].pixelColor.g, thisPixelDataDict[i].pixelColor.b, 1);
            }

            // Apply the particle changes to the Particle System
            m_ParticleSystem.SetParticles(m_Particles, numParticlesAlive);

            int ImgIndex = GetImageIndex();
            if (ImgIndex != -1)
            {
                PixelManager.m_ParticlePositionCompleteDict[ImgIndex] = true;
                Debug.Log("Finished position particles for img " + gameObject.name + " at index " + ImgIndex);
            }
        }

        public IEnumerator CreatePositionParticles(bool deserialize)
        {
            IEnumerator DeserializePixelDict = My_Serializer.DeserializePixelDictionary(gameObject.name);
            yield return (DeserializePixelDict);

            thisPixelDataDict = (Dictionary<int, PixelData>)DeserializePixelDict.Current;

            if (thisPixelDataDict.Count > 0)
            {
                //Debug.Log("Start Particles Max " + index);
                m_MainParticleSystem.maxParticles = thisPixelDataDict.Count;
                m_Particles = new ParticleSystem.Particle[m_MainParticleSystem.maxParticles];
                m_ParticleSystem.Play();
                m_ParticlesReady = false;
            }
            else
                Debug.Log("***No Particles To Create***");

            yield return null;
        }

        private void Update()
        {
            if (!m_ParticlesReady)
            {
                if (m_ParticleSystem.GetParticles(m_Particles) == thisPixelDataDict.Count)
                {
                    Debug.Log("All Particles Alive - Count: " + thisPixelDataDict.Count);
                    m_ParticlesReady = true;
                    ParticlesInstantiationCompleteDelegate();
                }
            }
        }

        public IEnumerator GetPixelColourDataProcess()
        {
            Debug.Log("Getting colour data for " + gameObject.name);
            Color[] pixelCols = m_SpriteTexture.GetPixels(0);

            index = 0;

            for (var y = 0; y < spriteDimensions.y; y++)
            {
                for (var x = 0; x < spriteDimensions.x; x++)
                {
                    thisPixelDataDict[index] = new PixelData(PixelManager.m_PixelSourceDict[index].pixelX, PixelManager.m_PixelSourceDict[index].pixelY, pixelCols[index]);
                    index++;
                }
            }

            My_Serializer.SaveImageCalculations(thisPixelDataDict, gameObject.name);

            int ImgIndex = GetImageIndex();
            if (ImgIndex == 0)
                PixelManager.m_PixelSourceDict = thisPixelDataDict;

            if (ImgIndex != -1)
            {
                PixelManager.m_GetColourDataCompleteDict[ImgIndex] = true;
                Debug.Log("Finished get colour for img " + gameObject.name + " at index " + ImgIndex);
            }

            yield return null;
        }

        public IEnumerator GetPixelPositionDataProcess()
        {
            m_StartGetDataTime = System.DateTime.UtcNow;
            Debug.Log("Target total Pixels " + spriteDimensions.x * spriteDimensions.y + " GetPixelPositionData StartTime:" + m_StartGetDataTime.ToString("HH:mm dd MMMM, yyyy"));

            PixelData m_PixelData;

            for (var y = 0; y < spriteDimensions.y; y++)
            {
                for (var x = 0; x < spriteDimensions.x; x++)
                {
                    IEnumerator thisCalc = CalculateWorldPosOfPixelCoordinate("X", x, spriteObjBounds.x, gameObject.transform.position.x, spriteObjScale.x, PPU);
                    yield return (thisCalc);
                    thisCalc = CalculateWorldPosOfPixelCoordinate("Y", y, spriteObjBounds.y, gameObject.transform.position.y, spriteObjScale.y, PPU);
                    yield return (thisCalc);

                    m_PixelData = new PixelData(pixelX, pixelY, new Color());

                    thisPixelDataDict.Add(index, m_PixelData);
                    if (index % 1000 == 0)
                        Debug.Log("Index Pixel:" + index);
                    index++;
                }
            }

            PixelManager.m_PixelSourceDict = thisPixelDataDict;
            My_Serializer.SaveImageCalculations(thisPixelDataDict, gameObject.name);

            PixelManager.GetPositionPixelDataComplete = true;
            PixelManager.m_StartGetPixelColourData = true;

            m_EndGetDataTime = System.DateTime.UtcNow;
            Debug.Log("Total Pixels " + thisPixelDataDict.Count + " End Get Time Data:" + m_EndGetDataTime.ToString("HH:mm dd MMMM, yyyy"));

            yield return null;
        }

        int GetImageIndex()
        {
            try
            {
                int thisImgIndex = Int32.Parse(gameObject.name.Substring(gameObject.name.LastIndexOf('_') + 1));
                return thisImgIndex;
            }
            catch (Exception)
            {
                Debug.Log("Get Index of Image Error for " + gameObject.name);
                return -1;
            }
        }

        public IEnumerator CalculateWorldPosOfPixelCoordinate(string CoorDtype, int coord, float boundsSize, float position, float scale, float PPU)
        {
            float PixelInWorldSpace = 1.0f / PPU;
            float startPos = position - (boundsSize * 0.5f * scale);
            //Debug.Log("" + "coord " + coord + " PPU " + PPU +  " startPos " + startPos + "PixelInWordlScape " + PixelInWorldSpace + " FINAL: " + (startPos + (PixelInWorldSpace * coord) * scale));

            if (CoorDtype.Equals("X"))
                pixelX = startPos + (PixelInWorldSpace * coord) * scale;
            else
                pixelY = startPos + (PixelInWorldSpace * coord) * scale;

            yield return null;
        }

        private void OnDisable()
        {
            ParticlesInstantiationCompleteDelegate -= PositionParticles;
        }
    }
}