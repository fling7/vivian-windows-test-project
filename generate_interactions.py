#!/usr/bin/env python3
"""Generate interaction specification files via OpenAI API.

This script reads documentation from the Vivian framework, takes a free text
user description and existing specification JSON files, and delegates the
creation of updated specification files to the OpenAI API.

It can be executed in two modes:
- GUI mode (default) providing a text box and a "Generate" button.
- CLI mode using command line arguments (use ``--no-gui``).

The OpenAI API key is read from the environment variable ``OPENAI_API_KEY``.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Dict

try:
    from openai import OpenAI  # type: ignore
except Exception:  # pragma: no cover - import error only triggers at runtime
    OpenAI = None  # type: ignore

try:
    import tkinter as tk
    from tkinter import filedialog, messagebox
except Exception:  # pragma: no cover - tkinter not available in some envs
    tk = None  # type: ignore

# Paths
REPO_ROOT = Path(__file__).resolve().parent
DOCS_DIR = REPO_ROOT / "Packages" / "vivian-core" / "Docs"
DOC_FILENAMES = [
    "InteractionElementsDocu.md",
    "StatesDocu.md",
    "TransitionsDocu.md",
    "VisualizationElementsDocu.md",
]
SPEC_FILENAMES = [
    "InteractionElements.json",
    "States.json",
    "Transitions.json",
    "VisualizationElements.json",
    "VisualizationArrays.json",
]


def load_docs() -> Dict[str, str]:
    """Read documentation files shipped with the project."""
    docs = {}
    for name in DOC_FILENAMES:
        path = DOCS_DIR / name
        if path.exists():
            docs[name] = path.read_text(encoding="utf-8")
    return docs


def load_specification(spec_dir: Path) -> Dict[str, str]:
    """Read existing specification JSON files from ``spec_dir``."""
    data = {}
    for filename in SPEC_FILENAMES:
        path = spec_dir / filename
        data[filename] = path.read_text(encoding="utf-8") if path.exists() else ""
    return data


def build_messages(description: str, docs: Dict[str, str], spec: Dict[str, str]):
    """Construct messages for the OpenAI API."""
    system_prompt = (
        "You generate JSON specifications for interactive objects."
        " The user provides a description of the desired interaction,"
        " documentation of the involved files and the current"
        " specifications. Produce updated JSON for InteractionElements,"
        " States, Transitions, VisualizationElements and"
        " VisualizationArrays. Return a JSON object with the keys"
        " 'InteractionElements', 'States', 'Transitions',"
        " 'VisualizationElements' and 'VisualizationArrays', each"
        " containing the JSON file content as a string."
    )
    user_parts = [f"User description:\n{description}"]
    for name, content in docs.items():
        user_parts.append(f"---\n{name}:\n{content}")
    for name, content in spec.items():
        if content:
            user_parts.append(f"---\nCurrent {name}:\n{content}")
    user_message = "\n\n".join(user_parts)
    return [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_message},
    ]


def call_openai(messages):
    """Call the OpenAI API and return the textual response."""
    if OpenAI is None:
        raise RuntimeError("openai package not installed")
    client = OpenAI()
    response = client.responses.create(model="gpt-4o-mini", input=messages)
    return response.output_text


def write_results(result: Dict[str, str], spec_dir: Path) -> None:
    """Write API results into specification files."""
    mapping = {
        "InteractionElements": "InteractionElements.json",
        "States": "States.json",
        "Transitions": "Transitions.json",
        "VisualizationElements": "VisualizationElements.json",
        "VisualizationArrays": "VisualizationArrays.json",
    }
    for key, filename in mapping.items():
        if key in result:
            path = spec_dir / filename
            try:
                parsed = json.loads(result[key])
                path.write_text(json.dumps(parsed, indent=2), encoding="utf-8")
            except json.JSONDecodeError:
                # Write raw text if it is not valid JSON
                path.write_text(result[key], encoding="utf-8")


def generate(spec_dir: Path, description: str, dry_run: bool = False) -> None:
    docs = load_docs()
    spec = load_specification(spec_dir)
    messages = build_messages(description, docs, spec)
    if dry_run:
        print("Prepared messages for OpenAI call. Dry run, no API call executed.")
        return
    output = call_openai(messages)
    try:
        result = json.loads(output)
    except json.JSONDecodeError as exc:  # pragma: no cover - depends on model output
        raise RuntimeError(f"API did not return valid JSON: {exc}\n{output}")
    write_results(result, spec_dir)


def gui_main() -> None:  # pragma: no cover - GUI not tested in CI
    if tk is None:
        raise RuntimeError("tkinter is not available on this system")
    root = tk.Tk()
    root.title("Generate Interactions")
    text = tk.Text(root, width=80, height=20)
    text.pack(padx=10, pady=10)

    def on_generate():
        description = text.get("1.0", tk.END).strip()
        if not description:
            messagebox.showerror("Error", "Description is empty")
            return
        spec_path = filedialog.askdirectory(title="Select FunctionalSpecification directory")
        if not spec_path:
            return
        try:
            generate(Path(spec_path), description)
            messagebox.showinfo("Success", "Files generated successfully")
        except Exception as e:  # pragma: no cover - error depends on runtime
            messagebox.showerror("Error", str(e))

    btn = tk.Button(root, text="Generate", command=on_generate)
    btn.pack(pady=(0, 10))
    root.mainloop()


def cli_main(args: argparse.Namespace) -> None:
    spec_dir = Path(args.spec_dir)
    description = args.description or input("Description: ")
    generate(spec_dir, description, dry_run=args.dry_run)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spec-dir", help="Directory containing specification JSON files")
    parser.add_argument("--description", help="Free text describing desired interactions")
    parser.add_argument("--dry-run", action="store_true", help="Build request but skip API call")
    parser.add_argument("--gui", action="store_true", help="Launch GUI instead of CLI")
    args = parser.parse_args()
    if args.gui:
        gui_main()
    else:
        if not args.spec_dir:
            parser.error("--spec-dir is required in CLI mode")
        cli_main(args)


if __name__ == "__main__":
    main()
