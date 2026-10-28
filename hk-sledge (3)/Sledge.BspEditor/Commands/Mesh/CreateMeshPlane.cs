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
    [CommandID("BspEditor:Mesh:CreatePlane")]
    [MenuItem("Mesh", "Primitives", "Create Plane", "B")]
    public sealed class CreateMeshPlane : MeshCommandBase
    {
        public override string Name { get; set; } = "Plane"; public override string Details { get; set; } = "Create a native editable mesh plane";
        protected override async Task Invoke(Sledge.BspEditor.Documents.MapDocument document, CommandParameters parameters)
        { var mesh=MeshPrimitives.Plane(document.Map.NumberGenerator.Next("MapObject")); await MapDocumentOperation.Perform(document,new Attach(document.Map.Root.ID,mesh)); foreach(var o in document.Map.Root.FindAll()) o.IsSelected=false; mesh.IsSelected=true; }
    }
}
