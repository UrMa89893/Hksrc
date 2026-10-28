using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Sledge.BspEditor.Hsm
{
    /// <summary>Reads HSM v1-v3 files (flat mesh/entity lists) and upgrades them to the v4 model.</summary>
    internal static class HsmLegacy
    {
        public static HsmFile Convert(JObject root)
        {
            var file = new HsmFile();

            var map = Obj(root, "map");
            if (map != null)
            {
                file.Map.Name = Str(map, "name") ?? "Untitled";
                file.Map.Author = Str(map, "author") ?? "";
            }

            var grid = Obj(root, "grid");
            if (grid != null)
            {
                file.Grid.Visible = Bool(grid, "visible", true);
                file.Grid.Snap = Bool(grid, "snap", true);
                file.Grid.Step = Flt(grid, "step", 16);
                file.Grid.Low = Flt(grid, "low", -8192);
                file.Grid.High = Flt(grid, "high", 8192);
            }

            foreach (var m in Arr(root, "meshes"))
            {
                var node = new HsmNode
                {
                    Type = "mesh",
                    Id = Lng(m, "id"),
                    Name = Str(m, "name") ?? "Mesh",
                    Asset = Str(m, "asset") ?? "",
                    Material = Str(m, "material") ?? "",
                    Visible = Bool(m, "visible", true),
                    Collision = Bool(m, "collision", true),
                    CastShadows = Bool(m, "castShadows", true),
                    SmoothShading = Bool(m, "smoothShading", false),
                    Vertices = new List<float[]>(),
                    Polygons = new List<int[]>()
                };
                var pool = new Dictionary<string, int>();
                foreach (var face in ((Tok(m, "faces") as JArray) ?? new JArray()).OfType<JArray>())
                {
                    var idx = new List<int>();
                    foreach (var v in face.OfType<JArray>().Where(a => a.Count >= 3))
                    {
                        var p = new[] { (float) v[0], (float) v[1], (float) v[2] };
                        var key = string.Join(",", p);
                        if (!pool.TryGetValue(key, out var i)) { i = node.Vertices.Count; pool[key] = i; node.Vertices.Add(p); }
                        idx.Add(i);
                    }
                    if (idx.Count >= 3) node.Polygons.Add(idx.ToArray());
                }
                file.Nodes.Add(node);
            }

            foreach (var e in Arr(root, "entities"))
            {
                var node = new HsmNode
                {
                    Type = "entity",
                    Id = Lng(e, "id"),
                    ClassName = Str(e, "className") ?? "entity",
                    Origin = Floats(e, "position"),
                    Properties = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                };
                var props = Obj(e, "properties");
                if (props != null) foreach (var p in props.Properties()) node.Properties[p.Name] = (string) p.Value;
                var rot = Floats(e, "rotation");
                if (rot != null && rot.Any(x => x != 0) && !node.Properties.ContainsKey("angles"))
                    node.Properties["angles"] = string.Join(" ", rot.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                file.Nodes.Add(node);
            }

            var ed = Obj(root, "editor");
            if (ed != null)
            {
                file.Editor.MeshMode = Str(ed, "meshMode") ?? "Object";
                file.Editor.MeshId = Lng(ed, "meshId");
                var af = (int?) Tok(ed, "activeFace");
                if (af.HasValue && af.Value >= 0) file.Editor.Faces = new[] { af.Value };
            }

            return file;
        }

        private static JToken Tok(JObject o, string name) => o.GetValue(name, StringComparison.OrdinalIgnoreCase);
        private static JObject Obj(JObject o, string name) => Tok(o, name) as JObject;
        private static IEnumerable<JObject> Arr(JObject o, string name) => (Tok(o, name) as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>();
        private static string Str(JObject o, string name) => (string) Tok(o, name);
        private static bool Bool(JObject o, string name, bool d) => (bool?) Tok(o, name) ?? d;
        private static float Flt(JObject o, string name, float d) => (float?) Tok(o, name) ?? d;
        private static long Lng(JObject o, string name) => (long?) Tok(o, name) ?? 0;
        private static float[] Floats(JObject o, string name) => (Tok(o, name) as JArray)?.Select(x => (float) x).ToArray();
    }
}
