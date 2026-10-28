using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Editing;
using Sledge.BspEditor.Mesh;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations.Mesh;
using Sledge.Common.Shell.Commands;
using Sledge.Common.Shell.Hotkeys;
using Sledge.Common.Shell.Menu;
namespace Sledge.BspEditor.Commands.Mesh
{
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:Extrude")][DefaultHotkey("E")][MenuItem("Mesh","Modeling","Extrude","A")]
    public sealed class MeshExtrude : MeshCommandBase
    {
        public override string Name{get;set;}="Extrude"; public override string Details{get;set;}="Extrude the active mesh face";
        protected override async Task Invoke(MapDocument document, CommandParameters parameters)
        { var m=GetMesh(document); if(m==null||m.Polygons.Count==0)return; var copy=m.Polygons.ToList(); MeshEditing.ExtrudeFace(m,m.Polygons[0],16); var after=m.Polygons.ToList(); m.Polygons.Clear();m.Polygons.AddRange(copy);m.Invalidate(); await MapDocumentOperation.Perform(document,new ReplaceMesh(m,after)); }
    }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:Inset")][MenuItem("Mesh","Modeling","Inset","B")]
    public sealed class MeshInset : MeshCommandBase
    { public override string Name{get;set;}="Inset"; public override string Details{get;set;}="Inset the active face"; protected override async Task Invoke(MapDocument document,CommandParameters parameters){var m=GetMesh(document);if(m==null||m.Polygons.Count==0)return;var before=m.Polygons.Select(p=>new Sledge.DataStructures.Geometric.Polygon(p.Vertices)).ToList();MeshModeling.InsetFace(m,0,0.15f);var after=m.Polygons.ToList();m.Polygons.Clear();m.Polygons.AddRange(before);m.Invalidate();await MapDocumentOperation.Perform(document,new ReplaceMesh(m,after));} }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:Triangulate")][MenuItem("Mesh","Modeling","Triangulate","C")]
    public sealed class MeshTriangulate : MeshCommandBase
    { public override string Name{get;set;}="Triangulate"; public override string Details{get;set;}="Triangulate the active face"; protected override async Task Invoke(MapDocument document,CommandParameters parameters){var m=GetMesh(document);if(m==null||m.Polygons.Count==0)return;var before=m.Polygons.Select(p=>new Sledge.DataStructures.Geometric.Polygon(p.Vertices)).ToList();MeshModeling.TriangulateFace(m,0);var after=m.Polygons.ToList();m.Polygons.Clear();m.Polygons.AddRange(before);m.Invalidate();await MapDocumentOperation.Perform(document,new ReplaceMesh(m,after));} }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:Flip")][MenuItem("Mesh","Modeling","Flip Normals","D")]
    public sealed class MeshFlip : MeshCommandBase
    { public override string Name{get;set;}="Flip Normals"; public override string Details{get;set;}="Reverse the active face winding"; protected override async Task Invoke(MapDocument document,CommandParameters parameters){var m=GetMesh(document);if(m==null||m.Polygons.Count==0)return;var after=m.Polygons.ToList();after[0]=after[0].Flip();await MapDocumentOperation.Perform(document,new ReplaceMesh(m,after));} }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:Weld")][MenuItem("Mesh","Modeling","Weld Vertices","E")]
    public sealed class MeshWeld : MeshCommandBase
    { public override string Name{get;set;}="Weld Vertices"; public override string Details{get;set;}="Weld nearby mesh vertices"; protected override async Task Invoke(MapDocument document,CommandParameters parameters){var m=GetMesh(document);if(m==null)return;var before=m.Polygons.Select(p=>new Sledge.DataStructures.Geometric.Polygon(p.Vertices)).ToList();MeshModeling.Weld(m);var after=m.Polygons.ToList();m.Polygons.Clear();m.Polygons.AddRange(before);m.Invalidate();await MapDocumentOperation.Perform(document,new ReplaceMesh(m,after));} }
}
