# Fluxion Viewer

Edit, view and convert Fluxion files easily with Fluxion Viewer.

This program uses [FluxionSharp](https://github.com/haltroy/FluxionSharp) to read/write
[Fluxion](https://github.com/haltroy/Fluxion) files.

This program is released under GNU GPL v3. [Click here](./LICENSE) to read more.

## Features

- Load/Save un/compressed Fluxion files.
- Convert Fluxion files from one encoding and/or compression to another.
- Cut, Copy, Paste
- Name, Value, Value Type
- Attributes
- Auto-save with encoding
- Cross-platform, available in:
   - Windows (64-bit Intel/AMD/ARM)
   - Linux (generic, for both GCC and MUSL, 64-bit Intel/AMD/ARM)
   - Debian-based distributions (64-bit Intel/AMD/ARM)
   - Red Hat/Fedora based distributions (64-bit Intel/AMD/ARM)
   - Arch Linux-based distributions (using PKGBUILD, 64-bit Intel/AMD/ARM)
   - Android
   - macOS (not officially, need to build the app)
   - iOS (not officially, need to build the app)

Please refer to the appropiate OS version from the project file for that specific platform (ex. FluxionViewer.Android/FluxionViewer.Android.csproj).

## Installation

Releases for Windows (Intel, AMD, ARM), Android (as APK) and generic binaries and distribution-specific packages for Linux are available on [Releases](https://github.com/Haltroy/FluxionViewer/releases).

For Arch-based Linux users, download the PKGBUILD files and put them in a folder. Then run `makepkg -i` inside that folder to build & install FluxionViewer.
 - [Normal](https://raw.githubusercontent.com/Haltroy/FluxionViewer/refs/heads/main/linux/arch/main/PKGBUILD) (builds the latest release) [`fluxionviewer`]
 - [Binary](https://raw.githubusercontent.com/Haltroy/FluxionViewer/refs/heads/main/linux/arch/bin/PKGBUILD) (skips building the latest release, downloads and repackages the generic binaries) [`fluxionviewer-bin`]
 - [Development](https://raw.githubusercontent.com/Haltroy/FluxionViewer/refs/heads/main/linux/arch/dev/PKGBUILD) (builds the main branch) [`fluxionviewer-git`]

## Build

Requires [.NET SDK](https://dotnet.microsoft.com) (latest available).

If using another .NET version, please change the "TargetFramework" versions of each .csproj file accordingly. We prefer
using the latest LTS version of .NET.

To build, head to the folder of any trget system (ex. BlueLabel.Linux) and run "dotnet publish" to build it (ex. dotnet
build -r linux-x64 -c Release). The application should be inside the bin folder of that folder.

***NOTE: PLEASE DO NOT SKIP ANY STEPS UNLESS IT IS TOLD TO OK TO SKIP IT.***

1. Get the code. Either use the green "Code" button on the GitHub page or use `git clone https://github.com/haltroy/FluxionViewer.git` command to get the code.
2. Either use the FluxionViewer.sln file in your IDE (Vİsual Studio, VSCode, Rider etc.) and build using that IDE or open up a terminal inside the platform you want to build and run `dontet build.
3. For production-level build, use `dotnet publish -c Release -r <Runtime Identifier>`.
    - `win-x64`: Windows Intel/AMD 64-bit
    - `win-arm64`: Windows ARM 64-bit
    - `linux-x64`: Linux Intel/AMD 64-bit
    - `linux-musl-x64`: Linux (systems that use MUSL instead) Intel/AMD 64-bit
    - `linux-arm64`: Linux ARM 64-bit
    - `android-arm64`: Android phone
    - `iOS-arm64`: iOS device (iPhone, iPad etc.)
    - `osx-x64`: macOS Intel 64-bit
    - `osx-arm64`: macOS ARM 64-bit
4. Build files should be in the `bin` folder of that specific platform.
