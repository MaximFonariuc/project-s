# Immutable RAW mapping

This directory is the logical RAW boundary. Project S already stores the physical source data under `../Elden`, so the files are referenced through `config/pipeline.json` instead of being duplicated here.

The mapped sources are immutable:

- `PlayerRepo` — `../Elden/All Characters` and its original ZIP archive;
- `EldenVins` — `../Elden/ELDEN VINS` and its original ZIP archive;
- `EldenRing_Vanilla` — not currently present.

Generated data must never be written into those locations.
