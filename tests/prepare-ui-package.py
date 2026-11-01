from pathlib import Path
import json
import hashlib
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
workspace = root.parents[1]
out = workspace / 'outputs/RouteX-3.6.0'
out.mkdir(parents=True, exist_ok=True)
tree = json.loads((workspace / 'outputs/routex-base-3.5.2-tree.json').read_text())
changed = [
 'README.md','RELEASE-3.6.0.md','src/AssemblyInfo.cs','src/Lang.En.cs','src/MainForm.cs',
 'src/PagesConnect.cs','src/PagesDpi.cs','src/PagesServers.cs','src/PagesSettings.cs',
 'src/Settings.cs','src/Sources.cs','src/ThemedList.cs','src/Ui.cs','src/Widget.cs','src/UiRefresh.cs',
 'tests/UiReleasePreview.cs','tests/run-ui-release.ps1','tests/prepare-ui-package.py',
 'docs/images/routex-3.6-routing.png','docs/images/routex-3.6-monitor.png',
]
unplanned=[]
for entry in tree['tree']:
    if entry['type'] != 'blob': continue
    name = entry['path']; path = root / name
    assert path.is_file(), f'Missing published file: {name}'
    data = path.read_bytes()
    versions = [data, data.replace(b'\r\n', b'\n'), data.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')]
    same = any(hashlib.sha1(b'blob ' + str(len(d)).encode() + b'\0' + d).hexdigest() == entry['sha'] for d in versions)
    if not same and name not in changed: unplanned.append(name)
assert not unplanned, f'Unplanned published changes: {unplanned}'
files = sorted({e['path'] for e in tree['tree'] if e['type'] == 'blob' and (e['path'].startswith(('src/','docs/','tests/','.github/')) or e['path']=='README.md')} | set(changed))
for name in files:
    source = root / name
    assert source.is_file(), name
    target = out / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
shutil.copy2(root / 'RouteX.exe', out / 'RouteX.exe')
zip_path = out.parent / (out.name + '.zip')
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(out.rglob('*')):
        if path.is_file(): archive.write(path,path.relative_to(out).as_posix())
with zipfile.ZipFile(zip_path) as archive:
    names=archive.namelist()
    assert [n for n in names if n.lower().endswith('.exe')]==['RouteX.exe']
    assert not any(n.startswith(('config/','log/')) or '.build' in n for n in names)
manifest={'version':'3.6.0','base':'51bd05d9c316e40da40443700cc5b31b748df6df','files':changed,'exeSha256':hashlib.sha256((out/'RouteX.exe').read_bytes()).hexdigest()}
(workspace / 'outputs/routex-3.6.0-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Staged application and source ZIP; only RouteX.exe, no runtime data.')
print('SHA256 '+manifest['exeSha256'])
