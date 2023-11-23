using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace ROUtils.DataTypes
{
    public abstract class PersistentHashSetBase<T> : HashSet<T>, IConfigNode
    {
        protected CollectionPersistence<T> _persister;

        public void Load(ConfigNode node)
        {
            _persister.Load(node);
        }

        public void Save(ConfigNode node)
        {
            _persister.Save(node);
        }
    }

    public class PersistentHashSet<T> : PersistentHashSetBase<T> where T : IConfigNode
    {
        public PersistentHashSet() { _persister = new CollectionPersistenceNode<T>(this); }
    }

    public class PersistentParsableHashSet<T> : PersistentHashSetBase<T> where T : class
    {
        public PersistentParsableHashSet() { _persister = new CollectionPersistenceParseable<T>(this); }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentHashSetValueType<T> : PersistentHashSetBase<T>, ICloneable
    {
        public PersistentHashSetValueType() { _persister = new CollectionPersistenceValueType<T>(this); }

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
