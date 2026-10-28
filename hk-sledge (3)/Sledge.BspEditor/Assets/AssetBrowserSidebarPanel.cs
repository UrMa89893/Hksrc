using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogicAndTrick.Oy;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Mesh;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Modification.Operations.Mesh;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.Common.Shell.Components;
using Sledge.Common.Shell.Context;
using Sledge.Common.Shell.Documents;
using Sledge.Common.Shell.Hooks;
using Sledge.Common.Shell.Settings;

namespace Sledge.BspEditor.Assets
{
    /// <summary>
    /// A simple, reliable game-asset/file browser.
    ///
    /// The browser deliberately behaves like a normal file browser rather than
    /// pre-indexing the whole Working directory. Folders are loaded lazily and
    /// files are read only for the folder currently being viewed.
    /// </summary>
    [Export(typeof(ISidebarComponent))]
    [Export(typeof(ISettingsContainer))]
    [Export(typeof(IInitialiseHook))]
    [OrderHint("A")]
    public sealed class FileBrowserSidebarPanel : UserControl, ISidebarComponent, ISettingsContainer, IInitialiseHook
    {
        private static readonly string[] TextureExtensions =
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga"
        };

        private readonly TextBox _search = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle
        };

        private readonly Button _browse = new Button
        {
            Text = "Working...",
            AutoSize = true,
            UseVisualStyleBackColor = true
        };

        private readonly Button _refresh = new Button
        {
            Text = "↻",
            Width = 32,
            Height = 24,
            UseVisualStyleBackColor = true
        };

        private readonly Button _up = new Button
        {
            Text = "↑",
            Width = 32,
            Height = 24,
            UseVisualStyleBackColor = true
        };

        private readonly Label _path = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private readonly TreeView _folders = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            BorderStyle = BorderStyle.FixedSingle
        };

        private readonly ListView _files = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle
        };

        private readonly ImageList _fileImages = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16)
        };

        private readonly ImageList _folderImages = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16)
        };

        private readonly Label _previewTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            AutoEllipsis = true,
            Padding = new Padding(4, 4, 4, 0)
        };

        private readonly Label _previewInfo = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            AutoEllipsis = true,
            Padding = new Padding(4, 3, 4, 3)
        };

        private readonly PictureBox _preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White
        };

        private readonly Button _applyTexture = new Button
        {
            Text = "Apply Texture",
            Dock = DockStyle.Bottom,
            Height = 28,
            Enabled = false
        };

        private readonly Label _status = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };

        private string _workingRoot;
        private string _currentFolder;
        private string _selectedFile;
        private WeakReference<MapDocument> _activeDocument =
            new WeakReference<MapDocument>(null);

        private sealed class BrowserNode
        {
            public string Path { get; private set; }

            public BrowserNode(string path)
            {
                Path = path;
            }
        }

        public string Title => "Assets";
        public object Control => this;

        string ISettingsContainer.Name =>
            "Sledge.BspEditor.Assets.FileBrowserSidebarPanel";

        public FileBrowserSidebarPanel()
        {
            BuildUi();
            BuildImages();
            UpdateWorkingRootLabel();
            TryAutoDetectWorking();
        }

        public Task OnInitialise()
        {
            Oy.Subscribe<IDocument>("Document:Activated", DocumentActivated);
            return Task.FromResult(0);
        }

        public IEnumerable<SettingKey> GetKeys()
        {
            yield return new SettingKey("Assets", "WorkingRoot", typeof(string));
            yield return new SettingKey("Assets", "CurrentFolder", typeof(string));
        }

        public void LoadValues(ISettingsStore store)
        {
            _workingRoot = store.Get<string>("WorkingRoot", "");

            if (!Directory.Exists(_workingRoot))
            {
                _workingRoot = FindWorkingFolder();
            }

            UpdateWorkingRootLabel();

            var savedFolder = store.Get<string>("CurrentFolder", "");
            if (Directory.Exists(savedFolder) && IsInsideWorkingRoot(savedFolder))
                _currentFolder = savedFolder;
            else
                _currentFolder = _workingRoot;

            if (Directory.Exists(_workingRoot))
                BeginInvoke((MethodInvoker)delegate { RebuildBrowser(); });
        }

        public void StoreValues(ISettingsStore store)
        {
            store.Set("WorkingRoot", _workingRoot ?? "");
            store.Set("CurrentFolder", _currentFolder ?? "");
        }

        // Unlike the old browser, the file browser is useful even with no map open.
        public bool IsInContext(IContext context)
        {
            return true;
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            MinimumSize = new Size(300, 220);

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 82,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(3)
            };

            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            toolbar.Controls.Add(_search, 0, 0);
            toolbar.SetColumnSpan(_search, 4);

            toolbar.Controls.Add(_browse, 0, 1);
            toolbar.Controls.Add(_up, 1, 1);
            toolbar.Controls.Add(_refresh, 2, 1);

            toolbar.Controls.Add(_path, 0, 2);
            toolbar.SetColumnSpan(_path, 4);

            var main = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 150
            };

            var right = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 220
            };

            var previewPanel = new Panel { Dock = DockStyle.Fill };
            previewPanel.Controls.Add(_preview);
            previewPanel.Controls.Add(_previewInfo);
            previewPanel.Controls.Add(_applyTexture);
            previewPanel.Controls.Add(_previewTitle);

            right.Panel1.Controls.Add(_files);
            right.Panel2.Controls.Add(previewPanel);

            main.Panel1.Controls.Add(_folders);
            main.Panel2.Controls.Add(right);

            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BorderStyle = BorderStyle.FixedSingle
            };
            statusPanel.Controls.Add(_status);

            _files.Columns.Add("Name", 180);
            _files.Columns.Add("Type", 80);
            _files.Columns.Add("Size", 75);
            _files.Columns.Add("Modified", 125);
            _files.SmallImageList = _fileImages;

            _folders.ImageList = _folderImages;

            Controls.Add(main);
            Controls.Add(statusPanel);
            Controls.Add(toolbar);

            _search.TextChanged += delegate { RefreshFileList(); };
            _browse.Click += delegate { ChooseWorkingFolder(); };
            _refresh.Click += delegate { RebuildBrowser(); };
            _up.Click += delegate { NavigateUp(); };

            _folders.BeforeExpand += FoldersBeforeExpand;
            _folders.AfterSelect += FoldersAfterSelect;
            _folders.NodeMouseDoubleClick += FoldersNodeDoubleClick;

            _files.SelectedIndexChanged += delegate { UpdateSelectionPreview(); };
            _files.DoubleClick += delegate { OpenSelectedFile(); };
            _files.KeyDown += FilesKeyDown;

            _applyTexture.Click += delegate { ApplySelectedTexture(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open in File Explorer", null, delegate { OpenFolderInExplorer(); });
            menu.Items.Add("Copy Path", null, delegate { CopySelectedPath(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Refresh", null, delegate { RebuildBrowser(); });
            _files.ContextMenuStrip = menu;
            _folders.ContextMenuStrip = menu;

            _files.MouseUp += FilesMouseUp;
            _folders.MouseUp += FoldersMouseUp;
        }

        private void BuildImages()
        {
            _folderImages.Images.Add("folder", CreateFolderIcon());
            _folderImages.Images.Add("folder-open", CreateFolderIcon());

            _fileImages.Images.Add("file", CreateFileIcon(false));
            _fileImages.Images.Add("image", CreateFileIcon(true));
        }

        private static Bitmap CreateFolderIcon()
        {
            var b = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.FromArgb(235, 190, 70)))
                    g.FillRectangle(brush, 1, 5, 14, 9);
                using (var brush = new SolidBrush(Color.FromArgb(248, 205, 88)))
                    g.FillRectangle(brush, 2, 3, 7, 4);
                using (var pen = new Pen(Color.FromArgb(180, 140, 35)))
                    g.DrawRectangle(pen, 1, 5, 14, 9);
            }
            return b;
        }

        private static Bitmap CreateFileIcon(bool image)
        {
            var b = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.White))
                    g.FillRectangle(brush, 3, 1, 10, 14);
                using (var pen = new Pen(Color.Gray))
                    g.DrawRectangle(pen, 3, 1, 10, 14);
                using (var brush = new SolidBrush(image ? Color.FromArgb(75, 150, 80) : Color.FromArgb(100, 130, 180)))
                    g.FillRectangle(brush, 5, 8, 6, 5);
                if (image)
                {
                    using (var pen = new Pen(Color.White))
                    {
                        g.DrawLine(pen, 5, 12, 7, 10);
                        g.DrawLine(pen, 7, 10, 9, 12);
                        g.DrawLine(pen, 9, 12, 11, 9);
                    }
                }
            }
            return b;
        }

        private void UpdateWorkingRootLabel()
        {
            _browse.Text = Directory.Exists(_workingRoot)
                ? "Working..."
                : "Choose Working...";

            _path.Text = Directory.Exists(_currentFolder)
                ? _currentFolder
                : (Directory.Exists(_workingRoot) ? _workingRoot : "No Working folder selected");
        }

        private void TryAutoDetectWorking()
        {
            if (Directory.Exists(_workingRoot)) return;

            var found = FindWorkingFolder();
            if (!string.IsNullOrWhiteSpace(found))
            {
                _workingRoot = found;
                _currentFolder = found;
                UpdateWorkingRootLabel();
                RebuildBrowser();
            }
        }

        private string FindWorkingFolder()
        {
            return WorkingAssetLocator.Find();
        }

        private void ChooseWorkingFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the game's Working folder.";
                dialog.SelectedPath = Directory.Exists(_workingRoot)
                    ? _workingRoot
                    : "";

                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                _workingRoot = dialog.SelectedPath;
                _currentFolder = _workingRoot;
                _search.Clear();
                RebuildBrowser();
            }
        }

        private void RebuildBrowser()
        {
            if (!Directory.Exists(_workingRoot))
            {
                _folders.Nodes.Clear();
                _files.Items.Clear();
                ClearPreview();
                _status.Text = "Choose a Working folder to begin.";
                UpdateWorkingRootLabel();
                return;
            }

            try
            {
                _folders.BeginUpdate();
                _folders.Nodes.Clear();

                var root = new TreeNode(Path.GetFileName(_workingRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)));
                root.Tag = new BrowserNode(_workingRoot);
                root.ImageKey = "folder";
                root.SelectedImageKey = "folder-open";
                root.Nodes.Add(CreateDummyNode());

                _folders.Nodes.Add(root);
                _folders.EndUpdate();

                _currentFolder = Directory.Exists(_currentFolder)
                    && IsInsideWorkingRoot(_currentFolder)
                    ? _currentFolder
                    : _workingRoot;

                SelectFolderNode(_currentFolder);

                if (_folders.SelectedNode == null)
                {
                    root.Expand();
                    _folders.SelectedNode = root;
                }

                RefreshFileList();
                UpdateWorkingRootLabel();
            }
            catch (Exception ex)
            {
                _status.Text = "Unable to read Working folder: " + ex.Message;
            }
        }

        private static TreeNode CreateDummyNode()
        {
            return new TreeNode("Loading...");
        }

        private void FoldersBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            var browserNode = node.Tag as BrowserNode;
            if (browserNode == null) return;

            if (node.Nodes.Count == 1 && node.Nodes[0].Tag == null)
                LoadFolderChildren(node, browserNode.Path);
        }

        private void LoadFolderChildren(TreeNode parent, string folder)
        {
            parent.Nodes.Clear();

            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.GetDirectories(folder)
                    .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return;
            }

            foreach (var dir in dirs)
            {
                var node = new TreeNode(Path.GetFileName(dir));
                node.Tag = new BrowserNode(dir);
                node.ImageKey = "folder";
                node.SelectedImageKey = "folder-open";

                try
                {
                    if (Directory.GetDirectories(dir).Length > 0)
                        node.Nodes.Add(CreateDummyNode());
                }
                catch
                {
                    // Ignore folders that cannot be inspected.
                }

                parent.Nodes.Add(node);
            }
        }

        private void FoldersAfterSelect(object sender, TreeViewEventArgs e)
        {
            var browserNode = e.Node.Tag as BrowserNode;
            if (browserNode == null) return;

            _currentFolder = browserNode.Path;
            _selectedFile = null;
            ClearPreview();
            RefreshFileList();
            UpdateWorkingRootLabel();
        }

        private void FoldersNodeDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var browserNode = e.Node.Tag as BrowserNode;
            if (browserNode == null) return;

            try
            {
                if (e.Node.IsExpanded) e.Node.Collapse();
                else e.Node.Expand();
            }
            catch { }
        }

        private void RefreshFileList()
        {
            if (!Directory.Exists(_currentFolder))
                return;

            var filter = (_search.Text ?? "").Trim();

            _files.BeginUpdate();
            _files.Items.Clear();

            var folderCount = 0;
            var fileCount = 0;

            try
            {
                var directories = Directory.GetDirectories(_currentFolder)
                    .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase);

                foreach (var dir in directories)
                {
                    var name = Path.GetFileName(dir);
                    if (!MatchesSearch(name, dir, filter)) continue;

                    var item = new ListViewItem(name, "file");
                    item.SubItems.Add("Folder");
                    item.SubItems.Add("");
                    item.SubItems.Add("");
                    item.Tag = new BrowserNode(dir);
                    _files.Items.Add(item);
                    folderCount++;
                }

                var files = Directory.GetFiles(_currentFolder)
                    .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (!MatchesSearch(name, file, filter)) continue;

                    FileInfo info;
                    try { info = new FileInfo(file); }
                    catch { continue; }

                    var item = new ListViewItem(name, IsTexture(file) ? "image" : "file");
                    item.SubItems.Add(GetFileType(file));
                    item.SubItems.Add(FormatSize(info.Length));
                    item.SubItems.Add(info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    item.Tag = file;
                    _files.Items.Add(item);
                    fileCount++;
                }
            }
            catch (Exception ex)
            {
                _status.Text = "Unable to read folder: " + ex.Message;
            }
            finally
            {
                _files.EndUpdate();
            }

            _status.Text = folderCount + " folder" + (folderCount == 1 ? "" : "s")
                + ", " + fileCount + " file" + (fileCount == 1 ? "" : "s");
        }

        private static bool MatchesSearch(string name, string path, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;

            return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateSelectionPreview()
        {
            ClearPreview();

            if (_files.SelectedItems.Count == 0)
            {
                _selectedFile = null;
                return;
            }

            var item = _files.SelectedItems[0];
            var browserNode = item.Tag as BrowserNode;

            if (browserNode != null)
            {
                _selectedFile = null;
                _previewTitle.Text = browserNode.Path;
                _previewInfo.Text = "Folder";
                return;
            }

            var file = item.Tag as string;
            if (string.IsNullOrWhiteSpace(file))
                return;

            _selectedFile = file;
            _previewTitle.Text = Path.GetFileName(file);

            try
            {
                var info = new FileInfo(file);
                _previewInfo.Text = GetFileType(file) + "  •  "
                    + FormatSize(info.Length) + "  •  "
                    + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                _previewInfo.Text = GetFileType(file);
            }

            if (IsTexture(file))
            {
                try
                {
                    _preview.Image = TextureImageLoader.Load(file);
                }
                catch
                {
                    _previewInfo.Text += "  •  Preview unavailable";
                }
            }

            UpdateApplyButton();
        }

        private void UpdateApplyButton()
        {
            _applyTexture.Enabled =
                !string.IsNullOrWhiteSpace(_selectedFile)
                && IsTexture(_selectedFile)
                && _activeDocument.TryGetTarget(out var document)
                && document != null
                && GetSelectedMeshes(document).Count > 0;
        }

        private List<MeshObject> GetSelectedMeshes(MapDocument document)
        {
            var result = new List<MeshObject>();
            if (document == null) return result;

            var selection = MeshContext.Selection(document);

            try
            {
                result = document.Map.Root.FindAll()
                    .OfType<MeshObject>()
                    .Where(x => x.ID == selection.MeshId || x.IsSelected)
                    .ToList();
            }
            catch
            {
                // A partially initialized document should not break the browser.
            }

            return result;
        }

        private void ApplySelectedTexture()
        {
            if (string.IsNullOrWhiteSpace(_selectedFile))
                return;

            if (!_activeDocument.TryGetTarget(out var document) || document == null)
                return;

            var meshes = GetSelectedMeshes(document);
            if (meshes.Count == 0)
                return;

            var relative = GetRelativePath(_selectedFile);
            MapDocumentOperation.Perform(
                document,
                new SetMeshMaterial(relative, meshes));

            _status.Text = "Applied texture: " + relative;
        }

        private string GetRelativePath(string path)
        {
            if (!IsInsideWorkingRoot(path))
                return path.Replace('\\', '/');

            var relative = path.Substring(_workingRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return relative.Replace('\\', '/');
        }

        private bool IsInsideWorkingRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(_workingRoot)
                || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var root = Path.GetFullPath(_workingRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                var target = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;

                return target.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        target.TrimEnd(Path.DirectorySeparatorChar),
                        root.TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void NavigateUp()
        {
            if (string.IsNullOrWhiteSpace(_currentFolder)) return;

            var parent = Directory.GetParent(_currentFolder);
            if (parent == null || !IsInsideWorkingRoot(parent.FullName))
                return;

            SelectFolderNode(parent.FullName);
        }

        private void SelectFolderNode(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            var root = _folders.Nodes.Count == 0 ? null : _folders.Nodes[0];
            if (root == null) return;

            SelectFolderNodeRecursive(root, folder);
        }

        private bool SelectFolderNodeRecursive(TreeNode node, string folder)
        {
            var browserNode = node.Tag as BrowserNode;
            if (browserNode != null
                && string.Equals(browserNode.Path, folder, StringComparison.OrdinalIgnoreCase))
            {
                _folders.SelectedNode = node;
                node.EnsureVisible();
                return true;
            }

            if (browserNode != null && !node.IsExpanded)
            {
                try { node.Expand(); }
                catch { }
            }

            foreach (TreeNode child in node.Nodes)
            {
                if (SelectFolderNodeRecursive(child, folder))
                    return true;
            }

            return false;
        }

        private void OpenSelectedFile()
        {
            if (_files.SelectedItems.Count == 0) return;

            var item = _files.SelectedItems[0];
            var folder = item.Tag as BrowserNode;

            if (folder != null)
            {
                SelectFolderNode(folder.Path);
                return;
            }

            var file = item.Tag as string;
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                return;

            if (IsTexture(file))
            {
                ApplySelectedTexture();
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _status.Text = "Could not open file: " + ex.Message;
            }
        }

        private void FilesKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                OpenSelectedFile();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                NavigateUp();
                e.Handled = true;
            }
        }

        private void FilesMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var hit = _files.HitTest(e.Location);
            if (hit.Item != null)
                hit.Item.Selected = true;
        }

        private void FoldersMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var hit = _folders.GetNodeAt(e.Location);
            if (hit != null)
                _folders.SelectedNode = hit;
        }

        private void OpenFolderInExplorer()
        {
            var path = _currentFolder;

            if (_files.SelectedItems.Count > 0)
            {
                var folder = _files.SelectedItems[0].Tag as BrowserNode;
                if (folder != null) path = folder.Path;
            }

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void CopySelectedPath()
        {
            string path = _currentFolder;

            if (_files.SelectedItems.Count > 0)
            {
                var folder = _files.SelectedItems[0].Tag as BrowserNode;
                if (folder != null)
                    path = folder.Path;
                else if (_files.SelectedItems[0].Tag is string)
                    path = (string)_files.SelectedItems[0].Tag;
            }

            if (!string.IsNullOrWhiteSpace(path))
                Clipboard.SetText(path);
        }

        private void ClearPreview()
        {
            var old = _preview.Image;
            _preview.Image = null;
            if (old != null) old.Dispose();

            _previewTitle.Text = "";
            _previewInfo.Text = "";
            UpdateApplyButton();
        }

        private async Task DocumentActivated(IDocument document)
        {
            var mapDocument = document as MapDocument;
            _activeDocument.SetTarget(mapDocument);

            if (mapDocument != null
                && !Directory.Exists(_workingRoot)
                && !string.IsNullOrWhiteSpace(mapDocument.FileName))
            {
                var directory = Path.GetDirectoryName(mapDocument.FileName);

                while (!string.IsNullOrWhiteSpace(directory))
                {
                    var candidate = Path.Combine(directory, "Working");

                    if (Directory.Exists(candidate))
                    {
                        _workingRoot = candidate;
                        _currentFolder = candidate;
                        RebuildBrowser();
                        break;
                    }

                    var parent = Directory.GetParent(directory);
                    if (parent == null) break;
                    directory = parent.FullName;
                }
            }

            if (IsHandleCreated)
                BeginInvoke((MethodInvoker)UpdateApplyButton);

            await Task.CompletedTask;
        }

        private static bool IsTexture(string path)
        {
            return TextureExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string GetFileType(string path)
        {
            if (Directory.Exists(path)) return "Folder";

            var extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                return "File";

            return extension.TrimStart('.').ToUpperInvariant() + " file";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + " GB";
        }
    }
}
