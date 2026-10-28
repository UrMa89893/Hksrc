using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Primitives.MapData;
using Sledge.Common.Transport;

namespace Sledge.BspEditor.Mesh
{
    /// <summary>Persistent mesh component selection state. Indices refer to the selected MeshObject's polygon list.</summary>
    public sealed class MeshSelectionData : IMapData
    {
        public bool AffectsRendering => false;
        public MeshEditMode Mode { get; set; } = MeshEditMode.Object;
        public long MeshId { get; set; }
        public List<int> Vertices { get; } = new List<int>();
        public List<int> Edges { get; } = new List<int>();
        public List<int> Faces { get; } = new List<int>();

        public MeshSelectionData() { }
        public MeshSelectionData(SerialisedObject obj)
        {
            Mode = obj.Get<MeshEditMode>("Mode");
            MeshId = obj.Get<long>("MeshId");
            Vertices.AddRange(obj.Get<int[]>("Vertices") ?? new int[0]);
            Edges.AddRange(obj.Get<int[]>("Edges") ?? new int[0]);
            Faces.AddRange(obj.Get<int[]>("Faces") ?? new int[0]);
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context) { }
        public SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject("MeshSelectionData");
            so.Set("Mode", Mode); so.Set("MeshId", MeshId);
            so.Set("Vertices", Vertices.ToArray()); so.Set("Edges", Edges.ToArray()); so.Set("Faces", Faces.ToArray());
            return so;
        }
        public IMapElement Copy(UniqueNumberGenerator numberGenerator) => Clone();
        public IMapElement Clone()
        {
            var c = new MeshSelectionData { Mode = Mode, MeshId = MeshId };
            c.Vertices.AddRange(Vertices); c.Edges.AddRange(Edges); c.Faces.AddRange(Faces); return c;
        }
    }
}
