"""Prepare a NEW closed Unity consumer; does not launch Unity or modify any source checkout."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import subprocess
import tarfile
import tempfile

DISPLAY = "9f166245d87da18dbca1c8de281831b6dad30272"
UTILITY = "bb56919e99f8def1b950453f70e52b4c4ab6f534"
INJECTIO = "e499f61abe9b736d337bc3d3043a9909235a20a7"


def git(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args])


def archive(repo, revision, target, paths=None):
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryFile() as stream:
        command = ["git", "-C", str(repo), "archive", "--format=tar", revision]
        if paths:
            command += ["--", *paths]
        subprocess.run(command, stdout=stream, check=True)
        stream.seek(0)
        with tarfile.open(fileobj=stream, mode="r:") as source:
            source.extractall(target, filter="data")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--display", type=Path, required=True, help="Display repository containing merged D05.10")
    parser.add_argument("--utility", type=Path, required=True, help="Utility repository containing the pinned revision")
    parser.add_argument("--injectio", type=Path, required=True, help="Injectio repository containing the pinned revision")
    parser.add_argument("--consumer", type=Path, required=True, help="A NEW directory; existing paths are refused")
    parser.add_argument("--faciem", type=Path, default=Path(__file__).resolve().parents[2], help="Committed F05 review checkout")
    args = parser.parse_args()
    root = args.faciem.resolve()
    consumer = args.consumer.resolve()
    if consumer.exists():
        parser.error("Consumer already exists; do not repoint or overwrite an existing Unity project.")
    if git(root, "status", "--porcelain").strip():
        parser.error("Use a clean, committed Faciem review checkout so evidence matches source.")
    # Validate all git objects before creating anything.
    revisions = [(args.display, DISPLAY), (args.utility, UTILITY), (args.injectio, INJECTIO), (root, "HEAD")]
    for repository, revision in revisions:
        git(repository, "rev-parse", "--verify", revision + "^{commit}")
    archive(args.display, DISPLAY, consumer, ["Assets", "Packages", "ProjectSettings", "docs"])
    archive(args.utility, UTILITY, consumer / "Submodules/Sim.Utility")
    archive(args.injectio, INJECTIO, consumer / "Packages/Sim.Injectio")
    native = consumer / "Submodules/Sim.Faciem"
    source_hashes = {}
    # Copy tracked package sources only; no .git, lockfiles, generated Library, or old validation consumers/logs.
    for name in git(root, "ls-files", "-z").decode().split("\0"):
        if not name:
            continue
        relative = Path(name)
        if any(part.endswith("~") and part != "Samples~" for part in relative.parts) or relative.parts[0] == "docs":
            continue
        source = root / relative
        if not source.is_file():
            continue
        destination = native / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        source_hashes[relative.as_posix()] = hashlib.sha256(source.read_bytes()).hexdigest()
    shutil.copytree(root / "Samples~/TypedView", consumer / "Assets/F05ImportedSample")
    shutil.copyfile(root / "Validation~/F05/ImportedTypedSampleTests.cs", native / "Tests/Editor/F05ImportedTypedSampleTests.cs")
    with tempfile.TemporaryDirectory() as temporary:
        stage = Path(temporary)
        for slice_name in ["D059", "D0510"]:
            path = "Validation~/" + slice_name + "/Assets"
            archive(args.display, DISPLAY, stage, [path])
            shutil.copytree(stage / path, consumer / "Assets", dirs_exist_ok=True)
    manifest = consumer / "Packages/manifest.json"
    data = json.loads(manifest.read_text())
    data["testables"] = ["com.sim.faciem"]
    manifest.write_text(json.dumps(data, indent=2) + "\n")
    record = {
        "faciemCommit": git(root, "rev-parse", "HEAD").decode().strip(),
        "displayCommit": DISPLAY,
        "utilityCommit": UTILITY,
        "injectioCommit": INJECTIO,
        "sourceSha256": source_hashes,
        "importedSample": True,
        "unityLaunched": False,
    }
    (consumer / "f05-preparation.json").write_text(json.dumps(record, indent=2) + "\n")
    print("Prepared", consumer, "without launching Unity or modifying a source checkout.")


if __name__ == "__main__":
    main()
