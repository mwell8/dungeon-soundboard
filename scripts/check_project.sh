#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
cd "$ROOT_DIR"

APP_PROJECT="${APP_PROJECT:-Dungeon Soundboard.xcodeproj}"
APP_SCHEME="${APP_SCHEME:-Dungeon_Soundboard}"

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

for tool in git plutil swift xcodebuild xcrun lipo; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "Required tool is unavailable: $tool" >&2
    exit 1
  fi
done

TEMP_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/dungeon-soundboard-check.XXXXXX")"
cleanup() {
  rm -rf "$TEMP_ROOT"
}
trap cleanup EXIT

# SwiftPM's manifest compiler otherwise falls back to user-level caches, which
# makes the check non-hermetic and can fail in CI/sandboxed shells.
export CLANG_MODULE_CACHE_PATH="$TEMP_ROOT/clang-module-cache"
export SWIFTPM_MODULECACHE_OVERRIDE="$TEMP_ROOT/swiftpm-module-cache"
export XDG_CACHE_HOME="$TEMP_ROOT/xdg-cache"
mkdir -p \
  "$CLANG_MODULE_CACHE_PATH" \
  "$SWIFTPM_MODULECACHE_OVERRIDE" \
  "$XDG_CACHE_HOME"

INITIAL_STATUS="$(git status --porcelain=v1 --untracked-files=all)"
SDK_PATH="$(xcrun --sdk macosx --show-sdk-path)"
NATIVE_ARCH="$(uname -m)"
case "$NATIVE_ARCH" in
  arm64|x86_64) ;;
  *)
    echo "Unsupported build host architecture: $NATIVE_ARCH" >&2
    exit 1
    ;;
esac

