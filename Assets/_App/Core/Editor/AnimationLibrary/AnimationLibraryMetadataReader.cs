using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace ProjectS.Core.Editor.AnimationLibrary
{
    internal static class AnimationLibraryMetadataReader
    {
        public const string ImportRoot = "Assets/_Game/Animations/Imported/FromSoftwarePlayer/";

        public static bool TryReadForModel(
            string modelAssetPath,
            out AnimationLibraryImportMetadata metadata,
            out string error)
        {
            metadata = null;
            error = null;

            if (!modelAssetPath.StartsWith(ImportRoot, StringComparison.Ordinal) ||
                !modelAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string metadataAssetPath = Path.ChangeExtension(modelAssetPath, ".animlib.json");
            string metadataPath = Path.GetFullPath(metadataAssetPath);
            if (!File.Exists(metadataPath))
            {
                error = $"Animation-library metadata is missing: {metadataAssetPath}";
                return true;
            }

            try
            {
                metadata = JsonUtility.FromJson<AnimationLibraryImportMetadata>(File.ReadAllText(metadataPath));
            }
            catch (Exception exception)
            {
                error = $"Cannot parse {metadataAssetPath}: {exception.Message}";
                return true;
            }

            if (metadata == null || metadata.SchemaVersion != 1 || string.IsNullOrWhiteSpace(metadata.LibraryID))
            {
                error = $"Unsupported or incomplete animation-library metadata: {metadataAssetPath}";
                metadata = null;
                return true;
            }

            string actualHash = ComputeSha256(Path.GetFullPath(modelAssetPath));
            if (!string.Equals(actualHash, metadata.FBXSHA256, StringComparison.OrdinalIgnoreCase))
            {
                error = $"FBX hash does not match its contract: {modelAssetPath}";
                metadata = null;
                return true;
            }

            string avatarPath = Path.GetFullPath(metadata.TargetAvatarAsset ?? string.Empty);
            if (!File.Exists(avatarPath) ||
                string.IsNullOrWhiteSpace(metadata.TargetAvatarSHA256) ||
                !string.Equals(
                    ComputeSha256(avatarPath),
                    metadata.TargetAvatarSHA256,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"Target Avatar hash does not match the import contract: {metadata.TargetAvatarAsset}";
                metadata = null;
            }

            return true;
        }

        public static string ComputeSha256(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
