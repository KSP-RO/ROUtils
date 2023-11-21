using System;
using System.Collections.Generic;
using KSPCommunityFixes.Modding;
using UnityEngine;

namespace ROUtils.DataTypes
{
    // Due to lack of multiple inheritance, this is a bunch of copypasta.
    // Happily all each class needs is the add and remove methods and the AllValues collection/constructor

    public sealed class CollectionDictionaryAllValues<TKey, TValue, TCollection> : ICollection<TValue>, IEnumerable<TValue>, System.Collections.IEnumerable, System.Collections.ICollection, IReadOnlyCollection<TValue> where TCollection : ICollection<TValue>
    {
        public struct CollectionDictionaryEnumerator : IEnumerator<TValue>, IDisposable, System.Collections.IEnumerator
        {
            private Dictionary<TKey, TCollection>.ValueCollection _dictValues;
            private Dictionary<TKey, TCollection>.ValueCollection.Enumerator _dictEnum;
            private IEnumerator<TValue> _collEnum;
            private bool _isValid;

            private TValue currentValue;

            public TValue Current => currentValue;

            object System.Collections.IEnumerator.Current => currentValue;

            public CollectionDictionaryEnumerator(Dictionary<TKey, TCollection> dictionary)
            {
                _dictValues = dictionary.Values;
                _dictEnum = _dictValues.GetEnumerator();
                _isValid = _dictEnum.MoveNext();
                if (_isValid)
                    _collEnum = _dictEnum.Current.GetEnumerator();
                else
                    _collEnum = default;
                currentValue = default(TValue);
            }

            /// <summary>Releases all resources used by the <see cref="T:System.Collections.Generic.Dictionary`2.ValueCollection.Enumerator" />.</summary>
            public void Dispose()
            {
                if (_isValid)
                    _collEnum.Dispose();
                _dictEnum.Dispose();
            }

            public bool MoveNext()
            {
                // If dict was empty, do nothing
                if (!_isValid)
                {
                    currentValue = default(TValue);
                    return false;
                }

                // we start with a valid collection enumerator if dict
                // was non-empty. So start there.
                if (_collEnum.MoveNext())
                {
                    currentValue = _collEnum.Current;
                    return true;
                }

                // If we run off the edge of the collection, try to advance
                // to the next collection
                if (!_dictEnum.MoveNext())
                {
                    currentValue = default(TValue);
                    return false;
                }

                // We have a new collection, so dispose the old enumerator
                // and grab the new one. Then try again.
                _collEnum.Dispose();
                _collEnum = _dictEnum.Current.GetEnumerator();
                return MoveNext();
            }

            void System.Collections.IEnumerator.Reset()
            {
                if (_isValid)
                    _collEnum.Dispose();
                _dictEnum = _dictValues.GetEnumerator();
                _dictEnum.MoveNext();
                if (_isValid)
                    _collEnum = _dictEnum.Current.GetEnumerator();

                currentValue = default(TValue);
            }
        }

        private Dictionary<TKey, TCollection> _dict;

        public CollectionDictionaryAllValues(Dictionary<TKey, TCollection> dict) { _dict = dict; }

        public int Count
        {
            get
            {
                int c = 0;
                foreach (var coll in _dict.Values)
                    c += coll.Count;

                return c;
            }
        }

        bool ICollection<TValue>.IsReadOnly => true;

        bool System.Collections.ICollection.IsSynchronized => false;

        object System.Collections.ICollection.SyncRoot => ((System.Collections.ICollection)_dict).SyncRoot;

        public CollectionDictionaryEnumerator GetEnumerator() => new CollectionDictionaryEnumerator(_dict);
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new CollectionDictionaryEnumerator(_dict);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new CollectionDictionaryEnumerator(_dict);

        public void CopyTo(TValue[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (index < 0 || index > array.Length)
            {
                throw new ArgumentOutOfRangeException("index", index, "Index was out of range. Must be non-negative and less than the size of the collection.");
            }
            if (array.Length - index < Count)
            {
                throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.");
            }

            foreach (var coll in _dict.Values)
            {
                coll.CopyTo(array, index);
                index += coll.Count;
            }
        }

        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (array.Rank != 1)
            {
                throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", "array");
            }
            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException("The lower bound of target array must be zero.", "array");
            }
            if (index < 0 || index > array.Length)
            {
                throw new ArgumentOutOfRangeException("index", index, "Index was out of range. Must be non-negative and less than the size of the collection.");
            }
            if (array.Length - index < Count)
            {
                throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.");
            }
            TValue[] array2 = array as TValue[];
            if (array2 != null)
            {
                CopyTo(array2, index);
                return;
            }
            object[] array3 = array as object[];
            if (array3 == null)
            {
                throw new ArgumentException("Target array type is not compatible with the type of items in the collection.", "array");
            }

            try
            {
                foreach (var coll in _dict.Values)
                {
                    ((System.Collections.ICollection)coll).CopyTo(array3, index);
                    index += coll.Count;
                }
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException("Target array type is not compatible with the type of items in the collection.", "array");
            }
        }

        void ICollection<TValue>.Add(TValue item)
        {
            throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");
        }

        bool ICollection<TValue>.Remove(TValue item)
        {
            throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");
        }

        void ICollection<TValue>.Clear()
        {
            throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");
        }

        bool ICollection<TValue>.Contains(TValue item)
        {
            foreach (var coll in _dict.Values)
            {
                if (coll.Contains(item))
                    return true;
            }

            return false;
        }
    }

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

    public class PersistentDictionaryNodeKeyed<TValue, TCollection> : PersistentDictionaryNodeKeyed<TCollection>, IConfigNode where TCollection : ICollection<TValue>, IConfigNode, new()
    {
        private CollectionDictionaryAllValues<string, TValue, TCollection> _allValues;
        public CollectionDictionaryAllValues<string, TValue, TCollection> AllValues => _allValues;

        public PersistentDictionaryNodeKeyed() : base() { _allValues = new CollectionDictionaryAllValues<string, TValue, TCollection>(this); }

        public PersistentDictionaryNodeKeyed(string keyName) : base(keyName) { _allValues = new CollectionDictionaryAllValues<string, TValue, TCollection>(this); }

        public void Add(string key, TValue value)
        {
            if (!TryGetValue(key, out var coll))
            {
                coll = new TCollection();
                Add(key, coll);
            }

            coll.Add(value);
        }

        public bool Remove(string key, TValue value)
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
