import argparse
import subprocess
import tomllib


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("file")
    args = ap.parse_args()
    file = args.file

    with open(file, "rb") as f:
        cfg = tomllib.load(f)

    # TODO: proper config.
    gameport = cfg["GAMEPORT"]
    dest_host = ""
    dest_gameport = ""

    subprocess.check_call([
        "socat",
        "-d",
        "-d",
        "-d",
        "-t1",
        "-T5",
        f"UDP-LISTEN{gameport},fork,reuseaddr",
        f"UDP:{dest_host}:{dest_gameport}",
    ])


if __name__ == "__main__":
    main()
