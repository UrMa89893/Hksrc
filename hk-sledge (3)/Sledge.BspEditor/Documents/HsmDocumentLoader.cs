using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Sledge.BspEditor.Environment.Empty;
using Sledge.BspEditor.Hsm;
using Sledge.BspEditor.Grid;
using Sledge.BspEditor.Primitives.MapData;
using Sledge.Common.Shell.Documents;

namespace Sledge.BspEditor.Documents
{
    /// <summary>
    /// Native Hacksaw Scene Map loader. HSM is JSON-backed and mesh-first; it does not use BSP.
    /// </summary>
    [Export(typeof(IDocumentLoader))]
    public class HsmDocumentLoader : IDocumentLoader
    {
        public string FileTypeDescription => "Hacksaw Scene Map (*.hsm)";
        public IEnumerable<FileExtensionInfo> SupportedFileExtensions { get; } = new[] { new FileExtensionInfo("Hacksaw Scene Map", ".hsm") };
        public bool CanLoad(string location) => location == null || location.EndsWith(".hsm", System.StringComparison.InvariantCultureIgnoreCase);

        public Task<IDocument> CreateBlank()
        {
            var blankMap = new Sledge.BspEditor.Primitives.Map();
            blankMap.Data.Add(new GridData(new SquareGrid(8192, -8192, 16)) { SnapToGrid = true });
            var doc = new MapDocument(blankMap, new EmptyEnvironment())
            {
                Name = "Untitled.hsm",
                HasUnsavedChanges = true
            };
            return Task.FromResult<IDocument>(doc);
        }

        public Task<IDocument> Load(string location)
        {
            var map = HsmSerializer.Load(location);
            var doc = new MapDocument(map, new EmptyEnvironment()) { FileName = location };
            return Task.FromResult<IDocument>(doc);
        }

        public bool CanSave(IDocument document) => document is MapDocument;

        public Task Save(IDocument document, string location)
        {
            var map = (MapDocument)document;
            HsmSerializer.Save(location, map.Map, Path.GetFileNameWithoutExtension(location));
            return Task.CompletedTask;
        }

        public DocumentPointer GetDocumentPointer(IDocument document)
        {
            var doc = document as MapDocument;
            if (doc == null) return null;
            return new DocumentPointer(nameof(HsmDocumentLoader)) { FileName = doc.FileName };
        }

        public Task<IDocument> Load(DocumentPointer documentPointer) => Load(documentPointer.FileName);
    }
}
