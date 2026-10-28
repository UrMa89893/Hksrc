using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjects;

namespace Sledge.BspEditor.Modification.Operations.Mesh
{
    /// <summary>Changes the material asset reference on one or more native mesh objects.</summary>
    public sealed class SetMeshMaterial : IOperation
    {
        private readonly Dictionary<long, string> _before;
        private readonly string _after;

        public bool Trivial => false;

        public SetMeshMaterial(string material, IEnumerable<MeshObject> meshes)
        {
            _after = material ?? "";
            _before = meshes.ToDictionary(x => x.ID, x => x.Material ?? "");
        }

        public Task<Change> Perform(MapDocument document)
        {
            var change = new Change(document);
            foreach (var pair in _before)
            {
                var mesh = document.Map.Root.FindByID(pair.Key) as MeshObject;
                if (mesh == null) continue;
                mesh.Material = _after;
                mesh.Invalidate();
                change.UpdateRange(mesh.FindAll());
            }
            return Task.FromResult(change);
        }

        public Task<Change> Reverse(MapDocument document)
        {
            var change = new Change(document);
            foreach (var pair in _before)
            {
                var mesh = document.Map.Root.FindByID(pair.Key) as MeshObject;
                if (mesh == null) continue;
                mesh.Material = pair.Value;
                mesh.Invalidate();
                change.UpdateRange(mesh.FindAll());
            }
            return Task.FromResult(change);
        }
    }
}
