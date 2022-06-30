using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Unity.Collections;

namespace Rendergon.UI
{
    public class UI_Methods : MonoBehaviour
    {
        public Text fpsText;
        public GameObject ColorRangeParentPanel;

        [HideInInspector]
        public enum GrayscaleRange { R_000 = 0, R_001 = 1, R_025 = 25, R_050 = 50, R_075 = 75, R_100 = 100, R_125 = 125, R_150 = 150, R_175 = 175, R_200 = 200, R_225 = 225, R_255 = 255 }
        public GrayscaleRange GrayscaleRangeSelection;
        public Dictionary<int, Vector2> GrayscaleRangeDict = new Dictionary<int, Vector2>();
        List<GameObject> ButtonChildList = new List<GameObject>();
        public bool showFPS = false;

        public bool b_DebugMssg;

        void Update()
        {
            if (showFPS)
            {
                var FPS = (int)(1f / Time.unscaledDeltaTime);
                fpsText.text = "Frames Per Second = " + FPS.ToString();
            }
        }

        public void Init(bool b_RunGrayscaleAnalysis)
        {
            b_DebugMssg = false;

            if(b_RunGrayscaleAnalysis)
            {
                ColorRangeParentPanel.SetActive(true);
                CreateColorRangePanel();
            }
        }

        public void CreateColorRangePanel()
        {
            string[] grayscaleRangeString = Enum.GetNames(typeof(GrayscaleRange));
            
            float[] grayscaleRange = new float[grayscaleRangeString.Length];

            for (int i = 0; i < grayscaleRangeString.Length - 1; i++)
            {
                int range;
                Int32.TryParse(grayscaleRangeString[i].Substring(grayscaleRangeString[i].Length - 3), out range);
                var applicableRange = GetColorMidRange(range);
                float[] applicableRangeFloats = { applicableRange.x, applicableRange.y };
                double midRange = applicableRangeFloats.AsQueryable().Average();
                grayscaleRange[i] = ((float)(Math.Round((double)(midRange), MidpointRounding.AwayFromZero))) / 255;
            }

            for (int i = 0; i < grayscaleRange.Length - 1; i++)
            {
                GameObject thisColorRangeButton = Instantiate(Resources.Load<GameObject>("Prefab/ColorRangeToggle"), ColorRangeParentPanel.transform);
                
                thisColorRangeButton.name = "Hounsfield_Toggle_" + i;
                if (i == 0)
                    thisColorRangeButton.tag = "Hounsfield_Toggle_0";

                thisColorRangeButton.transform.GetChild(1).GetComponent<Image>().color = new Color(grayscaleRange[i], grayscaleRange[i], grayscaleRange[i], 1);
                var range = GetColorMidRange((int)(grayscaleRange[i] * 255));
                thisColorRangeButton.transform.GetChild(2).GetComponent<Text>().text = string.Concat("Range ", range.x, " - ", range.y);
                
                //Add Color Range to Dict
                GrayscaleRangeDict.Add(i,range/255);
                if(b_DebugMssg) Debug.Log("Range " + thisColorRangeButton.name + " color " + grayscaleRange[i]*255);
            }
        }

        int EnumToInt(GrayscaleRange position)
        {
            int a = (int)position;
            string[] grayscaleRangeString = Enum.GetNames(typeof(GrayscaleRange));
            var c = (grayscaleRangeString[a].Substring(grayscaleRangeString[a].Length - 3));
            return Int32.Parse(grayscaleRangeString[a].Substring(grayscaleRangeString[a].Length - 3));
        }

