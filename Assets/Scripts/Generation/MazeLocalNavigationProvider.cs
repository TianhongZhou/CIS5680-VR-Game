using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    public abstract class MazeLocalNavigationProvider : MonoBehaviour
    {
        public abstract bool TryProjectPoint(
            MazeNavigationModuleRecord module,
            Vector3 desiredWorldPosition,
            out Vector3 projectedWorldPosition);

        public abstract bool TryBuildPath(
            MazeNavigationModuleRecord module,
            Vector3 startWorldPosition,
            Vector3 desiredWorldPosition,
            List<Vector3> waypointBuffer);

        public virtual bool TryBuildPath(
            MazeNavigationModuleRecord fromModule,
            MazeNavigationModuleRecord toModule,
            Vector3 startWorldPosition,
            Vector3 desiredWorldPosition,
            List<Vector3> waypointBuffer)
        {
            if (toModule != null)
                return TryBuildPath(toModule, startWorldPosition, desiredWorldPosition, waypointBuffer);

            return fromModule != null
                && TryBuildPath(fromModule, startWorldPosition, desiredWorldPosition, waypointBuffer);
        }
    }
}
