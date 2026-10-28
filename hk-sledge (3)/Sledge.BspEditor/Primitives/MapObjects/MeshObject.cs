using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Primitives.MapObjects
{
    /// <summary>
    /// A mesh-first scene object. Unlike Solid, this object has no BSP brush semantics.
    /// It is simply a collection of renderable polygons with an optional material.
    /// </summary>
    public class MeshObject : BaseMapObject
    {
        public string Name { get; set; }
        public string MeshPath { get; set; }
        public string Material { get; set; }
        public bool Visible { get; set; } = true;
        public bool Collision { get; set; } = true;
        public bool CastShadows { get; set; } = true;
        public bool SmoothShading { get; set; } = false;
        public List<Polygon> Polygons { get; }

        public MeshObject(long id) : base(id)
        {
            Name = "Mesh";
            MeshPath = "";
            Material = "";
            Polygons = new List<Polygon>();
        }

        protected override Box GetBoundingBox()
        {
            if (Polygons.Count == 0) return Box.Empty;
            return new Box(Polygons.Select(p => new Box(p.Vertices)));
        }

        public override IEnumerable<Polygon> GetPolygons() => Polygons;

        public override IEnumerable<IMapObject> Decompose(IEnumerable<Type> allowedTypes)
        {
            yield return this;
        }

        protected override string SerialisedName => "MeshObject";

        public override void Transform(Matrix4x4 matrix)
        {
            for (var i = 0; i < Polygons.Count; i++)
                Polygons[i] = new Polygon(Polygons[i].Vertices.Select(v => Vector3.Transform(v, matrix)));
            base.Transform(matrix);
        }
    }
}
