# Maintainer: <haltroy> <thehaltroy@gmail.com>
pkgname=fluxionviewer
pkgver=1.0.0.0
pkgrel=1
pkgdesc="View and Edit Fluxion files."
url="https://haltroy.com/Fluxion"
license=(GPL-3.0-or-later)
provides=(fluxionviewer)
arch=(x86_64 aarch64)
makedepends=(dotnet-sdk)
options=("!strip")

build() {
  if [ "$arch" = "x86_64" ]
  then
        dotnet publish \
        -c Release \
        -r linux-x64 \
        -o ../$pkgname \
        FluxionViewer.Linux/FluxionViewer.Linux.csproj
  elif [ "$arch" = "aarch64" ]
  then
        dotnet publish \
        -c Release \
        -r linux-arm64 \
        -o ../$pkgname \
        FluxionViewer.Linux/FluxionViewer.Linux.csproj
  fi
  rm ../$pkgname/FluxionViewer.Linux.dbg
}

package() {
  mkdir -p "$pkgdir/opt/haltroy/"
  cp -r "../$pkgname" "$pkgdir/opt/haltroy/"
  install -d "$pkgdir/opt/haltroy"
}
