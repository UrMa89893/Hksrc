# hk-sledge HSM format (v4)

`.hsm` is the native Hacksaw Scene Map format: a single human-readable JSON file that stores the **complete editable scene**.
Opening and re-saving a map is lossless. Older v1-v3 files still open and are upgraded to v4 on the next save.

## Top level

| Field | Meaning |
|---|---|
| `format` | always `"HSM"` |
| `version` | `4` |
| `generator` | tool that wrote the file |
| `map` | `name`, `author`, `savedUtc` |
| `grid` | `visible`, `snap`, `step`, `low`, `high` |
| `materials` | every distinct material/texture name used (for asset checks) |
| `visgroups` | `id`, `name`, `visible`, `color` |
| `cameras` | `eye`, `look`, `active` |
| `cordon` | `enabled`, `min`, `max` |
| `world` | world-level entity data: `className`, `flags`, `properties` |
| `nodes` | the scene tree (children of the map root) |
| `editor` | mesh edit mode and component selection |

## Nodes

Every node has `type`, `id`, optional `color` (`#RRGGBB`) and `visgroups` (ids). Types:

- `group` - `children`
- `brush` - `sides`: one per face with `id`, `vertices` (corner points), `material`, `uAxis`, `vAxis`, `shift`, `scale`, `rotation`
- `mesh` - `name`, `asset`, `material`, `visible`, `collision`, `castShadows`, `smoothShading`, a shared `vertices` pool and `polygons` (index lists into it)
- `entity` - `className`, `origin`, `flags`, `properties`, and `children` for brush entities

```json
{
  "format": "HSM",
  "version": 4,
  "map": { "name": "test_room" },
  "materials": ["concrete"],
  "nodes": [
    {
      "type": "brush",
      "id": 2,
      "sides": [
        { "id": 1, "vertices": [[-64,-64,64],[64,-64,64],[64,64,64],[-64,64,64]],
          "material": "concrete", "uAxis": [1,0,0], "vAxis": [0,-1,0],
          "shift": [0, 0], "scale": [0.25, 0.25], "rotation": 0 }
      ]
    },
    { "type": "entity", "id": 3, "className": "info_player_start", "origin": [0,0,32], "properties": { "angles": "0 90 0" } }
  ]
}
```

IDs are preserved on load and the editor's ID counters are advanced past them, so new objects never collide.
