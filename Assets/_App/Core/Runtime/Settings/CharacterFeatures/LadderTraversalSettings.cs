using UnityEngine;

namespace _App
{
    [CreateAssetMenu(
        fileName = "LadderTraversalSettings",
        menuName = "Project S/Character/Ladder Traversal")]
    public sealed class LadderTraversalSettings : CharacterFeatureSettings
    {
        [SerializeField] private LayerMask _ladderMask = 1;

        public LayerMask LadderMask => _ladderMask;
    }
}
