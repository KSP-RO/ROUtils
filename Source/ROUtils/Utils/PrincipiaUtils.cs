using System;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace ROUtils
{
    public static class PrincipiaUtils
    {
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
            if (IsPrincipiaInstalled && o.referenceBody != (FlightGlobals.currentMainBody ?? Planetarium.fetch.Home))
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
