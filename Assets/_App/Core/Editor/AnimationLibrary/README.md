# Animation Library Unity importer

Editor-only boundary that turns a Phase 8 FBX plus its adjacent `.animlib.json` contract into a deterministic Unity animation asset. It is Project S integration code, so it belongs to `Core/Editor` and does not become a reusable runtime module.

```mermaid
classDiagram
    class AnimationLibraryImportMetadata {
        +LibraryID string
        +AnimationID string
        +FBXSHA256 string
        +TargetAvatarAsset string
        +RootMotionStatus string
        +ClipName string
        +HasRootMotion bool
    }
    class AnimationLibraryMetadataReader {
        +TryReadForModel(modelAssetPath, metadata, error) bool
        +ComputeSha256(path) string
    }
    class AnimationLibraryAssetPostprocessor {
        -OnPreprocessModel()
        -OnPreprocessAnimation()
        -OnPostprocessModel(importedModel)
    }
    class AnimationLibraryImportValidator {
        -ValidatePhase9Pilot()
        -ValidateImport(assetPath, guid) ImportResult
    }

    AnimationLibraryMetadataReader --> AnimationLibraryImportMetadata : parses and verifies
    AnimationLibraryAssetPostprocessor --> AnimationLibraryMetadataReader : import gate
    AnimationLibraryImportValidator --> AnimationLibraryMetadataReader : validation gate
    AnimationLibraryAssetPostprocessor --> ModelImporter : configures
    AnimationLibraryImportValidator --> ModelImporter : inspects
```

Dependencies point only toward Unity Editor APIs and the adjacent metadata contract. Runtime assemblies, gameplay state and Animator controllers do not depend on this tree.

Import flow:

1. `AnimationLibraryMetadataReader` limits processing to the pilot import root, parses the contract and rejects an FBX whose SHA-256 differs.
2. `AnimationLibraryAssetPostprocessor` applies Humanoid/Avatar, clip, root-motion and label policies during the matching importer callbacks.
3. `AnimationLibraryImportValidator` independently reads the imported sub-assets and importer settings, then writes the Phase 9 receipt consumed by the external pipeline ledger.

The source Avatar is loaded from the contract. Clip semantics are never inferred by the importer; `Probable` and `Unknown` catalog entries remain unverified after a successful technical import.
