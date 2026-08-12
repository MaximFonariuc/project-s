using System;
using UnityEngine;

namespace _App
{
    public static class CollisionDetection
    {
        private const int MAX_HITS = 8;
        private static readonly RaycastHit[] _hitsBuffer = new RaycastHit[MAX_HITS];
        
        public static int Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction, out RaycastHit closestHit, RaycastHit[] hits,
            IColliderFilter colliderFilter)
        {
            closestHit = default;

            int rawHitCount = Physics.RaycastNonAlloc(origin, direction, _hitsBuffer, distance, layerMask,
                queryTriggerInteraction);

            if (rawHitCount == 0)
                return 0;

            int filteredHitCount = 0;
            float closestDistance = Mathf.Infinity;

            Array.Clear(hits, 0, hits.Length);

            for (int i = 0; i < rawHitCount; i++)
            {
                if (_hitsBuffer[i].distance <= 0.0f ||
                    colliderFilter != null && colliderFilter.Filter(_hitsBuffer[i].collider))
                    continue;

                if (_hitsBuffer[i].distance < closestDistance)
                {
                    closestHit = _hitsBuffer[i];
                    closestDistance = closestHit.distance;
                }

                hits[filteredHitCount++] = _hitsBuffer[i];
            }

            return filteredHitCount;
        }

        public static int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction, out RaycastHit closestHit, RaycastHit[] hits,
            IColliderFilter colliderFilter, float backStepDistance)
        {
            closestHit = default;

            Vector3 optOrigin = origin - direction * backStepDistance;
            float optDistance = distance + backStepDistance;

            int rawHitCount = Physics.SphereCastNonAlloc(optOrigin, radius, direction, _hitsBuffer, optDistance,
                layerMask, queryTriggerInteraction);

            if (rawHitCount == 0)
                return 0;

            int filteredHitCount = 0;
            float closestDistance = Mathf.Infinity;

            Array.Clear(hits, 0, hits.Length);

            for (int i = 0; i < rawHitCount; i++)
            {
                if (_hitsBuffer[i].distance <= 0.0f ||
                    colliderFilter != null && colliderFilter.Filter(_hitsBuffer[i].collider))
                    continue;

                _hitsBuffer[i].distance -= backStepDistance;

                if (_hitsBuffer[i].distance < closestDistance)
                {
                    closestHit = _hitsBuffer[i];
                    closestDistance = closestHit.distance;
                }

                hits[filteredHitCount++] = _hitsBuffer[i];
            }

            return filteredHitCount;
        }

        public static int CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float distance,
            int layerMask, QueryTriggerInteraction queryTriggerInteraction, out RaycastHit closestHit,
            RaycastHit[] hits, IColliderFilter colliderFilter, float backStepDistance)
        {
            closestHit = default;

            Vector3 optPoint1 = point1 - direction * backStepDistance;
            Vector3 optPoint2 = point2 - direction * backStepDistance;

            float optDistance = distance + backStepDistance;

            int rawHitCount = Physics.CapsuleCastNonAlloc(optPoint1, optPoint2, radius, direction, _hitsBuffer,
                optDistance, layerMask, queryTriggerInteraction);

            if (rawHitCount == 0)
                return 0;

            int filteredHitCount = 0;
            float closestDistance = Mathf.Infinity;

            Array.Clear(hits, 0, hits.Length);

            for (int i = 0; i < rawHitCount; i++)
            {
                if (_hitsBuffer[i].distance <= 0.0f ||
                    colliderFilter != null && colliderFilter.Filter(_hitsBuffer[i].collider))
                    continue;

                _hitsBuffer[i].distance -= backStepDistance;

                if (_hitsBuffer[i].distance < closestDistance)
                {
                    closestHit = _hitsBuffer[i];
                    closestDistance = closestHit.distance;
                }

                hits[filteredHitCount++] = _hitsBuffer[i];
            }

            return filteredHitCount;
        }

        public static int OverlapCapsule(Vector3 point1, Vector3 point2, float radius, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction, Collider[] results, IColliderFilter colliderFilter)
        {
            int rawOverlapCount =
                Physics.OverlapCapsuleNonAlloc(point1, point2, radius, results, layerMask, queryTriggerInteraction);

            if (rawOverlapCount == 0)
                return 0;

            int filteredOverlapCount = rawOverlapCount;

            for (int i = 0; i < rawOverlapCount; i++)
            {
                Collider overlappedCollider = results[i];

                if (colliderFilter != null && !colliderFilter.Filter(overlappedCollider))
                    continue;

                if (i < --filteredOverlapCount)
                    results[i] = results[filteredOverlapCount];
            }

            return filteredOverlapCount;
        }
    }
}