SWIFT_SOURCES=(
  app/DungeonSoundboardApp.swift
  Views/*.swift
  ViewModels/*.swift
  Models/*.swift
  Resources/Localization.swift
)

echo "==> Toolchain"
xcodebuild -version
swift --version

echo "==> Project structure"
xcodebuild -list -project "$APP_PROJECT"

echo "==> Repository checks"
git diff --check
git diff --cached --check
bash -n scripts/check_project.sh
bash -n scripts/release-macos.sh

if [[ -e "Dungeon_Soundboard.plist" ]]; then
  echo "Legacy entitlement resource still exists: Dungeon_Soundboard.plist" >&2
  exit 1
fi
if grep -Eq 'ENABLE_APP_SANDBOX|ENABLE_USER_SELECTED_FILES|CODE_SIGN_ENTITLEMENTS|Dungeon_Soundboard\.plist' "$APP_PROJECT/project.pbxproj"; then
  echo "Sandbox entitlement settings or the legacy plist remain in the Xcode project." >&2
  exit 1
fi

echo "==> Localizations"
plutil -lint Resources/en.lproj/Localizable.strings Resources/ru.lproj/Localizable.strings
sed -n 's/^[[:space:]]*"\([^"]*\)"[[:space:]]*=.*/\1/p' Resources/en.lproj/Localizable.strings | LC_ALL=C sort > "$TEMP_ROOT/en.keys"
sed -n 's/^[[:space:]]*"\([^"]*\)"[[:space:]]*=.*/\1/p' Resources/ru.lproj/Localizable.strings | LC_ALL=C sort > "$TEMP_ROOT/ru.keys"
if [[ -s "$TEMP_ROOT/en.keys" ]] && [[ -s "$TEMP_ROOT/ru.keys" ]]; then
  if [[ -n "$(uniq -d "$TEMP_ROOT/en.keys")" ]] || [[ -n "$(uniq -d "$TEMP_ROOT/ru.keys")" ]]; then
    echo "Duplicate localization keys found." >&2
    exit 1
  fi
else
  echo "Localization tables must not be empty." >&2
  exit 1
fi
if ! cmp -s "$TEMP_ROOT/en.keys" "$TEMP_ROOT/ru.keys"; then
  echo "English and Russian localization keys differ:" >&2
  comm -3 "$TEMP_ROOT/en.keys" "$TEMP_ROOT/ru.keys" >&2
  exit 1
fi

echo "==> SwiftPM core build"
swift build \
  --disable-sandbox \
  --scratch-path "$TEMP_ROOT/swift-build" \
  --cache-path "$TEMP_ROOT/swift-cache"

echo "==> SwiftPM core tests"
swift test \
  --disable-sandbox \
  --enable-code-coverage \
  --scratch-path "$TEMP_ROOT/swift-test" \
  --cache-path "$TEMP_ROOT/swift-cache"

echo "==> App typecheck"
xcrun swiftc \
  -typecheck \
  -parse-as-library \
  -module-name Dungeon_Soundboard_Typecheck \
  -swift-version 6 \
  -default-isolation MainActor \
  -sdk "$SDK_PATH" \
  -target "$NATIVE_ARCH-apple-macos13.0" \
  -module-cache-path "$TEMP_ROOT/swift-module-cache" \
  "${SWIFT_SOURCES[@]}"

echo "==> Strict Swift 6 app typecheck"
xcrun swiftc \
  -typecheck \
  -parse-as-library \
  -module-name Dungeon_Soundboard_Check \
  -swift-version 6 \
  -strict-concurrency=complete \
  -warnings-as-errors \
  -default-isolation MainActor \
  -sdk "$SDK_PATH" \
  -target "$NATIVE_ARCH-apple-macos13.0" \
  -module-cache-path "$TEMP_ROOT/swift-module-cache" \
  "${SWIFT_SOURCES[@]}"

COMMON_XCODE_ARGS=(
  -project "$APP_PROJECT"
  -scheme "$APP_SCHEME"
  -sdk macosx
  CODE_SIGNING_ALLOWED=NO
  CODE_SIGNING_REQUIRED=NO
)

echo "==> App unit tests"
xcodebuild \
  "${COMMON_XCODE_ARGS[@]}" \
  -configuration Debug \
  -destination "platform=macOS,arch=$NATIVE_ARCH" \
  -derivedDataPath "$TEMP_ROOT/derived-tests" \
  -parallel-testing-enabled NO \
  test

echo "==> Debug app build"
DEBUG_DERIVED="$TEMP_ROOT/derived-debug"
xcodebuild \
  "${COMMON_XCODE_ARGS[@]}" \
  -configuration Debug \
  -derivedDataPath "$DEBUG_DERIVED" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  build

DEBUG_BUILD_SETTINGS="$(xcodebuild \
  -project "$APP_PROJECT" \
  -scheme "$APP_SCHEME" \
  -configuration Debug \
  -derivedDataPath "$DEBUG_DERIVED" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  -showBuildSettings)"
DEBUG_FULL_PRODUCT_NAME="$(printf '%s\n' "$DEBUG_BUILD_SETTINGS" | awk -F ' = ' '/^[[:space:]]*FULL_PRODUCT_NAME = / { print $2; exit }')"
DEBUG_EXECUTABLE_NAME="$(printf '%s\n' "$DEBUG_BUILD_SETTINGS" | awk -F ' = ' '/^[[:space:]]*EXECUTABLE_NAME = / { print $2; exit }')"
DEBUG_EXECUTABLE_PATH="$DEBUG_DERIVED/Build/Products/Debug/$DEBUG_FULL_PRODUCT_NAME/Contents/MacOS/$DEBUG_EXECUTABLE_NAME"
DEBUG_INFO_PLIST="$DEBUG_DERIVED/Build/Products/Debug/$DEBUG_FULL_PRODUCT_NAME/Contents/Info.plist"
if [[ ! -x "$DEBUG_EXECUTABLE_PATH" ]] || [[ ! -f "$DEBUG_INFO_PLIST" ]]; then
  echo "Debug app executable is missing: $DEBUG_EXECUTABLE_PATH" >&2
  exit 1
fi
DEBUG_ARCHS_OUTPUT="$(lipo -archs "$DEBUG_EXECUTABLE_PATH")"
for required_arch in arm64 x86_64; do
  case " $DEBUG_ARCHS_OUTPUT " in
    *" $required_arch "*) ;;
    *)
      echo "Debug executable is missing $required_arch: $DEBUG_ARCHS_OUTPUT" >&2
      exit 1
      ;;
  esac
