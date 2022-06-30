using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Rendergon.VFX
{
    public class PointCloudRenderer : MonoBehaviour
    {
        Texture2D texColor;
        Texture2D texPosScale;
        VisualEffect vfx;

        bool toUpdate = false;
        uint particleCount = 0;

        [HideInInspector]
        public Vector3[] positions;
        [HideInInspector]
        public Color[] colors;
        [HideInInspector]
        public uint resolution = 0;//linked to max amount of particles
        [HideInInspector]
        public float particleSize = 0;//allows change of individual particles

        private void Start()
        {
            vfx = GetComponent<VisualEffect>();
            /*resolution = 512;
            particleSize = 0.1f;

            Vector3[] positions = new Vector3[(int)resolution*(int)resolution];
            Color[] colors = new Color[(int)resolution*(int)resolution];

            int j = 0;
            for(int x=0;x<(int)resolution;x++)
            {
                for(int y=0;y<(int)resolution;y++)
                {
                    positions[x + y * (int)resolution] = new Vector3(Random.value * 10, Random.value * 10, Random.value * 10);
                    colors[x + y * (int)resolution] = new Color(Random.value, Random.value, Random.value, 1);
                    j++;
                }
            }

            SetParticles(positions, colors);*/
        }

        private void Update()
        {
            if (toUpdate)
            {
                Debug.Log("At Update !!!");
                toUpdate = false;

                vfx.Reinit();//initial particles are erased and new particles spawned

                vfx.SetUInt(Shader.PropertyToID("ParticleCount"), particleCount);
                vfx.SetTexture(Shader.PropertyToID("TexColor"), texColor);
                vfx.SetTexture(Shader.PropertyToID("TexPosScale"), texPosScale);
                vfx.SetUInt(Shader.PropertyToID("Resolution"), resolution);
            }
        }

        //method to populate visual effect
        public void SetParticles(Vector3[] positions, Color[] colors)
        {
            //texColor = new Texture2D(positions.Length > (int)resolution ? (int)resolution : positions.Length, Mathf.Clamp(positions.Length / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);
            //texPosScale= new Texture2D(positions.Length > (int)resolution ? (int)resolution : positions.Length, Mathf.Clamp(positions.Length / (int)resolution, 1, (int)resolution), TextureFormat.RGBAFloat, false);

            texColor = new Texture2D((int)resolution, (int)resolution, TextureFormat.RGBAFloat, false);
            texPosScale = new Texture2D((int)resolution, (int)resolution, TextureFormat.RGBAFloat, false);

            int texWidth = texColor.width;
            int texHeight = texColor.height;

            Color[] posAndSizeData = new Color[texWidth * texHeight];

            for (int y = 0; y < texHeight; y++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    int index = x + y * texWidth;
                    posAndSizeData[index] = new Color(positions[index].x, positions[index].y, positions[index].z, particleSize);
                }
            }

            texColor.SetPixels(colors);
            texPosScale.SetPixels(posAndSizeData);

            /*int step = 3;

            for(int y=0;y<texHeight;y+=step)
            {
                if (y > texHeight)
                    y = (texHeight);
                else y += step;

                    for (int x = 0; x < texWidth; x+=step)
                    {
                        if (x > texWidth)
                            x = (texWidth);
                        else x += step;

                    int index = x + y * texWidth;
                        texColor.SetPixel(x, y, colors[index]);
                        var data = new Color(positions[index].x, positions[index].y, positions[index].z, particleSize);
                        texPosScale.SetPixel(x, y, data);
                    }
            }*/

            texColor.Apply();
            texPosScale.Apply();

            particleCount = (uint)positions.Length;
            Debug.Log("Particle Count:" + particleCount + " particle size" + particleSize + " resolution " + resolution + " width " + texWidth + " height " + texHeight);
            toUpdate = true;
        }
    }
}