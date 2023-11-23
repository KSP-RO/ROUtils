using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;
using UnityEngine;

namespace ROUtils.DataTypes
{
    public abstract class PersistentDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IConfigNode
    {
        protected DictionaryPersistence<TKey, TValue> _persister;

        public void Load(ConfigNode node)
        {
            _persister.Load(node);
        }

        public void Save(ConfigNode node)
        {
            _persister.Save(node);
        }
    }

    public class PersistentDictionaryBothObjects<TKey, TValue> : PersistentDictionary<TKey, TValue>, IConfigNode where TKey : IConfigNode where TValue : IConfigNode
    {
        public PersistentDictionaryBothObjects() { _persister = new DictionaryPersistenceBothObjects<TKey, TValue>(this); }
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryNodeValueKeyed<TKey, TValue> : PersistentDictionary<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        public PersistentDictionaryNodeValueKeyed() { _persister = new DictionaryPersistenceNodeValueKeyed<TKey, TValue>(this); }
        
        public PersistentDictionaryNodeValueKeyed(string keyName) { _persister = new DictionaryPersistenceNodeValueKeyed<TKey, TValue>(this, keyName); }
    }

    /// <summary>
    /// Loads a string from a node but stores as a separate int hashcode key
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryNodeStringHashKeyed<TValue> : PersistentDictionary<int, TValue>, IConfigNode where TValue : IConfigNode
    {
        public PersistentDictionaryNodeStringHashKeyed() { _persister = new DictionaryPersistenceNodeStringHashKeyed<TValue>(this); }

        public PersistentDictionaryNodeStringHashKeyed(string keyName) { _persister = new DictionaryPersistenceNodeStringHashKeyed<TValue>(this, keyName); }
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
        public PersistentDictionaryValueTypeKey() { _persister = new DictionaryPersistenceValueTypeKey<TKey, TValue>(this); }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypes<TKey, TValue> : PersistentDictionary<TKey, TValue>, ICloneable, IConfigNode
    {
        public PersistentDictionaryValueTypes() { _persister = new DictionaryPersistenceValueTypes<TKey, TValue>(this); }

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

    // Due to lack of multiple inheritance, this is a bunch of copypasta.
    // Happily all each class needs is the add and remove methods and the AllValues collection/constructor

    public class PersistentDictionaryBothObjects<TKey, TValue, TCollection> : PersistentDictionaryBothObjects<TKey, TCollection>, IConfigNode where TKey : IConfigNode where TCollection : ICollection<TValue>, IConfigNode, new()
    {
        private CollectionDictionaryAllValues<TKey, TValue, TCollection> _allValues;
        public CollectionDictionaryAllValues<TKey, TValue, TCollection> AllValues => _allValues;

        public PersistentDictionaryBothObjects() : base() { _allValues = new CollectionDictionaryAllValues<TKey, TValue, TCollection>(this); }

        public void Add(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
            {
                coll = new TCollection();
                Add(key, coll);
            }

            coll.Add(value);
        }

        public bool Remove(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
                return false;

            if (!coll.Remove(value))
                return false;

            if (coll.Count == 0)
                Remove(key);

            return true;
        }
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryNodeValueKeyed<TKey, TValue, TCollection> : PersistentDictionaryNodeValueKeyed<TKey, TCollection>, IConfigNode where TCollection : ICollection<TValue>, IConfigNode, new()
    {
        private CollectionDictionaryAllValues<TKey, TValue, TCollection> _allValues;
        public CollectionDictionaryAllValues<TKey, TValue, TCollection> AllValues => _allValues;

        public PersistentDictionaryNodeValueKeyed() : base() { _allValues = new CollectionDictionaryAllValues<TKey, TValue, TCollection>(this); }

        public PersistentDictionaryNodeValueKeyed(string keyName) : base(keyName) { _allValues = new CollectionDictionaryAllValues<TKey, TValue, TCollection>(this); }

        public void Add(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
            {
                coll = new TCollection();
                Add(key, coll);
            }

            coll.Add(value);
        }

        public bool Remove(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
                return false;

            if (!coll.Remove(value))
                return false;

            if (coll.Count == 0)
                Remove(key);

            return true;
        }
    }

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypeKey<TKey, TValue, TCollection> : PersistentDictionaryValueTypeKey<TKey, TCollection> where TCollection : ICollection<TValue>, IConfigNode, new()
    {
        private CollectionDictionaryAllValues<TKey, TValue, TCollection> _allValues;
        public CollectionDictionaryAllValues<TKey, TValue, TCollection> AllValues => _allValues;

        public PersistentDictionaryValueTypeKey() : base() { _allValues = new CollectionDictionaryAllValues<TKey, TValue, TCollection>(this); }

        public void Add(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
            {
                coll = new TCollection();
                Add(key, coll);
            }

            coll.Add(value);
        }

        public bool Remove(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
                return false;

            if (!coll.Remove(value))
                return false;

            if (coll.Count == 0)
                Remove(key);

            return true;
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentDictionaryValueTypes<TKey, TValue, TCollection> : PersistentDictionaryValueTypeKey<TKey, TCollection>, IConfigNode where TCollection : ICollection<TValue>, IConfigNode, new()
    {
        private CollectionDictionaryAllValues<TKey, TValue, TCollection> _allValues;
        public CollectionDictionaryAllValues<TKey, TValue, TCollection> AllValues => _allValues;

        public PersistentDictionaryValueTypes() : base() { _allValues = new CollectionDictionaryAllValues<TKey, TValue, TCollection>(this); }

        public void Add(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
            {
                coll = new TCollection();
                Add(key, coll);
            }

            coll.Add(value);
        }

        public bool Remove(TKey key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
                return false;

            if (!coll.Remove(value))
                return false;

            if (coll.Count == 0)
                Remove(key);

            return true;
        }
    }
}
