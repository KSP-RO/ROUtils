﻿using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ROUtils
{
    //Taken from Procedural Parts
    public static class ObjectSerializer
    {
        public static byte[] Serialize<T>(T obj)
        {
            MemoryStream stream = new MemoryStream();
            using (stream)
            {
                BinaryFormatter fmt = new BinaryFormatter();
                fmt.Serialize(stream, obj);
            }
            return stream.ToArray();
        }

        public static T Deserialize<T>(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            {
                BinaryFormatter fmt = new BinaryFormatter();
                return (T)fmt.Deserialize(stream);
            }
        }

        public static string Base64Encode(byte[] bytes)
        {
            return System.Convert.ToBase64String(bytes);
        }

        public static string Base64EncodeString(string text)
        {
            return Base64Encode(System.Text.Encoding.UTF8.GetBytes(text));
        }

        public static byte[] Base64Decode(string base64EncodedData)
        {
            return System.Convert.FromBase64String(base64EncodedData);
        }

        public static string Base64DecodeString(string base64Text)
        {
            return System.Text.Encoding.UTF8.GetString(Base64Decode(base64Text));
        }

        public static byte[] Zip(string text)
        {
            if (text == null)
                return null;

            byte[] input = System.Text.Encoding.UTF8.GetBytes(text);

            using (var memOutput = new MemoryStream())
            {
                var zip = new Ionic.Zip.ZipFile();
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestCompression;
                zip.AddEntry("a", input);
                zip.Save(memOutput);

                return memOutput.ToArray();
            }
        }

        public static string UnZip(byte[] bytes)
        {
            if (bytes == null)
                return null;

            using (var memInput = new MemoryStream(bytes))
            using (var zipStream = new Ionic.Zip.ZipInputStream(memInput))
            using (var reader = new StreamReader(zipStream))
            {
                zipStream.GetNextEntry();
                return reader.ReadToEnd();
            }
        }
    }
}
