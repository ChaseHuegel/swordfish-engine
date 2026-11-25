#!/usr/bin/env bash
set -euo pipefail

# Packages Wayward Beyond Asset Studio and AssetTools CLI as a self-contained
# folder archive (.tar.gz for linux-x64, .zip for win-x64).
# No single-file build, no installer, and no auto-update.
#
# Usage:
#   tools/package-asset-studio.sh [--rid linux-x64|win-x64] [--version 0.1.0] [--output <dir>] [--cli-only]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

RID="linux-x64"
VERSION="0.1.0"
OUTPUT_DIR="${REPO_ROOT}/artifacts/asset-studio"
CLI_ONLY="false"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid)
            RID="${2:?--rid requires a value (linux-x64 or win-x64)}"
            shift 2
            ;;
        --version)
            VERSION="${2:?--version requires a value}"
            shift 2
            ;;
        --output)
            OUTPUT_DIR="${2:?--output requires a directory path}"
            shift 2
            ;;
        --cli-only)
            CLI_ONLY="true"
            shift
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 2
            ;;
    esac
done

case "${RID}" in
    linux-x64|win-x64)
        ;;
    *)
        echo "Unsupported RID '${RID}'. Supported platforms: linux-x64, win-x64." >&2
        exit 2
        ;;
esac

ARTIFACT_NAME="wayward-beyond-asset-studio-${VERSION}-${RID}"
if [[ "${CLI_ONLY}" == "true" ]]; then
    ARTIFACT_NAME="wayward-beyond-asset-tools-cli-${VERSION}-${RID}"
fi

STAGE_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/wb-package-XXXXXX")"
cleanup() {
    rm -rf "${STAGE_ROOT}"
}
trap cleanup EXIT

BUNDLE_DIR="${STAGE_ROOT}/${ARTIFACT_NAME}"
mkdir -p "${BUNDLE_DIR}" "${OUTPUT_DIR}"

PUBLISH_ARGS=(
    -c Release
    -r "${RID}"
    --self-contained true
    -p:PublishSingleFile=false
    -p:PublishTrimmed=false
    -p:ErrorOnDuplicatePublishOutputFiles=false
    -p:DebugType=None
    -p:DebugSymbols=false
    -p:Version="${VERSION}"
    --nologo
    -v minimal
)

if [[ "${CLI_ONLY}" != "true" ]]; then
    STUDIO_PROJECT="${REPO_ROOT}/WaywardBeyond.AssetStudio/WaywardBeyond.AssetStudio.csproj"
    if [[ ! -f "${STUDIO_PROJECT}" ]]; then
        echo "Missing AssetStudio project: ${STUDIO_PROJECT}" >&2
        exit 2
    fi

    echo "Publishing WaywardBeyond.AssetStudio (${RID})..."
    dotnet publish "${STUDIO_PROJECT}" "${PUBLISH_ARGS[@]}" -o "${BUNDLE_DIR}"
fi

CLI_PROJECT="${REPO_ROOT}/WaywardBeyond.AssetTools.Cli/WaywardBeyond.AssetTools.Cli.csproj"
echo "Publishing WaywardBeyond.AssetTools.Cli (${RID})..."
dotnet publish "${CLI_PROJECT}" "${PUBLISH_ARGS[@]}" -o "${BUNDLE_DIR}"

# Remove debug-only pseudo-locale and transitive game module manifests/assets
# that are not part of the standalone tool distribution.
rm -rf "${BUNDLE_DIR}/qps-ploc" "${BUNDLE_DIR}/manifest.toml" "${BUNDLE_DIR}/assets"

cat > "${BUNDLE_DIR}/README.txt" <<EOF
Wayward Beyond Asset Studio ${VERSION} (${RID})
===============================================

Self-contained folder distribution. No installer and no auto-update.

Supported platforms:
- Windows 10 x64 (win-x64)
- Linux x64, glibc 2.35 or newer (linux-x64)

Linux runtime library dependencies:
- libgl1
- libx11-6
- libfontconfig1
- libgtk-3-0

Portable mode:
Create an empty file named "portable" beside the executable to store settings
and drafts in this folder instead of the per-user data directory.

CLI commands:
  WaywardBeyond.AssetTools.Cli validate <root> [--install <dir>] [--json]
  WaywardBeyond.AssetTools.Cli roundtrip <root> [--check] [--json]
  WaywardBeyond.AssetTools.Cli format <root> [--json]
  WaywardBeyond.AssetTools.Cli new pack <dir> [--json]
  WaywardBeyond.AssetTools.Cli ids <root> [--install <dir>] [--json]
EOF

if [[ "${RID}" == "win-x64" ]]; then
    ARCHIVE_PATH="${OUTPUT_DIR}/${ARTIFACT_NAME}.zip"
    rm -f "${ARCHIVE_PATH}"
    if command -v zip >/dev/null 2>&1; then
        (cd "${STAGE_ROOT}" && zip -qr "${ARCHIVE_PATH}" "${ARTIFACT_NAME}")
    elif command -v pwsh >/dev/null 2>&1; then
        pwsh -NoProfile -Command "Compress-Archive -Path '${BUNDLE_DIR}' -DestinationPath '${ARCHIVE_PATH}' -Force"
    else
        echo "zip or pwsh is required to build .zip archives for win-x64." >&2
        exit 2
    fi
else
    ARCHIVE_PATH="${OUTPUT_DIR}/${ARTIFACT_NAME}.tar.gz"
    rm -f "${ARCHIVE_PATH}"
    tar -czf "${ARCHIVE_PATH}" -C "${STAGE_ROOT}" "${ARTIFACT_NAME}"
fi

echo "Created archive: ${ARCHIVE_PATH}"
