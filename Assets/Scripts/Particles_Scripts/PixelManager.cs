using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Rendergon.Particle_Scripts
{
    public class PixelManager : MonoBehaviour
    {
        public GameObject m_ParentGO;
        public GameObject m_PixelLocationSource;
        public Sprite[] m_SourceImages;
        public List<SpriteRenderer> m_SpriteRendererList = new List<SpriteRenderer>();
        public List<GameObject> m_ImagePrefabList;
        float m_DistanceBetweenImages;

        public static Dictionary<int, PixelData> m_PixelSourceDict = new Dictionary<int, PixelData>();
        public static bool GetPositionPixelDataComplete = false;

        public static Dictionary<int, bool> m_GetColourDataCompleteDict = new Dictionary<int, bool>();
        public static bool m_StartGetPixelColourData = false;
        private bool GetColourPixelDataComplete = false;

        public static Dictionary<int, bool> m_ParticlePositionCompleteDict = new Dictionary<int, bool>();
        public static bool ParticlePositionComplete = false;

        public delegate void OnGetPixelDataComplete();
        public OnGetPixelDataComplete OnGetPixelDataCompleteDelegate;

        DateTime m_StartGetDataTime = DateTime.UtcNow;
        DateTime m_StartPositionParticleDataTime;

        [HideInInspector]
        public enum ImageCalculationMode { GetPixelLocationColourData, PositionParticles, Both }
        public ImageCalculationMode mode = ImageCalculationMode.GetPixelLocationColourData;

        private void OnEnable()
        {
            m_ParentGO = new GameObject();
            m_ParentGO.name = "Parent_GO";
            OnGetPixelDataCompleteDelegate += GetColourData;
            m_DistanceBetweenImages = 0.15f;
            m_SourceImages = Resources.LoadAll<Sprite>("Images");
        }

        private IEnumerator Start()
        {
            Debug.Log("Mode:" + mode.ToString());

            IEnumerator m_InstanceImages = InstantiateImagePrefabs();
            yield return (m_InstanceImages);

            m_PixelLocationSource = m_ImagePrefabList[0];

            switch (mode)
            {
                case ImageCalculationMode.GetPixelLocationColourData:
                case ImageCalculationMode.Both:
                    m_ImagePrefabList[0].GetComponent<LoopSprite>().Init();
                    StartCoroutine(m_ImagePrefabList[0].GetComponent<LoopSprite>().GetPixelPositionDataProcess());
                    break;

                case ImageCalculationMode.PositionParticles:
                    PositionParticles();
                    break;

                default:
                    Debug.Log("***ERROR***");
                    break;
            }
        }

        void PositionParticles()
        {
            bool deserializePixelData = (mode == ImageCalculationMode.PositionParticles) ? true : false;
            m_StartPositionParticleDataTime = DateTime.UtcNow;
            for (int i = 0; i < m_ImagePrefabList.Count; i++)
            {
                m_ImagePrefabList[i].GetComponent<LoopSprite>().Init();
                StartCoroutine(m_ImagePrefabList[i].GetComponent<LoopSprite>().CreatePositionParticles(deserializePixelData));
            }
        }

        void GetColourData()
        {
            for (int i = 0; i < m_ImagePrefabList.Count; i++)
            {
                m_ImagePrefabList[i].GetComponent<LoopSprite>().Init();
                StartCoroutine(m_ImagePrefabList[i].GetComponent<LoopSprite>().GetPixelColourDataProcess());
            }
        }

        private void Update()
        {
            //ImageCalculationMode.GetPixelLocationColourData Triggers
            if (m_StartGetPixelColourData && GetPositionPixelDataComplete)
            {
                Debug.Log("At mageCalculationMode.GetPixelLocationColourData Triggers 1");

                m_StartGetPixelColourData = false;
                Debug.Log("Finished getting Pixel Location.");

                //Kick off populate colour data process
                OnGetPixelDataCompleteDelegate();
            }

            if (!GetColourPixelDataComplete && GetPositionPixelDataComplete)
            {
                Debug.Log("At mageCalculationMode.GetPixelLocationColourData Triggers 2");
                if (m_GetColourDataCompleteDict.Count == m_ImagePrefabList.Count)
                {
                    Debug.Log("Finished getting Colour Data for all Images " + m_GetColourDataCompleteDict.Count);
                    GetColourPixelDataComplete = true;
                    Debug.Log("Time To GetColourData:" + (DateTime.UtcNow - m_StartGetDataTime));

                    if (mode == ImageCalculationMode.Both)
                        PositionParticles();
                }
            }

            //ImageCalculationMode.PositionParticles Triggers
            if (!ParticlePositionComplete && mode != ImageCalculationMode.GetPixelLocationColourData)
            {
                Debug.Log("At ImageCalculationMode.PositionParticles Triggers");
                if (m_ParticlePositionCompleteDict.Count == m_ImagePrefabList.Count)
                {
                    ParticlePositionComplete = true;
                    Debug.Log("Finished positioning Particles for all Images " + m_ParticlePositionCompleteDict.Count);
                    Debug.Log("Time To Place Particles:" + (DateTime.UtcNow - m_StartPositionParticleDataTime));

                    for (int i = 0; i < m_ImagePrefabList.Count; i++)
                        m_SpriteRendererList[i].enabled = false;
                }
            }
        }

        IEnumerator InstantiateImagePrefabs()
        {
            for (int i = 0; i < m_SourceImages.Length; i++)
            {
                var thisImageGO = Instantiate(Resources.Load<GameObject>("Prefab/SpriteRend"), new Vector3(0, 0, 0), Quaternion.identity, m_ParentGO.transform);
                m_SpriteRendererList.Add(thisImageGO.GetComponent<SpriteRenderer>());
                m_SpriteRendererList[i].sprite = m_SourceImages[i];
                thisImageGO.gameObject.name = string.Concat(m_SpriteRendererList[i].sprite.name, "_", i);
                m_ImagePrefabList.Add(thisImageGO);

                if (i == 0)
                {
                    m_PixelLocationSource = thisImageGO;
                    thisImageGO.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
                }
                else
                    thisImageGO.transform.position = new Vector3(0.0f, 0.0f, (m_PixelLocationSource.transform.position.z + m_DistanceBetweenImages) * i);
            }

            yield return null;
        }

        private void OnDisable()
        {
            OnGetPixelDataCompleteDelegate -= GetColourData;
        }
    }
}