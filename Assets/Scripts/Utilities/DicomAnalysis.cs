using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Rendergon.Managers;
using Rendergon.Dicom;

namespace Rendergon.Utilities
{
    public class DicomAnalysis
    {
        public static Vector2 WindowSetting(DicomManager.WindowWidth_Level currentWindowwidth_Level, float m_UserWindowWidth, float m_UserWindowCenter)
        {
            switch (currentWindowwidth_Level)//Vector2(windowWidth,windowCenter)
            {
                case DicomManager.WindowWidth_Level.Manufacturer:
                    return new Vector2(400,40);
                case DicomManager.WindowWidth_Level.User:
                    return new Vector2(m_UserWindowWidth, m_UserWindowCenter);
                case DicomManager.WindowWidth_Level.Abdominal_Soft_Tissue:
                    return new Vector2(400, 50);
                case DicomManager.WindowWidth_Level.Soft_Tissue:
                    return new Vector2(0,350);
                case DicomManager.WindowWidth_Level.Abdominal_Liver:
                    return new Vector2(150, 30);
                case DicomManager.WindowWidth_Level.Bone:
                    return new Vector2(1800, 400);
                case DicomManager.WindowWidth_Level.Brain:
                    return new Vector2(80, 40);
                case DicomManager.WindowWidth_Level.Lung:
                    return new Vector2(1500, 600);
                case DicomManager.WindowWidth_Level.Mediastinum:
                    return new Vector2(150, 50);
                case DicomManager.WindowWidth_Level.Blood_clot:
                    return new Vector2(60, 100);
                case DicomManager.WindowWidth_Level.Acute_Stroke:
                    return new Vector2(30, 30);
                default:
                    return new Vector2(-10000, -10000);
            }
        }
    }
}
