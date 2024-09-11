using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;

namespace ROUtils.DataTypes
{
    public abstract class PersistentListBase<T> : List<T>, IConfigNode
    {
        protected CollectionPersistence<T> _persister;

        public virtual void Load(ConfigNode node)
        {
            _persister.Load(node);
        }

        public void Save(ConfigNode node)
        {
            _persister.Save(node);
        }
    }

    public class PersistentList<T> : PersistentListBase<T>, ICloneable where T : IConfigNode
    {
        public PersistentList() { _persister = new CollectionPersistenceNode<T>(this); }

        public object Clone()
        {
            var clone = new PersistentList<T>();
            foreach (var v in this)
            {
                if (v is ICloneable c)
                {
                    clone.Add((T)c.Clone());
                }
                else
                {
                    ConfigNode n = new ConfigNode();
                    v.Save(n);
                    T item = (T)Activator.CreateInstance(v.GetType());
                    item.Load(n);
                    clone.Add(item);
                }
            }

            return clone;
        }
    }

    public class PersistentParsableList<T> : PersistentListBase<T> where T : class
    {
        public PersistentParsableList() { _persister = new CollectionPersistenceParseable<T>(this); }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentListValueType<T> : PersistentListBase<T>, ICloneable
    {
        public PersistentListValueType() { _persister = new CollectionPersistenceValueType<T>(this); }

        public object Clone()
        {
            var clone = new PersistentListValueType<T>();
            foreach (var v in this)
            {
                clone.Add(v);
            }

            return clone;
        }
    }

    /// <summary>
    /// KCT Observable list - has callbacks for add/remove/update
    /// Derives from PersistentList
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PersistentObservableList<T> : PersistentList<T> where T : IConfigNode
    {
        public event Action Updated = delegate { };
        public event Action<int, T> Added = delegate (int idx, T element) { };
        public event Action<int, T> Removed = delegate (int idx, T element) { };

        public new void Add(T item)
        {
            base.Add(item);
            Added(Count - 1, item);
            Updated();
        }

        public new bool Remove(T item)
        {
            int idx = IndexOf(item);
            if (idx >= 0)
            {
                base.RemoveAt(idx);
                Removed(idx, item);
                Updated();
                return true;
            }
            return false;
        }

        public new void RemoveAt(int index)
        {
            T item = this[index];
            base.RemoveAt(index);
            Removed(index, item);
            Updated();
        }

        public new void AddRange(IEnumerable<T> collection)
        {
            foreach (T item in collection)
            {
                base.Add(item);
                Added(Count - 1, item);
            }
            Updated();
        }

        public new void RemoveRange(int index, int count)
        {
            for (int i = index + count - 1; i >= index; i--)
            {
                T el = this[i];
                base.RemoveAt(i);
                Removed(i, el);
            }
            Updated();
        }

        public new void Clear()
        {
            T[] arr = ToArray();
            base.Clear();
            for (int i = arr.Length - 1; i >= 0; i--)
            {
                Removed(i, arr[i]);
            }
            Updated();
        }

        public new void Insert(int index, T item)
        {
            base.Insert(index, item);
            Added(index, item);
            Updated();
        }

        public new void InsertRange(int index, IEnumerable<T> collection)
        {
            foreach (T item in collection)
            {
                base.Insert(index++, item);
                Added(index - 1, item);
            }
            Updated();
        }

        public new int RemoveAll(Predicate<T> match)
        {
            int removed = 0;
            for (int i = Count - 1; i >= 0; --i)
            {
                T item = base[i];
                if (match(item))
                {
                    base.RemoveAt(i);
                    Removed(i, item);
                    ++removed;
                }
            }

            if (removed > 0)
                Updated();

            return removed;
        }

        public new T this[int index]
        {
            get
            {
                return base[index];
            }
            set
            {
                base[index] = value;
                Updated();
            }
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            for (int i = 0; i < Count; ++i)
            {
                Added(i, base[i]);
            }
            Updated();
        }
    }
}
