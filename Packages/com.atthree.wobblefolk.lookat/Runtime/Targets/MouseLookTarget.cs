using UnityEngine;
#if WOBBLEFOLK_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Wobblefolk.LookAt
{
    public enum ScreenPointSource
    {
        /// <summary>The mouse / pen / touch pointer.</summary>
        Pointer = 0,
        /// <summary>The centre of the screen, e.g. for gamepad-driven or locked-cursor cameras.</summary>
        ScreenCenter = 1,
        /// <summary>Screen centre while the cursor is locked or no pointer exists, else the pointer.</summary>
        Auto = 2,
    }

    /// <summary>
    /// Looks where the player points: casts a ray from the camera through the mouse position (or the screen centre)
    /// and returns the surface it hits, or a far point along the ray. Uses the Input System when the project has it,
    /// otherwise the legacy Input Manager.
    /// </summary>
    [AddComponentMenu("Wobblefolk/Look At/Mouse Look Target")]
    public sealed class MouseLookTarget : RayLookTarget
    {
        public ScreenPointSource source = ScreenPointSource.Auto;

        protected override Ray BuildRay(Camera cam)
        {
            return cam.ScreenPointToRay(ScreenPoint(cam));
        }

        Vector2 ScreenPoint(Camera cam)
        {
            Vector2 center = new Vector2(cam.pixelRect.center.x, cam.pixelRect.center.y);
            bool useCenter = source == ScreenPointSource.ScreenCenter
                             || (source == ScreenPointSource.Auto && Cursor.lockState == CursorLockMode.Locked);
            if (useCenter)
            {
                return center;
            }

            return TryGetPointer(out Vector2 pointer) ? pointer : center;
        }

        static bool TryGetPointer(out Vector2 position)
        {
#if WOBBLEFOLK_INPUT_SYSTEM
            if (Pointer.current != null)
            {
                position = Pointer.current.position.ReadValue();
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.mousePresent)
            {
                position = Input.mousePosition;
                return true;
            }
#endif
            position = default;
            return false;
        }
    }
}
