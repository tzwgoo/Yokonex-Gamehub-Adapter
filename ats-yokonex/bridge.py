import sys
from pathlib import Path

try:
    from scs_bridge import run
except ModuleNotFoundError:
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "_shared"))
    from scs_bridge import run


if __name__ == "__main__":
    try:
        run("ats")
    except KeyboardInterrupt:
        pass
