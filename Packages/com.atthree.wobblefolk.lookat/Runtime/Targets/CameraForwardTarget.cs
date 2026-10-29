using UnityEngine;

namespace Wobblefolk.LookAt
{
    /// <summary>Looks where the camera looks: along the camera's forward axis, optionally stopping at the first surface.</summary>
    [AddComponentMenu("Wobblefolk/Look At/Camera Forward Target")]
    public sealed class CameraForwardTarget : RayLookTarget
    {
        protected override Ray BuildRay(Camera cam)
        {
            Transform t = cam.transform;
            return new Ray(t.position, t.forward);
        }
    }
}
