using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Rendergon.Particle_Scripts
{
    [Serializable]
    public class PixelData
    {
        public float pixelX;
        public float pixelY;
        public SerializableVector4 pixelColor;

        [Serializable]
        public struct SerializableVector4
        {
            public float r;
            public float g;
            public float b;
            public float a;

            public SerializableVector4(float rX, float rY, float rZ, float rA)
            {
                r = rX;
                g = rY;
                b = rZ;
                a = rA;
            }
        }

        //public PixelData(float posX, float posY, float rX, float rY, float rZ, float rA)
        public PixelData(float posX, float posY, Color col)
        {
            pixelX = posX;
            pixelY = posY;
            pixelColor = new SerializableVector4(col.r, col.g, col.b, col.a);
        }

    }
}