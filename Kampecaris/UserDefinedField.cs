using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Doc;
using Grasshopper.Interop;
using Grasshopper.Types.Fields;
using GrasshopperIO;
using Microsoft.VisualBasic;
using Rhino.Geometry;

namespace Kampecaris;
[IoId("97D62935-19AC-455B-8408-2838C87F3A02")]
public class UserDefinedField : Field
{
    private readonly Document _document;
    private readonly string _name;
    private readonly FieldType _type;
    private readonly FieldStartComponent _fieldStart;
    private readonly FieldEndComponent _fieldEnd;

    public UserDefinedField(Document document, string name, FieldType type, FieldStartComponent fieldStart, FieldEndComponent fieldEnd)
    {
        _document = document;
        _name = name;
        _type = type;
        _fieldStart = fieldStart;
        _fieldEnd = fieldEnd;
    }
    public override void Store(IWriter writer)
    {
    }
    public override double ScalarAt(Point3d point)
    {
        if (Type is FieldType.None or FieldType.Vector) return 0;
        lock (_document)
        {
            _fieldStart.Point = point;
            _fieldStart.Expire();
            _document.Solution.StartWait();
            return _fieldEnd.Scalar;
        }
    }
    public override Vector3d VectorAt(Point3d point)
    {
        if (Type is FieldType.None or FieldType.Scalar) return Vector3d.Zero;
        lock (_document)
        {
            _fieldStart.Point = point;
            _fieldStart.Expire();
            _document.Solution.StartWait();
            return _fieldEnd.Vector;
        }
    }
    public override FieldType Type => _type;
    public override string Name => string.IsNullOrEmpty(_name) ? "User-Defined Field" : _name;
}
