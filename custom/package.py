"""Package and verify local SalaryBeat artifacts. No network or publishing."""
from pathlib import Path
import hashlib,json,shutil,struct,zipfile
root=Path(__file__).resolve().parent
repo=root.parent
version='1.1.1'
out=root/'dist'/version
binary=out/'win-x64'
exe=binary/'SalaryBeat.exe'
if not exe.exists():raise SystemExit('Build the self-contained Windows x64 executable first.')
data=exe.read_bytes();pe=struct.unpack_from('<I',data,0x3c)[0]
assert data[:2]==b'MZ' and data[pe:pe+4]==b'PE\0\0' and struct.unpack_from('<H',data,pe+4)[0]==0x8664
for name in ['README.md','THIRD_PARTY_NOTICES.md']:shutil.copy2(root/name,binary/name)
shutil.copy2(repo/'LICENSE',binary/'LICENSE.SalaryGlow')
for folder in ['licenses','docs']:shutil.copytree(root/folder,binary/folder,dirs_exist_ok=True)
sources={}
for f in sorted((root/'data').glob('*.json')):
 d=json.loads(f.read_text());sources[f.name]={'year':d['year'],'entries':len(d['days']),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}
assert '2026.json' in sources and '2027.json' in sources
(root/'docs/DATA_SOURCES.json').write_text(json.dumps(sources,indent=2)+'\n')
shutil.copy2(root/'docs/DATA_SOURCES.json',binary/'docs/DATA_SOURCES.json')
archives=[(out/f'SalaryBeat-{version}-win-x64.zip',binary,'SalaryBeat'),(out/f'SalaryBeat-{version}-source.zip',repo,'SalaryBeat-source')]
sums=[]
for target,base,prefix in archives:
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for f in sorted(base.rglob('*')):
   rel=f.relative_to(base)
   if not f.is_file() or any(p in {'.git','bin','obj','dist','.DS_Store','__pycache__'} for p in rel.parts) or f.suffix in {'.pdb','.pyc'} or f.name.startswith('.env'):continue
   if f.name.startswith('settings.json'):raise ValueError('Private settings must never enter a release.')
   z.write(f,str(Path(prefix)/rel))
 with zipfile.ZipFile(target) as z:
  assert z.testzip() is None
  names=z.namelist()
  assert not any('/settings.json' in n or '/.git/' in n or '/SalaryGlow.Leo.exe' in n for n in names)
  if base==binary:
   assert 'SalaryBeat/SalaryBeat.exe' in names
   assert 'SalaryBeat/data/2026.json' in names and 'SalaryBeat/data/2027.json' in names
 checksum=hashlib.sha256(target.read_bytes()).hexdigest();sums.append(checksum+'  '+target.name)
 print(target.name,len(names),'files',target.stat().st_size,'bytes',checksum)
(out/'SHA256SUMS.txt').write_text('\n'.join(sums)+'\n')
