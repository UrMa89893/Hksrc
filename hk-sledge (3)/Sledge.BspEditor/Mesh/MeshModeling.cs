using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Mesh
{
    public static class MeshModeling
    {
        public static void InsetFace(MeshObject mesh, int faceIndex, float amount)
        {
            if (mesh == null || faceIndex < 0 || faceIndex >= mesh.Polygons.Count) return;
            var face = mesh.Polygons[faceIndex]; var center = face.Origin;
            var verts = face.Vertices.Select(v => Vector3.Lerp(v, center, amount)).ToList();
            mesh.Polygons[faceIndex] = new Polygon(verts); mesh.Invalidate();
        }

        public static void TriangulateFace(MeshObject mesh, int faceIndex)
        {
            if (mesh == null || faceIndex < 0 || faceIndex >= mesh.Polygons.Count) return;
            var face = mesh.Polygons[faceIndex]; if (face.Vertices.Count < 4) return;
            var triangles = new List<Polygon>();
            for (var i=1;i<face.Vertices.Count-1;i++) triangles.Add(new Polygon(new[]{face.Vertices[0], face.Vertices[i], face.Vertices[i+1]}));
            mesh.Polygons.RemoveAt(faceIndex); mesh.Polygons.InsertRange(faceIndex, triangles); mesh.Invalidate();
        }

        public static void Weld(MeshObject mesh, float tolerance = 0.01f)
        {
            if (mesh == null) return;
            var all = mesh.Polygons.SelectMany(p => p.Vertices).ToList(); var unique = new List<Vector3>();
            Vector3 Find(Vector3 v) { foreach (var u in unique) if (Vector3.DistanceSquared(u,v)<=tolerance*tolerance) return u; unique.Add(v); return v; }
            for (var p=0;p<mesh.Polygons.Count;p++) mesh.Polygons[p] = new Polygon(mesh.Polygons[p].Vertices.Select(Find));
            mesh.Invalidate();
        }
    }
}
