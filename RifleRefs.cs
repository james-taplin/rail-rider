using UnityEngine;

namespace RailRider
{
    /// <summary>
    /// Everything the gameplay needs from a rifle model, as plain Transform references.
    /// A final model only has to provide this component (or children with the fallback names below).
    /// Gameplay never reads mesh names, hierarchy depth or dimensions.
    /// </summary>
    public class RifleRefs : MonoBehaviour
    {
        public Transform lever;      // pivot of the moving cocking lever (rotates about local X)
        public Transform rearSight;  // eye-side sight reference (point the eye looks through)
        public Transform frontSight; // muzzle-side sight reference (point the eye looks at)
        public Transform muzzle;     // shots leave here
        public Transform chamber;    // point behind the muzzle on the bore; the muzzle-blocked check starts here
        public Transform rackDock;   // SnapPointAnchor child: aligned to the rack's snap target when racked
        // Poses are relative to the player camera (the rifle floats; there is no hand model).
        // Carry: lower right, muzzle angled slightly inward/down. Cock: pulled in and rolled so the lever swings clear of the body.
        public Vector3 holdLocalPos = new Vector3(0.18f, -0.20f, 0.62f);
        public Vector3 holdLocalEuler = new Vector3(6f, -10f, 0f);
        public Vector3 cockLocalPos = new Vector3(0.12f, -0.17f, 0.58f);
        public Vector3 cockLocalEuler = new Vector3(12f, -6f, 38f);
        public Vector3 leverAxis = Vector3.right;
        public float leverOpenDegrees = 70f;

        public void ResolveByName()
        {
            lever ??= Find("Lever"); rearSight ??= Find("RearSight"); frontSight ??= Find("FrontSight");
            muzzle ??= Find("Muzzle"); chamber ??= Find("Chamber"); rackDock ??= Find("RackDock");
        }

        Transform Find(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        public bool Valid => lever && rearSight && frontSight && muzzle && chamber;
    }

    public class RifleHoldAnchor : MonoBehaviour, ICustomNonVRGrabAnchor
    {
        public (Vector3 localPos, Quaternion localRot) GetGrabAnchor()
        {
            var r = GetComponent<RifleRefs>();
            return (r.holdLocalPos, Quaternion.Euler(r.holdLocalEuler));
        }
    }
}
