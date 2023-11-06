using System.Collections.Generic;
using UnityEngine;
using System;

namespace ROUtils
{
    public static class MiscUtils
    {
        public static TValue ValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            dict.TryGetValue(key, out TValue value);
            return value;
        }

        public static T Pop<T>(this List<T> list)
        {
            T val = list[0];
            list.RemoveAt(0);
            return val;
        }

        public static Texture2D GetReadableCopy(this Texture2D texture)
        {
            Texture2D readable = new Texture2D(texture.width, texture.height);

            RenderTexture tmp = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            
            readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readable;
        }

        public static Sprite ReplaceSprite(Sprite sprite, string path, SpriteMeshType type)
        {
            return Sprite.Create(GameDatabase.Instance.GetTexture(path, false),
                sprite.rect,
                sprite.pivot,
                sprite.pixelsPerUnit,
                0,
                type,
                sprite.border);
        }

        public static string ToCommaString<T>(this List<T> list)
        {
            if (list.Count == 0)
                return string.Empty;

            var sb = StringBuilderCache.Acquire();
            sb.Append(list[0].ToString());
            for (int i = 1, iC = list.Count; i < iC; ++i)
                sb.Append(", " + list[i].ToString());

            return sb.ToStringAndRelease();
        }

        /// <summary>
        /// NOTE: Must be used only on value-type lists
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="str"></param>
        public static void FromCommaString<T>(this List<T> list, string str)
        {
            Type type = typeof(T);
            KSPCommunityFixes.Modding.DataType dataType = KSPCommunityFixes.Modding.FieldData.ValueDataType(type);

            list.Clear();
            var split = str.Split(',');
            foreach (var s in split)
            {
                string s2 = s.Trim();
                if (s2.Length == 0)
                    continue;
                list.Add((T)KSPCommunityFixes.Modding.FieldData.ReadValue(s2, dataType, type));
            }
        }

        public static bool AllTrue(this List<bool> list)
        {
            for (int i = list.Count; i-- > 0;)
                if (!list[i])
                    return false;

            return true;
        }

        public static bool AllFalse(this List<bool> list)
        {
            for (int i = list.Count; i-- > 0;)
                if (list[i])
                    return false;

            return true;
        }
    }
}
