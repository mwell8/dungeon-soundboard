#!/usr/bin/env bash
set -euo pipefail

# One-command macOS release pipeline:
# build -> sign -> notarize -> staple -> zip
#
# Required env vars:
#   DEVELOPER_ID_APPLICATION='Developer ID Application: Your Name (TEAMID)'
#   NOTARY_PROFILE='notarytool-profile-name'
#
# Optional env vars:
#   APP_SCHEME='Dungeon_Soundboard'
#   APP_PROJECT='Dungeon Soundboard.xcodeproj'
#   BUILD_DIR='.DerivedDataRelease'
#   DIST_DIR='dist'

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

APP_SCHEME="${APP_SCHEME:-Dungeon_Soundboard}"
APP_PROJECT="${APP_PROJECT:-Dungeon Soundboard.xcodeproj}"
BUILD_DIR="${BUILD_DIR:-.DerivedDataRelease}"
DIST_DIR="${DIST_DIR:-dist}"

: "${DEVELOPER_ID_APPLICATION:?Set DEVELOPER_ID_APPLICATION env var first}"
: "${NOTARY_PROFILE:?Set NOTARY_PROFILE env var first}"

echo "==> Cleaning old artifacts"
rm -rf "$BUILD_DIR" "$DIST_DIR"
mkdir -p "$DIST_DIR"

echo "==> Building Release app"
xcodebuild \
  -project "$APP_PROJECT" \
  -scheme "$APP_SCHEME" \
  -configuration Release \
  -sdk macosx \
  -derivedDataPath "$BUILD_DIR" \
  build

APP_PATH="$BUILD_DIR/Build/Products/Release/$APP_SCHEME.app"
if [[ ! -d "$APP_PATH" ]]; then
  echo "Release app not found at: $APP_PATH" >&2
  exit 1
fi

cp -R "$APP_PATH" "$DIST_DIR/"
APP_DIST_PATH="$DIST_DIR/$APP_SCHEME.app"

echo "==> Signing app with Developer ID"
codesign \
  --force \
  --deep \
  --options runtime \
  --timestamp \
  --sign "$DEVELOPER_ID_APPLICATION" \
  "$APP_DIST_PATH"

echo "==> Verifying signature"
codesign --verify --deep --strict --verbose=2 "$APP_DIST_PATH"
spctl --assess --type execute --verbose "$APP_DIST_PATH"

ZIP_PATH="$DIST_DIR/$APP_SCHEME.zip"
echo "==> Creating zip for notarization"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIST_PATH" "$ZIP_PATH"

echo "==> Submitting for notarization (wait)"
xcrun notarytool submit "$ZIP_PATH" --keychain-profile "$NOTARY_PROFILE" --wait

echo "==> Stapling notarization ticket"
xcrun stapler staple "$APP_DIST_PATH"
xcrun stapler validate "$APP_DIST_PATH"

FINAL_ZIP_PATH="$DIST_DIR/${APP_SCHEME}-macOS-notarized.zip"
echo "==> Packaging final notarized build"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIST_PATH" "$FINAL_ZIP_PATH"

echo "==> Done"
echo "App: $APP_DIST_PATH"
echo "Zip: $FINAL_ZIP_PATH"
