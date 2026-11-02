using System;
using UnityEngine;

namespace RailRider
{
    // Integration explicitly owns one registration and disposes it on unload.
    public static class ContactCallbacks
    {
        static Registration current;
        public static IDisposable Register(Action<Vector3> discharged, Func<Collider, Vector3, Vector3, bool> impact)
        {
            if (discharged == null || impact == null) throw new ArgumentNullException("Both callbacks are required");
            if (current != null) throw new InvalidOperationException("Contact callbacks already registered");
            return current = new Registration(discharged, impact);
        }
        sealed class Registration : IDisposable
        {
            internal readonly Action<Vector3> discharged;
            internal readonly Func<Collider, Vector3, Vector3, bool> impact;
            internal Registration(Action<Vector3> d, Func<Collider, Vector3, Vector3, bool> i) { discharged = d; impact = i; }
            public void Dispose() { if (ReferenceEquals(current, this)) current = null; }
        }
        internal static void NotifyDischarged(Vector3 muzzle)
        {
            var r = current;
            if (r == null) return;
            try { r.discharged(muzzle); } catch (Exception ex) { Debug.LogException(ex); }
        }
        internal static bool OfferImpact(Collider collider, Vector3 point, Vector3 direction)
        {
            var r = current;
            if (r == null) return false;
            try { return r.impact(collider, point, direction); }
            catch (Exception ex) { Debug.LogException(ex); return false; }
        }
    }
}
