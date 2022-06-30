/******
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 Bunny83
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of
 * this software and associated documentation files (the "Software"), to deal in
 * the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
 * of the Software, and to permit persons to whom the Software is furnished to do
 * so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * Original source:
 * https://www.dropbox.com/s/rqctkisgq178fba/Texture2DExtension.cs?dl=0
 * Discussion: 
 * https://answers.unity.com/questions/1209900/whats-the-best-way-to-save-an-argbfloat-texture-wi.html
 * With this file in your project you can simply do this:

 someTexture.SaveUncompressed("SomeFilename", Texture2DExtension.DataFormat.ARGBFloat);
To load an image you can use

     Texture2D tex = new Texture2D(1,1);
     tex.ReadUncompressed("SomeFilename");
I've also implemented a smaller 16 bit format that only works for color values in the range of 0.0 to 1.0

 someTexture.SaveUncompressed("SomeFilename", Texture2DExtension.DataFormat.ARGBUShort);
This format is smaller (half the size) but with a bit less precision.
 *******/

using UnityEngine;

public static class Texture2DExtension
{
    public enum DataFormat
    {
        NONE = 0,
        ARGBFloat = 1,
        ARGBUShort = 2,
    }
    #region ARGBFloat
    private static void SaveARGBFloatUncompressed(Texture2D aTex, System.IO.BinaryWriter aWriter)
    {
        int w = aTex.width;
        int h = aTex.height;
        //Color[] colors = aTex.GetPixels();
        Color[] colors = aTex.GetRawTextureData<Color>().ToArray();
        aWriter.Write((uint)DataFormat.ARGBFloat);
        aWriter.Write(w);
        aWriter.Write(h);
        for (int i = 0; i < colors.Length; i++)
        {
            Color c = colors[i];
            aWriter.Write(c.a);
            aWriter.Write(c.r);
            aWriter.Write(c.g);
            aWriter.Write(c.b);
        }
    }
    private static void ReadARGBFloatUncompressed(Texture2D aTex, System.IO.BinaryReader aReader)
    {
        int w = aReader.ReadInt32();
        int h = aReader.ReadInt32();
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++)
        {
            Color c;
            c.a = aReader.ReadSingle();
            c.r = aReader.ReadSingle();
            c.g = aReader.ReadSingle();
            c.b = aReader.ReadSingle();
            colors[i] = c;
        }
        aTex.Resize(w, h);
        aTex.SetPixels(colors);
        aTex.Apply();
    }
    #endregion ARGBFloat
    #region ARGBUShort
    private static void SaveARGBUShortUncompressed(this Texture2D aTex, System.IO.BinaryWriter aWriter)
    {
        int w = aTex.width;
        int h = aTex.height;
        //Color[] colors = aTex.GetPixels();
        Color[] colors = aTex.GetRawTextureData<Color>().ToArray();
        aWriter.Write((uint)DataFormat.ARGBUShort);
        aWriter.Write(w);
        aWriter.Write(h);
        for (int i = 0; i < colors.Length; i++)
        {
            Color c = colors[i];
            aWriter.Write((ushort)(c.a * 65535));
            aWriter.Write((ushort)(c.r * 65535));
            aWriter.Write((ushort)(c.g * 65535));
            aWriter.Write((ushort)(c.b * 65535));
        }
    }
    private static void ReadARGBUShortUncompressed(Texture2D aTex, System.IO.BinaryReader aReader)
    {
        int w = aReader.ReadInt32();
        int h = aReader.ReadInt32();
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++)
        {
            Color c;
            c.a = aReader.ReadUInt16() / 65535f;
            c.r = aReader.ReadUInt16() / 65535f;
            c.g = aReader.ReadUInt16() / 65535f;
            c.b = aReader.ReadUInt16() / 65535f;
            colors[i] = c;
        }
        aTex.Resize(w, h);
        aTex.SetPixels(colors);
        aTex.Apply();
    }
    #endregion ARGBUShort

    #region Extensions
    public static void SaveUncompressed(this Texture2D aTex, System.IO.Stream aStream, DataFormat aFormat)
    {
        using (var writer = new System.IO.BinaryWriter(aStream))
        {
            if (aFormat == DataFormat.ARGBFloat)
                SaveARGBFloatUncompressed(aTex, writer);
            else if (aFormat == DataFormat.ARGBUShort)
                SaveARGBUShortUncompressed(aTex, writer);
        }
    }
    public static void ReadUncompressed(this Texture2D aTex, System.IO.Stream aStream)
    {
        using (var reader = new System.IO.BinaryReader(aStream))
        {
            var format = (DataFormat)reader.ReadInt32();
            if (format == DataFormat.ARGBFloat)
                ReadARGBFloatUncompressed(aTex, reader);
            else if (format == DataFormat.ARGBUShort)
                ReadARGBUShortUncompressed(aTex, reader);
        }
    }

#if !UNITY_WEBPLAYER && !UNITY_WEBGL
    // File IO versions
    public static void ReadUncompressed(this Texture2D aTex, string aFilename)
    {
        using (var file = System.IO.File.OpenRead(aFilename))
        {
            aTex.ReadUncompressed(file);
            file.Close();
        }
    }
    public static void SaveUncompressed(this Texture2D aTex, string aFilename, DataFormat aFormat)
    {
        using (var file = System.IO.File.Create(aFilename))
        {
            aTex.SaveUncompressed(file, aFormat);
            file.Close();
        }
    }
#endif

    public static Texture2D CreateReadableTexture(this Texture2D source)
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
        Texture2D readableText = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }

    #endregion Extensions
}
