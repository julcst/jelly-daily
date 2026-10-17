#!/usr/bin/env python3
"""Package a built, tested DLL and emit Jellyfin's repository manifest."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--version', default='0.2.0.0')
parser.add_argument('--timestamp', default='2026-10-17T20:26:45Z')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
dll = root / 'src/JellyDaily/bin/Release/net10.0/JellyDaily.dll'
if not dll.is_file():
    raise SystemExit('Build and test the Release configuration first.')
release = root / 'releases' / ('JellyDaily_' + args.version + '.zip')
release.parent.mkdir(exist_ok=True)
# Stable ZIP metadata makes repeat packaging of the same DLL byte-for-byte repeatable.
with zipfile.ZipFile(release, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
    info = zipfile.ZipInfo('JellyDaily.dll', date_time=(2026, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o644 << 16
    archive.writestr(info, dll.read_bytes())
raw = release.read_bytes()
manifest = [{
    'guid': '646aafae-e81c-4379-baf0-a66aa3a1b7ca',
    'name': 'Daily Episodes', 'overview': 'Experimental per-child watch-gated episode releases',
    'description': 'Sequential daily episode releases, per-child series rules and manual bonuses. Experimental MVP; no daily screen-time enforcement. Targets Jellyfin 12.2.0.',
    'owner': 'julcst', 'category': 'General',
    'versions': [{
        'version': args.version, 'targetAbi': '12.2.0.0',
        'sourceUrl': f'https://raw.githubusercontent.com/julcst/jelly-daily/v{args.version}/releases/{release.name}',
        # Jellyfin's installer specifically requires MD5. Publish SHA-256 alongside it.
        'checksum': hashlib.md5(raw).hexdigest(),
        'timestamp': args.timestamp,
        'changelog': 'Jellyfin 12.2 / .NET 10 port: per-child daily releases, watch gates, manual bonuses, persistent state, dashboard controls and server-side request guards. Screen-time enforcement deferred.'
    }]
}]
(root / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
(release.parent / 'SHA256SUMS').write_text(f'{hashlib.sha256(raw).hexdigest()}  {release.name}\n')
print(release)
print(root / 'manifest.json')