done
DEBUG_MINIMUM_SYSTEM_VERSION="$(plutil -extract LSMinimumSystemVersion raw -o - "$DEBUG_INFO_PLIST")"
if [[ "$DEBUG_MINIMUM_SYSTEM_VERSION" != "13.0" ]]; then
  echo "Unexpected Debug minimum macOS version: $DEBUG_MINIMUM_SYSTEM_VERSION" >&2
  exit 1
fi

echo "==> Universal Release app build"
RELEASE_DERIVED="$TEMP_ROOT/derived-release"
xcodebuild \
  "${COMMON_XCODE_ARGS[@]}" \
  -configuration Release \
  -derivedDataPath "$RELEASE_DERIVED" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  build

BUILD_SETTINGS="$(xcodebuild \
  -project "$APP_PROJECT" \
  -scheme "$APP_SCHEME" \
  -configuration Release \
  -derivedDataPath "$RELEASE_DERIVED" \
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

APP_PATH="$RELEASE_DERIVED/Build/Products/Release/$FULL_PRODUCT_NAME"
EXECUTABLE_PATH="$APP_PATH/Contents/MacOS/$EXECUTABLE_NAME"
INFO_PLIST="$APP_PATH/Contents/Info.plist"
if [[ ! -d "$APP_PATH" ]] || [[ ! -x "$EXECUTABLE_PATH" ]] || [[ ! -f "$INFO_PLIST" ]]; then
  echo "Release app is incomplete at: $APP_PATH" >&2
  exit 1
fi

ARCHS_OUTPUT="$(lipo -archs "$EXECUTABLE_PATH")"
for required_arch in arm64 x86_64; do
  case " $ARCHS_OUTPUT " in
    *" $required_arch "*) ;;
    *)
      echo "Release executable is missing $required_arch: $ARCHS_OUTPUT" >&2
      exit 1
      ;;
  esac
done

MINIMUM_SYSTEM_VERSION="$(plutil -extract LSMinimumSystemVersion raw -o - "$INFO_PLIST")"
BUNDLE_VERSION="$(plutil -extract CFBundleShortVersionString raw -o - "$INFO_PLIST")"
if [[ "$MINIMUM_SYSTEM_VERSION" != "13.0" ]]; then
  echo "Unexpected minimum macOS version: $MINIMUM_SYSTEM_VERSION" >&2
  exit 1
fi
if [[ "$BUNDLE_VERSION" != "$MARKETING_VERSION" ]]; then
  echo "Built version $BUNDLE_VERSION does not match MARKETING_VERSION $MARKETING_VERSION" >&2
  exit 1
fi

FINAL_STATUS="$(git status --porcelain=v1 --untracked-files=all)"
if [[ "$FINAL_STATUS" != "$INITIAL_STATUS" ]]; then
  echo "Project checks changed the working tree." >&2
  diff -u <(printf '%s\n' "$INITIAL_STATUS") <(printf '%s\n' "$FINAL_STATUS") >&2 || true
  exit 1
fi

echo "==> All checks passed"
echo "Release app: $FULL_PRODUCT_NAME"
echo "Version: $MARKETING_VERSION"
echo "Debug architectures: $DEBUG_ARCHS_OUTPUT"
echo "Release architectures: $ARCHS_OUTPUT"
echo "Minimum macOS: $MINIMUM_SYSTEM_VERSION"