        public Vector2 GetColorMidRange(int GrayscaleValue)
        {
            GrayscaleRange thisRange = GrayscaleRange.R_000;

            if (GrayscaleValue >= (int)GrayscaleRange.R_000 && GrayscaleValue < (int)GrayscaleRange.R_001)
                thisRange = GrayscaleRange.R_000;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_001 && GrayscaleValue < (int)GrayscaleRange.R_025)
                thisRange = GrayscaleRange.R_025;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_025 && GrayscaleValue < (int)GrayscaleRange.R_050)
                thisRange = GrayscaleRange.R_050;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_050 && GrayscaleValue < (int)GrayscaleRange.R_075)
                thisRange = GrayscaleRange.R_075;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_075 && GrayscaleValue < (int)GrayscaleRange.R_100)
                thisRange = GrayscaleRange.R_100;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_100 && GrayscaleValue < (int)GrayscaleRange.R_125)
                thisRange = GrayscaleRange.R_125;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_125 && GrayscaleValue < (int)GrayscaleRange.R_150)
                thisRange = GrayscaleRange.R_150;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_150 && GrayscaleValue < (int)GrayscaleRange.R_175)
                thisRange = GrayscaleRange.R_175;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_175 && GrayscaleValue < (int)GrayscaleRange.R_200)
                thisRange = GrayscaleRange.R_200;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_200 && GrayscaleValue < (int)GrayscaleRange.R_225)
                thisRange = GrayscaleRange.R_225;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_225)
                thisRange = GrayscaleRange.R_255;

            return GetRangeValues(thisRange);

        }

        public GrayscaleRange GetColorEnumIndex(int GrayscaleValue)
        {
            GrayscaleRange thisRange = GrayscaleRange.R_000;

            if (GrayscaleValue >= (int)GrayscaleRange.R_000 && GrayscaleValue < (int)GrayscaleRange.R_001)
                thisRange = GrayscaleRange.R_000;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_001 && GrayscaleValue < (int)GrayscaleRange.R_025)
                thisRange = GrayscaleRange.R_025;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_025 && GrayscaleValue < (int)GrayscaleRange.R_050)
                thisRange = GrayscaleRange.R_050;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_050 && GrayscaleValue < (int)GrayscaleRange.R_075)
                thisRange = GrayscaleRange.R_075;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_075 && GrayscaleValue < (int)GrayscaleRange.R_100)
                thisRange = GrayscaleRange.R_100;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_100 && GrayscaleValue < (int)GrayscaleRange.R_125)
                thisRange = GrayscaleRange.R_125;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_125 && GrayscaleValue < (int)GrayscaleRange.R_150)
                thisRange = GrayscaleRange.R_150;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_150 && GrayscaleValue < (int)GrayscaleRange.R_175)
                thisRange = GrayscaleRange.R_175;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_175 && GrayscaleValue < (int)GrayscaleRange.R_200)
                thisRange = GrayscaleRange.R_200;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_200 && GrayscaleValue < (int)GrayscaleRange.R_225)
                thisRange = GrayscaleRange.R_225;
            else if (GrayscaleValue >= (int)GrayscaleRange.R_225)
                thisRange = GrayscaleRange.R_255;

            return thisRange;

        }

        Vector2 GetRangeValues(GrayscaleRange GrayscaleRangeSelected)
        {
            switch (GrayscaleRangeSelected)
            {
                case GrayscaleRange.R_000:
                    return new Vector2(0, 0.1f);
                case GrayscaleRange.R_025:
                    return new Vector2(1, 25);
                case GrayscaleRange.R_050:
                    return new Vector2(25, 50);
                case GrayscaleRange.R_075:
                    return new Vector2(50, 75);
                case GrayscaleRange.R_100:
                    return new Vector2(75, 100);
                case GrayscaleRange.R_125:
                    return new Vector2(100, 125);
                case GrayscaleRange.R_150:
                    return new Vector2(125, 150);
                case GrayscaleRange.R_175:
                    return new Vector2(150, 175);
                case GrayscaleRange.R_200:
                    return new Vector2(175, 200);
                case GrayscaleRange.R_225:
                    return new Vector2(200, 225);
                case GrayscaleRange.R_255:
                    return new Vector2(225, 300);
                default:
                    return new Vector2(225, 300);
            }
        }
    }
}