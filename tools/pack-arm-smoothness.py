"""Pack subtle smoothness variation from the existing arms base maps; no new spatial detail.

Inputs are the supplied uncompressed 24-bit TGAs. RGB stays zero (nonmetallic),
and alpha modulates each URP material's saved smoothness cap from 82% to 100%.
The existing base and normal maps are read only.
"""
import argparse
import json
from pathlib import Path
import struct

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--surfaces-dir', type=Path, required=True)
parser.add_argument('--output-dir', type=Path, required=True)
args = parser.parse_args()
args.output_dir.mkdir(parents=True, exist_ok=True)
results = []
for family in ('Leather', 'Ripstop'):
    source = args.surfaces_dir / (family + 'Base.tga')
    raw = source.read_bytes()
    header = struct.unpack('<BBBHHBHHHHBB', raw[:18])
    if header[1] != 0 or header[2] != 2 or header[10] != 24:
        raise ValueError('Expected the supplied uncompressed RGB TGA: ' + str(source))
    width, height = header[8:10]
    start = 18 + header[0]
    values = [sum(raw[i:i + 3]) / 3 for i in range(start, start + width * height * 3, 3)]
    low, high = min(values), max(values)
    alpha = [round(255 * (.82 + .18 * ((v - low) / (high - low) if high > low else .5))) for v in values]
    packed_header = struct.pack('<BBBHHBHHHHBB', 0, 0, 2, 0, 0, 0, 0, 0, width, height, 32, (header[11] & 0x20) | 8)
    destination = args.output_dir / (family + 'MetallicSmoothness.tga')
    destination.write_bytes(packed_header + b''.join(bytes((0, 0, 0, a)) for a in alpha))
    results.append({'source': str(source), 'output': str(destination), 'size': [width, height],
                    'metallic': 0, 'alpha_range': [min(alpha), max(alpha)], 'new_spatial_detail': False})
print(json.dumps(results, indent=2))
