using System.Linq;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Mesh;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations.Tree;
using Sledge.Common.Shell.Commands;

namespace Sledge.BspEditor.Commands.Mesh
{
    public abstract class MeshCommandBase : BaseCommand
    {
        protected MeshObject GetMesh(MapDocument document) => MeshContext.SelectedMesh(document);
        protected Task SelectMesh(MapDocument document, MeshObject mesh)
        {
            foreach (var old in document.Map.Root.FindAll().Where(x => x.IsSelected)) old.IsSelected = false;
            return MapDocumentOperation.Perform(document, new Attach(document.Map.Root.ID, mesh));
        }
    }
}
