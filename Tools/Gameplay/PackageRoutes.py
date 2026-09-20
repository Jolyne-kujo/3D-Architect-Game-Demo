"""Package an already-built V9 Windows player; this script does not build or play it."""
import hashlib
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


ROOT = Path(__file__).resolve().parents[2]
BUILD = ROOT / "Builds/CoastalRoutes-Windows"
PACKAGE = ROOT / "Builds/CoastalRoutes-Windows.zip"
REPORT = ROOT / "Documentation/CoastalV9Verification/PackageChecks.json"
README = """潮岸遗迹 · V9 断路玩法白模

解压整个文件夹后，双击 CoastalTemple.exe。
请保留 CoastalTemple_Data、UnityPlayer.dll 和其余附带文件。

三段断路
01 借光断廊：先借金色光桥走到中岛，再收光恢复红色承重板，抵达提灯。
02 断阶工坊：乘移动石墙到中层侧廊施工，用手动卷扬对齐横向石桥，
   再乘墙到上层通过。手柄按正向→停止→反向→停止循环。
03 折光修桥：用光搬动红石桥并停车，抬高光门出口，使光越过桥和中墩，
   照亮远端受光器，接通向上的光桥。

金色光桥受光出现、失光消失；双条纹红板受光消失、失光恢复。
单道破裂纹的永久红石幕烧除后不会在本次游玩中复原。
光驱动的墙和桥移光后保留位置；手动卷扬不需要光。
普通提灯只照明。光门传递光线，不传送角色。

WASD：移动；鼠标：转动视角；左 Shift：快走
空格：地面跳跃／水中上浮；左 Ctrl：下潜；松开后自动浮在水面
E：操作附近可达的控制台或拾取提灯
C：第一／第三人称；L：取得提灯后开关照明
H：查看渐进提示，重复按可获得更具体的观察与解题思路
Tab／Esc：行走与全景切换；F1／F2／F3：观察视点
Home：回到最初的岸边出生点；Alt+F4：退出

三段断口下方设有恢复区域：跌落后返回本段入口，机关保留当前状态。
已经搬好的桥、光门高度、已烧除的石幕和拾取的提灯不会因跌落重置。
Home 只返回原出生点，也不重置机关。
本次游玩的进度只保存在当前运行中，没有跨次存档；重新启动会复原实验。

原庭院水池与游泳仍然保留：靠近原控制台按 E 开启排水光源，再按 E 复原。
旁边另有可选的水中实体投影实验池，不是三段主路的进度门槛。

这是可编辑白模玩法原型。水中投影实体化采用幻想规则，
折射方向采用空气入水的实际光学计算。
"""


def main():
    required = (BUILD / "CoastalTemple.exe", BUILD / "UnityPlayer.dll")
    missing = [str(path.relative_to(ROOT)) for path in required if not path.is_file()]
    data = BUILD / "CoastalTemple_Data"
    if not data.is_dir() or not any(path.is_file() for path in data.rglob("*")):
        missing.append(str(data.relative_to(ROOT)) + " (nonempty directory)")
    if missing:
        raise SystemExit("Build V9 Windows first; missing: " + ", ".join(missing))

    (BUILD / "开始试玩.txt").write_text(README, encoding="utf-8-sig", newline="\n")
    files = sorted(
        path for path in BUILD.rglob("*")
        if path.is_file()
        and not any("DoNotShip" in part for part in path.relative_to(BUILD).parts)
    )
    temporary = PACKAGE.with_suffix(".zip.tmp")
    try:
        with ZipFile(temporary, "w", ZIP_DEFLATED, compresslevel=6) as archive:
            for path in files:
                archive.write(path, path.relative_to(BUILD))
        with ZipFile(temporary) as archive:
            names = archive.namelist()
            expected = [path.relative_to(BUILD).as_posix() for path in files]
            if names != expected or len(names) != len(set(names)):
                raise RuntimeError("ZIP entries do not match the build file list")
            for name in ("CoastalTemple.exe", "UnityPlayer.dll", "开始试玩.txt"):
                if name not in names:
                    raise RuntimeError("Required ZIP entry is missing: " + name)
            if not any(name.startswith("CoastalTemple_Data/") for name in names):
                raise RuntimeError("Player data is missing from the ZIP")
            if archive.read("开始试玩.txt").decode("utf-8-sig") != README:
                raise RuntimeError("Packaged play instructions differ from the V9 text")
            bad_entry = archive.testzip()
            if bad_entry is not None:
                raise RuntimeError("ZIP CRC check failed: " + bad_entry)
        temporary.replace(PACKAGE)
    finally:
        if temporary.exists():
            temporary.unlink()

    with PACKAGE.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    report = {
        "path": str(PACKAGE.relative_to(ROOT)),
        "zipMB": round(PACKAGE.stat().st_size / 1048576, 2),
        "entries": len(names),
        "crcValid": True,
        "sha256": digest,
        "validationScope": "Package contents and ZIP integrity only; no Windows play validation.",
    }
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
