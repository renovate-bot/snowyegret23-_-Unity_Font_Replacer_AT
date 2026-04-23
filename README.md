[English README](README_EN.md)

# Unity Font Replacer AT

`Unity_Font_Replacer_AT`는 `Unity_Font_Replacer`의 C# / `AssetsTools.NET` 포트입니다.  
Unity 게임의 TTF 폰트와 SDF 폰트를 스캔, 교체, 추출, 생성합니다.

현재 배포판은 Python 원본처럼 실행 파일이 여러 개로 나뉘지 않고, 하나의 CLI에 기능이 통합되어 있습니다.

## 주요 특징

- TTF `Font` 에셋 교체
- SDF `MonoBehaviour` / atlas / material 교체
- 단일 TTF 입력으로 패딩별 SDF/Raster를 자동 생성하는 `oneshot`
- `Managed` 폴더가 없는 Il2Cpp 게임에서 더미 DLL 자동 생성
- `parse` + `list` 기반 JSON 매핑 작업
- SDF 폰트 추출 (`export`)
- TTF -> SDF 생성 (`makesdf`)
- PS5 swizzle 처리 옵션
- `Mulmaru`(Raster), `NanumGothic`(SDF) 내장 preset 지원

## 배포 구성

릴리즈 ZIP은 보통 아래처럼 구성됩니다.

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

- `UnityFontReplacer_KO.exe`: 한국어 UI
- `UnityFontReplacer_EN.exe`: 영어 UI
- `CharList_3911.txt`: 기본 문자셋 목록
- `ASSETS/`: 내장 교체 폰트 및 SDF 리소스
- `Il2CppDumper/`: Il2Cpp 게임용 더미 `Managed` 생성 도구
- `LICENSE`: 이 저장소의 직접 작성 코드에 대한 라이선스

`classdata.tpk`는 릴리즈에 포함하지 않습니다.  
파일이 없으면 프로그램이 첫 실행 시 자동 다운로드를 시도합니다.

`Managed` 폴더가 없는 Il2Cpp 게임은, 포함된 `Il2CppDumper`로 첫 실행 시 더미 DLL을 자동 생성합니다.

## 빠른 시작

### 일괄 교체

```bat
UnityFontReplacer_KO.exe batch --gamepath "D:\Games\MyGame" --font mulmaru
UnityFontReplacer_EN.exe batch --gamepath "D:\Games\MyGame" --font nanumgothic --sdfonly
```

### 단일 TTF 원샷 교체

```bat
UnityFontReplacer_KO.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf"
UnityFontReplacer_EN.exe oneshot --gamepath "D:\Games\MyGame" --font "NanumGothic.ttf" --sdfonly
```

### 폰트 매핑 JSON 생성

```bat
UnityFontReplacer_KO.exe parse --gamepath "D:\Games\MyGame"
```

실행 파일 기준 폴더에 `MyGame.json` 같은 파일이 생성됩니다.

### JSON 기반 개별 교체

```bat
UnityFontReplacer_KO.exe list --gamepath "D:\Games\MyGame" --file ".\MyGame.json"
```

### SDF 폰트 추출

```bat
UnityFontReplacer_KO.exe export --gamepath "D:\Games\MyGame"
```

실행 파일 기준 폴더 아래 `exported_fonts/`에 JSON, atlas PNG, material JSON이 생성됩니다.

### SDF 생성

```bat
UnityFontReplacer_KO.exe makesdf --ttf ".\MyFont.ttf"
```

출력은 현재 작업 디렉터리의 `ASSETS/` 아래에 저장됩니다.

## 명령 요약

| 명령 | 설명 |
|------|------|
| `batch` | 내장 preset 또는 사용자 폰트 소스로 일괄 교체 |
| `oneshot` | 단일 TTF로 TTF 교체 + 패딩별 SDF/Raster 자동 생성 후 일괄 교체 |
| `parse` | 게임 폰트 정보를 JSON으로 저장 |
| `list` | JSON 매핑 파일 기준 개별 교체 |
| `export` | SDF 폰트 데이터를 `exported_fonts/`로 추출 |
| `makesdf` | TTF에서 SDF 호환 JSON/atlas 생성 |
| `diag` | 디버깅용 번들/에셋 진단 |

## `batch` 사용법

```bat
UnityFontReplacer_KO.exe batch --gamepath "D:\Games\MyGame" --font mulmaru
UnityFontReplacer_KO.exe batch --gamepath "D:\Games\MyGame" --font nanumgothic --ttfonly
UnityFontReplacer_KO.exe batch --gamepath "D:\Games\MyGame" --font ".\MyFontPack" --output-only "D:\output"
UnityFontReplacer_KO.exe batch --gamepath "D:\Games\MyGame" --font mulmaru --ps5-swizzle
```

### 주요 옵션

| 옵션 | 설명 |
|------|------|
| `--gamepath`, `-g` | 게임 루트 또는 `_Data` / `Data` 폴더 경로 |
| `--font`, `-f` | `mulmaru`, `nanumgothic`, 폰트 폴더, `.ttf`, `.otf`, `.json` |
| `--sdfonly` | SDF만 교체 |
| `--ttfonly` | TTF만 교체 |
| `--output-only <dir>` | 원본 대신 수정본만 별도 폴더에 저장 |
| `--ps5-swizzle` | PS5 atlas swizzle 처리 |

