# Grafo del proyecto (graphify)

Generado por [`tools/graphify-project.py`](../tools/graphify-project.py). No edites a mano
`graph.json`, `GRAPH_REPORT.md` ni `graph.html`: se regeneran.

- `graph.json` — nodos/aristas para `graphify query|path|explain`.
- `GRAPH_REPORT.md` — auditoría, comunidades y conexiones.
- `graph.html` — visualización interactiva (abrir en navegador).

## Consultar

```powershell
python -m graphify query "¿Cómo se aisla la red del agente?"
python -m graphify path "McpServer" "OpenCodeTaskRunner"
python -m graphify explain "PolicyEngine"
```

## Regenerar

```powershell
python tools/graphify-project.py
```

Requiere `pip install graphifyy`. Para enriquecer con docs, un subagente puede escribir
`graphify-out/.graphify_chunk_*.json` (formato de `graphify/references/extraction-spec.md`) y
después se vuelve a ejecutar el script. El AST de código no necesita API key.

La guía de continuidad está en [`../docs/HANDOFF.md`](../docs/HANDOFF.md).
