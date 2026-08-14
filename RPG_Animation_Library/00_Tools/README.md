# Tool registry

Third-party binaries and Blender add-ons are stored locally under the ignored `../Elden/.pipeline` directory. They are not committed or copied into Unity assets.

| Tool | Pinned version | License | Archive SHA-256 | Role |
|---|---:|---|---|---|
| WitchyBND | 3.0.1.0 | GPL-3.0 | `a3e6b2a0f7eac13f5e83b6602a1149322439c0662baa140ecdd84be28af50364` | DCX/BND inspection and controlled unpacking |
| DSAnimStudio | 4.9.9 | GPL-3.0 | `21a0f7e327bfefbae8ead9bc61121b18d5b2f9455f39f3c3cc8ce00a0291305b` | Animation/TAE preview and semantic identification |
| Soulstruct for Blender | 2.6.0 | No SPDX license declared by GitHub repository metadata | `19106b66494b327791d0a33d2a022fc490b6d12a0658a334eb45e5ee59b3e3bd` | Elden Ring HKX and skeleton bridge |
| Blender | 5.2.0 LTS | GPL | installed application | Conversion, inspection, retargeting, and FBX export |
| Unity Editor | 6000.0.80f1 | Unity license | installed application | Final import and Project S integration |

The configured paths are recorded in `config/pipeline.json`. Paths are resolved relative to this library root unless they are absolute.

The Soulstruct add-on was validated in Blender 5.2 by initializing Blender's GPU module before add-on registration in background mode. Its Elden Ring animation support remains experimental and must stay behind a pilot gate.

Phase 2 invokes WitchyBND with its silent CLI flag (`-s`) and passes only a copied container below `02_UNPACKED`. The configured executable SHA-256 is verified before the first job starts.
