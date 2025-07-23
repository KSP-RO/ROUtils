using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace ROUtils
{
    /// <summary>
    /// Common tool for various procedural part mods for generating drag cubes.
    /// </summary>
    public class DragCubeTool : MonoBehaviour
    {
        private static readonly Dictionary<string, DragCube> _cacheDict = new Dictionary<string, DragCube>();
        private static readonly HashSet<string> _inProgressMultiCubeRenderings = new HashSet<string>();
        private static bool _statsRoutineStarted = false;
        private static uint _cubesRenderedThisFrame = 0;
        private static uint _cacheHitsThisFrame = 0;
        private static long _elapsedTicks = 0;

        private string _shapeKey;
        private bool _updateSymCounterparts;
        private Coroutine _multiCubeRoutine;

        /// <summary>
        /// Globally enable of disable drag cube caching.
        /// </summary>
        public static bool UseCache { get; set; } = true;
        /// <summary>
        /// Whether to validate all cubes that got fetched from cache against freshly-rendered ones.
        /// </summary>
        public static bool ValidateCubes { get; set; } = false;
        /// <summary>
        /// Max number of items in cache. Once that number is reached then the cache is cleared entirely.
        /// </summary>
        public static uint MaxCacheSize { get; set; } = 5000;

        public Part Part { get; private set; }

        /// <summary>
        /// Invoked once all drag cubes have been generated and assigned to the part.
        /// </summary>
        public event EventHandler AllCubesAssigned;

        /// <summary>
        /// Creates and assigns a drag cube for the given procedural part.
        /// This process can have one to many frames of delay.
        /// During part compilation this can happen immediately but may not be possible if part needs multiple cubes.
        /// </summary>
        /// <param name="p">Part to create drag cube for</param>
        /// <param name="shapeKey">Key that uniquely identifies the geometry of the part.Used in caching logic. Use null if no caching is desired.</param>
        /// <param name="updateSymCounterparts">If true then will also apply the same drag cube to all other parts that are in symmetry</param>
        /// <returns>DragCubeTool instance if the updating cannot be done immediately; otherwise null</returns>
        public static DragCubeTool UpdateDragCubes(Part p, string shapeKey = null, bool updateSymCounterparts = false)
        { 
            if (!PartLoader.Instance.IsReady() && TryUpdateDragCubesForPartCompilation(p, shapeKey))
                return null;

            var tool = p.GetComponent<DragCubeTool>();
            if (tool == null)
            {
                tool = p.gameObject.AddComponent<DragCubeTool>();
                tool.Part = p;
            }
            tool._shapeKey = shapeKey;
            tool._updateSymCounterparts = updateSymCounterparts;
            return tool;
        }

        internal static void ClearStaticState()
        {
            _inProgressMultiCubeRenderings.Clear();
            _statsRoutineStarted = false;
            _cubesRenderedThisFrame = 0;
            _cacheHitsThisFrame = 0;
            _elapsedTicks = 0;
        }

        public void FixedUpdate()
        {
            if (Part == null)
            {
                // Somehow part can become null when doing cloning in symmetry
                Destroy(this);
                return;
            }

            if (_multiCubeRoutine == null && Ready())
                UpdateCubes();
        }

        public bool Ready() => Ready(Part);

        private static bool Ready(Part p)
        {
            if (HighLogic.LoadedSceneIsFlight)
                return FlightGlobals.ready;
            if (HighLogic.LoadedSceneIsEditor)
                return p.localRoot == EditorLogic.RootPart && p.gameObject.layer != LayerMask.NameToLayer("TransparentFX");
            return true;
        }

        private static bool TryUpdateDragCubesForPartCompilation(Part p, string shapeKey)
        {
            if (PartNeedsMultipleCubes(p))
                return false;

            UpdateCubes(p, shapeKey, updateSymCounterparts: false);
            return true;
        }

        private void UpdateCubes()
        {
            if (PartNeedsMultipleCubes(Part))
            {
                SpamDebug($"[DragCubeTool] Need to render multiple cubes for {Part.partInfo.name}");
                if (!UseCache || _shapeKey == null || !TryUpdateMultiCubesFromCache())
                {
                    // Render over multiple frames for animated parts
                    _multiCubeRoutine = StartCoroutine(UpdateMultiCubesRoutine());
                }
            }
            else
            {
                UpdateCubes(Part, _shapeKey, _updateSymCounterparts);
                AllCubesAssigned?.Invoke(this, EventArgs.Empty);
                Destroy(this);
            }
        }

        /// <summary>
        /// If all cubes are cached then these can be assigned on the same frame.
        /// </summary>
        /// <returns>True, if cubes were assigned from cache; False if cubes need to be generated</returns>
        private bool TryUpdateMultiCubesFromCache()
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            var multiCube = Part.FindModuleImplementing<IMultipleDragCube>();
            List<DragCube> newCubeList = new List<DragCube>();
            foreach (string cName in multiCube.GetDragCubeNames())
            {
                string shapeKey = $"{_shapeKey}${cName}";
                if (!_cacheDict.TryGetValue(shapeKey, out DragCube dragCube))
                {
                    _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                    return false;
                }
                else
                {
                    _cacheHitsThisFrame++;
                    dragCube = CloneCube(dragCube);
                    newCubeList.Add(dragCube);
                }
            }

            if (ValidateCubes)
            {
                // Validation will run the multicube generation routine which will in turn assign these to the part as well
                _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                _multiCubeRoutine = StartCoroutine(MultiCubeValidationRoutine(newCubeList));
            }
            else
            {
                AssignMultiCubes(Part, newCubeList, false);
                if (_updateSymCounterparts)
                {
                    SpamDebug($"[DragCubeTool] Assigning cubes to {Part.symmetryCounterparts.Count} other parts in symmetry");
                    foreach (Part symPart in Part.symmetryCounterparts)
                    {
                        AssignMultiCubes(symPart, newCubeList, true);
                        NotifyFARIfNeeded(symPart);
                    }
                }

                _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;

                NotifyFARIfNeeded(Part);
                EnsureStatsRoutineStarted(Part);
                AllCubesAssigned?.Invoke(this, EventArgs.Empty);

                Destroy(this);
            }

            return true;
        }

        private IEnumerator UpdateMultiCubesRoutine(bool isValidation = false)
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            Part p = Instantiate(Part, Vector3.zero, Quaternion.identity);
            GameObject gameObject = p.gameObject;
            bool hasVessel = p.GetComponent<Vessel>() != null;
            if (hasVessel)
                p.vessel.mapObject = null;

            DragCubeSystem.Instance.SetupPartForRender(p, gameObject);
            IMultipleDragCube multiCube = p.FindModuleImplementing<IMultipleDragCube>();

            List<DragCube> newCubeList = new List<DragCube>();
            foreach (string cName in multiCube.GetDragCubeNames())
            {
                string shapeKey = _shapeKey == null ? null : $"{_shapeKey}${cName}";
                bool alreadyInProgress = !_inProgressMultiCubeRenderings.Add(shapeKey);
                if (!alreadyInProgress || isValidation)
                {
                    try
                    {
                        SpamDebug($"[DragCubeTool] Rendering pos {cName}");
                        multiCube.AssumeDragCubePosition(cName);

                        _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                        EnsureStatsRoutineStarted(Part);

                        // Animations do not propagate to the meshes immediately so need to wait for the next frame before rendering
                        yield return null;

                        startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                        DragCube dragCube = DragCubeSystem.Instance.RenderProceduralDragCube(p);
                        dragCube.Name = cName;
                        newCubeList.Add(dragCube);
                        _cubesRenderedThisFrame++;
                        AddCubeToCache(dragCube, shapeKey);
                    }
                    finally
                    {
                        _inProgressMultiCubeRenderings.Remove(shapeKey);
                    }
                }
                else
                {
                    // TODO: shouldn't instantiate a new part in this case. Kinda painful to implement though.
                    _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;

                    DragCube dragCube;
                    do
                    {
                        SpamDebug($"[DragCubeTool] Rendering {cName} already in progress, waiting...");
                        yield return null;
                    }
                    while (!_cacheDict.TryGetValue(shapeKey, out dragCube));

                    dragCube = CloneCube(dragCube);
                    startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    SpamDebug($"[DragCubeTool] Finished waiting for {cName}");
                    newCubeList.Add(dragCube);
                }
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
            if (hasVessel)
                FlightCamera.fetch.CycleCameraHighlighter();

            AssignMultiCubes(Part, newCubeList, false);
            if (_updateSymCounterparts)
            {
                SpamDebug($"[DragCubeTool] Assigning cubes to {Part.symmetryCounterparts.Count} other parts in symmetry");
                foreach (Part symPart in Part.symmetryCounterparts)
                {
                    AssignMultiCubes(symPart, newCubeList, true);
                    NotifyFARIfNeeded(symPart);
                }
            }

            _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;

            NotifyFARIfNeeded(Part);
            EnsureStatsRoutineStarted(Part);
            AllCubesAssigned?.Invoke(this, EventArgs.Empty);

            Destroy(this);
        }

        private static void UpdateCubes(Part p, string shapeKey, bool updateSymCounterparts)
        {
            Profiler.BeginSample("UpdateCubes");
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!UseCache || shapeKey == null || !_cacheDict.TryGetValue(shapeKey, out DragCube dragCube))
            {
                dragCube = DragCubeSystem.Instance.RenderProceduralDragCube(p);
                _cubesRenderedThisFrame++;
                AddCubeToCache(dragCube, shapeKey);
            }
            else
            {
                _cacheHitsThisFrame++;
                dragCube = CloneCube(dragCube);
                if (ValidateCubes)
                    RunCubeValidation(p, dragCube, shapeKey);
            }

            AssignNonMultiCube(p, dragCube, clone: false);

            if (updateSymCounterparts)
            {
                foreach (Part symPart in p.symmetryCounterparts)
                {
                    AssignNonMultiCube(symPart, dragCube, clone: true);
                }
            }

            _elapsedTicks += System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            Profiler.EndSample();

            NotifyFARIfNeeded(p);
            EnsureStatsRoutineStarted(p);
        }

        private static void AddCubeToCache(DragCube dragCube, string shapeKey)
        {
            if (UseCache && shapeKey != null && PartLoader.Instance.IsReady())
            {
                // Keep a pristine copy in cache. I.e the instance must not be be used by a part.
                DragCube clonedCube = CloneCube(dragCube);
                _cacheDict[shapeKey] = clonedCube;
            }
        }

        private static void NotifyFARIfNeeded(Part p)
        {
            if (ModUtils.IsFARInstalled)
                p.SendMessage("GeometryPartModuleRebuildMeshData");
        }

        private static void EnsureStatsRoutineStarted(Part p)
        {
            if (!_statsRoutineStarted && PartLoader.Instance.IsReady())
                p.StartCoroutine(StatsCoroutine());
        }

        private static IEnumerator StatsCoroutine()
        {
            _statsRoutineStarted = true;
            yield return new WaitForEndOfFrame();
            _statsRoutineStarted = false;

            double timeMs = _elapsedTicks / (System.Diagnostics.Stopwatch.Frequency / 1000d);
            Debug.Log($"[DragCubeTool] Rendered {_cubesRenderedThisFrame} cubes; fetched {_cacheHitsThisFrame} from cache; exec time: {timeMs:F1}ms");
            _cacheHitsThisFrame = 0;
            _cubesRenderedThisFrame = 0;
            _elapsedTicks = 0;

            if (_cacheDict.Count > MaxCacheSize && _inProgressMultiCubeRenderings.Count == 0)
            {
                Debug.Log($"[DragCubeTool] Cache limit reached ({_cacheDict.Count} / {MaxCacheSize}), emptying...");
                _cacheDict.Clear();
            }
        }

        private static void AssignNonMultiCube(Part p, DragCube dragCube, bool clone)
        {
            if (clone)
                dragCube = CloneCube(dragCube);

            p.DragCubes.ClearCubes();
            p.DragCubes.Cubes.Add(dragCube);
            p.DragCubes.ResetCubeWeights();
            p.DragCubes.ForceUpdate(true, true, false);
            p.DragCubes.SetDragWeights();
        }

        private static void AssignMultiCubes(Part p, List<DragCube> cubes, bool clone)
        {
            if (clone)
            {
                for (int i = 0; i < cubes.Count; i++)
                {
                    cubes[i] = CloneCube(cubes[i]);
                }
            }

            if (p.DragCubes.Cubes.Count == cubes.Count)
            {
                // Copy over weights from part cubes. Most likely these were already updated to reflect animation state.
                foreach (DragCube c in cubes)
                {
                    DragCube c2 = p.DragCubes.GetCube(c.name);
                    if (c2 != null)
                        c.Weight = c2.Weight;
                }
            }

            p.DragCubes.ClearCubes();
            p.DragCubes.Cubes.AddRange(cubes);
            p.DragCubes.ForceUpdate(true, true, false);
            p.DragCubes.SetDragWeights();
        }

        private static bool PartNeedsMultipleCubes(Part p)
        {
            // Assume a single animation module
            IMultipleDragCube multiCube = p.FindModuleImplementing<IMultipleDragCube>();
            return multiCube?.IsMultipleCubesActive ?? false;
        }

        private static DragCube CloneCube(DragCube dragCube)
        {
            var c = new DragCube
            {
                center = dragCube.center,
                size = dragCube.size,
                name = dragCube.name
            };
            DragCubeList.SetCubeArray(c.area, dragCube.area);
            DragCubeList.SetCubeArray(c.drag, dragCube.drag);
            DragCubeList.SetCubeArray(c.depth, dragCube.depth);
            DragCubeList.SetCubeArray(c.dragModifiers, dragCube.dragModifiers);

            return c;
        }

        private static void RunCubeValidation(Part p, DragCube cacheCube, string shapeKey)
        {
            DragCube renderedCube = DragCubeSystem.Instance.RenderProceduralDragCube(p);
            RunCubeValidation(cacheCube, renderedCube, p, shapeKey);
        }

        private IEnumerator MultiCubeValidationRoutine(List<DragCube> cacheCubeList)
        {
            yield return UpdateMultiCubesRoutine(isValidation: true);

            IMultipleDragCube multiCube = Part.FindModuleImplementing<IMultipleDragCube>();
            string[] names = multiCube.GetDragCubeNames();
            if (names.Length != cacheCubeList.Count)
            {
                Debug.LogError($"[DragCubeTool] Cube count mismatch in MultiCubeValidationRoutine");
                yield break;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string cName = names[i];
                string shapeKey = $"{_shapeKey}${cName}";
                if (!_cacheDict.TryGetValue(shapeKey, out DragCube dragCube))
                {
                    // cache got cleared?
                    Debug.LogWarning($"[DragCubeTool] Failed to fetch {shapeKey} from cache in MultiCubeValidationRoutine");
                    yield break;
                }

                RunCubeValidation(cacheCubeList[i], dragCube, Part, shapeKey);
            }
        }

        private static void RunCubeValidation(DragCube cacheCube, DragCube renderedCube, Part p, string shapeKey)
        {
            // drag components randomly switch places so sort the arrays before comparing
            var cacheSortedDrag = cacheCube.drag.OrderBy(v => v).ToArray();
            var renderSortedDrag = renderedCube.drag.OrderBy(v => v).ToArray();

            if (cacheCube.name != renderedCube.name ||
                !ArraysNearlyEqual(cacheCube.area, renderedCube.area, 0.005f) ||
                !ArraysNearlyEqual(cacheSortedDrag, renderSortedDrag, 0.05f) ||
                //!ArraysNearlyEqual(cacheCube.depth, renderedCube.depth, 0.01f) ||
                !ArraysNearlyEqual(cacheCube.dragModifiers, renderedCube.dragModifiers, 0.005f) ||
                !VectorsNearlyEqual(cacheCube.center, renderedCube.center, 0.005f) ||
                !VectorsNearlyEqual(cacheCube.size, renderedCube.size, 0.005f))
            {
                Debug.LogError($"[DragCubeTool] Mismatch in cached cube for part {p.partInfo.name}, key {shapeKey}:");
                Debug.LogError($"Cache: {cacheCube.SaveToString()}");
                Debug.LogError($"Renderd: {renderedCube.SaveToString()}");
            }
        }

        private static bool ArraysNearlyEqual(float[] arr1, float[] arr2, float tolerance)
        {
            for (int i = 0; i < arr1.Length; i++)
            {
                float a = arr1[i];
                float b = arr2[i];
                if (Math.Abs(a - b) > tolerance)
                    return false;
            }
            return true;
        }

        private static bool VectorsNearlyEqual(Vector3 v1, Vector3 v2, float tolerance)
        {
            return (v1 - v2).sqrMagnitude < tolerance * tolerance;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void SpamDebug(string msg)
        {
            Debug.Log(msg);
        }
    }
}
