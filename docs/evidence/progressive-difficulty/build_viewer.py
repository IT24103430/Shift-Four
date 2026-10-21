"""Convert selected actual Unity captures to PNG and build a local evidence viewer."""
import hashlib
import html
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent


def main():
    project = Path((HERE / 'review-project.txt').read_text().strip())
    captures = project / 'ValidationCaptures'
    images = HERE / 'frames'
    images.mkdir(exist_ok=True)
    selected = []
    for agent in ['Scout', 'Guard', 'Interceptor', 'Flanker']:
        for phase in ['Suspicious', 'Combat']:
            selected.append((captures / 'awareness' / f'{agent}-{phase}.tga', agent,
                             f'{phase} presentation', 'Scripted awareness cue fixture; native movement paused.'))
        selected.append((captures / 'destruction' / f'{agent}-03-fire.tga', agent,
                         'Accepted firing', 'Production visibility/range/fire checks; scripted player placement and aim.'))
        selected.append((captures / 'destruction' / f'{agent}-08-wreck.tga', agent,
                         'Settled wreck', 'Scripted fatal damage; actual gravity, collision, impact and settling.'))
    for phase in ['forward', 'reversal', 'search']:
        selected.append((captures / 'regression' / f'prediction-{phase}.tga', 'Interceptor',
                         f'Prediction {phase}', 'Scripted visible observations/occlusion; paused motor and temporary diagnostic route overlays.'))
    for agent in ['Scout', 'Guard', 'Interceptor', 'Flanker']:
        selected.append((HERE / 'encounters' / f'{agent}-encounter.tga', agent, 'Native motion diagnostic',
                         'Scripted encounter window; normal target policy/motor/physics; player input and other agents paused. Flanker also receives a temporary raised route obstruction.'))
    records = []
    for raw, agent, title, context in selected:
        if not raw.exists():
            raise FileNotFoundError(raw)
        target = images / (raw.stem + '.png')
        subprocess.run(['sips', '-s', 'format', 'png', str(raw), '--out', str(target)], check=True, capture_output=True)
        records.append(dict(agent=agent, title=title, context=context, image='frames/' + target.name,
                            raw_source=str(raw.relative_to(project)) if raw.is_relative_to(project) else str(raw.relative_to(HERE)), raw_sha256=hashlib.sha256(raw.read_bytes()).hexdigest(),
                            png_sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    (HERE / 'captures.json').write_text(json.dumps(records, indent=2))
    cards = ''.join(f'<figure data-agent="{r["agent"]}"><img loading="lazy" src="{r["image"]}" alt="{html.escape(r["agent"]+": "+r["title"])}"><figcaption><b>{r["agent"]} · {r["title"]}</b><br>{r["context"]}</figcaption></figure>' for r in records)
    (HERE / 'index.html').write_text('''<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Shift-Four — recorded Unity validation</title><style>
body{font:16px/1.5 system-ui;background:#10151b;color:#e4e9ee;max-width:1320px;margin:32px auto;padding:0 24px}a{color:#86c8fc}select{font:inherit;padding:8px;background:#202a35;color:inherit;border:1px solid #567}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:22px}figure{margin:0;background:#1c252f;border-radius:8px;overflow:hidden}img{width:100%;display:block}figcaption{padding:14px;color:#bbc8d6}b{color:white}p{max-width:900px}</style>
<h1>Recorded Unity validation</h1><p>Actual MainLab captures from the final unified test run. These are automated fixtures, not a manual playthrough. Model cue and destruction images demonstrate presentation; they do not establish pending policy enhancements or encounter fairness.</p>
<p><a href="../../FINAL_PROGRESSIVE_DIFFICULTY_VALIDATION.md">Full validation report</a> · <a href="test-summary.json">Test totals</a> · <a href="captures.json">Capture provenance</a></p>
<p><label>Agent <select id="agent"><option>All</option><option>Scout</option><option>Guard</option><option>Interceptor</option><option>Flanker</option></select></label></p><main>''' + cards + '''</main><script>document.querySelector('#agent').onchange=e=>document.querySelectorAll('figure').forEach(f=>f.hidden=e.target.value!=='All'&&f.dataset.agent!==e.target.value)</script>''')
    print('Converted', len(records), 'actual captures:', HERE / 'index.html')


if __name__ == '__main__':
    main()
