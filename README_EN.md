[Korean README](README.md)

# Unity Font Replacer AT

`Unity_Font_Replacer_AT` is the C# / `AssetsTools.NET` port of `Unity_Font_Replacer`.  
It scans, replaces, exports, and generates Unity TTF and SDF fonts.

Unlike the original Python release layout, the C# port exposes its features through one CLI.

## Features

- Replace Unity `Font` TTF assets
- Replace SDF `MonoBehaviour`, atlas, and material data
- `oneshot` mode that auto-generates per-padding SDF/Raster assets from a single TTF
- Auto-generate dummy DLLs for Il2Cpp games when `Managed` is missing
- JSON-based `parse` + `list` workflow
- Export SDF font assets with `export`
- Generate SDF-compatible data with `makesdf`
- Optional PS5 swizzle handling
- Built-in `Mulmaru` (Raster) and `NanumGothic` (SDF) presets

## Release Layout

Release ZIPs typically look like this:

```text
release/
├── UnityFontReplacer_KO.exe
├── CharList_3911.txt
├── ASSETS/
├── Il2CppDumper/
├── LICENSE
└── README.md

release_en/
├── UnityFontReplacer_EN.exe
├── CharList_3911.txt
├── ASSETS/
├── Il2CppDumper/
├── LICENSE
└── README_EN.md
```

- `UnityFontReplacer_KO.exe`: Korean UI
- `UnityFontReplacer_EN.exe`: English UI
- `CharList_3911.txt`: default charset list
- `ASSETS/`: bundled replacement fonts and SDF resources
- `Il2CppDumper/`: helper for generating dummy `Managed` DLLs on Il2Cpp games
- `LICENSE`: license for the code authored in this repository

`classdata.tpk` is not included in releases.  
If it is missing, the tool downloads it on first run.

For Il2Cpp games without a `Managed` folder, the bundled `Il2CppDumper` is used automatically on first run to generate dummy DLLs.

## Quick Start

### Bulk replacement

```bat
UnityFontReplacer_KO.exe batch --gamepath "D:\Games\MyGame" --font mulmaru
UnityFontReplacer_EN.exe batch --gamepath "D:\Games\MyGame" --font nanumgothic --sdfonly
```

### One-shot TTF replacement

```bat
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf"
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font "NanumGothic.ttf" --sdfonly
```

### Generate font mapping JSON

```bat
UnityFontReplacer_EN.exe parse --gamepath "D:\Games\MyGame"
```

This writes a file such as `MyGame.json` next to the executable.

### Replace from JSON mapping

```bat
UnityFontReplacer_EN.exe list --gamepath "D:\Games\MyGame" --file ".\MyGame.json"
```

### Export SDF fonts

```bat
UnityFontReplacer_EN.exe export --gamepath "D:\Games\MyGame"
```

Exported files go into `exported_fonts/` next to the executable.

### Generate SDF data

```bat
UnityFontReplacer_EN.exe makesdf --ttf ".\MyFont.ttf"
```

Output files are written under `ASSETS/` in the current working directory.

## Command Summary

| Command | Description |
|---------|-------------|
| `batch` | Bulk replacement using a built-in preset or custom font source |
| `oneshot` | Bulk replacement from a single TTF with auto-generated per-padding SDF/Raster assets |
| `parse` | Save detected game font information as JSON |
| `list` | Replace selected fonts from a JSON mapping |
| `export` | Export SDF font data into `exported_fonts/` |
| `makesdf` | Generate SDF-compatible JSON/atlas data from TTF |
| `diag` | Bundle / assets diagnostic helper |

## `batch`

```bat
UnityFontReplacer_EN.exe batch --gamepath "D:\Games\MyGame" --font mulmaru
UnityFontReplacer_EN.exe batch --gamepath "D:\Games\MyGame" --font nanumgothic --ttfonly
UnityFontReplacer_EN.exe batch --gamepath "D:\Games\MyGame" --font ".\MyFontPack" --output-only "D:\output"
UnityFontReplacer_EN.exe batch --gamepath "D:\Games\MyGame" --font mulmaru --ps5-swizzle
```

### Important options

| Option | Description |
|--------|-------------|
| `--gamepath`, `-g` | Game root or `_Data` / `Data` directory |
| `--font`, `-f` | `mulmaru`, `nanumgothic`, a font folder, `.ttf`, `.otf`, or `.json` |
| `--sdfonly` | Replace SDF only |
| `--ttfonly` | Replace TTF only |
| `--output-only <dir>` | Write modified files to another folder instead of in-place |
| `--ps5-swizzle` | Enable PS5 atlas swizzle handling |

