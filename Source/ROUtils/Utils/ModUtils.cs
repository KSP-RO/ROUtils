using System;
using System.Reflection;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace ROUtils
{
    public static class ModUtils
    {
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
    }
}
