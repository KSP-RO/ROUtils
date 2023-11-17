using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;
using UnityEngine;

namespace ROUtils.DataTypes
{
    public abstract class PersistentDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        protected static readonly Type _ValueType = typeof(TValue);
        protected static readonly string _ValueTypeName = typeof(TValue).Name;
        protected static readonly Dictionary<string, Type> _TypeCache = new Dictionary<string, Type>();

        protected int version; // will be set on load but not used on save

        protected abstract TKey GetKey(int i, ConfigNode keyNode);
        protected abstract void AddKey(TKey key, ConfigNode keyNode);

        protected TValue GetValue(int i, ConfigNode valueNode)
        {
            var n = valueNode.nodes[i];
            TValue value;
            if (version == 1 || n.name == "VALUE" || n.name == _ValueTypeName)
            {
                value = Activator.CreateInstance<TValue>();
            }
            else
            {
                if (!_TypeCache.TryGetValue(n.name, out var type))
                    type = HarmonyLib.AccessTools.TypeByName(n.name);
                if (type == null || !_ValueType.IsAssignableFrom(type))
                    type = _ValueType;
                else
                    _TypeCache[n.name] = type;

                value = (TValue)Activator.CreateInstance(type);
            }
            value.Load(n);
            return value;
        }

        protected void AddValue(TValue value, ConfigNode valueNode)
        {
            var type = value.GetType();
            ConfigNode n = new ConfigNode(type == _ValueType ? _ValueTypeName : type.FullName);
            value.Save(n);
            valueNode.AddNode(n);
        }

        public void Load(ConfigNode node)
        {
            Clear();
            ConfigNode keyNode = node.nodes[0];
            ConfigNode valueNode = node.nodes[1];
            version = 1;
            node.TryGetValue("version", ref version);

            for (int i = 0; i < valueNode.nodes.Count; ++i)
            {
                TKey key = GetKey(i, keyNode);
                TValue value = GetValue(i, valueNode);
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("version", 2);
            ConfigNode keyNode = node.AddNode("Keys");
            ConfigNode valueNode = node.AddNode("Values");

            foreach (var kvp in this)
            {
                AddKey(kvp.Key, keyNode);
                AddValue(kvp.Value, valueNode);
            }
        }
    }

    public class PersistentDictionaryBothObjects<TKey, TValue> : PersistentDictionary<TKey, TValue>, IConfigNode where TKey : IConfigNode where TValue : IConfigNode
    {
        protected static readonly Type _KeyType = typeof(TKey);
        protected static readonly string _KeyTypeName = typeof(TKey).Name;

        protected override TKey GetKey(int i, ConfigNode keyNode)
        {
            var n = keyNode.nodes[i];
            TKey key;
            if (version == 1 || n.name == "KEY" || n.name == _KeyTypeName)
            {
                key = Activator.CreateInstance<TKey>();
            }
            else
            {
                if (!_TypeCache.TryGetValue(n.name, out var type))
                    type = HarmonyLib.AccessTools.TypeByName(n.name);
                if (type == null || !_KeyType.IsAssignableFrom(type))
                    type = _KeyType;
                else
                    _TypeCache[n.name] = type;

                key = (TKey)Activator.CreateInstance(type);
            }
            key.Load(n);
            return key;
        }

        protected override void AddKey(TKey key, ConfigNode keyNode)
        {
            var type = key.GetType();
            ConfigNode n = new ConfigNode(type == _KeyType ? _KeyTypeName : type.FullName);
            key.Save(n);
            keyNode.AddNode(n);
        }
    }

    public class PersistentDictionaryNodeKeyed<TValue> : PersistentDictionary<string, TValue>, IConfigNode where TValue : IConfigNode
    {
        private string _keyName = "name";

        public PersistentDictionaryNodeKeyed() {}
        
        public PersistentDictionaryNodeKeyed(string keyName)
        {
            _keyName = keyName;
        }

        protected override string GetKey(int i, ConfigNode keyNode)
        {
            return keyNode.nodes[i].GetValue(_keyName);
        }

        protected override void AddKey(string key, ConfigNode keyNode)
        {
            keyNode.SetValue(_keyName, key, true);
        }

        public new void Load(ConfigNode node)
        {
            Clear();
            version = 1;
            node.TryGetValue("version", ref version);
            for (int i = 0; i < node.nodes.Count; ++i)
            {
                string key = GetKey(i, node);
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogError("PersistentDictionaryNodeKeyed: null or empty key in node! Skipping. Node=\n" + node.nodes[i].ToString());
                    continue;
                }

                TValue value = GetValue(i, node);
                Add(key, value);
            }
        }

        public new void Save(ConfigNode node)
        {
            node.AddValue("version", 2);
            foreach (var kvp in this)
            {
                AddValue(kvp.Value, node); // this creates the node
                // and it will be the last node. So put the key there.
                node.nodes[node.nodes.Count - 1].SetValue(_keyName, kvp.Key, true);
            }
        }
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypeKey<TKey, TValue> : PersistentDictionary<TKey, TValue> where TValue : IConfigNode
    {
        private static readonly Type _KeyType = typeof(TKey);
        private static readonly DataType _KeyDataType = FieldData.ValueDataType(_KeyType);

        protected override TKey GetKey(int i, ConfigNode keyNode)
        {
            return (TKey)FieldData.ReadValue(keyNode.values[i].value, _KeyDataType, _KeyType);
        }

        protected override void AddKey(TKey key, ConfigNode keyNode)
        {
            keyNode.AddValue("key", FieldData.WriteValue(key, _KeyDataType));
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypes<TKey, TValue> : Dictionary<TKey, TValue>, ICloneable, IConfigNode
    {
        private static Type _KeyType = typeof(TKey);
        private static readonly DataType _KeyDataType = FieldData.ValueDataType(_KeyType);
        private static Type _ValueType = typeof(TValue);
        private static readonly DataType _ValueDataType = FieldData.ValueDataType(_ValueType);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                TKey key = (TKey)FieldData.ReadValue(v.name, _KeyDataType, _KeyType);
                TValue value = (TValue)FieldData.ReadValue(v.value, _ValueDataType, _ValueType);
                if (ContainsKey(key))
                {
                    Debug.LogError($"PersistentDictionary: Contains key {key}");
                    Remove(key);
                }
                Add(key, value);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var kvp in this)
            {

                string key = FieldData.WriteValue(kvp.Key, _KeyDataType);
                string value = FieldData.WriteValue(kvp.Value, _ValueDataType);
                node.AddValue(key, value);
            }
        }

        public void Clone(PersistentDictionaryValueTypes<TKey, TValue> source)
        {
            Clear();
            foreach (var kvp in source)
                Add(kvp.Key, kvp.Value);
        }

        public object Clone()
        {
            var dict = new PersistentDictionaryValueTypes<TKey, TValue>();
            foreach (var kvp in this)
                dict.Add(kvp.Key, kvp.Value);

            return dict;
        }

        public static bool AreEqual(Dictionary<TKey, TValue> d1, Dictionary<TKey, TValue> d2)
        {
            if (d1.Count != d2.Count) return false;
            foreach (TKey key in d1.Keys)
                if (!d2.TryGetValue(key, out TValue val) || !val.Equals(d1[key]))
                    return false;

            return true;
        }
    }
}
