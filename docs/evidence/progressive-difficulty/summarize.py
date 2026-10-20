"""Summarize recorded Unity results; never estimates missing samples or gameplay outcomes."""
import csv
import json
import math
from pathlib import Path
import statistics
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent


def stats(values):
    values = sorted(values)
    if not values:
        return None
    def percentile(p):
        index = (len(values) - 1) * p
        low = math.floor(index)
        return values[low] + (values[math.ceil(index)] - values[low]) * (index - low)
    return dict(samples=len(values), mean=statistics.mean(values), median=statistics.median(values),
                p95=percentile(.95), p99=percentile(.99), maximum=max(values))


def profile(path):
    with path.open() as stream:
        rows = list(csv.DictReader(stream))
    result = {'frames': len(rows), 'wall_ms': stats([float(r['wall_ms']) for r in rows]), 'markers': {}}
    for key in rows[0]:
        if key.endswith('_ms') and key != 'wall_ms':
            calls = key[:-3] + '_calls'
            active = [r for r in rows if int(r.get(calls, r.get('emissions', '0'))) > 0]
            result['markers'][key[:-3]] = dict(all_frames_ms=stats([float(r[key]) for r in rows]),
                active_frames_ms=stats([float(r[key]) for r in active]), calls=sum(int(r.get(calls, r.get('emissions', '0'))) for r in rows))
    if 'phase' in rows[0]:
        result['phases_wall_ms'] = {phase: stats([float(r['wall_ms']) for r in rows if r['phase'] == phase]) for phase in dict.fromkeys(r['phase'] for r in rows)}
    return result


def main():
    summary = {}
    matrix = []
    for mode in ['EditMode', 'PlayMode']:
        root = ET.parse(HERE / (mode + '.xml')).getroot()
        summary[mode] = root.attrib
        for case in root.iter('test-case'):
            matrix.append(dict(mode=mode, name=case.get('fullname'), result=case.get('result'), duration=case.get('duration')))
    summary['total_passed'] = sum(int(summary[m]['passed']) for m in ['EditMode', 'PlayMode'])
    for key, path in [('optional_encounter_fixture', HERE / 'encounters/ScenarioPlayMode.xml'),
                      ('unapproved_console_proposal', HERE / 'console-anchor-review/ProposalPlayMode.xml')]:
        if path.exists():
            summary[key] = ET.parse(path).getroot().attrib
    (HERE / 'test-summary.json').write_text(json.dumps(summary, indent=2))
    with (HERE / 'TEST-MATRIX.csv').open('w') as stream:
        writer = csv.DictWriter(stream, fieldnames=['mode', 'name', 'result', 'duration']); writer.writeheader(); writer.writerows(matrix)
    performance = {}
    for name in ['profile-frames.csv', 'sensing-profile-frames.csv']:
        path = HERE / 'generated' / name
        if path.exists():
            performance[name] = profile(path)
    (HERE / 'performance-summary.json').write_text(json.dumps(performance, indent=2))
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    main()
