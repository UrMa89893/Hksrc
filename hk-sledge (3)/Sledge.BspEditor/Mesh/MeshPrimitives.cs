using System;
using System.Collections.Generic;
using System.Numerics;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Mesh
{
    /// <summary>Primitive generators used by the mesh-first editor. Generated geometry is plain polygons, not BSP brushes.</summary>
    public static class MeshPrimitives
    {
        public static MeshObject Cube(long id, float size = 128f)
        {
            var h = size / 2f;
            var m = new MeshObject(id) { Name = "Cube" };
            var v = new[] { new Vector3(-h,-h,-h), new Vector3(h,-h,-h), new Vector3(h,h,-h), new Vector3(-h,h,-h), new Vector3(-h,-h,h), new Vector3(h,-h,h), new Vector3(h,h,h), new Vector3(-h,h,h) };
            m.Polygons.Add(new Polygon(new[] { v[0], v[3], v[2], v[1] }));
            m.Polygons.Add(new Polygon(new[] { v[4], v[5], v[6], v[7] }));
            m.Polygons.Add(new Polygon(new[] { v[0], v[1], v[5], v[4] }));
            m.Polygons.Add(new Polygon(new[] { v[1], v[2], v[6], v[5] }));
            m.Polygons.Add(new Polygon(new[] { v[2], v[3], v[7], v[6] }));
            m.Polygons.Add(new Polygon(new[] { v[3], v[0], v[4], v[7] }));
            m.Invalidate(); return m;
        }

        public static MeshObject Plane(long id, float size = 128f)
        {
            var h = size / 2f; var m = new MeshObject(id) { Name = "Plane" };
            m.Polygons.Add(new Polygon(new[] { new Vector3(-h,0,-h), new Vector3(h,0,-h), new Vector3(h,0,h), new Vector3(-h,0,h) }));
            m.Invalidate(); return m;
        }

        public static MeshObject Cylinder(long id, float radius = 64f, float height = 128f, int sides = 16)
        {
            sides = Math.Max(3, Math.Min(64, sides)); var m = new MeshObject(id) { Name = "Cylinder" };
            var bottom = new List<Vector3>(); var top = new List<Vector3>();
            for (var i=0;i<sides;i++) { var a=2*Math.PI*i/sides; var x=(float)Math.Cos(a)*radius; var z=(float)Math.Sin(a)*radius; bottom.Add(new Vector3(x,-height/2,z)); top.Add(new Vector3(x,height/2,z)); }
            m.Polygons.Add(new Polygon(bottom)); top.Reverse(); m.Polygons.Add(new Polygon(top));
            for (var i=0;i<sides;i++) { var j=(i+1)%sides; m.Polygons.Add(new Polygon(new[]{bottom[i],bottom[j],top[sides-1-j],top[sides-1-i]})); }
            m.Invalidate(); return m;
        }
    }
}
