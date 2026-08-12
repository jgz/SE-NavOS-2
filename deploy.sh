#!/usr/bin/env bash
# Build NavOS and stamp the packed script so the loaded version can be verified in game.
#
# MDK prepends Instructions.readme verbatim to the top of the output, so that file is
# generated fresh on every build and contains nothing but the stamp. Keep it tiny - the
# whole reason the old manual was dropped is that it cost 5,000 of the 100,000 characters.
set -euo pipefail
cd "$(dirname "$0")"

SHA="$(git rev-parse --short HEAD 2>/dev/null || echo nogit)"
git diff --quiet 2>/dev/null || SHA="${SHA}+"          # '+' means uncommitted changes
STAMP="$(date -u '+%Y-%m-%d %H:%M')Z"

printf 'NavOS 2.16 - jgz fork\nBUILD %s  %s\nDocs: README.md / CONFIG-REFERENCE.md in the repo\n' \
    "$STAMP" "$SHA" > Instructions.readme

"/mnt/c/Program Files/dotnet/dotnet.exe" build "NavOS 2.16.csproj" -c Release --no-incremental 2>&1 \
    | grep -E "error|Build succeeded" || true

OUT="/mnt/c/Users/jongr/AppData/Roaming/SpaceEngineers/IngameScripts/local/NavOS 2.16/script.cs"
CHARS=$(python3 -c "print(len(open(r'$OUT',encoding='utf-8-sig').read()))")
echo
echo "=================== LOOK FOR THIS AT THE TOP ==================="
head -3 "$OUT"
echo "================================================================"
echo "$CHARS chars, $((100000-CHARS)) under the PB limit"