### `--font` 입력 규칙

- `mulmaru`, `nanumgothic`: 내장 preset 사용
- 폴더 경로: 폴더 안의 TTF/OTF가 있으면 그것으로 임시 생성, 없으면 JSON/PNG 자산 사용
- `.ttf` / `.otf`: TTF 교체 소스로 사용하며 SDF도 원본 `atlas padding`마다 자동 생성
- `.json`: SDF 소스로 사용

`batch`는 SDF 대상에 대해 `oneshot`처럼 게임 원본 SDF 폰트의 `atlas padding` 값마다 임시 SDF/Raster 세트를 생성해서 교체합니다.  
내장 preset은 기본 생성 모드가 고정되어 있습니다.

- `mulmaru`: `raster`
- `nanumgothic`: `sdf`

## `oneshot` 사용법

```bat
UnityFontReplacer_KO.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf"
UnityFontReplacer_KO.exe oneshot --gamepath "D:\Games\MyGame" --font "NanumGothic.ttf" --sdfonly
UnityFontReplacer_KO.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf" --ttfonly
UnityFontReplacer_KO.exe oneshot --gamepath "D:\Games\MyGame" --font ".\NanumMyongjo.ttf" --output-only "D:\output"
```

### 주요 옵션

| 옵션 | 설명 |
|------|------|
| `--gamepath`, `-g` | 게임 루트 또는 `_Data` / `Data` 폴더 경로 |
| `--font`, `-f` | 입력 TTF/OTF 파일 경로 또는 해석 가능한 폰트 이름 |
| `--sdfonly` | SDF만 교체 |
| `--ttfonly` | TTF만 교체 |
| `--raster` | SDF 교체용 임시 atlas를 Raster로 생성 |
| `--sdf` | SDF 교체용 임시 atlas를 SDF로 생성 |
| `--atlas-size <W,H>` | 임시 atlas 크기 |
| `--point-size <n>` | 임시 생성 point size (`0`이면 자동) |
| `--charset <file-or-text>` | 임시 생성 문자셋 |
| `--filter-mode <mode>` | 임시 atlas 필터 모드 (`point` / `bilinear` / `trilinear`) |
| `--output-only <dir>` | 원본 대신 수정본만 별도 폴더에 저장 |
| `--ps5-swizzle` | PS5 atlas swizzle 처리 |

`oneshot`은 입력한 TTF를 일반 `Font` 교체에 그대로 사용하고, 스캔된 SDF 폰트들의 원본 `atlas padding` 값마다 SDF 세트를 자동 생성한 뒤 해당 padding에 맞춰 일괄 교체합니다.  
기본 생성 모드는 `sdf`이며, 기본 문자셋은 `CharList_3911.txt`, 기본 필터 모드는 `bilinear`입니다.

`oneshot`은 원본 게임의 `atlas padding`을 유지하므로 `--padding` 옵션은 따로 받지 않습니다.

## `parse` + `list` 워크플로

1. `parse`로 JSON 생성
2. JSON의 `Replace_to` 값을 채움
3. `list`로 교체 실행

### 예시

```bat
UnityFontReplacer_KO.exe parse --gamepath "D:\Games\MyGame"
UnityFontReplacer_KO.exe list --gamepath "D:\Games\MyGame" --file ".\MyGame.json"
```

### JSON 예시

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

`Replace_to`를 비워두면 해당 항목은 건너뜁니다.

SDF 항목의 `Replace_to`는 아래 둘 다 지원합니다.

- `Mulmaru` 같은 기존 SDF 세트 이름 또는 `ASSETS` 내 폴더/JSON 경로
- `NanumMyongjo.ttf` / `.\MyFont.otf` 같은 TTF/OTF 경로

SDF 항목의 `Replace_to`에 TTF/OTF를 넣으면 `list`가 `oneshot`처럼 해당 게임 원본 SDF 폰트의 `atlas padding` 값을 사용해 임시 SDF 세트를 자동 생성한 뒤 교체합니다.  
이때도 기본 문자셋은 `CharList_3911.txt`, 기본 필터 모드는 `Bilinear`입니다.

## `export`

```bat
UnityFontReplacer_KO.exe export --gamepath "D:\Games\MyGame"
```

출력 위치:

```text
exported_fonts/
├── Some Font SDF.json
├── Some Font SDF Atlas.png
└── Some Font SDF Material.json
```

## `makesdf`

```bat
UnityFontReplacer_KO.exe makesdf --ttf ".\Mulmaru.ttf"
UnityFontReplacer_KO.exe makesdf --ttf ".\Mulmaru.ttf" --padding 15
UnityFontReplacer_KO.exe makesdf --ttf ".\Mulmaru.ttf" --charset ".\charset.txt"
UnityFontReplacer_KO.exe makesdf --ttf ".\Mulmaru.ttf" --rendermode raster
UnityFontReplacer_KO.exe makesdf --ttf ".\Mulmaru.ttf" --filter-mode point
```

