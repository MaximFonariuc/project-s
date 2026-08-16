using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectS.Core.Editor.AnimationLibrary
{
    internal static class AnimationLibraryOrganizer
    {
        private const string CatalogRelativePath =
            "RPG_Animation_Library/Database/player_animation_catalog.json";

        private const string ReceiptRelativePath =
            "RPG_Animation_Library/08_UNITY/phase_9/organization_receipt.json";

        private static readonly string[] LegacyFolders =
        {
            "Attack",
            "Dodge",
            "Locomotion",
            "Unknown",
        };

        [MenuItem("Tools/Project S/Animation Library/Organize Player Animations")]
        private static void Organize()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root was not found.");
            CatalogFile catalog = LoadCatalog(Path.Combine(projectRoot, CatalogRelativePath));
            Dictionary<string, CatalogEntry> entries = catalog.entries.ToDictionary(
                entry => entry.LibraryID,
                StringComparer.Ordinal);
            string importRootPath = Path.Combine(
                projectRoot,
                AnimationLibraryMetadataReader.ImportRoot.TrimEnd('/'));
            string[] metadataPaths = Directory.GetFiles(
                importRootPath,
                "*.animlib.json",
                SearchOption.AllDirectories);
            if (metadataPaths.Length != entries.Count)
            {
                throw new InvalidOperationException(
                    $"Catalog has {entries.Count} entries, but Unity contains {metadataPaths.Length} contracts.");
            }

            var operations = new List<MoveOperation>(metadataPaths.Length);
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string absoluteMetadataPath in metadataPaths)
            {
                string metadataAssetPath = ToAssetPath(projectRoot, absoluteMetadataPath);
                AnimationLibraryImportMetadata metadata = ReadMetadata(absoluteMetadataPath);
                if (!entries.TryGetValue(metadata.LibraryID, out CatalogEntry entry))
                {
                    throw new InvalidOperationException(
                        $"Catalog entry is missing for {metadata.LibraryID} ({metadataAssetPath}).");
                }

                string sourceFbx = metadataAssetPath.EndsWith(
                    ".animlib.json",
                    StringComparison.OrdinalIgnoreCase)
                    ? metadataAssetPath[..^".animlib.json".Length] + ".fbx"
                    : throw new InvalidOperationException(
                        $"Unexpected metadata extension: {metadataAssetPath}");
                string destinationFolder =
                    $"{AnimationLibraryMetadataReader.ImportRoot}{entry.RelativeFolder}";
                string destinationFbx = $"{destinationFolder}/{entry.FileStem}.fbx";
                string destinationMetadata = $"{destinationFolder}/{entry.FileStem}.animlib.json";
                if (!destinations.Add(destinationFbx) || !destinations.Add(destinationMetadata))
                {
                    throw new InvalidOperationException($"Duplicate catalog destination: {destinationFbx}");
                }

                string guid = AssetDatabase.AssetPathToGUID(sourceFbx);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    throw new InvalidOperationException($"FBX asset is missing: {sourceFbx}");
                }

                operations.Add(
                    new MoveOperation(
                        metadata,
                        entry,
                        sourceFbx,
                        metadataAssetPath,
                        destinationFolder,
                        destinationFbx,
                        destinationMetadata,
                        guid));
            }

            foreach (string folder in operations.Select(operation => operation.DestinationFolder).Distinct())
            {
                EnsureFolder(folder);
            }

            int movedCount = 0;
            int unchangedCount = 0;
            var receiptEntries = new List<ReceiptEntry>(operations.Count);
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int index = 0; index < operations.Count; index++)
                {
                    MoveOperation operation = operations[index];
                    EditorUtility.DisplayProgressBar(
                        "Organizing player animations",
                        operation.Entry.AnimationID,
                        (float)index / operations.Count);
                    bool moved = MoveAsset(operation.SourceFbx, operation.DestinationFbx);
                    moved |= MoveAsset(operation.SourceMetadata, operation.DestinationMetadata);
                    if (moved)
                    {
                        movedCount++;
                    }
                    else
                    {
                        unchangedCount++;
                    }

                    ApplyCatalogMetadata(operation.Metadata, operation.Entry);
                    string destinationMetadataPath = Path.Combine(
                        projectRoot,
                        operation.DestinationMetadata.Replace('/', Path.DirectorySeparatorChar));
                    string serializedMetadata = JsonUtility.ToJson(operation.Metadata, true) + Environment.NewLine;
                    if (!File.Exists(destinationMetadataPath) ||
                        !string.Equals(
                            File.ReadAllText(destinationMetadataPath),
                            serializedMetadata,
                            StringComparison.Ordinal))
                    {
                        File.WriteAllText(destinationMetadataPath, serializedMetadata);
                    }
                    receiptEntries.Add(
                        new ReceiptEntry
                        {
                            LibraryID = operation.Entry.LibraryID,
                            AnimationID = operation.Entry.AnimationID,
                            AssetPath = operation.DestinationFbx,
                            AssetGuid = operation.AssetGuid,
                            CatalogType = operation.Entry.CatalogType,
                            ReviewStatus = operation.Entry.ReviewStatus,
                        });
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            foreach (MoveOperation operation in operations)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(operation.DestinationFbx);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"Organized FBX cannot be loaded: {operation.DestinationFbx}");
                }

                string[] expectedLabels = operation.Metadata.Labels ?? Array.Empty<string>();
                if (!LabelsMatch(AssetDatabase.GetLabels(asset), expectedLabels))
                {
                    AssetDatabase.SetLabels(asset, expectedLabels);
                }
                string actualGuid = AssetDatabase.AssetPathToGUID(operation.DestinationFbx);
                if (!string.Equals(actualGuid, operation.AssetGuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"GUID changed while moving {operation.Entry.AnimationID}: " +
                        $"{operation.AssetGuid} -> {actualGuid}");
                }
            }

            RemoveEmptyLegacyFolders(projectRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var receipt = new OrganizationReceipt
            {
                SchemaVersion = 1,
                Status = "PASS",
                Timestamp = DateTime.UtcNow.ToString("O"),
                CatalogPath = CatalogRelativePath,
                ImportRoot = AnimationLibraryMetadataReader.ImportRoot,
                EntryCount = receiptEntries.Count,
                MovedCount = movedCount,
                UnchangedCount = unchangedCount,
                Entries = receiptEntries.OrderBy(entry => entry.AnimationID).ToArray(),
            };
            string receiptPath = Path.Combine(projectRoot, ReceiptRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)
                ?? throw new InvalidOperationException("Organization receipt directory is missing."));
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true) + Environment.NewLine);
            Debug.Log(
                $"[AnimationLibrary] Organized {receipt.EntryCount} player clips: " +
                $"moved={movedCount}, unchanged={unchangedCount}. Receipt: {ReceiptRelativePath}");
        }

        [MenuItem("Tools/Project S/Animation Library/Organize Player Animations", true)]
        private static bool CanOrganize() =>
            !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        private static CatalogFile LoadCatalog(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Generate the player animation catalog before organizing Unity assets.",
                    path);
            }

            CatalogFile catalog = JsonUtility.FromJson<CatalogFile>(File.ReadAllText(path));
            if (catalog == null || catalog.schemaVersion != 1 || catalog.entries == null)
            {
                throw new InvalidOperationException($"Unsupported player animation catalog: {path}");
            }

            return catalog;
        }

        private static AnimationLibraryImportMetadata ReadMetadata(string path)
        {
            AnimationLibraryImportMetadata metadata =
                JsonUtility.FromJson<AnimationLibraryImportMetadata>(File.ReadAllText(path));
            if (metadata == null || metadata.SchemaVersion != 1 || string.IsNullOrWhiteSpace(metadata.LibraryID))
            {
                throw new InvalidOperationException($"Invalid animation contract: {path}");
            }

            return metadata;
        }

        private static void ApplyCatalogMetadata(
            AnimationLibraryImportMetadata metadata,
            CatalogEntry entry)
        {
            metadata.CatalogCategory = entry.CatalogCategory;
            metadata.CatalogType = entry.CatalogType;
            metadata.ClassificationSource = entry.ClassificationSource;
            metadata.Evidence = entry.Evidence;
            metadata.ReviewStatus = entry.ReviewStatus;
            metadata.IDBank = entry.IDBank;
            metadata.WeaponFamily = entry.WeaponFamily;
            metadata.WeaponClassificationSource = entry.WeaponClassificationSource;
            metadata.WeaponEvidence = entry.WeaponEvidence;
            var labels = new List<string>(metadata.Labels ?? Array.Empty<string>());
            labels.RemoveAll(
                label => label.StartsWith("Catalog:", StringComparison.Ordinal) ||
                         label.StartsWith("CatalogType:", StringComparison.Ordinal) ||
                         label.StartsWith("ReviewStatus:", StringComparison.Ordinal) ||
                         label.StartsWith("IDBank:", StringComparison.Ordinal) ||
                         label.StartsWith("WeaponFamily:", StringComparison.Ordinal));
            labels.Add($"Catalog:{entry.CatalogCategory}");
            labels.Add($"CatalogType:{entry.CatalogType}");
            labels.Add($"ReviewStatus:{entry.ReviewStatus}");
            labels.Add($"IDBank:{entry.IDBank}");
            if (!string.IsNullOrWhiteSpace(entry.WeaponFamily) &&
                !string.Equals(entry.WeaponFamily, "Unknown", StringComparison.Ordinal))
            {
                labels.Add($"WeaponFamily:{entry.WeaponFamily}");
            }
            metadata.Labels = labels.ToArray();
        }

        private static bool MoveAsset(string source, string destination)
        {
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination)))
            {
                throw new InvalidOperationException($"Destination already exists: {destination}");
            }

            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Cannot move {source} to {destination}: {error}");
            }

            return true;
        }

        private static bool LabelsMatch(string[] actual, string[] expected)
        {
            actual ??= Array.Empty<string>();
            expected ??= Array.Empty<string>();
            return actual.Length == expected.Length &&
                   new HashSet<string>(actual, StringComparer.Ordinal).SetEquals(expected);
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] segments = assetFolder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[index]);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        throw new InvalidOperationException($"Cannot create Unity folder: {next}");
                    }
                }

                current = next;
            }
        }

        private static void RemoveEmptyLegacyFolders(string projectRoot)
        {
            string importRoot = AnimationLibraryMetadataReader.ImportRoot.TrimEnd('/');
            foreach (string folderName in LegacyFolders)
            {
                string assetPath = $"{importRoot}/{folderName}";
                string absolutePath = Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(absolutePath) && !Directory.EnumerateFileSystemEntries(absolutePath).Any())
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }

        private static string ToAssetPath(string projectRoot, string absolutePath)
        {
            string relative = Path.GetRelativePath(projectRoot, absolutePath).Replace('\\', '/');
            if (!relative.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Path is outside Assets: {absolutePath}");
            }

            return relative;
        }

        private readonly struct MoveOperation
        {
            public MoveOperation(
                AnimationLibraryImportMetadata metadata,
                CatalogEntry entry,
                string sourceFbx,
                string sourceMetadata,
                string destinationFolder,
                string destinationFbx,
                string destinationMetadata,
                string assetGuid)
            {
                Metadata = metadata;
                Entry = entry;
                SourceFbx = sourceFbx;
                SourceMetadata = sourceMetadata;
                DestinationFolder = destinationFolder;
                DestinationFbx = destinationFbx;
                DestinationMetadata = destinationMetadata;
                AssetGuid = assetGuid;
            }

            public AnimationLibraryImportMetadata Metadata { get; }
            public CatalogEntry Entry { get; }
            public string SourceFbx { get; }
            public string SourceMetadata { get; }
            public string DestinationFolder { get; }
            public string DestinationFbx { get; }
            public string DestinationMetadata { get; }
            public string AssetGuid { get; }
        }

        [Serializable]
        private sealed class CatalogFile
        {
            public int schemaVersion;
            public CatalogEntry[] entries;
        }

        [Serializable]
        private sealed class CatalogEntry
        {
            public string LibraryID;
            public string AnimationID;
            public string RelativeFolder;
            public string FileStem;
            public string CatalogCategory;
            public string CatalogType;
            public string ClassificationSource;
            public string Evidence;
            public string ReviewStatus;
            public string IDBank;
            public string WeaponFamily;
            public string WeaponClassificationSource;
            public string WeaponEvidence;
        }

        [Serializable]
        private sealed class OrganizationReceipt
        {
            public int SchemaVersion;
            public string Status;
            public string Timestamp;
            public string CatalogPath;
            public string ImportRoot;
            public int EntryCount;
            public int MovedCount;
            public int UnchangedCount;
            public ReceiptEntry[] Entries;
        }

        [Serializable]
        private sealed class ReceiptEntry
        {
            public string LibraryID;
            public string AnimationID;
            public string AssetPath;
            public string AssetGuid;
            public string CatalogType;
            public string ReviewStatus;
        }
    }
}
