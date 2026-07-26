#!/usr/bin/env bash
# PlexusX QA - mechanical half (docs/QA.md section 1).
# Usage: bash scripts/qa-check.sh [--release]
#   --release  also enforces version agreement + release notes (run before tagging).
set -u
cd "$(dirname "$0")/.." || exit 1
fail=0
RELEASE=0
[ "${1:-}" = "--release" ] && RELEASE=1

echo "== 1. Release build =="
build_out="$(dotnet build VibranceHud.csproj -c Release --nologo 2>&1)"
if echo "$build_out" | grep -qE "0 (Chyba|Errors)|Počet chyb: 0|0 Error"; then
  echo "PASS: build clean"
else
  echo "FAIL: build errors"; echo "$build_out" | grep -iE 'error' | head -5; fail=1
fi
if echo "$build_out" | grep -qE "0 (Upozornění|Warnings)|0 Warning"; then
  echo "PASS: 0 warnings"
else
  echo "WARN: compiler warnings present"
fi

echo "== 2. Unit tests =="
test_out="$(dotnet test tests/VibranceHud.Tests/VibranceHud.Tests.csproj --nologo 2>&1)"
counts="$(echo "$test_out" | grep -oE '(Úspěšné|Passed):[ ]+[0-9]+' | tail -1)"
failed="$(echo "$test_out" | grep -oE '(Neúspěšné|Failed):[ ]+[0-9]+' | tail -1)"
echo "   $counts  $failed"
if echo "$failed" | grep -qE ' 0$'; then
  echo "PASS: all tests green"
else
  echo "FAIL: test failures"; fail=1
fi

if [ $RELEASE -eq 1 ]; then
  echo "== 3. Version agreement (release) =="
  csproj_v="$(grep -oE '<Version>[0-9.]+' VibranceHud.csproj | grep -oE '[0-9.]+')"
  iss_v="$(grep -oE '#define AppVersion "[0-9.]+"' VibranceHud.iss | grep -oE '[0-9.]+')"
  if [ "$csproj_v" = "$iss_v" ] && [ -f "docs/RELEASE_NOTES-v${csproj_v}.md" ]; then
    echo "PASS: $csproj_v everywhere + release notes"
  else
    echo "FAIL: csproj=$csproj_v iss=$iss_v notes=docs/RELEASE_NOTES-v${csproj_v}.md"; fail=1
  fi
fi

echo "== 4. GDI per-frame allocation heuristic =="
# new GDI objects inside OnPaint without 'using' on the same line = likely leak
hits="$(grep -rn --include='*.cs' -E 'protected override void OnPaint' -A 40 . 2>/dev/null \
  | grep -vE 'obj/|bin/|tests/' \
  | grep -E 'new (SolidBrush|Pen|Bitmap|LinearGradientBrush|Font)' \
  | grep -v 'using' || true)"
if [ -z "$hits" ]; then
  echo "PASS: no undisposed per-frame GDI allocations found"
else
  echo "WARN: review these per-frame allocations:"
  echo "$hits" | head -10
fi

echo "== 5. publish exe freshness =="
if [ -f publish/PlexusX.exe ] && [ publish/PlexusX.exe -nt VibranceHud.csproj ]; then
  echo "PASS: publish/PlexusX.exe present"
else
  echo "WARN: publish exe missing or stale (fine mid-development, not fine for release)"
  [ $RELEASE -eq 1 ] && fail=1
fi

echo
if [ $fail -eq 0 ]; then echo "QA MECHANICAL: PASS"; else echo "QA MECHANICAL: FAIL"; fi
exit $fail