| 옵션 | 설명 | 기본값 |
|------|------|--------|
| `--ttf` | 입력 TTF/OTF | 필수 |
| `--atlas-size` | atlas 크기 (`W,H`) | `4096,4096` |
| `--point-size` | point size (`0`이면 자동) | `0` |
| `--padding` | atlas padding | `7` |
| `--charset` | charset 파일 또는 문자열 | `./CharList_3911.txt` |
| `--rendermode` | `sdf` / `raster` | `sdf` |
| `--filter-mode` | Unity 텍스처 필터 모드 (`point` / `bilinear` / `trilinear`) | `bilinear` |

기본 `sdf` 생성은 일반 텍스트 폰트 기준의 SDFAA 계열 내부 생성 경로를 사용합니다.  
`raster`는 픽셀 폰트/비-SDF atlas용입니다.

`makesdf` 결과는 현재 작업 폴더의 `ASSETS/` 아래에 자동 저장됩니다.

## 커스텀 폰트 추가

`ASSETS/` 또는 사용자 지정 폴더에 아래 파일을 두면 됩니다.

| 파일 | 용도 |
|------|------|
| `FontName.ttf` 또는 `FontName.otf` | TTF 교체 |
| `FontName SDF.json` | SDF 데이터 |
| `FontName SDF Atlas.png` | SDF atlas |
| `FontName SDF Material.json` | SDF material 선택 항목 |

## 소스에서 빌드

요구 사항:

- .NET 8 SDK
- Git submodule 초기화

```bat
git submodule update --init --recursive
dotnet build .\Unity_Font_Replacer_AT\UnityFontReplacer.csproj -c Release
dotnet msbuild .\Unity_Font_Replacer_AT\UnityFontReplacer.csproj /t:PublishLocalizedVariants /p:Configuration=Release /p:VariantPublishDir="%CD%\publish\"
```

생성물:

- `publish\UnityFontReplacer_KO.exe`
- `publish\UnityFontReplacer_EN.exe`
- `publish\CharList_3911.txt`
- `publish\LICENSE`
- `publish\ASSETS\`
- `publish\Il2CppDumper\`

## GitHub Release

`.github/workflows/release.yml`은 수동 실행 기준으로 KO/EN ZIP 두 개를 만듭니다.

- `Unity_Font_Replacer_AT_KO_vX.Y.Z.zip`
- `Unity_Font_Replacer_AT_EN_vX.Y.Z.zip`

워크플로는 `classdata.tpk`를 포함하지 않고 KO/EN ZIP만 만든 뒤 draft release를 생성합니다.  
`classdata.tpk`는 사용자가 프로그램을 실행할 때 자동 다운로드됩니다.

## 주의 사항

- 게임 파일 수정 전 백업을 권장합니다.
- 일부 게임은 무결성 검사로 수정 파일을 복구합니다.
- `diag`는 최종 사용자용 기능보다는 문제 분석용입니다.
- `AssetsTools.NET`은 서브모듈로 포함되며 이 저장소에서 직접 수정하지 않는 것을 전제로 합니다.
- `Managed`가 없는 Il2Cpp 게임에서는 `GameAssembly.dll`, `global-metadata.dat`, `Il2CppDumper`가 필요합니다.

## License

이 저장소의 직접 작성 코드에는 [MIT License](LICENSE)가 적용됩니다.  
단, 서브모듈, 번들된 외부 도구, 자동 다운로드되는 파일, 폰트/게임 데이터 등 제3자 구성요소에는 각자의 라이선스와 권리 조건이 그대로 적용되며, 이 저장소의 `LICENSE`로 재라이선스되지 않습니다.

## Special Thanks

- 원본 프로젝트 [Unity_Font_Replacer](https://github.com/snowyegret23/Unity_Font_Replacer)
- Unity 에셋 읽기/쓰기 기반을 제공하는 [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET)
- `classdata.tpk`를 제공하는 [AssetRipper/Tpk](https://github.com/AssetRipper/Tpk)
- Il2Cpp 더미 `Managed` 생성을 위해 사용하는 [Perfare/Il2CppDumper](https://github.com/Perfare/Il2CppDumper)
- CLI/렌더링/폰트 처리에 사용한 `System.CommandLine`, `Spectre.Console`, `SixLabors.ImageSharp`, `SixLabors.Fonts`, `SixLabors.ImageSharp.Drawing`

## 면책

- 이 프로젝트는 비공식 독립 도구이며 Unity Technologies, TextMeshPro, 개별 게임 개발사/배급사, 폰트 제작사와 관계가 없고 승인이나 후원을 받지 않습니다.
- `Unity`, `TextMeshPro` 및 각 게임/폰트 명칭은 각 권리자의 자산입니다.
- 이 소프트웨어는 `LICENSE`에 따라 `as is`로 제공되며, 명시적 또는 묵시적 보증 없이 사용자가 책임지고 사용해야 합니다.
- 사용자는 각 게임의 이용 약관, 폰트 라이선스, 저작권 및 관련 법규를 직접 확인하고 준수해야 합니다.
