using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    internal static class EnemyPerceptionSensor
    {
        internal static void ResolvePlayerReferences(ref XROrigin playerRig, ref Transform playerView)
        {
            if (playerRig == null)
                playerRig = Object.FindObjectOfType<XROrigin>();

            if (playerRig != null && playerView == null)
            {
                if (playerRig.Camera != null)
                {
                    playerView = playerRig.Camera.transform;
                }
                else
                {
                    Camera rigCamera = playerRig.GetComponentInChildren<Camera>(true);
                    if (rigCamera != null)
                        playerView = rigCamera.transform;
                }
            }

            if (playerView == null && Camera.main != null)
                playerView = Camera.main.transform;
        }

        internal static bool TryGetPursuitLockPlayerPosition(
            bool canEvaluate,
            XROrigin playerRig,
            Transform playerView,
            Rigidbody enemyRigidbody,
            Transform enemyTransform,
            Vector3 sensorPosition,
            float groundY,
            float pursuitLockDistance,
            LayerMask obstacleMask,
            Collider[] selfColliders,
            out Vector3 playerGroundPosition)
        {
            playerGroundPosition = enemyTransform != null ? enemyTransform.position : Vector3.zero;
            if (!canEvaluate || playerView == null || pursuitLockDistance <= 0f)
                return false;

            Vector3 playerViewPosition = playerView.position;
            Vector3 lockSource = ResolveBodyPosition(enemyRigidbody, enemyTransform);
            Vector3 playerAnchorPosition = ResolvePlayerAnchorPosition(playerRig, playerView);
            Vector3 planarToPlayer = Vector3.ProjectOnPlane(playerAnchorPosition - lockSource, Vector3.up);
            if (planarToPlayer.magnitude > pursuitLockDistance)
                return false;

            if (!HasLineOfSight(sensorPosition, playerViewPosition, obstacleMask, selfColliders, playerRig, playerView))
                return false;

            playerGroundPosition = ProjectToGround(playerAnchorPosition, groundY);
            return true;
        }

        internal static bool TryGetOccludedSoftLockPlayerPosition(
            bool canEvaluate,
            XROrigin playerRig,
            Transform playerView,
            Rigidbody enemyRigidbody,
            Transform enemyTransform,
            float groundY,
            float occludedSoftLockDistance,
            out Vector3 playerGroundPosition)
        {
            playerGroundPosition = enemyTransform != null ? enemyTransform.position : Vector3.zero;
            if (!canEvaluate || playerView == null || occludedSoftLockDistance <= 0f)
                return false;

            Vector3 playerAnchorPosition = ResolvePlayerAnchorPosition(playerRig, playerView);
            Vector3 lockSource = ResolveBodyPosition(enemyRigidbody, enemyTransform);
            Vector3 planarToPlayer = Vector3.ProjectOnPlane(playerAnchorPosition - lockSource, Vector3.up);
            if (planarToPlayer.magnitude > occludedSoftLockDistance)
                return false;

            playerGroundPosition = ProjectToGround(playerAnchorPosition, groundY);
            return true;
        }

        internal static bool TryDetectPlayer(
            bool detectionSuppressed,
            XROrigin playerRig,
            Transform playerView,
            Transform enemyTransform,
            Vector3 sensorPosition,
            float groundY,
            float detectionRange,
            float fieldOfViewDegrees,
            LayerMask obstacleMask,
            Collider[] selfColliders,
            out Vector3 playerGroundPosition)
        {
            playerGroundPosition = enemyTransform != null ? enemyTransform.position : Vector3.zero;
            if (playerView == null || detectionSuppressed)
                return false;

            Vector3 playerViewPosition = playerView.position;
            Vector3 planarToPlayer = Vector3.ProjectOnPlane(playerViewPosition - sensorPosition, Vector3.up);
            float planarDistance = planarToPlayer.magnitude;
            if (planarDistance > detectionRange)
                return false;

            if (planarDistance <= 0.01f)
            {
                playerGroundPosition = ProjectToGround(playerViewPosition, groundY);
                return true;
            }

            Vector3 forward = enemyTransform != null ? enemyTransform.forward : Vector3.forward;
            float angleToPlayer = Vector3.Angle(forward, planarToPlayer.normalized);
            if (angleToPlayer > fieldOfViewDegrees * 0.5f)
                return false;

            if (!HasLineOfSight(sensorPosition, playerViewPosition, obstacleMask, selfColliders, playerRig, playerView))
                return false;

            playerGroundPosition = ProjectToGround(ResolvePlayerAnchorPosition(playerRig, playerView), groundY);
            return true;
        }

        internal static bool HasLineOfSight(
            Vector3 origin,
            Vector3 destination,
            LayerMask obstacleMask,
            Collider[] selfColliders,
            XROrigin playerRig,
            Transform playerView)
        {
            Vector3 castDirection = destination - origin;
            float castDistance = castDirection.magnitude;
            if (castDistance <= 0.01f)
                return true;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                castDirection / castDistance,
                castDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return true;

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.isTrigger)
                    continue;

                if (IsOwnCollider(hitCollider, selfColliders))
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (playerRig != null && hitRoot == playerRig.transform)
                    continue;

                if (playerView != null && hitCollider.transform.IsChildOf(playerView.root))
                    continue;

                return false;
            }

            return true;
        }

        static Vector3 ResolveBodyPosition(Rigidbody enemyRigidbody, Transform enemyTransform)
        {
            if (enemyRigidbody != null)
                return enemyRigidbody.position;

            return enemyTransform != null ? enemyTransform.position : Vector3.zero;
        }

        static Vector3 ResolvePlayerAnchorPosition(XROrigin playerRig, Transform playerView)
        {
            if (playerRig != null)
                return playerRig.transform.position;

            return playerView != null ? playerView.position : Vector3.zero;
        }

        static Vector3 ProjectToGround(Vector3 worldPosition, float groundY)
        {
            return new Vector3(worldPosition.x, groundY, worldPosition.z);
        }

        static bool IsOwnCollider(Collider candidate, Collider[] selfColliders)
        {
            if (candidate == null || selfColliders == null)
                return false;

            for (int i = 0; i < selfColliders.Length; i++)
            {
                if (selfColliders[i] == candidate)
                    return true;
            }

            return false;
        }
    }
}
