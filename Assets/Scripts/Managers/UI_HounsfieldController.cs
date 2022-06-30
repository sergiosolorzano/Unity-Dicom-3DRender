using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Rendergon.Computer_Graphics;
using Rendergon.Managers;
using Unity.Collections;
using Unity.Jobs;

namespace Rendergon.UI
{
    public class UI_HounsfieldController : MonoBehaviour
    {
        [HideInInspector]
        public UI_Hounsfield m_UI_Hounsfield;
        [HideInInspector]
        public DicomManager m_DicomManager;
        [HideInInspector]
        public UI_Methods m_UI_Methods;

        public Image thisColorImage;
        Color thisColor;
        Vector2 ColorRange = new Vector2(0, 0);
        UI_Methods.GrayscaleRange thisHounsfieldRangeIndex;

        public bool b_DebugMssg;

        private void Start()
        {
            b_DebugMssg = false;

            m_UI_Hounsfield = GameObject.FindGameObjectWithTag("Analytics").GetComponent<UI_Hounsfield>();
            m_DicomManager = GameObject.FindGameObjectWithTag("Managers").GetComponent<DicomManager>();
            m_UI_Methods = GameObject.FindGameObjectWithTag("UI_Methods").GetComponent<UI_Methods>();

            thisColor = thisColorImage.color;

            GetRange();
        }

        public void GetRange()
        {
            ColorRange = m_UI_Methods.GetColorMidRange((int)(thisColor.r * 255));
            thisHounsfieldRangeIndex = m_UI_Methods.GetColorEnumIndex((int)(thisColor.r * 255));
        }

        public void ToggleAlphaToColor(Toggle thisToggle)
        {
            if (m_DicomManager.b_UseHounsfield)
            {
                if(b_DebugMssg) Debug.Log("Toggle is " + thisToggle);

                if (thisToggle.isOn)
                {
                    if (b_DebugMssg) Debug.Log("togg now on");
                    m_UI_Hounsfield.UpdateColorRangeTransparencyButtonPressedBurst(true, thisHounsfieldRangeIndex, m_UI_Methods.GrayscaleRangeDict);
                }
                else
                {
                    if (b_DebugMssg) Debug.Log("togg now off");
                    m_UI_Hounsfield.UpdateColorRangeTransparencyButtonPressedBurst(false, thisHounsfieldRangeIndex, m_UI_Methods.GrayscaleRangeDict);
                }
            }
            else
                Debug.LogWarning("Hounsfield Analytics are disabled.");
        }
    }
}