using Grasshopper2.Components;
using Grasshopper2.Doc;
using Grasshopper2.Parameters;
using Grasshopper2.Parameters.Standard;
using Grasshopper2.Types.Fields;
using Grasshopper2.Types.Fields.Standard;
using Grasshopper2.UI;
using GrasshopperIO;
using Rhino.Geometry;

namespace Kampecaris.Voxel;
[IoId("DBF481C5-CFC5-4066-9B9A-47FDFAD90A84")]
public sealed class MarchingCubesComponent : Component, IPinCushion
{
    public enum Method { Cube, Tetrahedra }
    public MarchingCubesComponent() : base(new Nomen("Marching Cubes", "Marching Cubes", "Kampecaris", "Voxel"))
    {
    }
    public MarchingCubesComponent(IReader reader) : base(reader)
    {
    }
    protected override void AddInputs(InputAdder inputs)
    {
        inputs.AddBox("Box", "Bx", "Box to evaluate.").Set(new Box(Plane.WorldXY, new BoundingBox(-10, -10, -10, 10, 10, 10)));
        inputs.AddInteger("X Count", "X", "Resolution in x direction.").Set(20);
        inputs.AddInteger("Y Count", "Y", "Resolution in y direction.").Set(20);
        inputs.AddInteger("Z Count", "Z", "Resolution in z direction.").Set(20);
        inputs.AddField("Field", "Fl", "Field to evaluate.").Set(new SimplexScalarField(0.0, 1.0, 7.0));
        inputs.AddNumber("Target", "Tr", "Optional scalar target. if omitted, the center value of the domain will be picked.", requirement: Requirement.MayBeMissing);
        inputs.AddBoolean("Close", "Cl", "Close voxel data").Set(false);
        inputs.AddInteger("Method", "M", "Method").SetEnum(new Method[1]);
        inputs.AddBoolean("Calculate Normal", "Cn", "Calculate Normal.").Set(true);
        inputs[0].Display = ObjectDisplay.Hidden;
    }
    protected override void AddOutputs(OutputAdder outputs) => outputs.AddMesh("Mesh", "Ms", "Mesh");
    public IEnumerable<Guid> SupportedPins
    {
        get { yield return AbsoluteTolerancePin.Id; }
    }
    protected override void Process(IDataAccess access)
    {
        access.GetItem(0, out Box box);
        access.GetItem(1, out int x);
        access.GetItem(2, out int y);
        access.GetItem(3, out int z);
        access.GetItem(4, out Field field);
        var flag = access.GetItem(5, out double isoDouble);
        access.GetItem(6, out bool close);
        access.GetItem(7, out int method);
        access.GetItem(8, out bool computeNormal);
        access.GetTolerance(out double tolerance);
        access.RectifyNonNegative(ref tolerance, "tol");
        var tol = (float)tolerance;
        var pts = new Point3f[x * y * z];
        var values = new float[x * y * z];
        var width = 1.0 / (x - 1);
        var length = 1.0 / (y - 1);
        var height = 1.0 / (z - 1);
        Parallel.For(0, x, i =>
        {
            for (var j = 0; j < y; j++)
                for (var k = 0; k < z; k++)
                {
                    var index = i + j * x + k * x * y;
                    var pt = box.PointAt(i * width, j * length, k * height);
                    if (close && (i == 0 || j == 0 || k == 0 || i == x - 1 || j == y - 1 || k == z - 1))
                        values[index] = 0;
                    else
                        values[index] = (float)field.ScalarAt(pt);
                    pts[index] = (Point3f)pt;
                }
        });
        var iso = flag ? (float)isoDouble : (values.Min() + values.Max()) * 0.5f;
        var joined = new Mesh();
        joined.Append(method == 0 ? MarchingCube(x, y, z, values, pts, iso, tol) : MarchingTetrahedra(x, y, z, values, pts, iso, tol));
        joined.Vertices.CombineIdentical(true, true);
        joined.Faces.CullDegenerateFaces();
        if (computeNormal)
        {
            var gf = new GradientField(field, tolerance);
            var normals = new Vector3f[joined.Vertices.Count];
            Parallel.For(0, joined.Vertices.Count, i =>
            {
                var normal = (Vector3f)gf.VectorAt(joined.Vertices[i]);
                normal.Unitize();
                normals[i] = -normal;
            });
            joined.Normals.AddRange(normals);
        }
        else
            joined.RebuildNormals();
        access.SetItem(0, joined);
    }
    private static int[] BoxCorners(int i, int j, int k, int x, int y)
    {
        var first = i + j * x + k * x * y;
        var product = x * y;
        return new[]
        {
            first,
            first + 1,
            first + 1 + x,
            first + x,
            first + product,
            first + 1 + product,
            first + 1 + x + product,
            first + x + product
        };
    }
    private static Point3f Interpolate(float iso, Point3f p0, Point3f p1, double v0, double v1, float tol)
    {
        if (p1 < p0)
            return Math.Abs(v1 - v0) > tol ? p1 + (p0 - p1) * (float)(iso - v1) / (float)(v0 - v1) : p1;
        return Math.Abs(v0 - v1) > tol ? p0 + (p1 - p0) * (float)(iso - v0) / (float)(v1 - v0) : p0;
    }
    private static void PolygoniseTri(float iso, Mesh m, IReadOnlyList<Point3f> p, IReadOnlyList<float> v, float tol)
    {
        var count = m.Vertices.Count;
        var ti = 0;
        if (v[0] < iso) ti |= 1;
        if (v[1] < iso) ti |= 2;
        if (v[2] < iso) ti |= 4;
        if (v[3] < iso) ti |= 8;
        switch (ti)
        {
            case 0x01:
                m.Vertices.Add(Interpolate(iso, p[0], p[1], v[0], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[2], v[0], v[2], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[3], v[0], v[3], tol));
                m.Faces.AddFace(count + 2, count + 1, count);
                break;
            case 0x02:
                m.Vertices.Add(Interpolate(iso, p[1], p[0], v[1], v[0], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[3], v[1], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[2], v[1], v[2], tol));
                m.Faces.AddFace(count + 2, count + 1, count);
                break;
            case 0x03:
                m.Vertices.Add(Interpolate(iso, p[0], p[3], v[0], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[2], v[0], v[2], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[3], v[1], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[2], v[1], v[2], tol));
                m.Faces.AddFace(count, count + 1, count + 3, count + 2);
                break;
            case 0x04:
                m.Vertices.Add(Interpolate(iso, p[2], p[0], v[2], v[0], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[1], v[2], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[3], v[2], v[3], tol));
                m.Faces.AddFace(count + 2, count + 1, count);
                break;
            case 0x05:
                m.Vertices.Add(Interpolate(iso, p[0], p[1], v[0], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[3], v[2], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[3], v[0], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[2], v[1], v[2], tol));
                m.Faces.AddFace(count, count + 2, count + 1, count + 3);
                break;
            case 0x06:
                m.Vertices.Add(Interpolate(iso, p[0], p[1], v[0], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[3], v[1], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[3], v[2], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[2], v[0], v[2], tol));
                m.Faces.AddFace(count + 3, count + 2, count + 1, count);
                break;
            case 0x07:
                m.Vertices.Add(Interpolate(iso, p[3], p[0], v[3], v[0], tol));
                m.Vertices.Add(Interpolate(iso, p[3], p[2], v[3], v[2], tol));
                m.Vertices.Add(Interpolate(iso, p[3], p[1], v[3], v[1], tol));
                m.Faces.AddFace(count, count + 1, count + 2);
                break;
            case 0x08:
                m.Vertices.Add(Interpolate(iso, p[3], p[0], v[3], v[0], tol));
                m.Vertices.Add(Interpolate(iso, p[3], p[2], v[3], v[2], tol));
                m.Vertices.Add(Interpolate(iso, p[3], p[1], v[3], v[1], tol));
                m.Faces.AddFace(count + 2, count + 1, count);
                break;
            case 0x09:
                m.Vertices.Add(Interpolate(iso, p[0], p[1], v[0], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[3], v[1], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[3], v[2], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[2], v[0], v[2], tol));
                m.Faces.AddFace(count, count + 1, count + 2, count + 3);
                break;
            case 0x0A:
                m.Vertices.Add(Interpolate(iso, p[0], p[1], v[0], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[3], v[2], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[3], v[0], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[2], v[1], v[2], tol));
                m.Faces.AddFace(count + 3, count + 1, count + 2, count);
                break;
            case 0x0B:
                m.Vertices.Add(Interpolate(iso, p[2], p[0], v[2], v[0], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[1], v[2], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[2], p[3], v[2], v[3], tol));
                m.Faces.AddFace(count, count + 1, count + 2);
                break;
            case 0x0C:
                m.Vertices.Add(Interpolate(iso, p[0], p[3], v[0], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[2], v[0], v[2], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[3], v[1], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[2], v[1], v[2], tol));
                m.Faces.AddFace(count + 2, count + 3, count + 1, count);
                break;
            case 0x0D:
                m.Vertices.Add(Interpolate(iso, p[1], p[0], v[1], v[0], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[3], v[1], v[3], tol));
                m.Vertices.Add(Interpolate(iso, p[1], p[2], v[1], v[2], tol));
                m.Faces.AddFace(count, count + 1, count + 2);
                break;
            case 0x0E:
                m.Vertices.Add(Interpolate(iso, p[0], p[1], v[0], v[1], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[2], v[0], v[2], tol));
                m.Vertices.Add(Interpolate(iso, p[0], p[3], v[0], v[3], tol));
                m.Faces.AddFace(count, count + 1, count + 2);
                break;
        }
    }
    private static Mesh[] MarchingTetrahedra(int x, int y, int z, IReadOnlyList<float> values, IReadOnlyList<Point3f> pts, float iso, float tol)
    {
        var slices = new Mesh[x - 1];
        Parallel.For(0, x - 1, i =>
        {
            var mesh = new Mesh();
            for (var j = 0; j < y - 1; j++)
            {
                for (var k = 0; k < z - 1; k++)
                {
                    var indices = BoxCorners(i, j, k, x, y);
                    var val = indices.Select(m => values[m]).ToArray();
                    var cor = indices.Select(m => pts[m]).ToArray();
                    if (val[0] < iso == val[1] < iso &&
                        val[1] < iso == val[2] < iso &&
                        val[2] < iso == val[3] < iso &&
                        val[3] < iso == val[4] < iso &&
                        val[4] < iso == val[5] < iso &&
                        val[5] < iso == val[6] < iso &&
                        val[6] < iso == val[7] < iso) continue;
                    PolygoniseTri(iso, mesh, new[] { cor[0], cor[2], cor[3], cor[7] }, new[] { val[0], val[2], val[3], val[7] }, tol);
                    PolygoniseTri(iso, mesh, new[] { cor[6], cor[2], cor[0], cor[7] }, new[] { val[6], val[2], val[0], val[7] }, tol);
                    PolygoniseTri(iso, mesh, new[] { cor[4], cor[6], cor[0], cor[7] }, new[] { val[4], val[6], val[0], val[7] }, tol);
                    PolygoniseTri(iso, mesh, new[] { cor[0], cor[6], cor[1], cor[2] }, new[] { val[0], val[6], val[1], val[2] }, tol);
                    PolygoniseTri(iso, mesh, new[] { cor[1], cor[6], cor[0], cor[4] }, new[] { val[1], val[6], val[0], val[4] }, tol);
                    PolygoniseTri(iso, mesh, new[] { cor[5], cor[6], cor[1], cor[4] }, new[] { val[5], val[6], val[1], val[4] }, tol);
                }
            }
            slices[i] = mesh;
        });
        return slices;
    }
    private static Mesh[] MarchingCube(int x, int y, int z, IReadOnlyList<float> values, IReadOnlyList<Point3f> pts, float iso, float tol)
    {
        var slices = new Mesh[x - 1];
        Parallel.For(0, x - 1, i =>
        {
            var mesh = new Mesh();
            for (var j = 0; j < y - 1; j++)
            {
                for (var k = 0; k < z - 1; k++)
                {
                    var indices = BoxCorners(i, j, k, x, y);
                    var val = indices.Select(m => values[m]).ToArray();
                    var cor = indices.Select(m => pts[m]).ToArray();
                    var cubeIndex = 0;
                    if (val[0] < iso) cubeIndex |= 1;
                    if (val[1] < iso) cubeIndex |= 2;
                    if (val[2] < iso) cubeIndex |= 4;
                    if (val[3] < iso) cubeIndex |= 8;
                    if (val[4] < iso) cubeIndex |= 16;
                    if (val[5] < iso) cubeIndex |= 32;
                    if (val[6] < iso) cubeIndex |= 64;
                    if (val[7] < iso) cubeIndex |= 128;
                    var vertices = new Point3d[12];
                    var edgeTable = LookUpTables.EdgeTable;
                    if (edgeTable[cubeIndex] == 0) continue;
                    if ((edgeTable[cubeIndex] & 1) != 0)
                        vertices[0] = Interpolate(iso, cor[0], cor[1], val[0], val[1], tol);
                    if ((edgeTable[cubeIndex] & 2) != 0)
                        vertices[1] = Interpolate(iso, cor[1], cor[2], val[1], val[2], tol);
                    if ((edgeTable[cubeIndex] & 4) != 0)
                        vertices[2] = Interpolate(iso, cor[2], cor[3], val[2], val[3], tol);
                    if ((edgeTable[cubeIndex] & 8) != 0)
                        vertices[3] = Interpolate(iso, cor[3], cor[0], val[3], val[0], tol);
                    if ((edgeTable[cubeIndex] & 16) != 0)
                        vertices[4] = Interpolate(iso, cor[4], cor[5], val[4], val[5], tol);
                    if ((edgeTable[cubeIndex] & 32) != 0)
                        vertices[5] = Interpolate(iso, cor[5], cor[6], val[5], val[6], tol);
                    if ((edgeTable[cubeIndex] & 64) != 0)
                        vertices[6] = Interpolate(iso, cor[6], cor[7], val[6], val[7], tol);
                    if ((edgeTable[cubeIndex] & 128) != 0)
                        vertices[7] = Interpolate(iso, cor[7], cor[4], val[7], val[4], tol);
                    if ((edgeTable[cubeIndex] & 256) != 0)
                        vertices[8] = Interpolate(iso, cor[0], cor[4], val[0], val[4], tol);
                    if ((edgeTable[cubeIndex] & 512) != 0)
                        vertices[9] = Interpolate(iso, cor[1], cor[5], val[1], val[5], tol);
                    if ((edgeTable[cubeIndex] & 1024) != 0)
                        vertices[10] = Interpolate(iso, cor[2], cor[6], val[2], val[6], tol);
                    if ((edgeTable[cubeIndex] & 2048) != 0)
                        vertices[11] = Interpolate(iso, cor[3], cor[7], val[3], val[7], tol);
                    var triTable = LookUpTables.TriTable[cubeIndex];
                    for (var m = 0; triTable[m] != -1; m += 3)
                    {
                        mesh.Vertices.Add(vertices[triTable[m]]);
                        mesh.Vertices.Add(vertices[triTable[m + 1]]);
                        mesh.Vertices.Add(vertices[triTable[m + 2]]);
                        var first = mesh.Faces.Count * 3;
                        mesh.Faces.AddFace(first, first + 1, first + 2);
                    }
                }
            }
            slices[i] = mesh;
        });
        return slices;
    }
}