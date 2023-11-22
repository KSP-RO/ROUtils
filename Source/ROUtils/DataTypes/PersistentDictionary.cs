using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;
using UnityEngine;

namespace ROUtils.DataTypes
{
    public abstract class PersistentDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IConfigNode
    {
        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }

    public class PersistentDictionaryBothObjects<TKey, TValue> : PersistentDictionary<TKey, TValue>, IConfigNode where TKey : IConfigNode where TValue : IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceBothObjects<TKey, TValue> _handler;

        public PersistentDictionaryBothObjects() { _handler = new IDictionaryPersistenceBothObjects<TKey, TValue>(this); }
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryNodeValueKeyed<TKey, TValue> : PersistentDictionary<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceNodeValueKeyed<TKey, TValue> _handler;

        public PersistentDictionaryNodeValueKeyed() { _handler = new IDictionaryPersistenceNodeValueKeyed<TKey, TValue>(this); }
        
        public PersistentDictionaryNodeValueKeyed(string keyName) { _handler = new IDictionaryPersistenceNodeValueKeyed<TKey, TValue>(this, keyName); }
    }

    /// <summary>
    /// String-only version of this, for back-compat
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryNodeKeyed<TValue> : PersistentDictionaryNodeValueKeyed<string, TValue>, IConfigNode where TValue : IConfigNode
    {
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypeKey<TKey, TValue> : PersistentDictionary<TKey, TValue> where TValue : IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceValueTypeKey<TKey, TValue> _handler;

        public PersistentDictionaryValueTypeKey() { _handler = new IDictionaryPersistenceValueTypeKey<TKey, TValue>(this); }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypes<TKey, TValue> : PersistentDictionary<TKey, TValue>, ICloneable, IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceValueTypes<TKey, TValue> _handler;

        public PersistentDictionaryValueTypes() { _handler = new IDictionaryPersistenceValueTypes<TKey, TValue>(this); }

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
