using Grasshopper.Components;
using Grasshopper.Parameters;
using Grasshopper.Parameters.Standard;
using Grasshopper.Types.Fields;
using Grasshopper.Types.Fields.Standard;
using Grasshopper.UI;
using GrasshopperIO;
using Rhino.Geometry;
using System.Collections.Concurrent;
using Grasshopper.UI.Icon;
using Kampecaris.Properties;

namespace Kampecaris.Voxel;
[IoId("DBF481C5-CFC5-4066-9B9A-47FDFAD90A83")]
public sealed class MarchingMesh : ComponentWithPins
{
    public MarchingMesh() : base(new Nomen("Marching Mesh", "Marching on a Mesh", "Kampecaris", "Voxel"))
    {
    }
    public MarchingMesh(IReader reader) : base(reader) { }
    protected override void AddInputs(InputAdder inputs)
    {
        inputs.AddMesh("Mesh", "Ms", "Mesh to march").Set(Mesh.CreateFromPlane(Plane.WorldXY, new Interval(-10, 10), new Interval(-10, 10), 50, 50));
        inputs.AddField("Field", "Fl", "Field to evaluate.").Set(new SimplexScalarField(0.0, 1.0, 7.0));
        inputs.AddNumber("Target", "Tr", "Optional scalar target. if omitted, the center value of the domain will be picked.", requirement: Requirement.MayBeMissing);
    }
    protected override void AddOutputs(OutputAdder outputs) =>
        outputs.AddPolyline("Isocurves", "Ic", "Isocurves", Access.Twig);
    protected override IIcon IconInternal => AbstractIcon.FromStream(new MemoryStream(Resources.MarchingMesh));
    public override IEnumerable<Guid> SupportedPins
    {
        get
        {
            yield return AbsoluteTolerancePin.Id;
        }
    }
    protected override void Process(IDataAccess access)
    {
        access.GetItem(0, out Mesh mesh);
        access.GetItem(1, out Field field);
        var flag = access.GetItem(2, out double iso);
        access.GetTolerance(out double tolerance);
        access.RectifyNonNegative(ref tolerance, "tolerance");
        var values = mesh.Vertices.AsParallel().Select(v => field.ScalarAt(v)).ToArray();
        if (!flag) iso = (values.Min() + values.Max()) * 0.5;
        var lines = new ConcurrentBag<Line>();
        Parallel.ForEach(mesh.Faces, mf =>
        {
            var state = 0;
            for (var i = 0; i < (mf.IsQuad ? 4 : 3); i++)
                state += values[mf[i]] > iso ? 1 << i : 0;
            var lut = mf.IsQuad ? LookUpTables.Quadrangles[state] : LookUpTables.Triangles[state];
            for (var i = 0; i < lut.Length; i += 4)
            {
                var i0 = mf[lut[i]];
                var i1 = mf[lut[i + 1]];
                var i2 = mf[lut[i + 2]];
                var i3 = mf[lut[i + 3]];
                var t0 = (iso - values[i0]) / (values[i1] - values[i0]);
                var t1 = (iso - values[i2]) / (values[i3] - values[i2]);
                var s = new Point3d(mesh.Vertices[i0]) * (1.0 - t0) + new Point3d(mesh.Vertices[i1]) * t0;
                var e = new Point3d(mesh.Vertices[i2]) * (1.0 - t1) + new Point3d(mesh.Vertices[i3]) * t1;
                var line = new Line(s, e);
                if (line.Length < tolerance) return;
                lines.Add(line);
            }
        });
        access.SetTwig(0, Polyline.CreateByJoiningLines(lines.ToArray(), tolerance, false));
    }
}