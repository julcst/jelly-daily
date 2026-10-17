#!/usr/bin/env bash
# Creates and removes its own disposable server; never points at an existing server.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build src/JellyDaily -c Release
smoke_root=$(mktemp -d)
smoke_name="jelly-daily-smoke-$RANDOM-$$"
cleanup() { docker rm -f "$smoke_name" >/dev/null 2>&1 || true; rm -rf "$smoke_root"; }
trap cleanup EXIT
mkdir -p "$smoke_root/config/plugins/DailyEpisodes_0.1.0.0" "$smoke_root/media/Test Show/Season 01" "$smoke_root/cache"
cp src/JellyDaily/bin/Release/net9.0/JellyDaily.dll "$smoke_root/config/plugins/DailyEpisodes_0.1.0.0/"
ffmpeg -hide_banner -loglevel error -f lavfi -i color=c=blue:s=160x90:r=10 -f lavfi -i anullsrc=r=44100:cl=mono -t 10 -c:v libx264 -c:a aac "$smoke_root/media/Test Show/Season 01/Test Show - S01E01.mp4"
cp "$smoke_root/media/Test Show/Season 01/Test Show - S01E01.mp4" "$smoke_root/media/Test Show/Season 01/Test Show - S01E02.mp4"
cp "$smoke_root/media/Test Show/Season 01/Test Show - S01E01.mp4" "$smoke_root/media/Test Show/Season 01/Test Show - S01E03.mp4"
docker run -d --user "$(id -u):$(id -g)" --name "$smoke_name" -p 127.0.0.1::8096 -v "$smoke_root/config:/config" -v "$smoke_root/cache:/cache" -v "$smoke_root/media:/media:ro" jellyfin/jellyfin:10.11.11 >/dev/null
smoke_port=$(docker port "$smoke_name" 8096/tcp | cut -d: -f2)
JELLY_DAILY_SMOKE_URL="http://127.0.0.1:$smoke_port" JELLY_DAILY_SMOKE_CONTAINER="$smoke_name" python3 -u scripts/smoke-test.py
