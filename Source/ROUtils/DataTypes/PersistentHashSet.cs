﻿using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace ROUtils.DataTypes
{
    public class PersistentHashSet<T> : HashSet<T>, IConfigNode where T : IConfigNode
    {
        private static readonly Type _Type = typeof(T);
        private static readonly string _TypeName = typeof(T).Name;
        private static readonly Dictionary<string, Type> _TypeCache = new Dictionary<string, Type>();

        public void Load(ConfigNode node)
        {
            Clear();
            int version = 1;
            node.TryGetValue("version", ref version);

            foreach (ConfigNode n in node.nodes)
            {
                T item;
                if (version == 1 || n.name == "ITEM" || n.name == _TypeName)
                {
                    item = Activator.CreateInstance<T>();
                }
                else
                {
                    if (!_TypeCache.TryGetValue(n.name, out var type))
                        type = HarmonyLib.AccessTools.TypeByName(n.name);
                    if (type == null || !_Type.IsAssignableFrom(type))
                        type = _Type;
                    else
                        _TypeCache[n.name] = type;

                    item = (T)Activator.CreateInstance(type);
                }
                item.Load(n);
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 2);
            foreach (var item in this)
            {
                var type = item.GetType();
                ConfigNode n = new ConfigNode(type == _Type ? _TypeName : type.FullName);
                item.Save(n);
                node.AddNode(n);
            }
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentHashSetValueType<T> : HashSet<T>, ICloneable, IConfigNode
    {
        private readonly static Type _Type = typeof(T);
        private readonly static DataType _DataType = FieldData.ValueDataType(_Type);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                T item = (T)FieldData.ReadValue(v.value, _DataType, _Type);
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var item in this)
            {
                node.AddValue("item", FieldData.WriteValue(item, _DataType));
            }
        }

        public object Clone()
        {
            var clone = new PersistentHashSetValueType<T>();
            foreach (var key in this)
                clone.Add(key);

            return clone;
        }
    }
}
