"""Send a command to the already-open Unity Editor; Python 3 stdlib only."""

import argparse
import json
import math
import os
from pathlib import Path
import sys
import time
import uuid


def main():
    """Parse CLI arguments, atomically publish a command, and wait for its result."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--timeout", type=float, default=300)
    commands = parser.add_subparsers(dest="type", required=True)
    for name in ("ping", "refresh", "compile-status", "scene-status", "stop"):
        commands.add_parser(name)
    execute = commands.add_parser("execute-method")
    execute.add_argument("--method", required=True)
    tests = commands.add_parser("run-tests")
    tests.add_argument("--filter", default="")
    tests.add_argument("--results", type=Path, required=True)
    scene = commands.add_parser("open-scene")
    scene.add_argument("--path", "--scene", dest="path", required=True)
    capture = commands.add_parser("play-capture")
    capture.add_argument("--scene", dest="path")
    delay = capture.add_mutually_exclusive_group()
    delay.add_argument("--seconds", type=float)
    delay.add_argument("--after-frames", dest="afterFrames", type=int)
    capture.add_argument("--screenshot", type=Path, required=True)
    capture.add_argument("--extra-args", dest="extraArgs", default="")
    for subparser in (execute, scene, capture):
        subparser.add_argument("--save-scene", dest="saveScene", action=argparse.BooleanOptionalAction, default=True)
        subparser.add_argument("--restore-scene", dest="restoreScene", action=argparse.BooleanOptionalAction, default=True)
    # Common options are accepted before or after the subcommand.
    for subparser in commands.choices.values():
        subparser.add_argument("--timeout", type=float, default=argparse.SUPPRESS)
        subparser.add_argument("--project", type=Path, default=argparse.SUPPRESS)
    args = vars(parser.parse_args())
    project = args.pop("project").resolve()
    timeout = args.pop("timeout")
    if not math.isfinite(timeout) or timeout <= 0:
        parser.error("--timeout must be finite and positive")
    if not (project / "Assets").is_dir():
        parser.error("--project must be a Unity project directory")
    for key in ("seconds", "afterFrames"):
        value = args.get(key)
        if value is not None and (not math.isfinite(value) or value < 0):
            parser.error(f"--{key} must be finite and nonnegative")
    if args["type"] == "play-capture" and args.get("seconds") is None and args.get("afterFrames") is None:
        args["afterFrames"] = 90
    for key in ("results", "screenshot"):
        if key in args:
            args[key] = str(args[key].resolve())
    command = {key: value for key, value in args.items() if value is not None}
    command["id"] = uuid.uuid4().hex
    bridge = project / ".bridge"
    queue = bridge / "commands"
    queue.mkdir(parents=True, exist_ok=True)
    result_path = bridge / "results" / (command["id"] + ".json")
    temporary = queue / (command["id"] + ".tmp")
    pending = queue / (command["id"] + ".json")
    temporary.write_text(json.dumps(command, ensure_ascii=False), encoding="utf-8")
    os.replace(temporary, pending)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if result_path.is_file():
            result = json.loads(result_path.read_text(encoding="utf-8-sig"))
            print(json.dumps(result, ensure_ascii=False))
            return 0 if result.get("ok") else 1
        time.sleep(0.25)
    print(json.dumps({"id": command["id"], "ok": False, "output": "", "error":
        "Timed out waiting for the open Unity Editor. Command remains queued or running; timeout does not cancel it.",
        "finishedAt": None}, ensure_ascii=False))
    return 2


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError) as error:
        print(json.dumps({"ok": False, "output": "", "error": str(error), "finishedAt": None}))
        sys.exit(1)
