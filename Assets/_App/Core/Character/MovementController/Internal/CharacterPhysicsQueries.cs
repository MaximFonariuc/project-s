using UnityEngine;

namespace _App
{
    internal sealed class CharacterPhysicsQueries
    {
        private readonly RaycastHit[] _hits;
        private readonly Collider[] _overlaps;
        private readonly CharacterCollisionFilter _filter;

        public CharacterPhysicsQueries(int hitCapacity, int overlapCapacity, CharacterCollisionFilter filter)
        {
            _hits = new RaycastHit[hitCapacity];
            _overlaps = new Collider[overlapCapacity];
            _filter = filter;
        }

        public RaycastHit[] Hits => _hits;

        public Collider[] Overlaps => _overlaps;

        public int OverlapCapsule(Vector3 bottom, Vector3 top, float radius, int layerMask, Collider[] results,
            QueryTriggerInteraction triggerInteraction, CharacterMovement.ColliderFilterCallback externalFilter)
        {
            int rawCount = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, results, layerMask, triggerInteraction);
            int filteredCount = 0;

            for (int i = 0; i < rawCount; i++)
            {
                Collider candidate = results[i];
                if (_filter.ShouldFilter(candidate, externalFilter))
                    continue;

                results[filteredCount++] = candidate;
            }

            return filteredCount;
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask,
            QueryTriggerInteraction triggerInteraction, CharacterMovement.ColliderFilterCallback externalFilter,
            out RaycastHit hitResult, float thickness)
        {
            hitResult = default;

            int rawHitCount = thickness == 0.0f
                ? Physics.RaycastNonAlloc(origin, direction, _hits, distance, layerMask, triggerInteraction)
                : Physics.SphereCastNonAlloc(origin - direction * thickness, thickness, direction, _hits, distance,
                    layerMask, triggerInteraction);

            float closestDistance = Mathf.Infinity;
            int hitIndex = -1;

            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (hit.distance <= 0.0f || _filter.ShouldFilter(hit.collider, externalFilter))
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex < 0)
                return false;

            hitResult = _hits[hitIndex];
            return true;
        }

        public int CapsuleCastRaw(Vector3 bottom, Vector3 top, float radius, Vector3 direction, float distance,
            int layerMask, QueryTriggerInteraction triggerInteraction)
        {
            return Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, _hits, distance, layerMask,
                triggerInteraction);
        }

        public bool CapsuleCast(Vector3 bottom, Vector3 top, float radius, Vector3 direction, float distance,
            int layerMask, QueryTriggerInteraction triggerInteraction,
            CharacterMovement.ColliderFilterCallback externalFilter, out RaycastHit hitResult,
            out bool startPenetrating)
        {
            hitResult = default;
            startPenetrating = false;

            int rawHitCount = CapsuleCastRaw(bottom, top, radius, direction, distance, layerMask,
                triggerInteraction);

            float closestDistance = Mathf.Infinity;
            int hitIndex = -1;

            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (_filter.ShouldFilter(hit.collider, externalFilter))
                    continue;

                if (hit.distance <= 0.0f)
                    startPenetrating = true;
                else if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex < 0)
                return false;

            hitResult = _hits[hitIndex];
            return true;
        }

        public bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 direction,
            float distance, int layerMask, QueryTriggerInteraction triggerInteraction,
            CharacterMovement.ColliderFilterCallback externalFilter, out RaycastHit hitResult,
            out bool startPenetrating)
        {
            hitResult = default;
            startPenetrating = false;

            int rawHitCount = Physics.BoxCastNonAlloc(center, halfExtents, direction, _hits, orientation, distance,
                layerMask, triggerInteraction);

            float closestDistance = Mathf.Infinity;
            int hitIndex = -1;

            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (_filter.ShouldFilter(hit.collider, externalFilter))
                    continue;

                if (hit.distance <= 0.0f)
                    startPenetrating = true;
                else if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex < 0)
                return false;

            hitResult = _hits[hitIndex];
            return true;
        }

        public void SortHitsByDistance(int length)
        {
            for (int i = 1; i < length; i++)
            {
                RaycastHit key = _hits[i];
                int j = i - 1;

                while (j >= 0 && key.distance < _hits[j].distance)
                {
                    _hits[j + 1] = _hits[j];
                    j--;
                }

                _hits[j + 1] = key;
            }
        }
    }
}
