using UnityEngine;
using System.Collections.Generic;
using System;

namespace ROUtils
{
    /// <summary>
    /// This lets us use CachedObject<T> to
    /// avoid retyping the caching code
    /// </summary>
    public interface IResettable
    {
        void Reset();
    }

    /// <summary>
    /// A simple object cache, will support any class
    /// with a parameterless constructor
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CachedObject<T> where T : class, new()
    {
        protected List<T> objects = new List<T>();
        protected int active = 0;

        /// <summary>
        /// Returns a new T from the cache or, if there aren't
        /// any free, creates a new one and adds it to the cache
        /// </summary>
        /// <returns></returns>
        public T Next()
        {
            if (active < objects.Count)
                return objects[active++];

            var next = new T();
            ++active;
            objects.Add(next);
            return next;
        }

        /// <summary>
        /// Frees an object, returning it to the cache
        /// and compacting the active set
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>true if the object is found in the cache</returns>
        public bool Free(T obj)
        {
            for (int i = active; i-- > 0;)
            {
                var o = objects[i];
                if (o == obj)
                {
                    --active;
                    objects[i] = objects[active];
                    objects[active] = obj;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fully clears the cache. This will expose the objects to GC!
        /// </summary>
        public void Clear()
        {
            objects.Clear();
            active = 0;
        }
    }

    /// <summary>
    /// A simple object cache, will support any class with
    /// a parameterless constructor that implements IResettable.
    /// It will reset objects when freeing them, and adds the Reset
    /// method that resets all objects but leaves them in the cache
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ResettableCachedObject<T> : CachedObject<T> where T : class, IResettable, new()
    {
        /// <summary>
        /// Frees an object, resetting it, returning it
        /// to the cache, and compacting the active set
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>true if the object is found in the cache</returns>
        public new bool Free(T obj)
        {
            if (base.Free(obj))
            {
                obj.Reset();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets all objects in the cache and
        /// makes them all available
        /// </summary>
        public void Reset()
        {
            for (int i = active; i-- > 0;)
                objects[i].Reset();

            active = 0;
        }
    }
}
