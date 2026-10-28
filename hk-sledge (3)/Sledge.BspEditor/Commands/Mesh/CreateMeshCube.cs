using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Sledge.BspEditor.Mesh;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations.Tree;
using Sledge.Common.Shell.Commands;
using Sledge.Common.Shell.Menu;

namespace Sledge.BspEditor.Commands.Mesh
{
    [Export(typeof(ICommand))]
    [CommandID("BspEditor:Mesh:CreateCube")]
    [MenuItem("Mesh", "Primitives", "Create Cube", "A")]
    public sealed class CreateMeshCube : MeshCommandBase
    {
        public override string Name { get; set; } = "Cube";
        public override string Details { get; set; } = "Create a native editable mesh cube";
        protected override async Task Invoke(Sledge.BspEditor.Documents.MapDocument document, CommandParameters parameters)
        {
            var mesh = MeshPrimitives.Cube(document.Map.NumberGenerator.Next("MapObject"));
            await MapDocumentOperation.Perform(document, new Attach(document.Map.Root.ID, mesh));
            foreach (var o in document.Map.Root.FindAll()) o.IsSelected = false;
            mesh.IsSelected = true;
        }
    }
}
