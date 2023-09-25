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

    socat_cfg = cfg["socat"]
    gameport = cfg["server"]["gameport"]
    dest_host = socat_cfg["dest_host"]
    dest_port = socat_cfg["dest_port"]

    socat_args = [
        "socat",
        "-d",
        "-d",
        "-d",
        "-t1",
        "-T5",
        f"UDP-LISTEN:{gameport},fork,reuseaddr",
        f"UDP:{dest_host}:{dest_port}",
    ]

    print(f"running socat with: {socat_args}")

    subprocess.check_call(socat_args)


if __name__ == "__main__":
    main()