### Accepted `--font` forms

- `mulmaru`, `nanumgothic`: built-in presets
- directory path: if the folder contains TTF/OTF, batch auto-generates temporary replacements; otherwise it uses the provided JSON/PNG assets
- `.ttf` / `.otf`: TTF source for `Font` replacement, with auto-generated SDF replacements per original target padding
- `.json`: prebuilt SDF source

For SDF targets, `batch` now behaves like `oneshot`: it auto-generates temporary SDF/Raster assets for each original target `atlas padding` value it finds in the game.  
Built-in presets use fixed generation modes:

- `mulmaru`: `raster`
- `nanumgothic`: `sdf`

## `oneshot`

```bat
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf"
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font "NanumGothic.ttf" --sdfonly
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf" --ttfonly
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf" --output-only "D:\output"
```

### Important options

| Option | Description |
|--------|-------------|
| `--gamepath`, `-g` | Game root or `_Data` / `Data` directory |
| `--font`, `-f` | Input TTF/OTF path or a resolvable font name |
| `--sdfonly` | Replace SDF only |
| `--ttfonly` | Replace TTF only |
| `--raster` | Generate temporary raster atlases for SDF replacements |
| `--sdf` | Generate temporary SDF atlases for SDF replacements |
| `--atlas-size <W,H>` | Temporary atlas size |
| `--point-size <n>` | Temporary generation point size (`0` = auto) |
| `--charset <file-or-text>` | Temporary generation charset |
| `--filter-mode <mode>` | Temporary atlas filter mode (`point` / `bilinear` / `trilinear`) |
| `--output-only <dir>` | Write modified files to another folder instead of in-place |
| `--ps5-swizzle` | Enable PS5 atlas swizzle handling |

`oneshot` uses the input TTF directly for Unity `Font` replacement, then auto-generates SDF assets for each original target `atlas padding` value found during the scan and applies those generated assets automatically.  
The default generation mode is `sdf`, the default charset is `CharList_3911.txt`, and the default filter mode is `bilinear`.

`oneshot` preserves the original target game's `atlas padding`, so it does not expose a `--padding` override.

## `parse` + `list` workflow

1. Run `parse` to generate JSON.
2. Fill `Replace_to` for entries you want to replace.
3. Run `list` with that JSON file.

### Example

```bat
UnityFontReplacer_EN.exe parse --gamepath "D:\Games\MyGame"
UnityFontReplacer_EN.exe list --gamepath "D:\Games\MyGame" --file ".\MyGame.json"
```

### JSON example

```json
{
  "game_path": "D:\\Games\\MyGame",
  "unity_version": "2021.3.16f1",
  "fonts": {
    "resources.assets|resources.assets|NotoSansKR-Medium SDF|SDF|1827": {
      "File": "resources.assets",
      "assets_name": "resources.assets",
      "Path_ID": 1827,
      "Type": "SDF",
      "Name": "NotoSansKR-Medium SDF",
      "Replace_to": "Mulmaru",
      "schema": "New",
      "glyph_count": 11172,
      "atlas_path_id": 1828,
      "atlas_padding": 7
    }
  }
}
```

If `Replace_to` is empty, that entry is skipped.

For SDF entries, `Replace_to` supports both of these forms:

- an existing SDF set name or a folder/JSON path under `ASSETS`
- a TTF/OTF path such as `NanumMyongjo.ttf` or `.\MyFont.otf`

If an SDF entry uses a TTF/OTF in `Replace_to`, `list` behaves like `oneshot` for that entry: it auto-generates a temporary SDF set using that target game's original SDF `atlas padding`, then applies the generated replacement automatically.  
The default charset is still `CharList_3911.txt`, and the default texture filter mode is `Bilinear`.

## `export`

```bat
UnityFontReplacer_EN.exe export --gamepath "D:\Games\MyGame"
```

Output layout:

```text
exported_fonts/
├── Some Font SDF.json
├── Some Font SDF Atlas.png
└── Some Font SDF Material.json
```

## `makesdf`

