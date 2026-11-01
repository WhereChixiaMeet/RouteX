from pathlib import Path
import hashlib
import json
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
outputs = root.parents[1] / 'outputs'
baseline = json.loads((outputs / 'routex-base-before-4.0-tree.json').read_text(encoding='utf-8-sig'))
base = '570de2c11395e8b0e2f3545df031ba80143a6d7e'
remote = {item['path']: item['sha'] for item in baseline['tree'] if item['type'] == 'blob'}
files = {'.gitignore', 'README.md', 'RELEASE-4.0.md'}
files.update(p.relative_to(root).as_posix() for p in (root / 'src').iterdir() if p.is_file() and p.suffix in {'.cs', '.cmd', '.ps1', '.manifest', '.png', '.ico'})
files.update(p.relative_to(root).as_posix() for p in (root / 'src/WebUi').iterdir() if p.name != 'interface.html' and p.suffix in {'.html', '.js', '.cjs', '.py'})
files.update(p.relative_to(root).as_posix() for p in (root / 'docs/licenses').glob('*.txt'))
files.update(p.relative_to(root).as_posix() for p in (root / 'docs/images').glob('routex-4.0-*.png'))
files.update('tests/' + name for name in ('BrowserPreview.cs', 'run-browser-preview.ps1', 'BackupCompatibility.cs', 'run-backup-compatibility.ps1', 'TunStackTests.cs', 'run-tun-stack.ps1', 'MigrationTests.cs', 'MigrationChild.cs', 'MigrationBootstrap.cs', 'run-migration.ps1', 'prepare-4.0-release.py'))

def git_sha(data):
    return hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()

# Keep the already-published workflow unchanged; CLI owns this release.
workflow = '.github/workflows/release.yml'
assert git_sha((root / workflow).read_bytes()) == remote[workflow], 'Review workflow changes before publishing'
def unchanged(name):
    data = (root / name).read_bytes()
    if git_sha(data) == remote.get(name):
        return True
    if (root / name).suffix not in {'.cs', '.md', '.txt', '.ps1', '.cmd', '.js', '.py', '.yml', '.manifest'}:
        return False
    lf = data.replace(b'\r\n', b'\n')
    return any(git_sha(content) == remote.get(name) for content in (lf, lf.replace(b'\n', b'\r\n')))

changed = [{'path': name, 'sha256': hashlib.sha256((root / name).read_bytes()).hexdigest()}
           for name in sorted(files) if not unchanged(name)]
removed = [name for name in ('src/PagesRoutingRules.cs', 'tests/ConfigRoutingTests.cs', 'tests/run-config-routing.ps1') if name in remote]

out = outputs / 'RouteX-4.0'
out.mkdir(exist_ok=True)
# Retain published source/documentation files and overlay the reviewed changes.
package = files | {name for name in remote if name.startswith(('src/', 'docs/', 'tests/', '.github/')) or name in {'.gitignore', 'README.md'}}
package.difference_update(removed)
package.discard('src/WebUi/interface.html')
for name in sorted(package):
    source = root / name
    assert source.is_file(), f'Missing source file: {name}'
    destination = out / name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
shutil.copy2(root / 'RouteX.exe', out / 'RouteX.exe')
zip_path = outputs / 'RouteX-4.0.zip'
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in sorted(package | {'RouteX.exe'}):
        archive.write(out / name, name)
with zipfile.ZipFile(zip_path) as archive:
    names = archive.namelist()
    assert [name for name in names if name.lower().endswith('.exe')] == ['RouteX.exe']
    assert not any(name.startswith(('config/', 'log/', 'vendor/', 'publish-payloads')) or '.build-' in name for name in names)
manifest = {'base': base, 'version': '4.0', 'changes': changed, 'removed': removed,
            'exeSha256': hashlib.sha256((out / 'RouteX.exe').read_bytes()).hexdigest(),
            'zipSha256': hashlib.sha256(zip_path.read_bytes()).hexdigest()}
(outputs / 'routex-4.0-release-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print(f'Staged {len(package)} source/documentation files; {len(changed)} changed paths; {len(removed)} removals.')
print('Only executable: RouteX.exe')
print('EXE SHA256: ' + manifest['exeSha256'])
