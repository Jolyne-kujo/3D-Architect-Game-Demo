"""Package the already-built V8 Windows player and validate every ZIP entry."""
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[2]
build = root / "Builds/CoastalTutorial-Windows"
package = build.with_suffix(".zip")
readme = """潮岸遗迹 · V8 教学玩法白模

解压整个文件夹后，双击 CoastalTemple.exe。
请保留 CoastalTemple_Data、UnityPlayer.dll 和其余附带文件。

沿岸边的红幕机关开始，循山路前往半山。
庭院保留排水实验，岸边另有独立的水中实体投影实验池。

WASD：移动；鼠标：转动视角；Shift：快走
空格：地面跳跃／水中上浮；Ctrl：下潜；松开后自动浮在水面
E：操作近处控制台或拾取提灯
C：第一／第三人称；L：取得提灯后开关照明
H：观察线索，反复按可获得更具体的提示
Tab／Esc：行走与全景切换；F1／F2／F3：观察视点
Home：回到岸边起点；Alt+F4：退出

原庭院水池：靠近原控制台按 E 开启排水光源，再按 E 复原。
永久石幕会在本次游玩中保持消失；Home 不重置机关。
重新启动游戏会复原所有实验，本版尚未实现跨次游玩的存档。

这是可编辑白模玩法原型。光门目前传递光线，不传送角色。
投影台阶实体化是幻想规则；折射方向采用空气入水的实际光学计算。
"""
(build / "开始试玩.txt").write_text(readme, encoding="utf-8-sig")
with ZipFile(package, "w", ZIP_DEFLATED, compresslevel=6) as archive:
    for path in sorted(build.rglob("*")):
        if path.is_file() and not any("DoNotShip" in part for part in path.relative_to(build).parts):
            archive.write(path, path.relative_to(build))
with ZipFile(package) as archive:
    names = archive.namelist()
    assert "CoastalTemple.exe" in names and "UnityPlayer.dll" in names
    assert "开始试玩.txt" in names and archive.testzip() is None
report = {
    "path": str(package.relative_to(root)),
    "zipMB": round(package.stat().st_size / 1048576, 2),
    "entries": len(names),
    "crcValid": True,
    "sha256": hashlib.file_digest(package.open("rb"), "sha256").hexdigest(),
}
(root / "Documentation/CoastalV8Verification/PackageChecks.json").write_text(
    json.dumps(report, indent=2) + "\n", encoding="utf-8"
)
print(json.dumps(report))