```bat
UnityFontReplacer_EN.exe makesdf --ttf ".\Mulmaru.ttf"
UnityFontReplacer_EN.exe makesdf --ttf ".\Mulmaru.ttf" --padding 15
UnityFontReplacer_EN.exe makesdf --ttf ".\Mulmaru.ttf" --charset ".\charset.txt"
UnityFontReplacer_EN.exe makesdf --ttf ".\Mulmaru.ttf" --rendermode raster
UnityFontReplacer_EN.exe makesdf --ttf ".\Mulmaru.ttf" --filter-mode point
```

| Option | Description | Default |
|--------|-------------|---------|
| `--ttf` | input TTF/OTF | required |
| `--atlas-size` | atlas size (`W,H`) | `4096,4096` |
| `--point-size` | point size (`0` = auto) | `0` |
| `--padding` | atlas padding | `7` |
| `--charset` | charset file or literal string | `./CharList_3911.txt` |
| `--rendermode` | `sdf` / `raster` | `sdf` |
| `--filter-mode` | Unity texture filter mode (`point` / `bilinear` / `trilinear`) | `bilinear` |

Default `sdf` generation now uses an internal SDFAA-style path intended for normal text fonts.  
`raster` remains for pixel-font / non-SDF atlas output.

`makesdf` now saves its generated files automatically under `ASSETS/` in the current working directory.

## Adding custom fonts

Place these files under `ASSETS/` or another font folder:

| File | Purpose |
|------|---------|
| `FontName.ttf` or `FontName.otf` | TTF replacement |
| `FontName SDF.json` | SDF font data |
| `FontName SDF Atlas.png` | SDF atlas |
| `FontName SDF Material.json` | optional SDF material |

## Build from source

Requirements:

- .NET 8 SDK
- initialized Git submodules

```bat
git submodule update --init --recursive
dotnet build .\Unity_Font_Replacer_AT\UnityFontReplacer.csproj -c Release
dotnet msbuild .\Unity_Font_Replacer_AT\UnityFontReplacer.csproj /t:PublishLocalizedVariants /p:Configuration=Release /p:VariantPublishDir="%CD%\publish\"
```

Generated files:

- `publish\UnityFontReplacer_KO.exe`
- `publish\UnityFontReplacer_EN.exe`
- `publish\CharList_3911.txt`
- `publish\LICENSE`
- `publish\ASSETS\`
- `publish\Il2CppDumper\`

## GitHub Release

`.github/workflows/release.yml` builds two ZIPs from a manual workflow dispatch:

- `Unity_Font_Replacer_AT_KO_vX.Y.Z.zip`
- `Unity_Font_Replacer_AT_EN_vX.Y.Z.zip`

The workflow does not ship `classdata.tpk`.  
Users download it automatically when the executable starts.

## Notes

- Back up game files before editing them.
- Some games restore modified files through integrity checks.
- `diag` is intended for troubleshooting rather than regular end users.
- `AssetsTools.NET` is included as a Git submodule and should remain vendor-owned.
- For Il2Cpp games without `Managed`, `GameAssembly.dll`, `global-metadata.dat`, and the bundled `Il2CppDumper` are required.

## License

The original code written for this repository is released under the [MIT License](LICENSE).  
However, third-party components such as submodules, bundled external tools, auto-downloaded files, fonts, and game data remain under their own licenses and rights, and are not relicensed by this repository's `LICENSE`.

## Special Thanks

- The original [Unity_Font_Replacer](https://github.com/snowyegret23/Unity_Font_Replacer) project
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) for the Unity asset read/write foundation
- [AssetRipper/Tpk](https://github.com/AssetRipper/Tpk) for providing `classdata.tpk`
- [Perfare/Il2CppDumper](https://github.com/Perfare/Il2CppDumper) for Il2Cpp dummy `Managed` generation
- `System.CommandLine`, `Spectre.Console`, `SixLabors.ImageSharp`, `SixLabors.Fonts`, and `SixLabors.ImageSharp.Drawing` for CLI, rendering, and font-processing support

## Disclaimer

- This project is an unofficial independent tool and is not affiliated with, endorsed by, or sponsored by Unity Technologies, TextMeshPro, any game developer/publisher, or any font vendor.
- `Unity`, `TextMeshPro`, and all game/font names remain the property of their respective owners.
- This software is provided `as is` under the `LICENSE`, without express or implied warranties, and users assume responsibility for its use.
- Users are responsible for checking and complying with each game's terms, font licenses, copyright rules, and applicable laws.
