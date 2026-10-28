using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sledge.BspEditor.Assets
{
    /// <summary>Finds the game's Working asset root without depending on the engine.</summary>
    public static class WorkingAssetLocator
    {
        public static string Find(string preferredStart = null)
        {
            var starts = new List<string>();
            if (!string.IsNullOrWhiteSpace(preferredStart)) starts.Add(preferredStart);
            try { starts.Add(AppDomain.CurrentDomain.BaseDirectory); } catch { }
            try { starts.Add(Directory.GetCurrentDirectory()); } catch { }

            foreach (var start in starts.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = start;
                try
                {
                    if (File.Exists(dir)) dir = Path.GetDirectoryName(dir);
                    while (!string.IsNullOrWhiteSpace(dir))
                    {
                        if (string.Equals(Path.GetFileName(dir), "Working", StringComparison.OrdinalIgnoreCase) && Directory.Exists(dir))
                            return dir;

                        var direct = Path.Combine(dir, "Working");
                        if (Directory.Exists(direct)) return direct;

                        var parent = Directory.GetParent(dir);
                        if (parent == null) break;
                        dir = parent.FullName;
                    }
                }
                catch { }
            }
            return "";
        }
    }
}
