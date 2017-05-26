﻿using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Insert_Creative_Name
{
    internal static class Utility
    {
        private static Random m_random = new Random();

        internal static int Random()
        {
            return m_random.Next();
        }

        internal static int Random(int maxValue)
        {
            return m_random.Next(maxValue);
        }

        internal static int Random(int minValue, int maxValue)
        {
            return m_random.Next(minValue, maxValue);
        }

        internal static byte[] ImageToByteArray(Image img)
        {
            byte[] byteArray = new byte[0];
            using (MemoryStream stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Close();

                byteArray = stream.ToArray();
            }
            return byteArray;
        }
        internal static string GetHash(string str)
        {
            return Encoding.ASCII.GetString(MD5.Create().ComputeHash(Encoding.ASCII.GetBytes(str)));
        }
    }
}
