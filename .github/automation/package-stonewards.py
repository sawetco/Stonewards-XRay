"""Build the source installer archive; never redistribute game assemblies."""
from pathlib import Path
import json,re,zipfile,hashlib
def build(root=Path.cwd()):
 version=json.loads((root/'version.json').read_text())['version']
 if not re.fullmatch(r'\d+\.\d+\.\d+',version):raise ValueError('Invalid version')
 for filename in ['src/StonewardsXRayPlugin.cs','scripts/Install-Stonewards-XRay.ps1','README.md']:
  if version not in (root/filename).read_text():raise ValueError('Version metadata mismatch: '+filename)
 if f'## {version}' not in (root/'CHANGELOG.md').read_text():raise ValueError('Missing changelog version')
 files={p.name:p for p in (root/'scripts').iterdir() if p.suffix in ['.cmd','.ps1']}
 files['StonewardsXRayPlugin.cs']=root/'src/StonewardsXRayPlugin.cs'
 for filename in ['README.md','CHANGELOG.md','LICENSE','SECURITY.md']:files[filename]=root/filename
 out=root/'dist';out.mkdir(exist_ok=True)
 archive=out/f'Stonewards-XRay-by-sawet-v{version}.zip'
 with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
  for name,p in sorted(files.items()):
   if p.is_symlink() or not p.resolve().is_relative_to(root.resolve()):raise ValueError('Unsafe package source')
   info=zipfile.ZipInfo(name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o100644<<16
   z.writestr(info,p.read_bytes())
 with zipfile.ZipFile(archive) as z:
  if set(z.namelist())!=set(files) or any(name.lower().endswith(('.dll','.exe','.log')) for name in z.namelist()):raise ValueError('Unexpected package member')
  for name,p in files.items():
   if z.read(name)!=p.read_bytes():raise ValueError('Archive content mismatch')
 digest=hashlib.sha256(archive.read_bytes()).hexdigest()
 (out/'SHA256SUMS').write_text(f'{digest}  {archive.name}\n')
 (out/'package-manifest.json').write_text(json.dumps({'version':version,'archive':archive.name,'sha256':digest,'files':{name:hashlib.sha256(p.read_bytes()).hexdigest() for name,p in sorted(files.items())},'validation':'source installer package; game compilation is performed locally by the installer'},indent=2)+'\n')
 print(archive.name)
 return archive
if __name__=='__main__':build()
