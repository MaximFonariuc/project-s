[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Config
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-LibraryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $LibraryRoot,
        [Parameter(Mandatory = $true)]
        [string] $ConfiguredPath
    )

    if ([IO.Path]::IsPathRooted($ConfiguredPath)) {
        return [IO.Path]::GetFullPath($ConfiguredPath)
    }

    return [IO.Path]::GetFullPath((Join-Path $LibraryRoot $ConfiguredPath))
}

function Get-HavocFieldValues {
    param(
        [Parameter(Mandatory = $true)]
        [object] $HavocObject
    )

    $fields = @{}
    foreach ($entry in $HavocObject.Value.GetEnumerator()) {
        $fields[$entry.Key.Name] = $entry.Value.Value
    }
    return $fields
}

function Get-WeaponEvidence {
    param(
        [string[]] $BehaviorNames = @()
    )

    # A family is accepted only when every behavior name mapped to the animation
    # contains the same explicit weapon token. Shared/generic behavior stays unknown.
    $patterns = [ordered]@{
        DuelingShield  = "(?i)DuelingShield|DulellingShield"
        Torch          = "(?i)Torch|Toche"
        Crossbow       = "(?i)Crossbow"
        Bow            = "(?i)(?<!Cross)Bow|Arrow"
        Spear          = "(?i)Spear"
        Axe            = "(?i)Axe"
        LightGreatsword = "(?i)LightLargeSword"
        Greatsword     = "(?i)(?<!Light)GreatSword|(?<!Light)LargeSword"
        Katana         = "(?i)Katana"
        Rapier         = "(?i)Rapier"
        Fist           = "(?i)Fist"
        Claw           = "(?i)Claw"
        Whip           = "(?i)Whip"
        Flail          = "(?i)Flail"
        Hammer         = "(?i)Hammer"
        Reaper         = "(?i)Scythe|Reaper"
        Twinblade      = "(?i)TwinBlade"
        Halberd        = "(?i)Halberd"
        Staff          = "(?i)Staff"
        SacredSeal     = "(?i)SacredSeal|SealMagic"
        Shield         = "(?i)Shield"
    }

    $families = [Collections.Generic.List[string]]::new()
    foreach ($pattern in $patterns.GetEnumerator()) {
        if (($BehaviorNames -join "|") -match $pattern.Value) {
            $families.Add($pattern.Key)
        }
    }

    # A specialized shield name also matches the broad Shield token.
    if ($families.Contains("DuelingShield")) {
        [void] $families.Remove("Shield")
    }

    if ($families.Count -ne 1) {
        return @{
            Family = ""
            Confidence = if ($families.Count -gt 1) { "AmbiguousNameToken" } else { "" }
        }
    }

    $family = $families[0]
    $familyPattern = $patterns[$family]
    $nonFamilyNames = @($BehaviorNames | Where-Object { $_ -notmatch $familyPattern })
    if ($nonFamilyNames.Count -gt 0) {
        return @{
            Family = ""
            Confidence = "SharedBehavior"
        }
    }

    return @{
        Family = $family
        Confidence = "ExclusiveNameEvidence"
    }
}

$configPath = [IO.Path]::GetFullPath($Config)
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Pipeline config was not found: $configPath"
}

$configuration = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$configDirectory = Split-Path -Parent $configPath
$libraryRoot = [IO.Path]::GetFullPath(
    (Join-Path (Split-Path -Parent $configDirectory) ([string] $configuration.project_root)))
$catalogPath = Join-Path $libraryRoot "Database/player_animation_catalog.csv"
$behaviorBinder = Resolve-LibraryPath $libraryRoot ([string] $configuration.semantic_preview.behavior_binder)
$witchy = Resolve-LibraryPath $libraryRoot ([string] $configuration.tools.witchybnd)
$dsAnimStudio = Resolve-LibraryPath $libraryRoot ([string] $configuration.tools.dsanimstudio)
$havocAssembly = Join-Path (Split-Path -Parent $dsAnimStudio) "Havoc.dll"
$outputCsv = Join-Path $libraryRoot "Database/player_animation_behavior_evidence.csv"
$outputJson = Join-Path $libraryRoot "Database/player_animation_behavior_evidence.json"

foreach ($requiredPath in @($catalogPath, $behaviorBinder, $witchy, $havocAssembly)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required behavior-evidence input was not found: $requiredPath"
    }
}

