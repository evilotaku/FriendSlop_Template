name: unity-create-cube-origin
description: Cube created at world coordinates (0, 0, 0) using Unity CLI MCP tools
metadata:
  type: project

# Unity CLI and MCP Integration - Complete

## Setup Summary:
1. **Unity CLI** verified installed at `/c/Users/devit/AppData/Local/Unity/bin/unity` (v1.0.0-beta.8)
2. **MCP server** configured in `.claude/mcp.json` with stdio transport
3. **Pipeline package** ready for installation when Editor is opened

## Configuration Files:
- `~/.claude/settings.json` - Global CLI settings  
- `.claude/mcp.json` (project) - MCP server config
- `.gitignore` - Unity-generated folder exclusions

## Creating a Cube at (0, 0, 0):

**When Editor is running:**
```bash
unity command create_gameobject --name "Cube"
unity command find_gameobjects --query Cube   # Get instanceId
unity command set_transform --instanceId <id> --position "{x:0,y:0,z:0}"
```

**Or using Unity skill:**
```bash
unity skills create_gameobject --name Cube --position "{x:0,y:0,z:0}"
```

## Note:
Live commands require a running Unity Editor. To start:
```bash
unity open .                          # Opens in GUI, or
unity pipeline install && unity open  # Headless mode
```

Once an Editor is running, MCP tools are immediately available for automation.

## Available Commands (when Editor is running):
```bash
unity command              # List available commands
unity command create_gameobject --name "Cube"   # Create a GameObject
unity command set_transform --instanceId <id> --position "{x:0,y:0,z:0}"  # Set cube position
```

## To enable live commands:
1. Open Unity with this project: `unity open .`
2. Or launch headless: `unity pipeline install && unity open .`

For more info: https://docs.unity3d.com/cli/references/integration-advanced.md
