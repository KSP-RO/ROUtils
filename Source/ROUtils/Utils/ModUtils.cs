using System;
using System.Reflection;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace ROUtils
{
    public static class ModUtils
    {
        private static bool _RP1Installed = false;
        private static bool _NeedFindRP1 = true;

        public static bool IsRP1Installed
        {
            get
            {
                if (_NeedFindRP1)
                {
                    _NeedFindRP1 = false;
                    _RP1Installed = AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals("RP-0", StringComparison.OrdinalIgnoreCase));
                }

                return _RP1Installed;
            }
        }

                
        private static bool? _isTestFlightInstalled = null;
        private static bool? _isTestLiteInstalled = null;

        private static PropertyInfo _piTFInstance;
        private static PropertyInfo _piTFSettingsEnabled;
        private static Type _tlSettingsType;
        private static FieldInfo _fiTLSettingsDisabled;

        public static bool IsTestFlightInstalled
        {
            get
            {
                if (!_isTestFlightInstalled.HasValue)
                {
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "TestFlightCore", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isTestFlightInstalled = a != null;
                    if (_isTestFlightInstalled.Value)
                    {
                        Type t = a.GetType("TestFlightCore.TestFlightManagerScenario");
                        _piTFInstance = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        _piTFSettingsEnabled = t?.GetProperty("SettingsEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    }
                }
                return _isTestFlightInstalled.Value;
            }
        }

        public static bool IsTestLiteInstalled
        {
            get
            {
                if (!_isTestLiteInstalled.HasValue)
                {
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "TestLite", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isTestLiteInstalled = a != null;
                    if (_isTestLiteInstalled.Value)
                    {
                        _tlSettingsType = a.GetType("TestLite.TestLiteGameSettings");
                        _fiTLSettingsDisabled = _tlSettingsType?.GetField("disabled");
                    }
                }
                return _isTestLiteInstalled.Value;
            }
        }

        public static void ToggleFailures(bool isEnabled)
        {
            if (IsTestFlightInstalled) ToggleTFFailures(isEnabled);
            else if (IsTestLiteInstalled) ToggleTLFailures(isEnabled);
        }

        public static void ToggleTFFailures(bool isEnabled)
        {
            object tfInstance = _piTFInstance.GetValue(null);
            _piTFSettingsEnabled.SetValue(tfInstance, isEnabled);
        }

        private static void ToggleTLFailures(bool isEnabled)
        {
            _fiTLSettingsDisabled.SetValue(HighLogic.CurrentGame.Parameters.CustomParams(_tlSettingsType), !isEnabled);
            GameEvents.OnGameSettingsApplied.Fire();
        }

        public static bool IsPrincipiaInstalled => _needCheckPrincipia ? FindPrincipia() : _isPrincipiaInstalled;

        private static bool _isPrincipiaInstalled = false;
        private static bool _needCheckPrincipia = true;
        private static bool FindPrincipia()
        {
            _needCheckPrincipia = false;
            _isPrincipiaInstalled = AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals("ksp_plugin_adapter", StringComparison.OrdinalIgnoreCase));
            return _isPrincipiaInstalled;
        }

        public static double PrincipiaCorrectInclination(Orbit o)
        {
            if (ModUtils.IsPrincipiaInstalled && o.referenceBody != (FlightGlobals.currentMainBody ?? Planetarium.fetch.Home))
            {
                Vector3d polarAxis = o.referenceBody.BodyFrame.Z;

                double hSqrMag = o.h.sqrMagnitude;
                if (hSqrMag == 0d)
                {
                    return Math.Acos(Vector3d.Dot(polarAxis, o.pos) / o.pos.magnitude) * (180.0 / Math.PI);
                }
                else
                {
                    Vector3d orbitZ = o.h / Math.Sqrt(hSqrMag);
                    return Math.Atan2((orbitZ - polarAxis).magnitude, (orbitZ + polarAxis).magnitude) * (2d * (180.0 / Math.PI));
                }
            }
            else
            {
                return o.inclination;
            }
        }
    }
}
