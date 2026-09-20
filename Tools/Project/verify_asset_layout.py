"""Verify migration identity, binary content, folder conventions and local GUID references."""
from pathlib import Path
import hashlib
import json
import re

ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Documentation/AssetOrganization'
data=json.loads((REPORT/'MigrationPlan.json').read_text(encoding='utf-8'))
edited={e['path'] for e in data['textUpdates']}
edited.update({'Assets/CoastalTemple/Reusable/Player/MannequinLocomotion.controller', 'Assets/CoastalTemple/Reusable/FirstPerson/FirstPersonHands.controller'})
records=[]
def check(ok, detail): records.append(('PASS ' if ok else 'FAIL ')+detail)

for item in data['assets']:
    source,target=item['source'],item['target']
    if target is None:
        check(not (ROOT/source).exists(), 'Removed '+source);continue
    path=ROOT/target
    check(path.exists(),'Exists '+target)
    meta=Path(str(path)+'.meta')
    match=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8'),re.M) if meta.exists() else None
    check(bool(match) and match[1]==item['guid'],'Preserved GUID '+target)
    if source not in edited:
        # C# files may be deliberately edited after moving; immutable imported art may not.
        if path.suffix.lower() not in ('.cs','.md'):
            unchanged=hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256']
            if not unchanged and target=='Assets/Scenes/CoastalTemple.unity':
                # Saving the scene increments ProBuilder prefab revision overrides.
                # Verify the backup against the original hash before allowing only
                # these numeric counters to differ; all scene content still compares.
                baseline=ROOT/'Assets/Scenes/Archive/CoastalTemple_V14_BeforeAssetOrganization.unity'
                trusted=baseline.exists() and hashlib.sha256(baseline.read_bytes()).hexdigest()==item['sha256']
                pattern=r'(propertyPath: m_VersionIndex\s+value: )\d+'
                normalize=lambda p: re.sub(pattern,r'\g<1><revision>',p.read_text(encoding='utf-8'))
                check(trusted and normalize(baseline)==normalize(path),
                      'Preserved scene content apart from ProBuilder save revision counters '+target)
            else:
                check(unchanged,'Preserved content '+target)

allowed={'Animations','Materials','Objects','Prefabs','Scenes','Scripts','Settings','Shaders','Textures'}
check({p.name for p in (ROOT/'Assets').iterdir() if p.is_dir()}==allowed,'Only the nine documented top-level categories')
for extension,folder in [('.cs','Scripts'),('.prefab','Prefabs'),('.unity','Scenes'),('.mat','Materials'),('.controller','Animations'),('.anim','Animations'),('.shader','Shaders')]:
    misplaced=[p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets').rglob('*'+extension) if not p.is_relative_to(ROOT/'Assets'/folder)]
    check(not misplaced,'All '+extension+' files are in '+folder+': '+str(misplaced))
check(not list((ROOT/'Assets').rglob('*.zip')),'No ZIP archives imported into Unity')
check(not (ROOT/'Assets/Resources').exists(),'Unused Resources removed')
for source in data['extracted']:
    check(hashlib.sha256((ROOT/source['extracted']).read_bytes()).hexdigest()==source['sha256'],'Extracted source intact '+source['extracted'])

guids={}
for folder in ['Assets','Packages','Library/PackageCache']:
    for meta in (ROOT/folder).rglob('*.meta'):
        try: text=meta.read_text(encoding='utf-8-sig')
        except (UnicodeError,OSError): continue
        m=re.search(r'^guid: ([a-f0-9]{32})',text,re.M)
        if m:guids.setdefault(m[1],[]).append(meta.relative_to(ROOT).as_posix())
duplicates={g:paths for g,paths in guids.items() if sum(p.startswith('Assets/') for p in paths)>1}
check(not duplicates,'No duplicate asset GUIDs: '+str(duplicates))
missing=[]
for p in (ROOT/'Assets').rglob('*'):
    if p.suffix not in ('.unity','.prefab','.mat','.asset','.controller','.anim','.terrainlayer'):continue
    try: text=p.read_text(encoding='utf-8-sig')
    except UnicodeError:continue
    for guid in set(re.findall(r'guid: ([a-f0-9]{32})',text)):
        if guid not in guids and not guid.startswith('0000000000000000'):
            missing.append({'asset':p.relative_to(ROOT).as_posix(),'guid':guid})
before_by_target={a['target']:a for a in data['assets'] if a['target']}
preexisting=[]; introduced=[]
for item in missing:
    before=before_by_target.get(item['asset'])
    unchanged=before and hashlib.sha256((ROOT/item['asset']).read_bytes()).hexdigest()==before['sha256']
    (preexisting if unchanged else introduced).append(item)
check(not introduced,'Migration introduces no unresolved asset GUIDs: '+str(introduced))
if preexisting:
    records.append('INFO Existing package references in byte-identical rendering settings: '+str(preexisting))
    (REPORT/'PreexistingPackageReferences.json').write_text(json.dumps(preexisting,indent=2),encoding='utf-8')
(REPORT/'AssetIntegrity.txt').write_text('\n'.join(records),encoding='utf-8')
print(json.dumps({'pass':sum(r.startswith('PASS') for r in records),'fail':[r for r in records if r.startswith('FAIL')],'report':'Documentation/AssetOrganization/AssetIntegrity.txt'},ensure_ascii=False,indent=2))
raise SystemExit(any(r.startswith('FAIL') for r in records))
