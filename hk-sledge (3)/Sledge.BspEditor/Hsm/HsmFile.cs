using System.Collections.Generic;

namespace Sledge.BspEditor.Hsm
{
    /// <summary>
    /// HSM v4 - the native hk-sledge scene format.
    /// A JSON document holding the complete editable scene: a node tree of groups, brushes (with
    /// per-face materials and texture mapping), indexed meshes and entities, plus visgroups, cameras,
    /// grid and editor state. Everything the editor shows is stored, so open -> save -> open is lossless.
    /// </summary>
    public sealed class HsmFile
    {
        public const int CurrentVersion = 4;

        public string Format { get; set; } = "HSM";
        public int Version { get; set; } = CurrentVersion;
        public string Generator { get; set; } = "hk-sledge";
        public HsmMapInfo Map { get; set; } = new HsmMapInfo();
        public HsmGrid Grid { get; set; } = new HsmGrid();

        /// <summary>Every distinct material/texture name used by the scene (informational, handy for asset checks).</summary>
        public List<string> Materials { get; set; } = new List<string>();

        public List<HsmVisgroup> Visgroups { get; set; } = new List<HsmVisgroup>();
        public List<HsmCamera> Cameras { get; set; } = new List<HsmCamera>();
        public HsmCordon Cordon { get; set; }

        /// <summary>World-level entity data (worldspawn-style properties), if any.</summary>
        public HsmWorld World { get; set; }

        /// <summary>The scene tree: the children of the map root.</summary>
        public List<HsmNode> Nodes { get; set; } = new List<HsmNode>();

        public HsmEditorState Editor { get; set; } = new HsmEditorState();
    }

    public sealed class HsmMapInfo
    {
        public string Name { get; set; } = "Untitled";
        public string Author { get; set; } = "";
        public string SavedUtc { get; set; }
    }

    public sealed class HsmGrid
    {
        public bool Visible { get; set; } = true;
        public bool Snap { get; set; } = true;
        public float Step { get; set; } = 16;
        public float Low { get; set; } = -8192;
        public float High { get; set; } = 8192;
    }

    public sealed class HsmVisgroup
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public bool Visible { get; set; } = true;
        public string Color { get; set; }
    }

    public sealed class HsmCamera
    {
        public float[] Eye { get; set; }
        public float[] Look { get; set; }
        public bool Active { get; set; }
    }

    public sealed class HsmCordon
    {
        public bool Enabled { get; set; }
        public float[] Min { get; set; }
        public float[] Max { get; set; }
    }

    public sealed class HsmWorld
    {
        public string ClassName { get; set; }
        public int Flags { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }

    /// <summary>
    /// One scene node. <see cref="Type"/> is "group", "brush", "mesh" or "entity";
    /// only the fields relevant to that type are written.
    /// </summary>
    public sealed class HsmNode
    {
        public string Type { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public long[] Visgroups { get; set; }

        // entity
        public string ClassName { get; set; }
        public float[] Origin { get; set; }
        public int? Flags { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        // brush
        public List<HsmSide> Sides { get; set; }

        // mesh
        public string Asset { get; set; }
        public string Material { get; set; }
        public bool? Visible { get; set; }
        public bool? Collision { get; set; }
        public bool? CastShadows { get; set; }
        public bool? SmoothShading { get; set; }
        /// <summary>Shared vertex pool, one [x,y,z] per entry.</summary>
        public List<float[]> Vertices { get; set; }
        /// <summary>Faces as lists of indices into <see cref="Vertices"/>.</summary>
        public List<int[]> Polygons { get; set; }

        // group / brush entity
        public List<HsmNode> Children { get; set; }
    }

    /// <summary>A brush face: its corner points plus full texture mapping.</summary>
    public sealed class HsmSide
    {
        public long Id { get; set; }
        public List<float[]> Vertices { get; set; }
        public string Material { get; set; }
        public float[] UAxis { get; set; }
        public float[] VAxis { get; set; }
        public float[] Shift { get; set; }
        public float[] Scale { get; set; }
        public float Rotation { get; set; }
    }

    public sealed class HsmEditorState
    {
        public string MeshMode { get; set; } = "Object";
        public long MeshId { get; set; }
        public int[] Vertices { get; set; }
        public int[] Edges { get; set; }
        public int[] Faces { get; set; }
    }
}
