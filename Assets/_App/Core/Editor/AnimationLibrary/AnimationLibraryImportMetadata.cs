using System;

namespace ProjectS.Core.Editor.AnimationLibrary
{
    [Serializable]
    internal sealed class AnimationLibraryImportMetadata
    {
        public int SchemaVersion;
        public string LibraryID;
        public string AnimationID;
        public string SourceHash;
        public string SourceBlendSHA256;
        public string FBXSHA256;
        public string PipelineVersion;
        public string SkeletonProfile;
        public string SourceSkeletonProfile;
        public string RetargetProfile;
        public string RootMotionStatus;
        public float[] RootTranslation;
        public string ExportTimestamp;
        public string FriendlyName;
        public string SemanticConfidence;
        public string CharacterType;
        public string WeaponFamily;
        public string ActionType;
        public string ActionVariant;
        public string Direction;
        public bool Loop;
        public int FrameCount;
        public int FrameRate;
        public float DurationSeconds;
        public string UnityImportProfile;
        public string TargetAvatarAsset;
        public string[] Labels;

        public bool HasRootMotion => !string.Equals(RootMotionStatus, "InPlace", StringComparison.Ordinal);

        public string ClipName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ActionVariant) &&
                    !string.Equals(ActionVariant, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    return ActionVariant;
                }

                if (!string.IsNullOrWhiteSpace(FriendlyName) &&
                    !string.Equals(FriendlyName, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    return FriendlyName.Replace(" ", string.Empty);
                }

                return AnimationID;
            }
        }
    }
}
