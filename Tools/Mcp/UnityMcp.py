"""Call the installed Coplay Unity MCP server without changing Codex configuration."""
import asyncio
import json
import sys
import logging
from pathlib import Path
from fastmcp import Client

def compact(value):
    if isinstance(value, dict):
        return {k: compact(v) for k, v in value.items() if k not in ("imageBase64", "image_base64")}
    if isinstance(value, list):
        return [compact(v) for v in value]
    return value

async def main():
    async with Client("http://127.0.0.1:8765/mcp", timeout=180) as client:
        for group in ("probuilder", "scripting_ext"):
            await client.call_tool("manage_tools", {"action": "activate", "group": group})
        if sys.argv[1] == "--list":
            tools = await client.list_tools()
            names = sys.argv[2:]
            print(json.dumps([t.model_dump() for t in tools if not names or t.name in names], ensure_ascii=False))
        else:
            params = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8-sig")) if len(sys.argv) > 2 else {}
            result = await client.call_tool(sys.argv[1], params)
            print(json.dumps(compact(result.data if result.data is not None else [c.model_dump() for c in result.content]), ensure_ascii=False, default=str))

if __name__ == "__main__":
    logging.disable(logging.INFO)
    sys.stdout.reconfigure(encoding="utf-8")
    asyncio.run(main())
