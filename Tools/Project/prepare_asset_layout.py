"""Prepare a GUID-preserving Unity migration plan; never move imported assets here."""
from pathlib import Path
import hashlib
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Documentation/AssetOrganization'
TYPES = {a['path']: a['type'] for a in json.loads((REPORT/'Before.json').read_text(encoding='utf-8'))}

# Stable folder replacements for code/tools. Specific asset mappings take precedence.
PREFIXES = {
    'Assets/CoastalTemple/Runtime': 'Assets/Scripts/Gameplay',
    'Assets/CoastalTemple/Editor': 'Assets/Scripts/Editor/Gameplay',
    'Assets/WaterSystem/Runtime': 'Assets/Scripts/WaterSystem',
    'Assets/WaterCourtyard/Runtime': 'Assets/Scripts/WaterDemo/Runtime',
    'Assets/WaterCourtyard/Editor': 'Assets/Scripts/Editor/WaterDemo',
    'Assets/Editor': 'Assets/Scripts/Editor/Tools',
    'Assets/CoastalTemple/Archive': 'Assets/Scenes/Archive',
    'Assets/CoastalTemple/Scenes': 'Assets/Scenes',
    'Assets/WaterCourtyard/Scenes': 'Assets/Scenes',
    'Assets/CoastalTemple/Prefabs': 'Assets/Prefabs/Architecture',
    'Assets/CoastalTemple/Reusable/Player': 'Assets/Prefabs/Player',
    'Assets/CoastalTemple/Reusable/FirstPerson': 'Assets/Prefabs/Player',
    'Assets/CoastalTemple/Reusable/Light': 'Assets/Prefabs/Light',
    'Assets/CoastalTemple/TutorialV9/Prefabs': 'Assets/Prefabs/Tutorial/V9',
    'Assets/CoastalTemple/Tutorial/Prefabs': 'Assets/Prefabs/Tutorial/V8',
    'Assets/WaterCourtyard/Generated/Prefabs': 'Assets/Prefabs/Water',
    'Assets/WaterSystem/Prefabs': 'Assets/Prefabs/Water',
    'Assets/CoastalTemple/TutorialV9/Materials': 'Assets/Materials/Tutorial/V9',
    'Assets/CoastalTemple/Tutorial/Materials': 'Assets/Materials/Tutorial/V8',
    'Assets/CoastalTemple/Materials': 'Assets/Materials/Environment',
    'Assets/WaterCourtyard/Generated/Materials': 'Assets/Materials/WaterDemo',
    'Assets/WaterSystem/Materials': 'Assets/Materials/Water',
    'Assets/CoastalTemple/TutorialV9/Meshes': 'Assets/Objects/LevelGeometry/Tutorial/V9',
    'Assets/CoastalTemple/Tutorial/Meshes': 'Assets/Objects/LevelGeometry/Tutorial/V8',
    'Assets/CoastalTemple/TutorialV9/Excavation': 'Assets/Objects/LevelGeometry/Tutorial/V9/Excavation',
    'Assets/CoastalTemple/Terrain': 'Assets/Objects/Environment/Terrain',
    'Assets/WaterCourtyard/Generated': 'Assets/Objects/LevelGeometry/WaterDemo',
    'Assets/CoastalTemple/Shaders': 'Assets/Shaders',
    'Assets/WaterSystem/Shaders': 'Assets/Shaders',
    'Assets/ThirdParty/PureNatureSubset/Models': 'Assets/Objects/Environment/PureNature',
    'Assets/ThirdParty/PureNatureSubset/Textures': 'Assets/Textures/Environment/PureNature',
    'Assets/ThirdParty/PureNatureSubset': 'Assets/Objects/Environment/PureNature/SourceInfo',
    'Assets/ThirdParty/Quaternius/UniversalAnimationLibrary': 'Assets/Objects/Characters/Quaternius',
    'Assets/Character': 'Assets/Animations/Player/Locomotion',
}

def replace_prefix(path):
    for old, new in sorted(PREFIXES.items(), key=lambda p: -len(p[0])):
        if path == old or path.startswith(old+'/'):
            return new + path[len(old):]
    return path

