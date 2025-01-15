using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ROUtils
{
    /// <summary>
    /// A collection of keys mapping times to values, interpolated between keys by a cubic hermite spline.
    /// The implementation produce identical results as a UnityEngine.AnimationCurve, but calling Evaluate() is at least twice faster.
    /// However, it doesn't support keys in/out weights, and behavior is always identical to WrapMode.ClampForever.
    /// </summary>
    public class FastFloatCurve : IConfigNode, IEnumerable<FastFloatCurve.Key>
    {
        /// <summary>
        /// A key defining a point on a FastFloatCurve
        /// </summary>
        public struct Key : IComparable<Key>
        {
            /// <summary>
            /// The time of the key.
            /// </summary>
            public float time;

            /// <summary>
            /// The value of the curve at the key time.
            /// </summary>
            public float value;

            /// <summary>
            /// The incoming tangent affects the slope of the curve from the previous key to this key.
            /// </summary>
            public float inTangent;

            /// <summary>
            /// The outgoing tangent affects the slope of the curve from this key to the next key.
            /// </summary>
            public float outTangent;

            public Key(float time, float value, float inTangent, float outTangent)
            {
                this.time = time;
                this.value = value;
                this.inTangent = inTangent;
                this.outTangent = outTangent;
            }

            public Key(float time, float value)
            {
                this.time = time;
                this.value = value;
                inTangent = 0f;
                outTangent = 0f;
            }

            public int CompareTo(Key other) => time.CompareTo(other.time);

            public override string ToString()
            {
                if (inTangent == 0f && outTangent == 0f)
                    return $"{time} | {value}";

                return $"{time} | {value} | {inTangent} | {outTangent}";
            }
        }

        private struct Range
        {
            public double a, b, c, d;
            public float minTime;
        }

        private bool isCompiled;

        private List<Key> keys;

        private int rangeCount;
        private int lastRangeIdx;
        private Range[] ranges;

        private float firstTime;
        private float lastTime;
        private float firstValue;
        private float lastValue;

        /// <summary> Create a new empty curve.</summary>
        public FastFloatCurve()
        {
            keys = new List<Key>();
            isCompiled = false;
        }

        /// <summary> Create a new curve from the provided keys. </summary>
        /// <param name="keys">The keys to add to the curve.</param>
        public FastFloatCurve(params Key[] keys)
        {
            this.keys = new List<Key>(keys);
            isCompiled = false;
        }

        /// <summary> Create a new curve with the same keys as an UnityEngine.AnimationCurve.</summary>
        /// <param name="animationCurve">The unity AnimationCurve to copy keys from.</param>
        public FastFloatCurve(AnimationCurve animationCurve)
        {
            Keyframe[] unityKeys = animationCurve.keys;
            keys = new List<Key>(unityKeys.Length);
            for (int i = 0; i < unityKeys.Length; i++)
            {
                Keyframe unityKey = unityKeys[i];
                keys.Add(new Key(unityKey.time, unityKey.value, unityKey.inTangent, unityKey.outTangent));
            }
            isCompiled = false;
        }

        /// <summary> Create a new curve with the same keys as a KSP FloatCurve.</summary>
        /// <param name="kspFloatCurve">The FloatCurve to copy keys from.</param>
        public FastFloatCurve(FloatCurve kspFloatCurve) : this(kspFloatCurve.fCurve) { }

        /// <summary> Create a copy of this curve instance. </summary>
        /// <returns>A newly instantiated clone of this curve.</returns>
        public FastFloatCurve Clone()
        {
            FastFloatCurve clone = new FastFloatCurve();
            clone.keys.AddRange(keys);
            if (isCompiled && rangeCount > 0)
            {
                clone.isCompiled = true;
                clone.rangeCount = rangeCount;
                clone.ranges = new Range[rangeCount];
                Array.Copy(ranges, clone.ranges, rangeCount);
                clone.lastRangeIdx = lastRangeIdx;
                clone.firstTime = firstTime;
                clone.firstValue = firstValue;
                clone.lastTime = lastTime;
                clone.lastValue = lastValue;
            }
            return clone;
        }

        /// <summary> Set all keys in this curve to the keys of another curve.</summary>
        /// <param name="other">The other curve to copy the key from</param>
        public void CopyFrom(FastFloatCurve other)
        {
            keys.Clear();
            keys.AddRange(other.keys);
            if (other.isCompiled && other.rangeCount > 0)
            {
                isCompiled = true;
                rangeCount = other.rangeCount;
                ranges = new Range[rangeCount];
                Array.Copy(other.ranges, ranges, rangeCount);
                lastRangeIdx = other.lastRangeIdx;
                firstTime = other.firstTime;
                firstValue = other.firstValue;
                lastTime = other.lastTime;
                lastValue = other.lastValue;
            }
            else
            {
                isCompiled = false;
            }
        }

        /// <summary> The amount of keys in the curve.</summary>
        public int KeyCount => keys.Count;

        /// <summary> The time of the first key. </summary>
        public float FirstTime
        {
            get
            {
                if (!isCompiled)
                    CompileRanges();

                switch (keys.Count)
                {
                    case 0: return 0f;
                    case 1: return keys[0].time;
                    default: return firstTime;
                }
            }
        }

        /// <summary> The time of the last key. </summary>
        public float LastTime
        {
            get
            {
                if (!isCompiled)
                    CompileRanges();

                switch (keys.Count)
                {
                    case 0: return 0f;
                    case 1: return keys[0].time;
                    default: return lastTime;
                }
            }
        }

        /// <summary> The value of the first key</summary>
        public float FirstValue
        {
            get
            {
                if (!isCompiled)
                    CompileRanges();

                switch (keys.Count)
                {
                    case 0: return 0f;
                    case 1: return keys[0].value;
                    default: return firstValue;
                }
            }
        }

        /// <summary> The value of the last key</summary>
        public float LastValue
        {
            get
            {
                if (!isCompiled)
                    CompileRanges();

                switch (keys.Count)
                {
                    case 0: return 0f;
                    case 1: return keys[0].value;
                    default: return lastValue;
                }
            }
        }

        /// <summary> Get or set a key at the specified index</summary>
        /// <param name="keyIndex">The zero-based index of the key</param>
        /// <returns>The key at the specified index.</returns>
        public Key this[int keyIndex]
        {
            get
            {
                return keys[keyIndex];
            }
            set
            {
                keys[keyIndex] = value;
                isCompiled = false;
            }
        }

        /// <summary> Add a new key to the curve.</summary>
        /// <param name="key">The key to add to the curve.</param>
        public void AddKey(Key key)
        {
            keys.Add(key);
            isCompiled = false;
        }

        /// <summary> Add a new key to the curve.</summary>
        /// <param name="time">The time of the key.</param>
        /// <param name="value">The value of the curve at the key time.</param>
        public void AddKey(float time, float value)
        {
            keys.Add(new Key(time, value));
            isCompiled = false;
        }

        /// <summary> Add a new key to the curve.</summary>
        /// <param name="time">The time of the key.</param>
        /// <param name="value">The value of the curve at the key time.</param>
        /// <param name="inTangent">The incoming tangent affects the slope of the curve from the previous key to this key.</param>
        /// <param name="outTangent">The outgoing tangent affects the slope of the curve from this key to the next key.</param>
        public void AddKey(float time, float value, float inTangent, float outTangent)
        {
            keys.Add(new Key(time, value, inTangent, outTangent));
            isCompiled = false;
        }

        /// <summary> Remove a key from the curve</summary>
        /// <param name="keyIndex">The index of key to remove</param>
        public void RemoveKey(int keyIndex)
        {
            keys.RemoveAt(keyIndex);
            isCompiled = false;
        }

        /// <summary> Returns an enumerator that iterates through curve keys.</summary>
        public IEnumerator<Key> GetEnumerator() => keys.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => keys.GetEnumerator();

        /// <summary> Evaluate the curve at the specified time.</summary>
        /// <param name="time">The time within the curve you want to evaluate (the horizontal axis in the curve graph).</param>
        /// <returns> The value of the curve, at the point in time specified.</returns>
        public unsafe float Evaluate(float time)
        {
            if (!isCompiled)
                CompileRanges();

            if (time <= firstTime)
                return firstValue;
            if (time >= lastTime)
                return lastValue;

            int i = rangeCount;
            fixed (Range* lastRangePtr = &ranges[lastRangeIdx]) // avoid struct copying and array bounds checks
            {
                Range* rangePtr = lastRangePtr;
                while (i > 0)
                {
                    if (time > rangePtr->minTime)
                        return (float)(rangePtr->a * time * time * time + rangePtr->b * time * time + rangePtr->c * time + rangePtr->d);

                    rangePtr--;
                    i--;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Find the minimum and maximum values on the curve, and the corresponding times.
        /// </summary>
        /// <param name="minTime">The time for the minimum value.</param>
        /// <param name="minValue">The minimum value.</param>
        /// <param name="maxTime">The time for the maximum value.</param>
        /// <param name="maxValue">The maximum value.</param>
        public unsafe void FindMinMax(out float minTime, out float minValue, out float maxTime, out float maxValue)
        {
            int keyCount = keys.Count;
            if (keyCount == 0)
            {
                minTime = 0f;
                minValue = 0f;
                maxTime = 0f;
                maxValue = 0f;
                return;
            }

            if (keyCount == 1)
            {
                Key key = keys[0];
                minTime = key.time;
                minValue = key.value;
                maxTime = key.time;
                maxValue = key.value;
                return;
            }

            if (!isCompiled)
                keys.Sort();

            minTime = float.MaxValue;
            minValue = float.MaxValue;
            maxTime = float.MinValue;
            maxValue = float.MinValue;

            float* times = stackalloc float[4];
            float* values = stackalloc float[4];

            for (int i = keyCount - 1; i-- > 0;)
            {
                Key k1 = keys[i];
                Key k2 = keys[i + 1];

                times[0] = k1.time;
                times[1] = k2.time;
                values[0] = k1.value;
                values[1] = k2.value;
                int valuesToCheck = 2;

                double p0 = k1.value;
                double p1 = k2.value;
                double m0 = k1.outTangent;
                double m1 = k2.inTangent;
                double t0 = k1.time;
                double t1 = k2.time;
                double tI = t1 - t0;

                if (tI > 0.0 && !double.IsInfinity(m0) && !double.IsInfinity(m1))
                {
                    // The time of the 2 potential extremums are of the form :
                    // r0 = (a + √b) / c
                    // r1 = (a - √b) / c
                    // Which are given by solving h'(t) = 0 for t where h'(t) is the 
                    // derivative of the hermit function, see FindTangentBetweenKeysAtTime();

                    double tI2 = tI * tI;
                    double tI3 = tI2 * tI;
                    double tI4 = tI2 * tI2;

                    double sqrt = Math.Sqrt(
                          9.0 * p0 * p0 * tI2
                        + 6.0 * p0 * m0 * tI3
                        - 18.0 * p0 * p1 * tI2
                        + 6.0 * p0 * m1 * tI3
                        + m0 * m0 * tI4
                        - 6.0 * m0 * p1 * tI3
                        + m0 * m1 * tI4
                        + 9.0 * p1 * p1 * tI2
                        - 6.0 * p1 * m1 * tI3
                        + m1 * m1 * tI4);

                    double factor =
                          6.0 * p0 * t0
                        + 3.0 * p0 * tI
                        + 3.0 * m0 * t0 * tI
                        + 2.0 * m0 * tI2
                        - 6.0 * p1 * t0
                        - 3.0 * p1 * tI
                        + 3.0 * m1 * t0 * tI
                        + m1 * tI2;

                    double divisor = 3.0 * (2.0 * p0 + m0 * tI - 2.0 * p1 + m1 * tI);

                    float time1 = (float)((factor + sqrt) / divisor);
                    if (!double.IsNaN(time1) && time1 > t0 && time1 < t1)
                    {
                        times[valuesToCheck] = time1;
                        values[valuesToCheck] = Evaluate(time1);
                        valuesToCheck++;
                    }

                    float time2 = (float)((factor - sqrt) / divisor);
                    if (!double.IsNaN(time2) && time2 > t0 && time2 < t1)
                    {
                        times[valuesToCheck] = time2;
                        values[valuesToCheck] = Evaluate(time2);
                        valuesToCheck++;
                    }
                }

                while (valuesToCheck-- > 0)
                {
                    float value = values[valuesToCheck];
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

        /// <summary> Find the slope of the tangent (the derivative) to the curve at the given time.</summary>
        /// <param name="time">The time to evaluate, must be within the min and max time defined by the curve.</param>
        /// <returns>The slope of the tangent (the derivative) at the given time</returns>
        public float FindTangent(float time)
        {
            int i = keys.Count;
            if (i < 2)
                return 0f;

            if (!isCompiled)
                keys.Sort();

            if (time < keys[0].time)
                return 0f;

            if (time > keys[--i].time)
                return 0f;

            while (i-- > 0)
            {
                if (time < keys[i].time)
                    continue;

                return FindTangentBetweenKeysAtTime(time, keys[i], keys[i + 1]);
            }

            return 0f;
        }

        /// <summary> Find the slope of the tangent (the derivative) to a curve segment at the given time.</summary>
        /// <param name="time">The time to evaluate, must be within the time range defined by the keys.</param>
        /// <param name="k0">The first key defining the curve segment.</param>
        /// <param name="k1">The second key defining the curve segment.</param>
        /// <returns>The slope of the tangent (the derivative) at the given time</returns>
        public static float FindTangentBetweenKeysAtTime(float time, Key k0, Key k1)
        {
            // Derivatives of the Hermite base functions :
            // h00'(t) = 2t² - 6t
            // h10'(t) = 3t² - 4t + 1
            // h01'(t) = -6t² + 6t
            // h11'(t) = 3t² - 2t
            // Derivative at time t on an arbitrary interval [t0, t1], for the values p0, p1 and tangents m0, m1 :
            //                      1                                     1                                (t - t0)
            // h'(t) = h00'(tI)·---------·p0 + h10'(tI)·m0 + h01'(tI)·---------·p1 + h11'(tI)·m1 with tI = ---------
            //                  (t1 - t0)                             (t1 - t0)                            (t1 - t0)

            double i = (double)k1.time - k0.time;
            double tI = (time - k0.time) / i;
            double tI2 = tI * tI;

            double h00 = 6.0 * tI2 - 6.0 * tI;
            double h10 = 3.0 * tI2 - 4.0 * tI + 1;
            double h01 = -6.0 * tI2 + 6.0 * tI;
            double h11 = 3.0 * tI2 - 2.0 * tI;

            double iD = 1.0 / i;

            return (float)(h00 * iD * k0.value + h10 * k0.outTangent + h01 * iD * k1.value + h11 * k1.inTangent);
        }

        /// <summary> Set the keys of this curve from a serialized list of keys. Any existing keys will be overriden.</summary>
        /// <param name="node">A ConfigNode with a list of keys formatted as "<c>key = time value inTangent outTangent</c>". The tangent parameters are optional.</param>
        public void Load(ConfigNode node)
        {
            isCompiled = false;
            keys = new List<Key>(node.values.Count);

            for (int i = 0; i < node.values.Count; i++)
            {
                ConfigNode.Value nodeValue = node.values[i];
                if (nodeValue.name != "key")
                    continue;

                string[] keyValues = nodeValue.value.Split(FloatCurve.delimiters, StringSplitOptions.RemoveEmptyEntries);
                if (keyValues.Length < 2)
                {
                    Debug.LogError($"Invalid FloatCurve key : \"{nodeValue.value}\"");
                    continue;
                }

                if (keyValues.Length == 4)
                    keys.Add(new Key(float.Parse(keyValues[0]), float.Parse(keyValues[1]), float.Parse(keyValues[2]), float.Parse(keyValues[3])));
                else
                    keys.Add(new Key(float.Parse(keyValues[0]), float.Parse(keyValues[1])));
            }
        }

        /// <summary> Serialize this curve keys as values in the ConfigNode.</summary>
        /// <param name="node">The ConfigNode to add keys to.</param>
        public void Save(ConfigNode node)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                Key key = keys[i];
                node.AddValue("key", $"{key.time} {key.value} {key.inTangent} {key.outTangent}");
            }
        }

        public override string ToString() => $"{keys.Count} keys, range = [{FirstTime}, {LastTime}], values = [{FirstValue}, {LastValue}]";

        /// <summary> Sort the keys by time, and cache the polynomial form of the hermit curve for every key pair.</summary>
        private void CompileRanges()
        {
            isCompiled = true;
            int keyCount = keys.Count;

            if (keyCount < 2)
            {
                firstTime = float.PositiveInfinity;
                firstValue = keyCount == 1 ? keys[0].value : 0f;
                return;
            }

            keys.Sort();

            rangeCount = keyCount - 1;
            lastRangeIdx = rangeCount - 1;

            ranges = new Range[rangeCount];
            for (int i = 0; i < rangeCount; i++)
                ranges[i] = ComputeRangePolynomial(keys[i], keys[i + 1]);

            Key firstKey = keys[0];
            firstValue = firstKey.value;
            firstTime = firstKey.time;

            Key lastKey = keys[rangeCount];
            lastValue = lastKey.value;
            lastTime = lastKey.time;
        }

        /// <summary> Compute the factors of the polynomial form of the hermit curve equation for the range between two keys.</summary>
        /// <remarks> The resulting factors are the expression of the hermit spline in the form ax³ + bx² + bx + d.</remarks>
        private static Range ComputeRangePolynomial(Key p1, Key p2)
        {
            double p1x = p1.time;
            double p1y = p1.value;
            double tp1 = p1.outTangent;
            double p2x = p2.time;
            double p2y = p2.value;
            double tp2 = p2.inTangent;
            double a, b, c, d;

            if (double.IsInfinity(tp1) || double.IsInfinity(tp2))
            {
                a = 0.0;
                b = 0.0;
                c = 0.0;
                if (tp1 == double.NegativeInfinity && tp2 == double.NegativeInfinity
                    || tp1 == double.NegativeInfinity && !double.IsInfinity(tp2)
                    || tp2 == double.NegativeInfinity && !double.IsInfinity(tp1))
                {
                    d = p2.value;
                }
                else
                {
                    d = p1.value;
                }
            }
            else
            {
                double divisor = (p1x * p1x * p1x) - (p2x * p2x * p2x) + (3.0 * p1x * p2x * (p2x - p1x));
                a = ((tp1 + tp2) * (p1x - p2x) + (p2y - p1y) * 2.0) / divisor;
                b = (2.0 * (p2x * p2x * tp1 - p1x * p1x * tp2) - p1x * p1x * tp1 + p2x * p2x * tp2 + p1x * p2x * (tp2 - tp1) + 3.0 * (p1x + p2x) * (p1y - p2y)) / divisor;
                c = (p1x * p1x * p1x * tp2 - p2x * p2x * p2x * tp1 + p1x * p2x * (p1x * (2.0 * tp1 + tp2) - p2x * (tp1 + 2.0 * tp2)) + 6.0 * p1x * p2x * (p2y - p1y)) / divisor;
                d = ((p1x * p2x * p2x - p1x * p1x * p2x) * (p2x * tp1 + p1x * tp2) - p1y * p2x * p2x * p2x + p1x * p1x * p1x * p2y + 3.0 * p1x * p2x * (p2x * p1y - p1x * p2y)) / divisor;
            }
            return new Range() { a = a, b = b, c = c, d = d, minTime = p1.time };
        }
    }
}
