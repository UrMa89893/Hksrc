using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Editing
{
    /// <summary>Mesh-first modelling operations for native HSM scenes.</summary>
    public static class MeshEditing
    {
        public static void MoveVertices(MeshObject mesh, IEnumerable<Vector3> vertices, Vector3 delta, float weldTolerance = 0.01f)
        {
            if (mesh == null || vertices == null) return;
            var targets = vertices.ToList();
            if (targets.Count == 0) return;
            // Polygon.Vertices is intentionally read-only, so rebuild each affected
            // polygon instead of trying to assign through IReadOnlyList.
            for (var p = 0; p < mesh.Polygons.Count; p++)
            {
                var polygon = mesh.Polygons[p];
                var changed = false;
                var movedVertices = polygon.Vertices.Select(vertex =>
                {
                    if (!targets.Any(t => Vector3.DistanceSquared(t, vertex) <= weldTolerance * weldTolerance))
                        return vertex;

                    changed = true;
                    return vertex + delta;
                }).ToList();

                if (changed) mesh.Polygons[p] = new Polygon(movedVertices);
            }
            mesh.Invalidate();
        }

        public static Polygon ExtrudeFace(MeshObject mesh, Polygon face, float distance)
        {
            if (mesh == null || face == null || face.Vertices.Count < 3) throw new ArgumentException("A face with at least three vertices is required.");
            var normal = Vector3.Normalize(Vector3.Cross(face.Vertices[1] - face.Vertices[0], face.Vertices[2] - face.Vertices[0]));
            var offset = normal * distance;
            var top = face.Vertices.Select(v => v + offset).ToList();
            var created = new Polygon(top);
            var original = face.Vertices.ToList();
            mesh.Polygons.Add(created);
            for (var i = 0; i < original.Count; i++)
            {
                var j = (i + 1) % original.Count;
                mesh.Polygons.Add(new Polygon(new List<Vector3> { original[i], original[j], top[j], top[i] }));
            }
            mesh.Invalidate();
            return created;
        }

        public static void FlipFace(MeshObject mesh, Polygon face)
        {
            if (mesh == null || face == null) return;
            var reversed = face.Vertices.Reverse().ToList();
            var index = mesh.Polygons.IndexOf(face);
            if (index >= 0) mesh.Polygons[index] = new Polygon(reversed);
            mesh.Invalidate();
        }

        public static void DeleteFace(MeshObject mesh, Polygon face)
        {
            if (mesh == null || face == null) return;
            mesh.Polygons.Remove(face);
            mesh.Invalidate();
        }
    }
}
