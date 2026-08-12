using UnityEngine;

namespace _App
{
    public sealed class Ladder : MonoBehaviour, IInteractable
    {
        [Header("Ladder Path")]
        [SerializeField] private float _pathLength = 10.0f;
        [SerializeField] private Vector3 _pathOffset = new Vector3(0f, 0f, -0.5f);

        [Header("Anchor Points")]
        [SerializeField] private Transform _topPoint;
        [SerializeField] private Transform _bottomPoint;
        public Vector3 BottomAnchorPoint => transform.position + transform.TransformVector(_pathOffset);
        public Vector3 TopAnchorPoint => BottomAnchorPoint + transform.up * _pathLength;
        
        public Transform TopPoint => _topPoint;
        public Transform BottomPoint => _bottomPoint;
        
        public Transform ClosestPointOnPath(Vector3 position, out float pathPosition)
        {
            Vector3 path = TopAnchorPoint - BottomAnchorPoint;
            Vector3 pathToPoint = position - BottomAnchorPoint;
            
            float height = Vector3.Dot(pathToPoint, path.normalized);

            if (height > 0.0f)
            {
                pathPosition = height - path.magnitude;
                return TopPoint;
            }
            
            pathPosition = height;
            return BottomPoint;
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(BottomAnchorPoint, TopAnchorPoint);

            if (_bottomPoint == null || _topPoint == null)
                return;

            Gizmos.DrawWireCube(_bottomPoint.position, Vector3.one * 0.25f);
            Gizmos.DrawWireCube(_topPoint.position, Vector3.one * 0.25f);
        }
    }
}