#!/usr/bin/env python3
"""Build the graphify knowledge graph for ODW.

Usage:
    python tools/graphify-project.py

It extracts code structurally (AST) and merges any semantic chunks written to
graphify-out/.graphify_chunk_*.json by a graphify extraction subagent. It then
builds graph.json, GRAPH_REPORT.md and the analysis sidecar.

Requires: pip install graphifyy
"""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

from graphify.analyze import god_nodes, suggest_questions, surprising_connections
from graphify.build import build_from_json
from graphify.cluster import cluster, score_all
from graphify.detect import detect, save_manifest
from graphify.export import to_json
from graphify.extract import collect_files, extract
from graphify.report import generate

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "graphify-out"
OUT.mkdir(exist_ok=True)

detection = detect(
    ROOT,
    extra_excludes={".git", "bin", "obj", "graphify-out", "TestResults"},
)
(OUT / ".graphify_detect.json").write_text(
    json.dumps(detection, ensure_ascii=False), encoding="utf-8"
)

code_files: list[Path] = []
for item in detection.get("files", {}).get("code", []):
    path = Path(item)
    code_files.extend(collect_files(path) if path.is_dir() else [path])

if code_files:
    ast = extract(code_files, cache_root=ROOT, root=ROOT)
else:
    ast = {"nodes": [], "edges": [], "input_tokens": 0, "output_tokens": 0}

semantic = {"nodes": [], "edges": [], "hyperedges": [], "input_tokens": 0, "output_tokens": 0}
chunks = sorted(OUT.glob(".graphify_chunk_*.json"))
for chunk in chunks:
    try:
        data = json.loads(chunk.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        print(f"warning: chunk {chunk.name} is not valid JSON; skipped", file=sys.stderr)
        continue
    for key in ("nodes", "edges", "hyperedges"):
        semantic[key].extend(data.get(key, []))
    semantic["input_tokens"] += data.get("input_tokens", 0)
    semantic["output_tokens"] += data.get("output_tokens", 0)

seen = {node["id"] for node in ast["nodes"]}
nodes = list(ast["nodes"])
for node in semantic["nodes"]:
    if node["id"] not in seen:
        nodes.append(node)
        seen.add(node["id"])

extraction = {
    "nodes": nodes,
    "edges": ast["edges"] + semantic["edges"],
    "hyperedges": semantic.get("hyperedges", []),
    "input_tokens": semantic.get("input_tokens", 0),
    "output_tokens": semantic.get("output_tokens", 0),
}
(OUT / ".graphify_extract.json").write_text(
    json.dumps(extraction, indent=2, ensure_ascii=False), encoding="utf-8"
)

graph = build_from_json(extraction, root=ROOT, directed=False)
if graph.number_of_nodes() == 0:
    print("error: extraction produced no nodes", file=sys.stderr)
    raise SystemExit(1)

communities = cluster(graph)
cohesion = score_all(graph, communities)
labels = {}
for cid, members in communities.items():
    ranked = sorted(members, key=lambda node: graph.degree(node), reverse=True)
    names = [str(graph.nodes[node].get("label", node)) for node in ranked[:2]]
    labels[cid] = " / ".join(names)[:60] or f"Community {cid}"
gods = god_nodes(graph)
surprises = surprising_connections(graph, communities)
questions = suggest_questions(graph, communities, labels)
tokens = {
    "input": extraction.get("input_tokens", 0),
    "output": extraction.get("output_tokens", 0),
}

if not to_json(graph, communities, str(OUT / "graph.json"), force=True):
    print("error: graph.json was not written", file=sys.stderr)
    raise SystemExit(1)

report = generate(
    graph,
    communities,
    cohesion,
    labels,
    gods,
    surprises,
    detection,
    tokens,
    ROOT,
    suggested_questions=questions,
)
(OUT / "GRAPH_REPORT.md").write_text(report, encoding="utf-8")

analysis = {
    "communities": {str(k): v for k, v in communities.items()},
    "cohesion": {str(k): v for k, v in cohesion.items()},
    "gods": gods,
    "surprises": surprises,
    "questions": questions,
}
(OUT / ".graphify_analysis.json").write_text(
    json.dumps(analysis, indent=2, ensure_ascii=False), encoding="utf-8"
)

try:
    save_manifest(
        {key: list(value) for key, value in detection.get("files", {}).items()},
        root=ROOT,
    )
except Exception as exception:  # manifest is best-effort
    print(f"warning: manifest not saved: {exception}", file=sys.stderr)

print(
    f"Graph: {graph.number_of_nodes()} nodes, {graph.number_of_edges()} edges, "
    f"{len(communities)} communities; semantic chunks: {len(chunks)}"
)

subprocess.run(
    [sys.executable, "-m", "graphify", "export", "html"],
    cwd=ROOT,
    check=False,
)