$catalog = @(Import-Csv -LiteralPath $catalogPath -Encoding UTF8)
$catalogIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($row in $catalog) {
    [void] $catalogIds.Add([string] $row.AnimationID)
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "project-s-behavior-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

try {
    $binderCopy = Join-Path $temporaryRoot "c0000.behbnd.dcx"
    Copy-Item -LiteralPath $behaviorBinder -Destination $binderCopy
    & $witchy -s $binderCopy
    if ($LASTEXITCODE -ne 0) {
        throw "WitchyBND exited with code $LASTEXITCODE while unpacking the behavior binder."
    }

    $behaviorHkx = @(Get-ChildItem -LiteralPath $temporaryRoot -Recurse -File -Filter "c0000.hkx" |
        Where-Object { $_.Directory.Name -eq "Behaviors" })
    if ($behaviorHkx.Count -ne 1) {
        throw "Expected one Behaviors/c0000.hkx after unpacking, found $($behaviorHkx.Count)."
    }

    $havoc = [Reflection.Assembly]::LoadFrom($havocAssembly)
    $readerType = $havoc.GetType("Havoc.IO.Tagfile.Binary.HkBinaryTagfileReader", $true)
    $readAllObjects = @($readerType.GetMethods() | Where-Object {
        $_.Name -eq "ReadAllObjects" -and
        $_.IsStatic -and
        $_.GetParameters().Count -eq 2 -and
        $_.GetParameters()[0].ParameterType -eq [string]
    })
    if ($readAllObjects.Count -ne 1) {
        throw "Compatible Havoc ReadAllObjects(string, string) API was not found."
    }

    $objects = $readAllObjects[0].Invoke($null, @($behaviorHkx[0].FullName, $null))
    $behaviorByAnimation = @{}
    foreach ($object in $objects) {
        if ($object.Type.Name -ne "CustomManualSelectorGenerator") {
            continue
        }

        $fields = Get-HavocFieldValues $object
        $animationId = "a000_{0:D6}" -f [int] $fields.animId
        if (-not $catalogIds.Contains($animationId)) {
            continue
        }

        if (-not $behaviorByAnimation.ContainsKey($animationId)) {
            $behaviorByAnimation[$animationId] = [ordered]@{
                Names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                OffsetTypes = [Collections.Generic.HashSet[int]]::new()
                EnableScript = [Collections.Generic.HashSet[bool]]::new()
                EnableTae = [Collections.Generic.HashSet[bool]]::new()
                MappingCount = 0
            }
        }

        $evidence = $behaviorByAnimation[$animationId]
        [void] $evidence.Names.Add([string] $fields.name)
        [void] $evidence.OffsetTypes.Add([int] $fields.offsetType)
        [void] $evidence.EnableScript.Add([bool] $fields.enableScript)
        [void] $evidence.EnableTae.Add([bool] $fields.enableTae)
        $evidence.MappingCount++
    }

    $rows = foreach ($catalogRow in ($catalog | Sort-Object AnimationID)) {
        $animationId = [string] $catalogRow.AnimationID
        $mapped = $behaviorByAnimation.ContainsKey($animationId)
        $names = if ($mapped) {
            @($behaviorByAnimation[$animationId].Names | Sort-Object)
        }
        else {
            @()
        }
        $weapon = Get-WeaponEvidence $names
        [pscustomobject][ordered]@{
            LibraryID = [string] $catalogRow.LibraryID
            AnimationID = $animationId
            BehaviorMapped = $mapped
            BehaviorMappingCount = if ($mapped) { $behaviorByAnimation[$animationId].MappingCount } else { 0 }
            BehaviorNames = $names -join "|"
            OffsetTypes = if ($mapped) {
                @($behaviorByAnimation[$animationId].OffsetTypes | Sort-Object) -join "|"
            } else { "" }
            EnableScriptValues = if ($mapped) {
                @($behaviorByAnimation[$animationId].EnableScript | Sort-Object) -join "|"
            } else { "" }
            EnableTaeValues = if ($mapped) {
                @($behaviorByAnimation[$animationId].EnableTae | Sort-Object) -join "|"
            } else { "" }
            WeaponFamily = [string] $weapon.Family
            WeaponEvidenceConfidence = [string] $weapon.Confidence
            Source = "ELDEN VINS:c0000.behbnd.dcx/CustomManualSelectorGenerator"
        }
    }

    $rows | Export-Csv -LiteralPath $outputCsv -NoTypeInformation -Encoding utf8BOM
    $summary = [ordered]@{
        schemaVersion = 1
        source = "ELDEN VINS:c0000.behbnd.dcx/CustomManualSelectorGenerator"
        catalogEntries = $rows.Count
        behaviorMapped = @($rows | Where-Object BehaviorMapped).Count
        behaviorUnmapped = @($rows | Where-Object { -not $_.BehaviorMapped }).Count
        exclusiveWeaponEvidence = @($rows | Where-Object {
            $_.WeaponEvidenceConfidence -eq "ExclusiveNameEvidence"
        }).Count
        sharedBehaviorRejected = @($rows | Where-Object {
            $_.WeaponEvidenceConfidence -eq "SharedBehavior"
        }).Count
        ambiguousWeaponEvidence = @($rows | Where-Object {
            $_.WeaponEvidenceConfidence -eq "AmbiguousNameToken"
        }).Count
        weaponFamilies = [ordered]@{}
        entries = $rows
    }
    foreach ($group in ($rows | Where-Object { $_.WeaponFamily } |
        Group-Object WeaponFamily | Sort-Object Name)) {
        $summary.weaponFamilies[$group.Name] = $group.Count
    }
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputJson -Encoding utf8

    Write-Output "BEHAVIOR_EVIDENCE_COMPLETE:$($rows.Count)"
    Write-Output "  BehaviorMapped=$($summary.behaviorMapped)"
    Write-Output "  ExclusiveWeaponEvidence=$($summary.exclusiveWeaponEvidence)"
    foreach ($family in $summary.weaponFamilies.GetEnumerator()) {
        Write-Output "  $($family.Key)=$($family.Value)"
    }
}
finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedSystemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (
        $resolvedTemporaryRoot.StartsWith($resolvedSystemTemp, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemporaryRoot).StartsWith(
            "project-s-behavior-", [StringComparison]::Ordinal)
    ) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