def destination(path):
    p=Path(path); kind=TYPES.get(path)
    if path.startswith('Assets/TutorialInfo/') or path=='Assets/Readme.asset' or p.suffix=='.zip' or path.startswith('Assets/Resources/'):
        return None
    if path.startswith('Assets/Character/') and p.suffix=='.fbx':
        return 'Assets/Animations/Player/Locomotion/' + ('SlowRun.fbx' if 'Slow' in p.name else 'FastRun.fbx')
    if p.suffix in ('.controller', '.anim'):
        return 'Assets/Animations/Player/'+p.name
    if path=='Assets/CoastalTemple/Reusable/FirstPerson/Quaternius_ArmsOnly.asset':
        return 'Assets/Objects/Characters/Quaternius/Quaternius_ArmsOnly.asset'
    if path.startswith('Assets/CoastalTemple/Reusable/') and p.suffix=='.mat':
        return ('Assets/Materials/Light/' if '/Light/' in path else 'Assets/Materials/Player/')+p.name
    if kind=='Texture2D' and p.suffix=='.asset':
        if '/Terrain/' in path:
            return 'Assets/Textures/Terrain/'+path.split('/Terrain/',1)[1]
        return ('Assets/Textures/WaterDemo/' if '/WaterCourtyard/' in path else 'Assets/Textures/Environment/')+p.name
    if kind=='VolumeProfile':
        return 'Assets/Settings/Lighting/'+p.name
    if p.suffix=='.terrainlayer':
        return 'Assets/Objects/Environment/Terrain/Layers/'+p.name
    if path=='Assets/CoastalTemple/Terrain/Terrain.mat':
        return 'Assets/Materials/Environment/Terrain.mat'
    if path.startswith('Assets/ThirdParty/Quaternius/') and p.suffix.lower()=='.png':
        return 'Assets/Textures/Reference/Quaternius/'+p.name
    if path=='Assets/InputSystem_Actions.inputactions':
        return 'Assets/Settings/Input/'+p.name
    if path.startswith('Assets/Settings/') and kind in ('UniversalRendererData','UniversalRenderPipelineAsset','UniversalRenderPipelineGlobalSettings'):
        return 'Assets/Settings/Rendering/'+p.name
    return replace_prefix(path)

def prepare():
    if not (ROOT/'Assets/Character').exists():
        raise SystemExit('Migration already applied; keep the original audit plan and use verify_asset_layout.py.')
    REPORT.mkdir(parents=True,exist_ok=True)
    sources=[]
    target=ROOT/'SourceAssets/Characters/Mixamo'
    target.mkdir(parents=True,exist_ok=True)
    for archive in sorted((ROOT/'Assets/Character').glob('*.zip')):
        with zipfile.ZipFile(archive) as z:
            assert z.testzip() is None, f'Corrupt archive: {archive}'
            for info in z.infolist():
                if info.is_dir(): continue
                output=(target/info.filename).resolve()
                assert output.is_relative_to(target.resolve()), 'Unsafe archive path'
                data=z.read(info)
                if output.exists(): assert output.read_bytes()==data, f'Existing file differs: {output}'
                else:
                    output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(data)
                sources.append({'archive':archive.relative_to(ROOT).as_posix(),'extracted':output.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
    plan=[]
    for p in sorted((ROOT/'Assets').rglob('*')):
        if not p.is_file() or p.suffix=='.meta': continue
        path=p.relative_to(ROOT).as_posix()
        meta=Path(str(p)+'.meta')
        guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M).group(1) if meta.exists() else ''
        plan.append({'source':path,'target':destination(path),'guid':guid,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    targets=[a['target'].lower() for a in plan if a['target']]
    assert len(targets)==len(set(targets)), 'Duplicate destinations'
    for a in plan:
        if a['target'] and a['source']!=a['target']:
            assert not (ROOT/a['target']).exists(), f'Destination occupied: {a["target"]}'
    payload={'assets':plan,'prefixes':PREFIXES,'extracted':sources}
    (REPORT/'MigrationPlan.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'files':len(plan),'moves':sum(a['target'] not in (a['source'],None) for a in plan),'remove':[a['source'] for a in plan if not a['target']],'extracted':sources},indent=2))

if __name__=='__main__': prepare()
