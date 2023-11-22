using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace ROUtils.DataTypes
{
    public abstract class PersistentSortedList<TKey, TValue> : SortedList<TKey, TValue>, IConfigNode
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

    /// <summary>
    /// This does not have a struct constraint because string is not a valuetype but can be handled by ConfigNode's parser
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentSortedListValueTypeKey<TKey, TValue> : PersistentSortedList<TKey, TValue> where TValue : IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceValueTypeKey<TKey, TValue> _handler;

        public PersistentSortedListValueTypeKey() { _handler = new IDictionaryPersistenceValueTypeKey<TKey, TValue>(this); }
    }

    public class PersistentSortedListNodeValueKeyed<TKey, TValue> : PersistentSortedList<TKey, TValue>, IConfigNode where TValue : IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceNodeValueKeyed<TKey, TValue> _handler;

        public PersistentSortedListNodeValueKeyed() { _handler = new IDictionaryPersistenceNodeValueKeyed<TKey, TValue>(this); }

        public PersistentSortedListNodeValueKeyed(string keyName) { _handler = new IDictionaryPersistenceNodeValueKeyed<TKey, TValue>(this, keyName); }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class PersistentSortedListValueTypes<TKey, TValue> : PersistentSortedList<TKey, TValue>, IConfigNode
    {
        [Persistent]
        protected IDictionaryPersistenceValueTypes<TKey, TValue> _handler;

        public PersistentSortedListValueTypes() { _handler = new IDictionaryPersistenceValueTypes<TKey, TValue>(this); }
    }
}
