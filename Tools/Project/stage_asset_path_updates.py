"""Stage path edits for the Unity-controlled migration. Does not edit imported assets."""
from pathlib import Path
import json
import re

ROOT=Path(__file__).resolve().parents[2]
PLAN=ROOT/'Documentation/AssetOrganization/MigrationPlan.json'
data=json.loads(PLAN.read_text(encoding='utf-8'))
if not (ROOT/'Assets/CoastalTemple').exists():
    raise SystemExit('Migration already applied; do not overwrite the original text-update audit.')
mappings={a['source']:a['target'] for a in data['assets'] if a['target'] and a['source']!=a['target']}
mappings.update(data['prefixes'])
pattern=re.compile('|'.join(re.escape(p) for p in sorted(mappings,key=len,reverse=True)))

def rewrite(text, path):
    if path.suffix=='.cs':
        # Old generators mixed meshes, materials and prefabs beneath a single root.
        # Expand those paths before classifying, so each output goes to its proper folder.
        qualified={
            'ReusableGameplayAuthoring.Root':'Assets/CoastalTemple/Reusable',
            'FirstPersonAuthoring.Folder':'Assets/CoastalTemple/Reusable/FirstPerson',
            'RouteAuthoringKit.AssetsRoot':'Assets/CoastalTemple/TutorialV9',
        }
        for name,value in qualified.items():
            text=re.sub(re.escape(name)+r'\s*\+\s*"([^"]*)"',lambda m:'"'+value+m[1]+'"',text)
        declaration=re.compile(r'^\s*(?:(?:public|internal|private)\s+)?const\s+string\s+(Root|Folder|Base|AssetsRoot)\s*=\s*"(Assets/[^"]+)";[ \t]*$',re.M)
        for match in list(declaration.finditer(text)):
            name,value=match[1],match[2]
            text=text.replace(match[0],'')
            text=re.sub(r'(?<![\w.])'+name+r'\s*\+\s*"([^"]*)"',lambda m:'"'+value+m[1]+'"',text)
            text=re.sub(r'(?<![\w.])'+name+r'\b','"'+value+'"',text)
    # A single substitution prevents cascaded replacements of newly-created paths.
    text=pattern.sub(lambda m:mappings[m[0]],text)
    for old,new in sorted(mappings.items(),key=lambda item:-len(item[0])):
        text=text.replace(old.replace('/','\\'),new.replace('/','\\'))
    return text

excluded={a['source'] for a in data['assets'] if not a['target']}
files=[]
for base in ['Assets','Tools','Tests','Documentation','ProjectSettings']:
    for path in (ROOT/base).rglob('*'):
        if not path.is_file() or any(part in ('bin','obj') for part in path.parts):continue
        if path.suffix.lower() not in ('.cs','.py','.ps1','.md','.json','.csproj','.unity','.prefab','.asset'):continue
        relative=path.relative_to(ROOT).as_posix()
        if relative in excluded or relative.startswith(('Documentation/AssetOrganization/','Tools/Project/')):continue
        files.append(path)
files.append(ROOT/'README.md')
staged=[]
for path in files:
    try: original=path.read_text(encoding='utf-8-sig')
    except UnicodeError:continue
    updated=rewrite(original,path)
    if updated==original:continue
    relative=path.relative_to(ROOT).as_posix()
    target=ROOT/'Logs/AssetOrganization/UpdatedSources'/relative
    target.parent.mkdir(parents=True,exist_ok=True);target.write_text(updated,encoding='utf-8')
    staged.append({'path':relative,'staged':target.relative_to(ROOT).as_posix()})
data['textUpdates']=staged
PLAN.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'stagedUpdates':len(staged),'sourceFiles':[s['path'] for s in staged if s['path'].endswith('.cs')]},indent=2))
