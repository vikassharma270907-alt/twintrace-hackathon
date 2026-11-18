"""A small demo created by Codex inside Antigravity."""

from pathlib import Path
import sys


def main():
    print("Hello from Codex inside Antigravity!")
    print("I can create, edit, and run code in your workspace.")
    print(f"Demo file: {Path(__file__).resolve()}")
    print(f"Working directory: {Path.cwd()}")
    print(f"Python version: {sys.version.split()[0]}")
    print("Demo completed successfully.")


if __name__ == "__main__":
    main()
