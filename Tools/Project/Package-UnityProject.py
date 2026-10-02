"""Build a complete, LFS-materialized Unity ZIP from a clean committed checkout.

Usage: python Tools/Project/Package-UnityProject.py
Output is in ignored Builds/TeamDownload, never inside the Git repository history.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Builds/TeamDownload"
PREFIX = "3D-Architect/"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def main():
    if git("status", "--porcelain", "--untracked-files=no").strip():
        raise SystemExit("Commit tracked changes before packaging the release.")
    commit = git("rev-parse", "HEAD").decode().strip()
    paths = sorted(git("ls-files", "-z").decode("utf-8").rstrip("\0").split("\0"))
    required = {"Packages/manifest.json", "Packages/packages-lock.json",
                "ProjectSettings/ProjectVersion.txt", "Assets/Scenes/CoastalTemple.unity"}
    if not required.issubset(paths):
        raise SystemExit("Missing essential Unity project files.")
    forbidden = {".git", "Library", "Temp", "Logs", "Builds", "UserSettings"}
    if any(p.split("/")[0] in forbidden for p in paths):
        raise SystemExit("Generated/private directories must not be tracked or packaged.")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    archive = OUTPUT / "3D-Architect-UnityProject.zip"
    checksums = {}
    total = 0
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
        for relative in paths:
            data = (ROOT / relative).read_bytes()
            if data.startswith(b"version https://git-lfs.github.com/spec/v1"):
                raise SystemExit("LFS pointer instead of real resource: " + relative)
            checksums[relative] = hashlib.sha256(data).hexdigest()
            total += len(data)
            bundle.writestr(PREFIX + relative, data)
        info = {"commit": commit, "unity": "6000.5.9f1", "sourceFiles": len(paths),
                "uncompressedBytes": total, "lfsMaterialized": True,
                "mainScene": "Assets/Scenes/CoastalTemple.unity",
                "labScene": "Assets/Scenes/MechanismPlayground.unity",
                "open": "Extract ZIP, then Unity Hub > Add project from disk > 3D-Architect."}
        bundle.writestr(PREFIX + "PACKAGE_INFO.json", json.dumps(info, ensure_ascii=False, indent=2))
        bundle.writestr(PREFIX + "CHECKSUMS.sha256", "".join(f"{digest}  {p}\n" for p, digest in checksums.items()))
    with zipfile.ZipFile(archive) as bundle:
        if len(bundle.infolist()) != len(paths) + 2 or bundle.testzip() is not None:
            raise SystemExit("ZIP entry count or CRC validation failed.")
        for relative, expected in checksums.items():
            if hashlib.sha256(bundle.read(PREFIX + relative)).hexdigest() != expected:
                raise SystemExit("ZIP content verification failed: " + relative)
    digest = hashlib.file_digest(archive.open("rb"), "sha256").hexdigest()
    checksum_file = OUTPUT / "3D-Architect-UnityProject.zip.sha256"
    checksum_file.write_text(f"{digest}  {archive.name}\n", encoding="utf-8")
    report = {**info, "zip": str(archive), "zipBytes": archive.stat().st_size,
              "sha256": digest, "verifiedFiles": len(checksums)}
    (OUTPUT / "PackageVerification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
