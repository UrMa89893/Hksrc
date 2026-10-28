using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Mesh;
using Sledge.Common.Shell.Commands;
using Sledge.Common.Shell.Hotkeys;
using Sledge.Common.Shell.Menu;
namespace Sledge.BspEditor.Commands.Mesh
{
    public abstract class MeshModeCommand : BaseCommand
    {
        protected readonly MeshEditMode Mode;
        protected MeshModeCommand(MeshEditMode mode) { Mode=mode; }
        protected override Task Invoke(MapDocument document, CommandParameters parameters) { MeshContext.Selection(document).Mode=Mode; return Task.CompletedTask; }
    }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:ObjectMode")][DefaultHotkey("1")][MenuItem("Mesh","Mode","Object","A")]
    public sealed class MeshObjectMode : MeshModeCommand { public MeshObjectMode():base(MeshEditMode.Object){} public override string Name{get;set;}="Object Mode"; public override string Details{get;set;}="Select and transform complete mesh objects"; }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:VertexMode")][DefaultHotkey("2")][MenuItem("Mesh","Mode","Vertex","B")]
    public sealed class MeshVertexMode : MeshModeCommand { public MeshVertexMode():base(MeshEditMode.Vertex){} public override string Name{get;set;}="Vertex Mode"; public override string Details{get;set;}="Edit mesh vertices"; }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:EdgeMode")][DefaultHotkey("3")][MenuItem("Mesh","Mode","Edge","C")]
    public sealed class MeshEdgeMode : MeshModeCommand { public MeshEdgeMode():base(MeshEditMode.Edge){} public override string Name{get;set;}="Edge Mode"; public override string Details{get;set;}="Edit mesh edges"; }
    [Export(typeof(ICommand))][CommandID("BspEditor:Mesh:FaceMode")][DefaultHotkey("4")][MenuItem("Mesh","Mode","Face","D")]
    public sealed class MeshFaceMode : MeshModeCommand { public MeshFaceMode():base(MeshEditMode.Face){} public override string Name{get;set;}="Face Mode"; public override string Details{get;set;}="Edit mesh faces"; }
}
