using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace ROUtils.DataTypes
{
    public abstract class PersistentHashSetBase<T> : HashSet<T>, IConfigNode
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

    public class PersistentHashSet<T> : PersistentHashSetBase<T> where T : IConfigNode
    {
        [Persistent]
        protected ICollectionPersistenceNode<T> _helper;

        public PersistentHashSet() { _helper = new ICollectionPersistenceNode<T>(this); }
    }

    public class PersistentParsableHashSet<T> : PersistentHashSetBase<T> where T : class
    {
        [Persistent]
        protected ICollectionPersistenceParseable<T> _helper;

        public PersistentParsableHashSet() { _helper = new ICollectionPersistenceParseable<T>(this); }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentHashSetValueType<T> : PersistentHashSetBase<T>, ICloneable
    {
        [Persistent]
        protected ICollectionPersistenceValueType<T> _helper;

        public PersistentHashSetValueType() { _helper = new ICollectionPersistenceValueType<T>(this); }

        public object Clone()
        {
            var clone = new PersistentHashSetValueType<T>();
            foreach (var v in this)
            {
                clone.Add(v);
            }

            return clone;
        }
    }
}
