using System.Linq;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Primitives.MapObjects;

namespace Sledge.BspEditor.Mesh
{
    public static class MeshContext
    {
        public static MeshObject SelectedMesh(MapDocument document)
        {
            return document?.Map?.Root?.FindAll().OfType<MeshObject>().FirstOrDefault(x => x.IsSelected);
        }

        public static MeshSelectionData Selection(MapDocument document)
        {
            var data = document.Map.Data.Get<MeshSelectionData>().FirstOrDefault();
            if (data != null) return data;
            data = new MeshSelectionData();
            document.Map.Data.Add(data);
            return data;
        }
    }
}
