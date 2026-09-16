#!/bin/sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
OUT="$ROOT/bin"
mkdir -p "$OUT"

mcs -sdk:4 \
  -target:winexe \
  -out:"$OUT/CampusAutoLogin.exe" \
  -r:System.Windows.Forms \
  -r:System.Drawing \
  -r:System.Web.Extensions \
  "$ROOT/CampusAutoLogin.cs" \
  "$ROOT/Program.cs"

mcs -sdk:4 \
  -out:"$OUT/CampusAutoLoginTests.exe" \
  -r:System.Web.Extensions \
  "$ROOT/CampusAutoLogin.cs" \
  "$ROOT/tests/CampusAutoLoginTests.cs"

cp "$ROOT/config.ini" "$OUT/config.ini"
cp "$ROOT/install-autostart.bat" "$OUT/install-autostart.bat"

mono "$OUT/CampusAutoLoginTests.exe"
