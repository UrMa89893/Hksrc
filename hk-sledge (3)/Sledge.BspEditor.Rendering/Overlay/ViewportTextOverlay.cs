using System.ComponentModel.Composition;
using System.Drawing;
using System.Numerics;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Mesh;
using Sledge.BspEditor.Primitives.MapObjects;
using System.Linq;
using Sledge.Rendering.Cameras;
using Sledge.Rendering.Overlay;
using Sledge.Rendering.Viewports;

namespace Sledge.BspEditor.Rendering.Overlay
{
    [Export(typeof(IMapDocumentOverlayRenderable))]
    public class ViewportTextOverlay : IMapDocumentOverlayRenderable
    {
        private MapDocument _document;
        public void SetActiveDocument(MapDocument doc)
        {
            _document = doc;
        }

        public void Render(IViewport viewport, OrthographicCamera camera, Vector3 worldMin, Vector3 worldMax, I2DRenderer im)
        {
            var mode = _document == null ? "" : $"  |  MESH {MeshContext.Selection(_document).Mode.ToString().ToUpperInvariant()}";
            var str = $"2D {camera.ViewType}{mode}";
            var size = im.CalcTextSize(FontType.Normal, str);
            im.AddText(new Vector2(2, 2), Color.White, FontType.Normal, str);
            im.AddRectFilled(Vector2.Zero, size + new Vector2(4, 4), Color.FromArgb(180, Color.FromArgb(32, 32, 36)));
        }

        public void Render(IViewport viewport, PerspectiveCamera camera, I2DRenderer im)
        {
            var mode = _document == null ? "" : $"  |  MESH {MeshContext.Selection(_document).Mode.ToString().ToUpperInvariant()}";
            var str = $"3D View{mode}";
            var size = im.CalcTextSize(FontType.Normal, str);
            im.AddText(new Vector2(2, 2), Color.White, FontType.Normal, str);
            im.AddRectFilled(Vector2.Zero, size + new Vector2(4, 4), Color.FromArgb(180, Color.FromArgb(32, 32, 36)));
        }
    }
}
