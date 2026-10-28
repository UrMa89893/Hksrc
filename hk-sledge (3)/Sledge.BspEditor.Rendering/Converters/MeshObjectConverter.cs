using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Rendering.Resources;
using Sledge.DataStructures.Geometric;
using Sledge.Rendering.Cameras;
using Sledge.Rendering.Pipelines;
using Sledge.Rendering.Primitives;
using Sledge.Rendering.Resources;

namespace Sledge.BspEditor.Rendering.Converters
{
    /// <summary>Renders native HSM MeshObjects directly. No BSP or Face data is required.</summary>
    [Export(typeof(IMapObjectSceneConverter))]
    public sealed class MeshObjectConverter : IMapObjectSceneConverter
    {
        public MapObjectSceneConverterPriority Priority => MapObjectSceneConverterPriority.DefaultHighest;
        public bool Supports(IMapObject obj) => obj is MeshObject;
        public bool ShouldStopProcessing(MapDocument document, IMapObject obj) => true;

        public Task Convert(BufferBuilder builder, MapDocument document, IMapObject obj, ResourceCollector resourceCollector)
        {
            var mesh = (MeshObject)obj;
            if (!mesh.Visible || mesh.Polygons.Count == 0) return Task.CompletedTask;

            var faces = mesh.Polygons.Where(p => p.Vertices.Count >= 3).ToList();
            var solidIndices = faces.Sum(p => (p.Vertices.Count - 2) * 3);
            var wireIndices = faces.Sum(p => p.Vertices.Count * 2);
            var points = new VertexStandard[faces.Sum(p => p.Vertices.Count)];
            var indices = new uint[solidIndices + wireIndices];
            var tint = (mesh.IsSelected ? Color.FromArgb(255, 150, 150) : Color.FromArgb(190, 190, 200)).ToVector4();
            var vi = 0u; var si = 0u; var wi = (uint)solidIndices;

            foreach (var face in faces)
            {
                var origin = face.Origin;
                var normal = face.Plane.Normal;
                var start = vi;
                foreach (var v in face.Vertices)
                {
                    points[vi++] = new VertexStandard
                    {
                        Position = v,
                        Normal = normal,
                        Colour = Color.White.ToVector4(),
                        Tint = tint,
                        Flags = VertexFlags.FlatColour | (mesh.IsSelected ? VertexFlags.SelectiveTransformed : VertexFlags.None)
                    };
                }
                for (uint i=2; i<(uint)face.Vertices.Count; i++)
                {
                    indices[si++] = start; indices[si++] = start+i-1; indices[si++] = start+i;
                }
                for (uint i=0; i<(uint)face.Vertices.Count; i++)
                {
                    indices[wi++] = start+i; indices[wi++] = start + (i+1 == (uint)face.Vertices.Count ? 0 : i+1);
                }
            }

            var groups = new[]
            {
                new BufferGroup(PipelineType.TexturedOpaque, CameraType.Perspective, false, Vector3.Zero, string.Empty, 0, (uint)solidIndices),
                new BufferGroup(PipelineType.Wireframe, mesh.IsSelected ? CameraType.Both : CameraType.Orthographic, (uint)solidIndices, (uint)wireIndices)
            };
            builder.Append(points, indices, groups);
            return Task.CompletedTask;
        }
    }
}
