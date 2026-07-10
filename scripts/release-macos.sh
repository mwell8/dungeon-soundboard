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

if [[ -z "${DEVELOPER_DIR:-}" ]]; then
  if [[ -d "/Applications/Xcode_26.3.app/Contents/Developer" ]]; then
    export DEVELOPER_DIR="/Applications/Xcode_26.3.app/Contents/Developer"
  elif [[ -d "/Applications/Xcode.app/Contents/Developer" ]]; then
    export DEVELOPER_DIR="/Applications/Xcode.app/Contents/Developer"
  else
    echo "Full Xcode is required. Set DEVELOPER_DIR to its Developer directory." >&2
    exit 1
  fi
fi

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
  CODE_SIGNING_ALLOWED=NO \
  CODE_SIGNING_REQUIRED=NO \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  build

BUILD_SETTINGS="$(xcodebuild \
  -project "$APP_PROJECT" \
  -scheme "$APP_SCHEME" \
  -configuration Release \
  -derivedDataPath "$BUILD_DIR" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  -showBuildSettings)"
FULL_PRODUCT_NAME="$(printf '%s\n' "$BUILD_SETTINGS" | awk -F ' = ' '/^[[:space:]]*FULL_PRODUCT_NAME = / { print $2; exit }')"
EXECUTABLE_NAME="$(printf '%s\n' "$BUILD_SETTINGS" | awk -F ' = ' '/^[[:space:]]*EXECUTABLE_NAME = / { print $2; exit }')"
MARKETING_VERSION="$(printf '%s\n' "$BUILD_SETTINGS" | awk -F ' = ' '/^[[:space:]]*MARKETING_VERSION = / { print $2; exit }')"

if [[ -z "$FULL_PRODUCT_NAME" ]] || [[ -z "$EXECUTABLE_NAME" ]] || [[ -z "$MARKETING_VERSION" ]]; then
  echo "Could not derive Release product settings." >&2
  exit 1
fi

APP_PATH="$BUILD_DIR/Build/Products/Release/$FULL_PRODUCT_NAME"
if [[ ! -d "$APP_PATH" ]]; then
  echo "Release app not found at: $APP_PATH" >&2
  exit 1
fi

PRODUCT_BASENAME="${FULL_PRODUCT_NAME%.app}"
ARCHIVE_PRODUCT_NAME="${PRODUCT_BASENAME// /-}"

ARCHS_OUTPUT="$(lipo -archs "$APP_PATH/Contents/MacOS/$EXECUTABLE_NAME")"
for required_arch in arm64 x86_64; do
  case " $ARCHS_OUTPUT " in
    *" $required_arch "*) ;;
    *)
      echo "Release executable is missing $required_arch: $ARCHS_OUTPUT" >&2
      exit 1
      ;;
  esac
done

cp -R "$APP_PATH" "$DIST_DIR/"
APP_DIST_PATH="$DIST_DIR/$FULL_PRODUCT_NAME"

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
if codesign -d --entitlements :- "$APP_DIST_PATH" 2>&1 | grep -q "com.apple.security.app-sandbox"; then
  echo "Release app unexpectedly contains the App Sandbox entitlement." >&2
  exit 1
fi

ZIP_PATH="$DIST_DIR/$ARCHIVE_PRODUCT_NAME-$MARKETING_VERSION-notarization.zip"
echo "==> Creating zip for notarization"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIST_PATH" "$ZIP_PATH"

echo "==> Submitting for notarization (wait)"
xcrun notarytool submit "$ZIP_PATH" --keychain-profile "$NOTARY_PROFILE" --wait

echo "==> Stapling notarization ticket"
xcrun stapler staple "$APP_DIST_PATH"
xcrun stapler validate "$APP_DIST_PATH"
spctl --assess --type execute --verbose "$APP_DIST_PATH"

FINAL_ZIP_PATH="$DIST_DIR/$ARCHIVE_PRODUCT_NAME-$MARKETING_VERSION-macOS-universal-notarized.zip"
echo "==> Packaging final notarized build"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIST_PATH" "$FINAL_ZIP_PATH"

echo "==> Done"
echo "App: $APP_DIST_PATH"
echo "Zip: $FINAL_ZIP_PATH"
echo "Version: $MARKETING_VERSION"
echo "Architectures: $ARCHS_OUTPUT"
