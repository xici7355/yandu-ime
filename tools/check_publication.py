#!/usr/bin/env python3
"""Check a publication tree without printing credential values."""
from pathlib import Path
import re,sys,subprocess
ROOT=Path(__file__).resolve().parents[1]
EXCLUDED={'.git','.tools','bin','dist','build','.gradle','.kotlin','.cxx','__pycache__'}
DENIED={'.env','local.properties','settings.json','key.properties','keystore.properties','.git-credentials','google-services.json'}
SUFFIX={'.jks','.keystore','.ks','.pfx','.p12','.pem','.key','.apk','.aab','.exe','.log','.exit'}
PATTERNS=[
 ('private-key',re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----')),
 ('github-token',re.compile(rb'(?:gh[pousr]_[A-Za-z0-9]{24,}|github_pat_[A-Za-z0-9_]{35,})')),
 ('api-key',re.compile(rb'\bsk-(?:(?:proj|svcacct|ant-api[0-9]+)-)?[A-Za-z0-9_-]{24,}\b')),
 ('dpapi-config',re.compile(rb'\x41QAAANCMnd8BFdERjHoAwE')),
 ('credential-url',re.compile(rb'https?://[^\s/"\']+:[^\s/"\']+@')),
]
def files():
 if (ROOT/'.git').exists():
  data=subprocess.check_output(['git','-C',str(ROOT),'ls-files','--cached','--others','--exclude-standard','-z'])
  return [ROOT/x.decode('utf8') for x in data.split(b'\0') if x]
 return [p for p in ROOT.rglob('*') if p.is_file() and not any(x in EXCLUDED for x in p.relative_to(ROOT).parts)]
issues=[];count=0;largest=[]
for path in files():
 if not path.is_file():continue
 rel=path.relative_to(ROOT).as_posix()
 count+=1
 if path.name in DENIED or (path.name.startswith('.env.') and path.name!='.env.example') or path.suffix.lower() in SUFFIX:
  issues.append((rel,'forbidden-file'));continue
 size=path.stat().st_size
 if size>95*1024*1024:issues.append((rel,'exceeds-github-file-size'))
 largest.append((size,rel))
 if path.suffix.lower() in {'.dll','.lib','.jar','.png','.jpg','.jpeg','.ico','.wav','.webp','.ttf','.otf'}:continue
 data=path.read_bytes()
 if b'\x00' in data[:4096]:continue
 for label,pattern in PATTERNS:
  if pattern.search(data):issues.append((rel,label))
if issues:
 for rel,label in issues:print('FAIL',label,rel)
 print('No matching credential values were printed.');sys.exit(1)
print('OK',count,'publication files; no forbidden credential/config files or matching secrets')
print('Largest file:',max(largest)[1],max(largest)[0],'bytes')
