using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Sledge.BspEditor.Grid;
using Sledge.BspEditor.Mesh;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapData;
using Sledge.BspEditor.Primitives.MapObjectData;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Hsm
{
    /// <summary>
    /// Reads and writes the native HSM scene format (see <see cref="HsmFile"/>).
    /// </summary>
    public static class HsmSerializer
    {
        private static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = { new InlineArrayConverter() }
            };
        }

        // ---------------------------------------------------------------- Save

        public static void Save(string path, Map map, string name = null)
        {
            var file = FromMap(map, name);
            var json = JsonConvert.SerializeObject(file, CreateSettings());

            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Write to a temp file first so a failed save can never destroy the existing map
            var temp = path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static HsmFile FromMap(Map map, string name = null)
        {
            var file = new HsmFile();
            file.Map.Name = name ?? "Untitled";
            file.Map.SavedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            // Grid
            var gridData = map.Data.GetOne<GridData>();
            var square = gridData?.Grid as SquareGrid;
            if (square != null)
            {
                file.Grid.Visible = true;
                file.Grid.Snap = gridData.SnapToGrid;
                file.Grid.Step = square.Step;
                file.Grid.Low = square.Low;
                file.Grid.High = square.High;
            }
            else
            {
                file.Grid.Visible = gridData?.Grid != null;
                file.Grid.Snap = gridData?.SnapToGrid != false;
            }

            // Visgroups
            foreach (var vg in map.Data.OfType<Visgroup>())
            {
                file.Visgroups.Add(new HsmVisgroup { Id = vg.ID, Name = vg.Name, Visible = vg.Visible, Color = ToHex(vg.Colour) });
            }

            // Cameras
            foreach (var cam in map.Data.OfType<Camera>())
            {
                file.Cameras.Add(new HsmCamera { Eye = ToArray(cam.EyePosition), Look = ToArray(cam.LookPosition), Active = cam.IsActive });
            }

            // Cordon
            var cordon = map.Data.GetOne<CordonBounds>();
            if (cordon != null)
            {
                file.Cordon = new HsmCordon { Enabled = cordon.Enabled, Min = ToArray(cordon.Box.Start), Max = ToArray(cordon.Box.End) };
            }

            // World properties
            var world = map.Root.Data.GetOne<EntityData>();
            if (world != null)
            {
                file.World = new HsmWorld { ClassName = world.Name, Flags = world.Flags, Properties = new Dictionary<string, string>(world.Properties) };
            }

            // Scene tree
            var materials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in map.Root.Hierarchy)
            {
                var node = ToNode(child, materials);
                if (node != null) file.Nodes.Add(node);
            }
            file.Materials = materials.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            // Editor state
            var selection = map.Data.Get<MeshSelectionData>().FirstOrDefault();
            if (selection != null)
            {
                file.Editor.MeshMode = selection.Mode.ToString();
                file.Editor.MeshId = selection.MeshId;
                if (selection.Vertices.Any()) file.Editor.Vertices = selection.Vertices.ToArray();
                if (selection.Edges.Any()) file.Editor.Edges = selection.Edges.ToArray();
                if (selection.Faces.Any()) file.Editor.Faces = selection.Faces.ToArray();
            }

            return file;
        }

        private static HsmNode ToNode(IMapObject obj, ISet<string> materials)
        {
            HsmNode node;

            if (obj is Solid solid)
            {
                node = new HsmNode { Type = "brush", Sides = new List<HsmSide>() };
                foreach (var face in solid.Faces)
                {
                    var tex = face.Texture ?? new Texture();
                    if (!string.IsNullOrEmpty(tex.Name)) materials.Add(tex.Name);
                    node.Sides.Add(new HsmSide
                    {
                        Id = face.ID,
                        Vertices = face.Vertices.Select(ToArray).ToList(),
                        Material = tex.Name ?? "",
                        UAxis = ToArray(tex.UAxis),
                        VAxis = ToArray(tex.VAxis),
                        Shift = new[] { tex.XShift, tex.YShift },
                        Scale = new[] { tex.XScale, tex.YScale },
                        Rotation = tex.Rotation
                    });
                }
            }
            else if (obj is MeshObject mesh)
            {
                if (!string.IsNullOrEmpty(mesh.Material)) materials.Add(mesh.Material);
                node = new HsmNode
                {
                    Type = "mesh",
                    Name = mesh.Name,
                    Asset = mesh.MeshPath,
                    Material = mesh.Material,
                    Visible = mesh.Visible,
                    Collision = mesh.Collision,
                    CastShadows = mesh.CastShadows,
                    SmoothShading = mesh.SmoothShading,
                    Vertices = new List<float[]>(),
                    Polygons = new List<int[]>()
                };
                var pool = new Dictionary<Vector3, int>();
                foreach (var poly in mesh.Polygons)
                {
                    var indices = new List<int>();
                    foreach (var v in poly.Vertices)
                    {
                        if (!pool.TryGetValue(v, out var index))
                        {
                            index = node.Vertices.Count;
                            pool[v] = index;
                            node.Vertices.Add(ToArray(v));
                        }
                        indices.Add(index);
                    }
                    node.Polygons.Add(indices.ToArray());
                }
            }
            else if (obj is Entity entity)
            {
                var data = entity.EntityData;
                node = new HsmNode
                {
                    Type = "entity",
                    ClassName = data?.Name ?? "entity",
                    Flags = data != null && data.Flags != 0 ? data.Flags : (int?) null,
                    Properties = data == null ? new Dictionary<string, string>() : new Dictionary<string, string>(data.Properties)
                };
                var origin = entity.Data.GetOne<Origin>();
                if (origin != null) node.Origin = ToArray(origin.Location);
            }
            else if (obj is Group)
            {
                node = new HsmNode { Type = "group" };
            }
            else
            {
                return null;
            }

            node.Id = obj.ID;

            var color = obj.Data.GetOne<ObjectColor>();
            if (color != null) node.Color = ToHex(color.Color);

            var visgroups = obj.Data.Get<VisgroupID>().Select(x => x.ID).Distinct().ToArray();
            if (visgroups.Length > 0) node.Visgroups = visgroups;

            if ((obj is Group || obj is Entity) && obj.Hierarchy.HasChildren)
            {
                var children = obj.Hierarchy.Select(c => ToNode(c, materials)).Where(c => c != null).ToList();
                if (children.Count > 0) node.Children = children;
            }

            return node;
        }

        // ---------------------------------------------------------------- Load

        public static Map Load(string path)
        {
            var text = File.ReadAllText(path);
            JObject root;
            try
            {
                root = JObject.Parse(text);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("The HSM file could not be parsed: " + ex.Message, ex);
            }

            var format = (string) root.GetValue("format", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(format, "HSM", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The file is not a valid HSM map.");

            var version = (int?) root.GetValue("version", StringComparison.OrdinalIgnoreCase) ?? 1;
            if (version < 1 || version > HsmFile.CurrentVersion)
                throw new InvalidDataException("Unsupported HSM version: " + version);

            HsmFile file;
            if (version < 4) file = HsmLegacy.Convert(root);
            else file = root.ToObject<HsmFile>(JsonSerializer.Create(CreateSettings()));

            return ToMap(file);
        }

        private class BuildContext
        {
            public Map Map;
            public HashSet<long> Objects = new HashSet<long>();
            public HashSet<long> Faces = new HashSet<long>();
        }

        public static Map ToMap(HsmFile file)
        {
            var map = new Map();
            var ctx = new BuildContext { Map = map };
            ctx.Objects.Add(map.Root.ID);

            // Grid
            var grid = file.Grid ?? new HsmGrid();
            if (grid.Visible)
                map.Data.Add(new GridData(new SquareGrid(grid.High, grid.Low, Math.Max(0.25f, grid.Step))) { SnapToGrid = grid.Snap });
            else
                map.Data.Add(new GridData(new NoGrid()) { SnapToGrid = grid.Snap });

            // Visgroups
            long maxVisgroup = 0;
            foreach (var vg in file.Visgroups ?? new List<HsmVisgroup>())
            {
                map.Data.Add(new Visgroup { ID = vg.Id, Name = vg.Name ?? "", Visible = vg.Visible, Colour = FromHex(vg.Color, Color.Gray) });
                maxVisgroup = Math.Max(maxVisgroup, vg.Id);
            }
            map.NumberGenerator.Seed("Visgroup", maxVisgroup);

            // Cameras
            foreach (var cam in file.Cameras ?? new List<HsmCamera>())
            {
                map.Data.Add(new Camera { EyePosition = FromArray(cam.Eye), LookPosition = FromArray(cam.Look), IsActive = cam.Active });
            }

            // Cordon
            if (file.Cordon != null)
            {
                map.Data.Add(new CordonBounds { Enabled = file.Cordon.Enabled, Box = new Box(FromArray(file.Cordon.Min), FromArray(file.Cordon.Max)) });
            }

            // World
            if (file.World != null)
            {
                map.Root.Data.Replace(new EntityData
                {
                    Name = file.World.ClassName ?? "worldspawn",
                    Flags = file.World.Flags,
                    Properties = new Dictionary<string, string>(file.World.Properties ?? new Dictionary<string, string>(), StringComparer.InvariantCultureIgnoreCase)
                });
            }

            // Make sure new objects can never reuse a loaded ID
            var nodes = file.Nodes ?? new List<HsmNode>();
            map.NumberGenerator.Seed("MapObject", MaxId(nodes, n => n.Id));
            map.NumberGenerator.Seed("Face", nodes.SelectMany(Flatten).Where(n => n.Sides != null).SelectMany(n => n.Sides).Select(s => s.Id).DefaultIfEmpty(0).Max());

            // Scene tree
            foreach (var node in nodes)
            {
                var obj = Build(node, ctx);
                if (obj != null) obj.Hierarchy.Parent = map.Root;
            }

            // Editor state
            if (file.Editor != null)
            {
                var selection = new MeshSelectionData { MeshId = file.Editor.MeshId, Mode = ParseMode(file.Editor.MeshMode) };
                if (file.Editor.Vertices != null) selection.Vertices.AddRange(file.Editor.Vertices);
                if (file.Editor.Edges != null) selection.Edges.AddRange(file.Editor.Edges);
                if (file.Editor.Faces != null) selection.Faces.AddRange(file.Editor.Faces);
                map.Data.Add(selection);
            }

            // Rebuild visgroup membership and visibility (normally done by the BSP load processors)
            var visgroupLookup = map.Data.OfType<Visgroup>().ToDictionary(x => x.ID, x => x);
            foreach (var obj in map.Root.FindAll())
            {
                var visible = true;
                foreach (var vid in obj.Data.Get<VisgroupID>().ToList())
                {
                    if (!visgroupLookup.TryGetValue(vid.ID, out var vis)) continue;
                    vis.Objects.Add(obj);
                    visible = vis.Visible;
                }
                obj.Data.Replace(new VisgroupHidden(!visible));
            }

            map.Root.DescendantsChanged();
            map.Root.Invalidate();
            return map;
        }

        private static IMapObject Build(HsmNode node, BuildContext ctx)
        {
            if (node == null) return null;

            var id = Claim(node.Id, ctx.Objects, ctx.Map.NumberGenerator, "MapObject");
            IMapObject obj;

            switch ((node.Type ?? "").ToLowerInvariant())
            {
                case "group":
                    obj = new Group(id);
                    break;

                case "brush":
                {
                    var solid = new Solid(id);
                    foreach (var side in node.Sides ?? new List<HsmSide>())
                    {
                        var verts = (side.Vertices ?? new List<float[]>()).Where(v => v != null && v.Length >= 3).Select(FromArray).ToList();
                        if (verts.Count < 3) continue;

                        var tex = new Texture { Name = side.Material ?? "" };
                        if (side.UAxis != null && side.UAxis.Length >= 3) tex.UAxis = FromArray(side.UAxis);
                        if (side.VAxis != null && side.VAxis.Length >= 3) tex.VAxis = FromArray(side.VAxis);
                        if (side.Shift != null && side.Shift.Length >= 2) { tex.XShift = side.Shift[0]; tex.YShift = side.Shift[1]; }
                        if (side.Scale != null && side.Scale.Length >= 2) { tex.XScale = side.Scale[0]; tex.YScale = side.Scale[1]; }
                        tex.Rotation = side.Rotation;

                        var face = new Face(Claim(side.Id, ctx.Faces, ctx.Map.NumberGenerator, "Face")) { Texture = tex };
                        face.Vertices.AddRange(verts);
                        solid.Data.Add(face);
                    }
                    solid.DescendantsChanged();
                    obj = solid;
                    break;
                }

                case "mesh":
                {
                    var mesh = new MeshObject(id)
                    {
                        Name = node.Name ?? "Mesh",
                        MeshPath = node.Asset ?? "",
                        Material = node.Material ?? "",
                        Visible = node.Visible ?? true,
                        Collision = node.Collision ?? true,
                        CastShadows = node.CastShadows ?? true,
                        SmoothShading = node.SmoothShading ?? false
                    };
                    var pool = (node.Vertices ?? new List<float[]>()).Select(FromArray).ToList();
                    foreach (var poly in node.Polygons ?? new List<int[]>())
                    {
                        var verts = (poly ?? new int[0]).Where(i => i >= 0 && i < pool.Count).Select(i => pool[i]).ToList();
                        if (verts.Count >= 3) mesh.Polygons.Add(new Polygon(verts));
                    }
                    mesh.Invalidate();
                    obj = mesh;
                    break;
                }

                case "entity":
                {
                    var entity = new Entity(id);
                    entity.Data.Add(new EntityData
                    {
                        Name = node.ClassName ?? "entity",
                        Flags = node.Flags ?? 0,
                        Properties = new Dictionary<string, string>(node.Properties ?? new Dictionary<string, string>(), StringComparer.InvariantCultureIgnoreCase)
                    });
                    if (node.Origin != null) entity.Origin = FromArray(node.Origin);
                    obj = entity;
                    break;
                }

                default:
                    return null;
            }

            if (!string.IsNullOrEmpty(node.Color)) obj.Data.Replace(new ObjectColor(FromHex(node.Color, Color.Gray)));
            if (node.Visgroups != null) foreach (var vg in node.Visgroups.Distinct()) obj.Data.Add(new VisgroupID(vg));

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var c = Build(child, ctx);
                    if (c != null) c.Hierarchy.Parent = obj;
                }
            }

            return obj;
        }

        // ---------------------------------------------------------------- Helpers

        private static long Claim(long wanted, ISet<long> used, UniqueNumberGenerator generator, string type)
        {
            if (wanted > 0 && used.Add(wanted)) return wanted;
            long next;
            do { next = generator.Next(type); } while (!used.Add(next));
            return next;
        }

        private static IEnumerable<HsmNode> Flatten(HsmNode node)
        {
            yield return node;
            if (node.Children == null) yield break;
            foreach (var c in node.Children.SelectMany(Flatten)) yield return c;
        }

        private static long MaxId(IEnumerable<HsmNode> nodes, Func<HsmNode, long> selector)
        {
            return nodes.SelectMany(Flatten).Select(selector).DefaultIfEmpty(0).Max();
        }

        private static MeshEditMode ParseMode(string value)
        {
            return Enum.TryParse(value, true, out MeshEditMode mode) ? mode : MeshEditMode.Object;
        }

        private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static Color FromHex(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            hex = hex.TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return fallback;
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        private static float[] ToArray(Vector3 v) => new[] { v.X, v.Y, v.Z };
        private static Vector3 FromArray(float[] v) => v != null && v.Length >= 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.Zero;

        /// <summary>Writes number arrays on a single line so the JSON stays readable and diff-friendly.</summary>
        private class InlineArrayConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type t) => t == typeof(float[]) || t == typeof(int[]) || t == typeof(long[]);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                IEnumerable<string> items;
                if (value is float[] f) items = f.Select(x => float.IsNaN(x) || float.IsInfinity(x) ? "0" : x.ToString("R", CultureInfo.InvariantCulture));
                else if (value is int[] i) items = i.Select(x => x.ToString(CultureInfo.InvariantCulture));
                else items = ((long[]) value).Select(x => x.ToString(CultureInfo.InvariantCulture));
                writer.WriteRawValue("[" + string.Join(", ", items) + "]");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}
