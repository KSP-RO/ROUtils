using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ROUtils
{
    /// <summary>
    /// A collection of keys mapping times to values, interpolated between keys by a cubic Hermite spline. <para/>
    /// The implementation produce identical results as a Unity <see cref="AnimationCurve"/> (unless keys in/out weights are used) but
    /// calling <see cref="Evaluate"/> is around 2.5 times faster. <para/>
    /// It also provide a bunch of utility methods for analyzing and constructing the curve from code, including various ways to
    /// auto-compute the keys tangents.
    /// </summary>
    public class HermiteCurve : IEnumerable<HermiteCurve.Key>, IConfigNode
    {
        /// <summary>
        /// Define how the key tangents will be adjusted when <see cref="HermiteCurve.AutoAdjustTangents"/> is <see langword="true"/>
        /// or when <see cref="HermiteCurve.ForceAdjustTangents"/> is called.
        /// </summary>
        /// 
        [Flags]
        public enum TangentMode
        {
            /// <summary>
            /// Default value, will be set to <see cref="ManualDissociated"/> on adding keys to the curve.
            /// </summary>
            None = 0,
            /// <summary>
            /// Having <see cref="HermiteCurve.AutoAdjustTangents"/> set to <see langword="true"/> or calling <see cref="HermiteCurve.ForceAdjustTangents"/>
            /// will have no effect on this key tangents.
            /// </summary>
            ManualDissociated = 1 << 0,
            /// <summary>
            /// Having <see cref="HermiteCurve.AutoAdjustTangents"/> set to <see langword="true"/> or calling <see cref="HermiteCurve.ForceAdjustTangents"/>
            /// will make the key in and out tangents averaged to a common value if they are different.
            /// </summary>
            ManualEqual = 1 << 1,
            /// <summary>
            /// The curve will be flat from the previous key value to this key,
            /// with an instant transition to this key value up until the next key.<para/>
            /// This setting effectively cause the adjacent keys tangents to be ignored.<para/>
            /// The key tangents will be set to <see cref="double.PositiveInfinity"/>
            /// </summary>
            Step = 1 << 2,
            /// <summary>
            /// The curve will be flat at the transition with this key.<para/>
            /// The key tangents will be set to <value>0.0</value>.
            /// </summary>
            Flat = 1 << 3,
            /// <summary>
            /// The tangents will point toward the adjacent keys, with a hard angle at this key (instead of a smooth transition).<para/>
            /// This can be used to produce straight lines between keys.
            /// </summary>
            Straight = 1 << 4,
            /// <summary>
            /// The tangents will be adjusted as to have unrestricted, very smooth curved transitions between keys.<para/>
            /// In this mode, points inducing drastic slope changes will often result in concave or convex subsections that might induce
            /// local inversions of the curve, and might cause overshoot of this key or the adjacent keys values.
            /// </summary>
            Smooth = 1 << 5,
            /// <summary>
            /// Identical to the <see cref="Smooth"/> mode, but the tangents will be clamped to reduce the occurence of concave or convex
            /// subsections, and to guarantee that between two keys in this mode, the value can't overshoot these keys values.<para/>
            /// This is the recommended mode to get predictable results when creating curves from arbitrary times and values without
            /// manual/visual editing of the tangents.
            /// </summary>
            SmoothClamped = 1 << 9,
            /// <summary>
            /// Bitmask for manual modes
            /// </summary>
            Manual = ManualDissociated | ManualEqual,
            /// <summary>
            /// Bitmask for automatic modes
            /// </summary>
            Auto = Step | Flat | Straight | Smooth | SmoothClamped
        }

        /// <summary>
        /// A key defining a point on the curve and its tangents.
        /// </summary>
        public struct Key
        {
            /// <summary>
            /// The time of the key (the value of this key on the horizontal axis of the curve graph).
            /// </summary>
            public double time;

            /// <summary>
            /// The value of the key (the value of this key on the vertical axis of the curve graph).
            /// </summary>
            public double value;

            /// <summary>
            /// The incoming tangent affects the slope of the curve from the previous key to this key.
            /// </summary>
            /// <remarks>This can be set to <see cref="double.PositiveInfinity"/> to have last key value being constant up to this key.</remarks>
            public double inTangent;

            /// <summary>
            /// The outgoing tangent affects the slope of the curve from this key to the next key.
            /// </summary>
            /// <remarks>This can be set to <see cref="double.PositiveInfinity"/> to get a constant value from this key to the next one.</remarks>
            public double outTangent;

            /// <summary>
            /// Define how a key tangent will be adjusted when <see cref="HermiteCurve.AutoAdjustTangents"/> is <see langword="true"/>
            /// or when <see cref="HermiteCurve.ForceAdjustTangents"/> is called.
            /// </summary>
            public TangentMode tangentMode;

            /// <summary>
            /// Initializes a new instance of the <see cref="Key"/> structure with the specified parameters.
            /// </summary>
            /// <param name="time">The time of the key (the value of this key on the horizontal axis of the curve graph).</param>
            /// <param name="value">The value of the key (the value of this key on the vertical axis of the curve graph).</param>
            /// <param name="inTangent">The incoming tangent affects the slope of the curve from the previous key to this key.</param>
            /// <param name="outTangent">The outgoing tangent affects the slope of the curve from this key to the next key.</param>
            public Key(double time, double value, double inTangent, double outTangent)
            {
                this.time = time;
                this.value = value;
                this.inTangent = inTangent;
                this.outTangent = outTangent;
                tangentMode = TangentMode.ManualDissociated;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Key"/> structure with the specified time and value.<para/>
            /// The tangents will be adjusted according to the specified <see cref="TangentMode"/> if <see cref="HermiteCurve.AutoAdjustTangents"/>
            /// is <see langword="true"/> or by calling <see cref="HermiteCurve.ForceAdjustTangents"/>.
            /// </summary>
            /// <param name="time">The time of the key (the value of this key on the horizontal axis of the curve graph).</param>
            /// <param name="value">The value of the key (the value of this key on the vertical axis of the curve graph).</param>
            /// <param name="tangentMode">The tangent mode that will be used to adjust the key tangents.</param>
            public Key(double time, double value, TangentMode tangentMode = TangentMode.ManualEqual)
            {
                if (!tangentMode.IsDefinedFlag())
                    tangentMode = TangentMode.ManualEqual;

                this.time = time;
                this.value = value;
                this.tangentMode = tangentMode;
                inTangent = 0.0;
                outTangent = 0.0;
            }

            public override string ToString()
            {
                if (inTangent == 0.0 && outTangent == 0.0)
                    return $"T: {time} | V: {value}";

                return $"T: {time} | V: {value} | Tin: {inTangent} | Tout: {outTangent}";
            }

            public static explicit operator Keyframe(Key key)
            {
                return new Keyframe((float)key.time, (float)key.value, (float)key.inTangent, (float)key.outTangent);
            }

            public static explicit operator Key(Keyframe key)
            {
                return new Key(key.time, key.value, key.inTangent, key.outTangent);
            }
        }

        /// <summary>
        /// A cache structure storing the polynomial coefficients for a curve segment, as well as the start time of the segment.
        /// </summary>
        private struct Range
        {
            public double a, b, c, d, minTime;
        }

        /// <summary>
        /// A cache structure storing the polynomial coefficients for a curve segment derivative, as well as the start time of the segment.
        /// </summary>
        private struct DerivativeRange
        {
            public double a, b, c, minTime;
        }

        /// <summary>
        /// Enumerates the <see cref="Key"/> elements of a <see cref="HermiteCurve"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<Key>
        {
            private HermiteCurve _curve;
            private int _index;
            private int _version;
            private Key _key;

            public Key Current => _key;
            object IEnumerator.Current => _key;

            internal Enumerator(HermiteCurve curve)
            {
                _curve = curve;
                _index = 0;
                _version = curve._version;
                _key = default;
            }

            public bool MoveNext()
            {
                if (_version != _curve._version)
                    ThrowOnCollectionModified();

                if (_index < _curve._keyCount)
                {
                    _key = _curve._keys[_index];
                    _index++;
                    return true;
                }
                return false;
            }

            private static void ThrowOnCollectionModified()
            {
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            }

            public void Reset()
            {
                _index = 0;
                _key = default;
            }

            public void Dispose() { }
        }

        private bool _autoTangents;

        private Key[] _keys;
        private int _keyCount;
        private int _version;

        private int _rangeCount;
        private Range[] _ranges;

        private int _derivateRangeCount;
        private DerivativeRange[] _derivateRanges;

        private double _firstTime;
        private double _lastTime;
        private double _firstValue;
        private double _lastValue;

        /// <summary>
        /// Create a new empty curve.
        /// </summary>
        public HermiteCurve()
        {
            _keys = Array.Empty<Key>();
            CompileCurve();
        }

        /// <summary>
        /// Create a new curve from the provided keys.
        /// </summary>
        /// <param name="keys">The keys to add to the curve.</param>
        public HermiteCurve(params Key[] keys)
        {
            // sort the keys and remove any duplicate time
            _keyCount = SortKeysRemoveDuplicates(keys);
            _keys = keys;

            // ensure tangent mode is defined
            for (int i = _keyCount; i-- > 0;)
                if (!_keys[i].tangentMode.IsDefinedFlag())
                    _keys[i].tangentMode = TangentMode.ManualDissociated;

            CompileCurve();
        }

        /// <summary>
        /// Create a new curve with the same keys as a Unity <see cref="AnimationCurve"/>
        /// </summary>
        /// <param name="animationCurve">The unity AnimationCurve to copy keys from.</param>
        public HermiteCurve(AnimationCurve animationCurve)
        {
            // note : an Unity AnimationCurve cannot have same-time keys and the keys are always
            // sorted by time already, so we don't need to check again.
            Keyframe[] unityKeys = animationCurve.keys;
            _keyCount = unityKeys.Length;
            _keys = new Key[_keyCount];
            for (int i = 0; i < _keyCount; i++)
            {
                Keyframe unityKey = unityKeys[i];
                _keys[i] = new Key(unityKey.time, unityKey.value, unityKey.inTangent, unityKey.outTangent);
            }
            CompileCurve();
        }

        /// <summary>
        /// Create a new curve with the same keys as a KSP <see cref="FloatCurve"/>.
        /// </summary>
        /// <param name="kspFloatCurve">The FloatCurve to copy keys from.</param>
        public HermiteCurve(FloatCurve kspFloatCurve) : this(kspFloatCurve.fCurve) { }

        /// <summary>
        /// Create a copy of this curve instance.
        /// </summary>
        /// <returns>A newly instantiated clone of this curve.</returns>
        public HermiteCurve Clone()
        {
            HermiteCurve clone = new HermiteCurve();
            clone._keyCount = _keyCount;
            clone._keys = new Key[_keyCount];
            if (_keyCount > 0)
                Array.Copy(_keys, clone._keys, _keyCount);
            clone._autoTangents = _autoTangents;
            clone._firstTime = _firstTime;
            clone._firstValue = _firstValue;
            clone._lastTime = _lastTime;
            clone._lastValue = _lastValue;
            clone._rangeCount = _rangeCount;
            clone._derivateRangeCount = -1;
            if (_rangeCount > 0)
            {
                clone._ranges = new Range[_rangeCount];
                Array.Copy(_ranges, clone._ranges, _rangeCount);
            }
            return clone;
        }

        /// <summary>
        /// Set all keys in this curve to the keys of another curve.
        /// </summary>
        /// <param name="other">The other curve to copy the key from</param>
        public void CopyFrom(HermiteCurve other)
        {
            _keyCount = other._keyCount;
            _keys = new Key[_keyCount];
            if (_keyCount > 0)
                Array.Copy(other._keys, _keys, _keyCount);
            _autoTangents = other._autoTangents;
            _firstTime = other._firstTime;
            _firstValue = other._firstValue;
            _lastTime = other._lastTime;
            _lastValue = other._lastValue;
            _rangeCount = other._rangeCount;
            _derivateRangeCount = -1;
            if (other._rangeCount > 0)
            {
                _ranges = new Range[_rangeCount];
                Array.Copy(other._ranges, _ranges, _rangeCount);
            }
            _version++;
        }

        /// <summary>
        /// Create a new <see cref="AnimationCurve"/> instance with the same keys as the current instance.
        /// </summary>
        public AnimationCurve ToAnimationCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            Keyframe[] keys = new Keyframe[_keyCount];
            for (int i = _keyCount; i-- > 0;)
                keys[i] = (Keyframe)_keys[i];

            curve.keys = keys;
            return curve;
        }

        /// <summary>
        /// Set all keys on the specified <see cref="AnimationCurve"/> to match the keys of the current instance.
        /// </summary>
        public void ToAnimationCurve(AnimationCurve curve)
        {
            Keyframe[] keys = new Keyframe[_keyCount];
            for (int i = _keyCount; i-- > 0;)
                keys[i] = (Keyframe)_keys[i];

            curve.keys = keys;
        }

        /// <summary> Set the keys of this curve from a serialized list of keys. Any existing keys will be overriden.</summary>
        /// <param name="node">A <see cref="ConfigNode"/> with a list of keys.</param>
        /// <remarks>
        /// The implementation is cross-compatible with the expected format for a KSP <see cref="FloatCurve"/>.
        /// The delimiter between key parameters can be a space, a tab, a comma or a semicolon.
        /// <list type="bullet">
        /// <item>
        /// "<c>key = time value inTangent outTangent</c>"<para/>
        /// Adds a <see cref="Key"/> with the provided parameters. If all keys use this format, <see cref="AutoAdjustTangents"/> will be set to <see langword="false"/>.
        /// </item>
        /// <item>
        /// "<c>key = time value</c>"<para/>
        /// Adds a <see cref="Key"/> with the provided time and value, and <see cref="TangentMode.SmoothClamped"/>.
        /// If any such key is present, <see cref="AutoAdjustTangents"/> will be set to <see langword="true"/>.
        /// </item>
        /// <item>
        /// "<c>key = time value TangentMode</c>"<para/>
        /// Adds a <see cref="Key"/> with the provided time, value and <see cref="TangentMode"/>.
        /// <see cref="AutoAdjustTangents"/> will be set to <see langword="true"/>.
        /// </item>
        /// </list>
        /// </remarks>
        public void Load(ConfigNode node)
        {
            _autoTangents = false;
            _version++;
            _keyCount = 0;

            int keyValueCount = node.values.Count;
            if (_keys.Length < keyValueCount)
                _keys = new Key[keyValueCount];

            for (int i = 0; i < keyValueCount; i++)
            {
                ConfigNode.Value keyValue = node.values[i];
                if (keyValue.name != "key")
                    continue;

                string[] keyValues = keyValue.value.Split(FloatCurve.delimiters, StringSplitOptions.RemoveEmptyEntries);
                Key key;
                switch (keyValues.Length)
                {
                    case 4: // stock format
                        key = new Key(double.Parse(keyValues[0]), double.Parse(keyValues[1]), double.Parse(keyValues[2]), double.Parse(keyValues[3]));
                        break;
                    case 2: // stock format
                        key = new Key(double.Parse(keyValues[0]), double.Parse(keyValues[1]), TangentMode.SmoothClamped);
                        _autoTangents = true;
                        break;
                    case 3: // custom format allowing to define the tangent mode of the key 
                        if (!Enum.TryParse(keyValues[2], out TangentMode tangentMode) || tangentMode.Is(TangentMode.Manual))
                            tangentMode = TangentMode.SmoothClamped;
                        key = new Key(double.Parse(keyValues[0]), double.Parse(keyValues[1]), tangentMode);
                        _autoTangents = true;
                        break;
                    default:
                        Debug.LogError($"Invalid FloatCurve key : \"{keyValue.value}\"");
                        continue;
                }

                _keys[_keyCount] = key;
                _keyCount++;
            }

            _keyCount = SortKeysRemoveDuplicates(_keys);
            CompileCurve();
        }

        /// <summary>
        /// Serialize this curve keys as values in the provided <see cref="ConfigNode"/>.
        /// </summary>
        /// <param name="node">The <see cref="ConfigNode"/> to add keys to.</param>
        public void Save(ConfigNode node)
        {
            for (int i = 0; i < _keyCount; i++)
            {
                Key key = _keys[i];
                if (_autoTangents && key.tangentMode.Is(TangentMode.Auto))
                    node.AddValue("key", $"{key.time:G17} {key.value:G17} {key.tangentMode}");
                else
                    node.AddValue("key", $"{key.time:G17} {key.value:G17} {key.inTangent:G17} {key.outTangent:G17}");
            }
        }

        /// <summary>
        /// When <see langword="true" />, the curve keys tangents will be automatically adjusted according to the <see cref="TangentMode"/> defined in each key.
        /// </summary>
        public bool AutoAdjustTangents
        {
            get => _autoTangents;
            set
            {
                if (!_autoTangents && value)
                    ForceAdjustTangents();

                _autoTangents = value;
            }
        }

        /// <summary>
        /// The amount of keys in the curve.
        /// </summary>
        public int KeyCount => _keyCount;

        /// <summary>
        /// The time of the first key.
        /// </summary>
        public double FirstTime => _firstTime;

        /// <summary>
        /// The value of the first key.
        /// </summary>
        public double FirstValue => _firstValue;

        /// <summary>
        /// The time of the last key.
        /// </summary>
        public double LastTime => _lastTime;

        /// <summary>
        /// The value of the last key.
        /// </summary>
        public double LastValue => _lastValue;

        /// <summary>
        /// Gets or sets the key at the specified index
        /// </summary>
        /// <param name="keyIndex">The zero-based index of the key</param>
        /// <remarks>
        /// Setting the key will fail if a key with the same time exists already.
        /// Use <see cref="ReplaceKey"/> for better control.
        /// </remarks>
        public Key this[int keyIndex]
        {
            get
            {
                if ((uint)keyIndex >= (uint)_keyCount)
                    throw new ArgumentOutOfRangeException(nameof(keyIndex));

                return _keys[keyIndex];
            }
            set
            {
                ReplaceKey(keyIndex, value);
            }
        }

        /// <summary>
        /// Remove the key at the specified index, then add the provided key.
        /// </summary>
        /// <param name="keyIndex">The index of the key to remove.</param>
        /// <param name="newKey">The key to add.</param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        /// <returns>The index of the added key, or -1 if the operation failed because another key with the same time exists already.</returns>
        public int ReplaceKey(int keyIndex, Key newKey, bool forceAdjustTangents = false)
        {
            if ((uint)keyIndex >= (uint)_keyCount)
                throw new ArgumentOutOfRangeException(nameof(keyIndex));

            // ensure the tangent mode is defined
            if (!newKey.tangentMode.IsDefinedFlag())
                newKey.tangentMode = TangentMode.ManualDissociated;

            // fast path for when the time hasn't changed
            if (_keys[keyIndex].time == newKey.time)
            {
                _keys[keyIndex] = newKey;
            }
            else
            {
                // check if any other key has the same time
                for (int i = _keyCount; i-- > 0;)
                    if (i != keyIndex && _keys[i].time == newKey.time)
                        return -1;

                // first remove the key at the specified index
                int tempKeyCount = _keyCount - 1;
                if (keyIndex < tempKeyCount)
                    Array.Copy(_keys, keyIndex + 1, _keys, keyIndex, tempKeyCount - keyIndex);

                // now insert the new key
                for (int i = tempKeyCount; i-- > 0;)
                {
                    double currTime = _keys[i].time;

                    // if the added key time is greater than the current key, move all
                    // the upper keys and insert the new key in between to keep a sorted array.
                    if (newKey.time > currTime)
                    {
                        if (i < tempKeyCount - 1)
                            Array.Copy(_keys, i + 1, _keys, i + 2, tempKeyCount - i - 1);

                        keyIndex = i + 1;
                        _keys[keyIndex] = newKey;
                        break;
                    }

                    // if added time is lower than the other keys time, move the whole
                    // array and insert the new key at the first index.
                    if (i == 0)
                    {
                        Array.Copy(_keys, 0, _keys, 1, tempKeyCount);
                        keyIndex = 0;
                        _keys[0] = newKey;
                    }
                }
            }

            _version++;

            if (forceAdjustTangents && !_autoTangents)
                AdjustTangents();

            CompileCurve();
            return keyIndex;
        }

        /// <summary>
        /// Add a new key to the curve.
        /// </summary>
        /// <param name="key">The key to add to the curve.</param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        /// <returns>The index of the key if the key was added, or -1 if the operation failed because a key with the same time exists already.</returns>
        public int AddKey(Key key, bool forceAdjustTangents = false)
        {
            // ensure the tangent mode is defined
            if (!key.tangentMode.IsDefinedFlag())
                key.tangentMode = TangentMode.ManualDissociated;

            // resize the array if necessary
            IncreaseKeyCapacity();

            int keyIndex = -1;

            if (_keyCount == 0)
            {
                keyIndex = 0;
                _keys[0] = key;
            }
            else
            {
                for (int i = _keyCount; i-- > 0;)
                {
                    double currTime = _keys[i].time;

                    // if key time is a duplicate, can't add the key
                    if (key.time == currTime)
                        return -1;

                    // if the added key time is greater than the current key, move all
                    // the upper keys and insert the new key in between to keep a sorted array.
                    if (key.time > currTime)
                    {
                        if (i < _keyCount - 1)
                            Array.Copy(_keys, i + 1, _keys, i + 2, _keyCount - i - 1);

                        keyIndex = i + 1;
                        _keys[keyIndex] = key;
                        break;
                    }

                    // if added time is lower than the other keys time, move the whole
                    // array and insert the new key at the first index.
                    if (i == 0)
                    {
                        Array.Copy(_keys, 0, _keys, 1, _keyCount);
                        keyIndex = 0;
                        _keys[0] = key;
                    }
                }
            }

            _keyCount++;
            _version++;

            if (forceAdjustTangents && !_autoTangents)
                AdjustTangents();

            CompileCurve();
            return keyIndex;
        }

        /// <summary>
        /// Add a key to curve. If the key time match the time of an existing key, that key will be replaced.
        /// </summary>
        /// <param name="key">The key to add to the curve.</param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        /// <returns>The index of the key that was added or replaced.</returns>
        public int AddOrReplaceKey(Key key, bool forceAdjustTangents = false)
        {
            // ensure the tangent mode is defined
            if (!key.tangentMode.IsDefinedFlag())
                key.tangentMode = TangentMode.ManualDissociated;

            int keyIndex = default;
            if (_keyCount == 0)
            {
                IncreaseKeyCapacity();
                _keys[0] = key;
                keyIndex = 0;
                _keyCount++;
            }
            else
            {
                for (int i = _keyCount; i-- > 0;)
                {
                    double currTime = _keys[i].time;

                    // if key time is a duplicate, just replace the key.
                    if (key.time == currTime)
                    {
                        _keys[i] = key;
                        keyIndex = i;
                        break;
                    }

                    // if the added key time is greater than the current key, move all
                    // the upper keys and insert the new key in between to keep a sorted array.
                    if (key.time > currTime)
                    {
                        IncreaseKeyCapacity();
                        if (i < _keyCount - 1)
                            Array.Copy(_keys, i + 1, _keys, i + 2, _keyCount - i - 1);

                        keyIndex = i + 1;
                        _keys[keyIndex] = key;
                        _keyCount++;
                        break;
                    }

                    // if added time is lower than the other keys time, move the whole
                    // array and insert the new key at the first index.
                    if (i == 0)
                    {
                        IncreaseKeyCapacity();
                        Array.Copy(_keys, 0, _keys, 1, _keyCount);
                        _keys[0] = key;
                        keyIndex = 0;
                        _keyCount++;
                        break;
                    }
                }
            }

            _version++;

            if (forceAdjustTangents && !_autoTangents)
                AdjustTangents();

            CompileCurve();
            return keyIndex;
        }

        /// <summary>
        /// Add a new key to the curve.
        /// </summary>
        /// <param name="time">The time of the key.</param>
        /// <param name="value">The value of the curve at the key time.</param>
        /// <param name="inTangent">The incoming tangent affects the slope of the curve from the previous key to this key.</param>
        /// <param name="outTangent">The outgoing tangent affects the slope of the curve from this key to the next key.</param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        /// <returns>The index of the key if the key was added, or -1 if the operation failed because a key with the same time exists already.</returns>
        public int AddKey(double time, double value, double inTangent, double outTangent, bool forceAdjustTangents = false)
        {
            return AddKey(new Key(time, value, inTangent, outTangent), forceAdjustTangents);
        }

        /// <summary>
        /// Add a new key to the curve.
        /// </summary>
        /// <param name="time">The time of the key.</param>
        /// <param name="value">The value of the curve at the key time.</param>
        /// <param name="tangentMode">The tangent mode to use when automatically computing tangents.</param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        /// <returns>The index of the key if the key was added, or -1 if the operation failed because a key with the same time exists already.</returns>
        /// <remarks>
        /// The default values for the <paramref name="tangentMode"/> and <paramref name="forceAdjustTangents"/> parameters are matching the behavior of the <see cref="AnimationCurve.AddKey(float, float)"/> method.<para/>
        /// However, the default <see cref="TangentMode.Smooth"/> tangent mode can cause the curve to overshoot the value defined in the key. For more predictable results, use <see cref="TangentMode.SmoothClamped"/>.
        /// </remarks>
        public int AddKey(double time, double value, TangentMode tangentMode = TangentMode.Smooth, bool forceAdjustTangents = true)
        {
            return AddKey(new Key(time, value, tangentMode), forceAdjustTangents);
        }

        /// <summary>
        /// Remove a key from the curve at the given index
        /// </summary>
        /// <param name="keyIndex">The index of key to remove</param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        public void RemoveKey(int keyIndex, bool forceAdjustTangents = false)
        {
            if ((uint)keyIndex >= (uint)_keyCount)
                throw new ArgumentOutOfRangeException(nameof(keyIndex));

            _version++;
            _keyCount--;
            if (keyIndex < _keyCount)
                Array.Copy(_keys, keyIndex + 1, _keys, keyIndex, _keyCount - keyIndex);

            if (forceAdjustTangents && !_autoTangents)
                AdjustTangents();

            CompileCurve();
        }

        /// <summary>
        /// Set the tangent mode on all keys.
        /// </summary>
        /// <param name="tangentMode"></param>
        /// <param name="forceAdjustTangents">If <see langword="true" />, the curve tangents will be adjusted according to the keys <see cref="TangentMode"/> even if <see cref="AutoAdjustTangents"/> is <see langword="false" /></param>
        public void SetKeysTangentMode(TangentMode tangentMode, bool forceAdjustTangents = false)
        {
            if (!tangentMode.IsDefinedFlag())
                return;

            for (int i = _keyCount; i-- > 0;)
                _keys[i].tangentMode = tangentMode;

            if (forceAdjustTangents && !_autoTangents)
                AdjustTangents();

            CompileCurve();
        }

        /// <summary>
        /// Increase the key array size if necessary to be able to add a key.
        /// </summary>
        private void IncreaseKeyCapacity()
        {
            if (_keys.Length == _keyCount)
            {
                int newLength = _keyCount == 0 ? 4 : _keys.Length * 2;
                Key[] newKeys = new Key[newLength];
                Array.Copy(_keys, newKeys, _keyCount);
                _keys = newKeys;
            }
        }

        /// <summary>
        /// Sort the keys by ascending time
        /// </summary>
        private void SortKeys()
        {
            for (int i = 1; i < _keyCount; i++)
            {
                Key key = _keys[i];
                int j = i;
                while (j > 0 && _keys[j - 1].time > key.time)
                    _keys[j] = _keys[--j];

                _keys[j] = key;
            }
        }

        /// <summary>
        /// Sort the key array by ascending time, and put any duplicate at the end of the array.
        /// </summary>
        /// <param name="keys">The key array to sort.</param>
        /// <returns>The amount of non-duplicate keys after sorting.</returns>
        private static int SortKeysRemoveDuplicates(Key[] keys)
        {
            int duplicates = 0;
            int length = keys.Length;
            for (int i = 1; i < length; i++)
            {
                Key key = keys[i];
                int j = i;
                while (j > 0)
                {
                    ref Key prevKey = ref keys[j - 1];
                    if (key.time != double.NegativeInfinity && prevKey.time == key.time)
                    {
                        key.time = double.NegativeInfinity;
                        duplicates++;
                    }

                    if (prevKey.time > key.time)
                    {
                        keys[j] = prevKey;
                        j--;
                    }
                    else
                    {
                        break;
                    }
                }
                keys[j] = key;
            }

            if (duplicates > 0)
                Array.Copy(keys, duplicates - 1, keys, 0, keys.Length - duplicates);

            return length - duplicates;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the curve keys.
        /// </summary>
        public IEnumerator<Key> GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Returns an enumerator that iterates through the curve keys.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Evaluate the curve at the specified time.
        /// </summary>
        /// <param name="time">The time to evaluate (the horizontal axis in the curve graph).</param>
        /// <returns> The value of the curve at the specified time.</returns>
        public unsafe double Evaluate(double time)
        {
            if (time <= _firstTime)
                return _firstValue;
            if (time >= _lastTime)
                return _lastValue;

            int i = _rangeCount;
            fixed (Range* lastRangePtr = &_ranges[i - 1]) // avoid struct copying and array bounds checks
            {
                Range* rangePtr = lastRangePtr;
                while (i > 0)
                {
                    if (time >= rangePtr->minTime)
                        return rangePtr->d + time * (rangePtr->c + time * (rangePtr->b + time * rangePtr->a));

                    rangePtr--;
                    i--;
                }
            }
            return 0.0;
        }

        /// <summary>
        /// Evaluate the curve at the specified time, allowing evaluation before the first key time and after the last key time.
        /// </summary>
        /// <param name="time">The time to evaluate (the horizontal axis in the curve graph).</param>
        /// <returns> The value of the curve at the specified time.</returns>
        public unsafe double EvaluateUnclamped(double time)
        {
            int i = _rangeCount;

            if (i == 0)
                return _firstValue;

            fixed (Range* lastRangePtr = &_ranges[i - 1]) // avoid struct copying and array bounds checks
            {
                Range* rangePtr = lastRangePtr;
                while (true)
                {
                    i--;
                    if (time >= rangePtr->minTime || i == 0)
                        return rangePtr->d + time * (rangePtr->c + time * (rangePtr->b + time * rangePtr->a));

                    rangePtr--;
                }
            }
        }

        /// <summary>
        /// Find the slope of the tangent (the derivative) to the curve at the given time.
        /// </summary>
        /// <param name="time">The time to evaluate, must be within the min and max time defined by the curve.</param>
        /// <returns>The slope of the tangent (the derivative) at the given time</returns>
        public unsafe double FindTangent(double time)
        {
            if (time <= _firstTime)
                return _keys[0].outTangent;
            if (time >= _lastTime)
                return _keys[_rangeCount].inTangent;

            // As this method is unlikely to be called in common usages, we compile the ranges on-demand to avoid
            // additional overhead when manipulating the curve keys and compiling it. _derivateRangeCount is reset
            // to -1 everytime CompileCurve() is called.
            if (_derivateRangeCount == -1)
            {
                _derivateRangeCount = _rangeCount;
                if (_derivateRanges == null || _derivateRanges.Length < _derivateRangeCount)
                    _derivateRanges = new DerivativeRange[_derivateRangeCount];

                for (int j = 0; j < _derivateRangeCount; j++)
                    _derivateRanges[j] = ComputeDerivativeRangeCoefficents(ref _keys[j], ref _keys[j + 1]);
            }

            int i = _rangeCount;
            fixed (DerivativeRange* lastRangePtr = &_derivateRanges[i - 1]) // avoid struct copying and array bounds checks
            {
                DerivativeRange* rangePtr = lastRangePtr;
                while (i > 0)
                {
                    if (time >= rangePtr->minTime)
                        return rangePtr->c + time * (rangePtr->b + time * rangePtr->a);

                    rangePtr--;
                    i--;
                }
            }
            return 0.0;

        }

        /// <summary>
        /// Find the minimum and maximum values on the curve, and the corresponding times.
        /// </summary>
        /// <param name="minTime">The time for the minimum value.</param>
        /// <param name="minValue">The minimum value.</param>
        /// <param name="maxTime">The time for the maximum value.</param>
        /// <param name="maxValue">The maximum value.</param>
        public unsafe void FindMinMax(out double minTime, out double minValue, out double maxTime, out double maxValue)
        {
            if (_rangeCount == 0)
            {
                minTime = _firstTime;
                minValue = _firstValue;
                maxTime = _lastTime;
                maxValue = _lastValue;
                return;
            }

            minTime = double.MaxValue;
            minValue = double.MaxValue;
            maxTime = double.MinValue;
            maxValue = double.MinValue;

            double* times = stackalloc double[4];
            double* values = stackalloc double[4];

            for (int i = _rangeCount; i-- > 0;)
            {
                Key k1 = _keys[i];
                Key k2 = _keys[i + 1];

                times[0] = k1.time;
                values[0] = k1.value;
                times[1] = k2.time;
                values[1] = k2.value;
                int valuesToCheck = 2;

                if (!double.IsInfinity(k1.outTangent) && !double.IsInfinity(k2.inTangent))
                {
                    Range r = _ranges[i];

                    // If a is zero, this is actually a quadratic polynomial that can only have one root :
                    if (r.a == 0.0)
                    {
                        double time = -r.c / (2.0 * r.b);
                        if (!double.IsNaN(time) && time > k1.time && time < k2.time)
                        {
                            times[valuesToCheck] = time;
                            values[valuesToCheck] = Evaluate(time);
                            valuesToCheck++;
                        }
                    }
                    // This is a cubic polynomial with two potential roots :
                    else
                    {
                        double factor = -r.b;
                        double sqrt = Math.Sqrt(r.b * r.b - 3.0 * r.a * r.c);
                        double divisor = 3.0 * r.a;

                        double time = (factor + sqrt) / divisor;
                        if (!double.IsNaN(time) && time > k1.time && time < k2.time)
                        {
                            times[valuesToCheck] = time;
                            values[valuesToCheck] = Evaluate(time);
                            valuesToCheck++;
                        }

                        time = (factor - sqrt) / divisor;
                        if (!double.IsNaN(time) && time > k1.time && time < k2.time)
                        {
                            times[valuesToCheck] = time;
                            values[valuesToCheck] = Evaluate(time);
                            valuesToCheck++;
                        }
                    }
                }

                while (valuesToCheck-- > 0)
                {
                    double value = values[valuesToCheck];
                    if (value < minValue)
                    {
                        minValue = value;
                        minTime = times[valuesToCheck];
                    }
                    if (value > maxValue)
                    {
                        maxValue = value;
                        maxTime = times[valuesToCheck];
                    }
                }
            }
        }

        /// <summary>
        /// Create a key at the specified time, where the key value and tangents are set such as to match the curve current shape.
        /// </summary>
        /// <param name="time">The time at which the key must be created.</param>
        /// <param name="tangentKey">The resulting key where the value and tangents match the curve current shape.</param>
        /// <returns><see langword="true" /> if the key was succesfully created.</returns>
        public bool CreateTangentKey(double time, out Key tangentKey)
        {
            tangentKey = default;
            int i = _keyCount;
            if (i < 2)
                return false;

            i--;
            while (i-- > 0)
            {
                if (i > 0 && time < _keys[i].time)
                    continue;

                Key k0 = _keys[i];
                Key k1 = _keys[i + 1];

                tangentKey.time = time;
                tangentKey.value = EvaluateBetweenKeys(time, ref k0, ref k1);
                double tangent = FindTangentBetweenKeys(time, ref k0, ref k1);
                tangentKey.inTangent = tangent;
                tangentKey.outTangent = tangent;
                tangentKey.tangentMode = TangentMode.ManualEqual;

                return tangentKey.value.IsFinite() && !double.IsNaN(tangent);
            }

            return false;
        }

        /// <summary>
        /// Adjust all the curve keys tangents according to the tangent mode defined in each key.
        /// </summary>
        /// <remarks>
        /// This will adjust all tangents even if <see cref="AutoAdjustTangents"/> is <see langword="false"/>.<para/>
        /// When <see cref="AutoAdjustTangents"/> is <see langword="true"/>, the tangents are adjusted automatically, there is no need to call that method.
        /// </remarks>
        public void ForceAdjustTangents()
        {
            AdjustTangents();
            CompileCurve();
            _version++;
        }

        /// <summary>
        /// Adjust all the curve keys tangents according to the tangent mode defined in each key.
        /// </summary>
        /// <remarks>
        /// Require the keys to be sorted, but doesn't require <see cref="CompileCurve"/> to have been called.
        /// </remarks>
        private void AdjustTangents()
        {
            if (_keyCount < 2)
                return;

            _version++;

            // Special handling of the first key
            Key key0 = _keys[0];
            if (key0.tangentMode == TangentMode.ManualEqual)
            {
                key0.inTangent = key0.outTangent;
            }
            else if (key0.tangentMode.Is(TangentMode.Auto))
            {
                if (key0.tangentMode == TangentMode.Flat)
                {
                    key0.inTangent = 0.0;
                    key0.outTangent = 0.0;
                }
                else if (key0.tangentMode == TangentMode.Step)
                {
                    key0.inTangent = double.PositiveInfinity;
                    key0.outTangent = double.PositiveInfinity;
                }
                else
                {
                    Key key1 = _keys[1];
                    double tgt1 = (key1.value - key0.value) / (key1.time - key0.time);
                    double tangent;
                    if (_keyCount > 2 && key0.tangentMode != TangentMode.Straight)
                    {
                        Key key2 = _keys[2];
                        double tgt2 = (key2.value - key0.value) / (key2.time - key0.time);
                        tangent = 2.0 * tgt1 - tgt2;

                        if (key0.tangentMode == TangentMode.SmoothClamped)
                        {
                            if (tgt1 > 0.0)
                            {
                                tangent = Math.Min(tgt1 * 3.0, tangent);
                                if (tangent < 0.0)
                                    tangent = 0.0;
                            }
                            else
                            {
                                tangent = Math.Max(tgt1 * 3.0, tangent);
                                if (tangent > 0.0)
                                    tangent = 0.0;
                            }
                        }
                    }
                    else
                    {
                        tangent = tgt1;
                    }

                    key0.inTangent = tangent;
                    key0.outTangent = tangent;
                }
                _keys[0] = key0;
            }

            // Generic handling of the middle keys
            int lastKeyIdx = _keyCount - 1;
            for (int i = 1; i < lastKeyIdx; i++)
            {
                Key key = _keys[i];
                if (key.tangentMode.Is(TangentMode.Manual))
                {
                    if (key.tangentMode == TangentMode.ManualEqual && key.inTangent != key.outTangent)
                    {
                        double avgTangent = (key.inTangent + key.outTangent) * 0.5;
                        key.inTangent = avgTangent;
                        key.outTangent = avgTangent;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    if (key.tangentMode == TangentMode.Flat)
                    {
                        key.inTangent = 0.0;
                        key.outTangent = 0.0;
                    }
                    else if (key.tangentMode == TangentMode.Step)
                    {
                        key.inTangent = double.PositiveInfinity;
                        key.outTangent = double.PositiveInfinity;
                    }
                    else
                    {
                        Key prevKey = _keys[i - 1];
                        Key nextKey = _keys[i + 1];
                        double inTangent = (key.value - prevKey.value) / (key.time - prevKey.time);
                        double outTangent = (nextKey.value - key.value) / (nextKey.time - key.time);
                        if (key.tangentMode == TangentMode.Straight)
                        {
                            key.inTangent = inTangent;
                            key.outTangent = outTangent;
                        }
                        else if (key.tangentMode == TangentMode.Smooth)
                        {
                            // Actually not the average angle as we're working with non-linear tangents values,
                            // but doing this actually turns out to produce much nicer results in terms of
                            // smoothing the curve, as this automagically account for the relative weight of
                            // the adjacent points. Plus it *seems* to be what Unity does in the `Auto` and 
                            // `ClampedAuto` modes, and we want to be as close as possible to what it does.
                            double avgTangent = (inTangent + outTangent) * 0.5;
                            key.inTangent = avgTangent;
                            key.outTangent = avgTangent;
                        }
                        else if (key.tangentMode == TangentMode.SmoothClamped)
                        {
                            double tangent;
                            if (inTangent * outTangent < 0.0)
                            {
                                tangent = 0.0; // if different sign, the tangent must be flat
                            }
                            else
                            {
                                double avgTangent = (inTangent + outTangent) * 0.5;
                                // else clamp to avoid overshoot
                                if (inTangent == 0.0)
                                {
                                    tangent = 0.0;
                                }
                                else if (inTangent > 0.0)
                                {
                                    double minTangent = Math.Min(inTangent, outTangent);
                                    tangent = Math.Min(minTangent * 3.0, avgTangent); // 3 is the magic number !
                                }
                                else
                                {
                                    double maxTangent = Math.Max(inTangent, outTangent);
                                    tangent = Math.Max(maxTangent * 3.0, avgTangent);
                                }
                            }

                            key.inTangent = tangent;
                            key.outTangent = tangent;
                        }
                    }
                }

                _keys[i] = key;
            }

            // Special handling of the last key
            Key keyL = _keys[lastKeyIdx];
            if (keyL.tangentMode == TangentMode.ManualEqual)
            {
                keyL.outTangent = keyL.inTangent;
            }
            else if (keyL.tangentMode.Is(TangentMode.Auto))
            {
                if (keyL.tangentMode == TangentMode.Flat)
                {
                    keyL.inTangent = 0.0;
                    keyL.outTangent = 0.0;
                }
                else if (keyL.tangentMode == TangentMode.Step)
                {
                    keyL.inTangent = double.PositiveInfinity;
                    keyL.outTangent = double.PositiveInfinity;
                }
                else
                {
                    Key keyL1 = _keys[lastKeyIdx - 1];
                    double tgtL1 = (keyL.value - keyL1.value) / (keyL.time - keyL1.time);
                    double tangent;
                    if (_keyCount > 2 && keyL.tangentMode != TangentMode.Straight)
                    {
                        Key keyL2 = _keys[lastKeyIdx - 2];
                        double tgtL2 = (keyL.value - keyL2.value) / (keyL.time - keyL2.time);
                        tangent = 2.0 * tgtL1 - tgtL2;

                        if (keyL.tangentMode == TangentMode.SmoothClamped)
                        {
                            if (tgtL1 > 0.0)
                            {
                                tangent = Math.Min(tgtL1 * 3.0, tangent);
                                if (tangent < 0.0)
                                    tangent = 0.0;
                            }
                            else
                            {
                                tangent = Math.Max(tgtL1 * 3.0, tangent);
                                if (tangent > 0.0)
                                    tangent = 0.0;
                            }
                        }
                    }
                    else
                    {
                        tangent = tgtL1;
                    }

                    keyL.inTangent = tangent;
                    keyL.outTangent = tangent;
                }
                _keys[lastKeyIdx] = keyL;
            }
        }

        /// <summary>
        /// Adjust the tangents if requested and cache the polynomial form of the hermit curve for every key pair.
        /// Doesn't need to called again unless the keys are modified.
        /// </summary>
        private void CompileCurve()
        {
            if (_autoTangents)
                AdjustTangents();

            _rangeCount = _keyCount < 2 ? 0 : _keyCount - 1;
            _derivateRangeCount = -1;

            switch (_keyCount)
            {
                case 0:
                    _firstTime = 0.0;
                    _lastTime = 0.0;
                    _firstValue = 0.0;
                    _lastValue = 0.0;
                    return;
                case 1:
                    ref Key singleKey = ref _keys[0];
                    _firstTime = singleKey.time;
                    _lastTime = singleKey.time;
                    _firstValue = singleKey.value;
                    _lastValue = singleKey.value;
                    return;
                default:
                    ref Key firstKey = ref _keys[0];
                    _firstValue = firstKey.value;
                    _firstTime = firstKey.time;
                    ref Key lastKey = ref _keys[_rangeCount];
                    _lastValue = lastKey.value;
                    _lastTime = lastKey.time;
                    break;
            }

            if (_ranges == null || _ranges.Length < _rangeCount)
                _ranges = new Range[_rangeCount];

            for (int i = 0; i < _rangeCount; i++)
                _ranges[i] = ComputeRangeCoefficents(ref _keys[i], ref _keys[i + 1]);
        }

        /// <summary>
        /// Compute the coefficients of the polynomial form of the hermit curve equation for the range between two keys.
        /// </summary>
        private static Range ComputeRangeCoefficents(ref Key k0, ref Key k1)
        {
            // Hermite curve base functions :
            // h00(t) = 2t³ - 3t² + 1
            // h10(t) = t³ - 2t² + t
            // h01(t) = -2t³ + 3t²
            // h11(t) = t³ - t²
            // Interpolation at time t on an arbitrary interval [t0, t1], for the values p0, p1 and tangents m0, m1 :
            //                                                                                        (t - t0)
            // h(t) = h00(tI)·(t1 - t0)·p0 + h10(tI)·m0 + h01(tI)·(t1 - t0)·p1 + h11(tI)·m1 with tI = ---------
            //                                                                                        (t1 - t0)
            // For faster evaluation, we precompute the coeficients of the polynomial form : 
            // h(t) = at³ + bt² + ct + d;
            // Which we then evaluate (in the Evaluate() method) in its Horner form :
            // h(t) = d + t (c + t (b + a t))

            double t0 = k0.time;
            double p0 = k0.value;
            double m0 = k0.outTangent;
            double t1 = k1.time;
            double p1 = k1.value;
            double m1 = k1.inTangent;
            double a, b, c, d;

            if (double.IsInfinity(m0) || double.IsInfinity(m1))
            {
                a = 0.0;
                b = 0.0;
                c = 0.0;
                d = p0;
            }
            else
            {
                double p1x2 = t0 * t0;
                double p1x3 = p1x2 * t0;
                double p2x2 = t1 * t1;
                double p2x3 = p2x2 * t1;

                double divisor = p1x3 - p2x3 + 3.0 * t0 * t1 * (t1 - t0);
                a = ((m0 + m1) * (t0 - t1) + (p1 - p0) * 2.0) / divisor;
                b = (2.0 * (p2x2 * m0 - p1x2 * m1) - p1x2 * m0 + p2x2 * m1 + t0 * t1 * (m1 - m0) + 3.0 * (t0 + t1) * (p0 - p1)) / divisor;
                c = (p1x3 * m1 - p2x3 * m0 + t0 * t1 * (t0 * (2.0 * m0 + m1) - t1 * (m0 + 2.0 * m1)) + 6.0 * t0 * t1 * (p1 - p0)) / divisor;
                d = ((t0 * p2x2 - p1x2 * t1) * (t1 * m0 + t0 * m1) - p0 * p2x3 + p1x3 * p1 + 3.0 * t0 * t1 * (t1 * p0 - t0 * p1)) / divisor;
            }
            return new Range { a = a, b = b, c = c, d = d, minTime = t0 };
        }

        /// <summary>
        /// Compute the coefficients of the polynomial form of the hermit curve equation first derivative for the range between two keys.
        /// </summary>
        private static DerivativeRange ComputeDerivativeRangeCoefficents(ref Key k0, ref Key k1)
        {
            // Derivatives of the Hermite base functions :
            // h00'(t) = 6t² - 6t 
            // h10'(t) = 3t² - 4t + 1
            // h01'(t) = -6t² + 6t 
            // h11'(t) = 3t² - 2t
            // Derivative at time t on an arbitrary interval [t0, t1], for the values p0, p1 and tangents m0, m1 :
            //                      1                                     1                                (t - t0)
            // h'(t) = h00'(tI)·---------·p0 + h10'(tI)·m0 + h01'(tI)·---------·p1 + h11'(tI)·m1 with tI = ---------
            //                  (t1 - t0)                             (t1 - t0)                            (t1 - t0)
            // For faster evaluation, we precompute the coefficients of the polynomial form : 
            // h(t) = at² + bt + c;
            // Which we then evaluate (in the FindTangent() method) in its Horner form :
            // h(t) = c + t (b + a t)

            double t0 = k0.time;
            double t1 = k1.time;
            double p0 = k0.value;
            double p1 = k1.value;
            double m0 = k0.outTangent;
            double m1 = k1.inTangent;

            if (double.IsInfinity(m0) || double.IsInfinity(m1))
                return new DerivativeRange { minTime = t0 };

            double tI = t1 - t0;
            double tI2 = tI * tI;
            double tI3 = tI2 * tI;

            double t0tI = t0 + tI;
            double p0p1 = p0 - p1;
            double m0m1 = m0 + m1;

            double a = (6.0 * p0p1 + 3.0 * m0m1 * tI) / tI3;
            double b = (-12.0 * p0p1 * t0 + -6.0 * (p0 - p1 + m0m1 * t0) * tI + -2.0 * (2.0 * m0 + m1) * tI2) / tI3;
            double c = (6.0 * p0 * t0 * t0tI - 6.0 * p1 * t0 * t0tI + m0 * tI * t0tI * (3.0 * t0 + tI) + m1 * t0 * tI * (3.0 * t0 + 2.0 * tI)) / tI3;


            return new DerivativeRange { a = a, b = b, c = c, minTime = t0 };
        }

        /// <summary>
        /// Interpolate a Hermite cubic spline segment at the given time.
        /// </summary>
        /// <param name="time">The time to evaluate, must be inside the time range defined by the keys or equal to the keys time.</param>
        /// <param name="k0">The first key defining the curve segment.</param>
        /// <param name="k1">The second key defining the curve segment.</param>
        /// <returns>The interpolated value at the given time.</returns>
        /// <remarks>
        /// This produce identical results as the <see cref="Evaluate(double)"/> instance method, but for every range, the instance
        /// methods compute and cache the polynomial form of the Hermite function, which is much faster to evaluate.
        /// </remarks>
        public static double EvaluateBetweenKeys(double time, ref Key k0, ref Key k1)
        {
            // Hermite base functions :
            // h00(t) = 2t³ - 3t² + 1
            // h10(t) = t³ - 2t² + t
            // h01(t) = -2t³ + 3t²
            // h11(t) = t³ - t²
            // Interpolation at time t on an arbitrary interval [t0, t1], for the values p0, p1 and tangents m0, m1 :
            //                                                                                        (t - t0)
            // h(t) = h00(tI)·(t1 - t0)·p0 + h10(tI)·m0 + h01(tI)·(t1 - t0)·p1 + h11(tI)·m1 with tI = ---------
            //                                                                                        (t1 - t0)

            double m0 = k0.outTangent;
            double m1 = k1.inTangent;

            if (double.IsInfinity(m0) || double.IsInfinity(m1))
                return k0.value;

            double i = k1.time - k0.time;
            double tI = (time - k0.time) / i;
            double tI2 = tI * tI;
            double tI3 = tI2 * tI;

            double h00 = 2.0 * tI3 - 3.0 * tI2 + 1.0;
            double h10 = tI3 - 2.0 * tI2 + tI;
            double h01 = -2.0 * tI3 + 3.0 * tI2;
            double h11 = tI3 - tI2;

            return h00 * k0.value + h10 * i * m0 + h01 * k1.value + h11 * i * m1;
        }

        /// <summary>
        /// Find the slope of the tangent (the derivative) to a curve segment at the given time.
        /// </summary>
        /// <param name="time">The time to evaluate, must be inside the time range defined by the keys or equal to the keys time.</param>
        /// <param name="k0">The first key defining the curve segment.</param>
        /// <param name="k1">The second key defining the curve segment.</param>
        /// <returns>The slope of the tangent (the derivative) at the given time</returns>
        /// <remarks>
        /// This produce identical results as the <see cref="FindTangent(double)"/> instance method, but for every range, the instance
        /// methods compute and cache the polynomial form of the Hermite function, which is much faster to evaluate.
        /// </remarks>
        public static double FindTangentBetweenKeys(double time, ref Key k0, ref Key k1)
        {
            // Derivatives of the Hermite base functions :
            // h00'(t) = 6t² - 6t 
            // h10'(t) = 3t² - 4t + 1
            // h01'(t) = -6t² + 6t 
            // h11'(t) = 3t² - 2t
            // Derivative at time t on an arbitrary interval [t0, t1], for the values p0, p1 and tangents m0, m1 :
            //                      1                                     1                                (t - t0)
            // h'(t) = h00'(tI)·---------·p0 + h10'(tI)·m0 + h01'(tI)·---------·p1 + h11'(tI)·m1 with tI = ---------
            //                  (t1 - t0)                             (t1 - t0)                            (t1 - t0)


            if (double.IsInfinity(k0.outTangent) || double.IsInfinity(k1.inTangent))
                return time == k0.time ? double.PositiveInfinity : 0.0;

            double i = k1.time - k0.time;
            double tI = (time - k0.time) / i;
            double tI2 = tI * tI;

            double h00 = 6.0 * tI2 - 6.0 * tI;
            double h10 = 3.0 * tI2 - 4.0 * tI + 1.0;
            double h01 = -6.0 * tI2 + 6.0 * tI;
            double h11 = 3.0 * tI2 - 2.0 * tI;

            double iD = 1.0 / i;

            return h00 * iD * k0.value + h10 * k0.outTangent + h01 * iD * k1.value + h11 * k1.inTangent;
        }

        public override string ToString()
        {
            return $"{_keyCount} keys, range = [{FirstTime}, {LastTime}], values = [{FirstValue}, {LastValue}]";
        }
    }

    public static class HermitCurveExtensions
    {
        /// <summary>
        /// Perform a bitwise comparison between this tangent mode and the specified mode
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Is(this HermiteCurve.TangentMode instance, HermiteCurve.TangentMode mode)
        {
            return (instance & mode) != 0;
        }

        /// <summary>
        /// Return <see langword="false"/> if the specified instance isn't a flag defined in the <see cref="HermiteCurve.TangentMode"/> enum.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefinedFlag(this HermiteCurve.TangentMode instance)
        {
            return instance > 0 && instance <= HermiteCurve.TangentMode.SmoothClamped && (instance & (instance - 1)) == 0; // check if instance is a power of 2
        }

        /// <summary>
        /// Return false if the specified value is NaN or Infinity, true otherwise
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IsFinite(this double value)
        {
            return (*(long*)&value & 0x7FFFFFFFFFFFFFFFL) < 9218868437227405312L;
        }
    }
}