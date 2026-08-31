#!/usr/bin/env bash
set -euo pipefail

cd /home/site/wwwroot

if [[ -z "${ConnectionStrings__Database:-}" ]]; then
  echo "ConnectionStrings__Database is required." >&2
  exit 1
fi

chmod +x ./efbundle
./efbundle --connection "${ConnectionStrings__Database}"

exec dotnet Intably.Api.dll
