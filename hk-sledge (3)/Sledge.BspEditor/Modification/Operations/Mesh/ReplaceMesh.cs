using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Modification.Operations.Mesh
{
    public sealed class ReplaceMesh : IOperation
    {
        private readonly long _id;
        private readonly List<List<Vector3>> _before;
        private readonly List<List<Vector3>> _after;
        public bool Trivial => false;
        public ReplaceMesh(MeshObject mesh, IEnumerable<Polygon> after)
        {
            _id = mesh.ID;
            _before = mesh.Polygons.Select(p => p.Vertices.ToList()).ToList();
            _after = after.Select(p => p.Vertices.ToList()).ToList();
        }
        public Task<Change> Perform(MapDocument document) => Apply(document, _after);
        public Task<Change> Reverse(MapDocument document) => Apply(document, _before);
        private Task<Change> Apply(MapDocument document, List<List<Vector3>> data)
        {
            var ch = new Change(document);
            var mesh = document.Map.Root.FindByID(_id) as MeshObject;
            if (mesh == null) return Task.FromResult(ch);
            mesh.Polygons.Clear();
            mesh.Polygons.AddRange(data.Select(v => new Polygon(v)));
            mesh.Invalidate();
            ch.Update(mesh);
            return Task.FromResult(ch);
        }
    }
}
