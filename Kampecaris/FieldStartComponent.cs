using Grasshopper2.Components;
using Grasshopper2.Data;
using Grasshopper2.Doc;
using Grasshopper2.Extensions;
using Grasshopper2.Parameters;
using Grasshopper2.Parameters.Standard;
using Grasshopper2.UI;
using GrasshopperIO;
using Rhino.Geometry;

namespace Kampecaris;
[IoId("9CC48382-FED9-46CB-90B1-B9D5AD3F65FC")]
public sealed class FieldStartComponent : Component
{
    internal Point3d Point;
    public FieldStartComponent() : base(new Nomen("Field Start", "Set inputs for user-defined field", "Kampecaris", "Fields"))
    {
        Point = Point3d.Origin;
        if (Parameters is not null)
            Parameters.ParameterRenamed += ParametersOnParameterRenamed;
    }

    public FieldStartComponent(IReader reader) : base(reader)
    {
        Point = Point3d.Origin;
        if (Parameters is not null)
            Parameters.ParameterRenamed += ParametersOnParameterRenamed;
    }
    private void ParametersOnParameterRenamed(object? sender, ParameterEventArgs e)
    {
        if (e.Side == Side.Input)
            Parameters.Output(e.Index + 1).UserName = e.Parameter.UserName;
        else
            Parameters.Input(e.Index - 1).UserName = e.Parameter.UserName;
    }
    protected override void AddInputs(InputAdder inputs)
    {
        inputs.AddPoint("Point", "Pt", "Sampling location.", Access.Item, Requirement.MayBeMissing).Set(Point3d.Origin);
    }
    protected override void AddOutputs(OutputAdder outputs)
    {
        outputs.AddGeneric(">", ">", "Connect to Field Output.");
        outputs.AddPoint("Point", "Pt", "Sampling location.");
    }

    public override bool CanCreateParameter(Side side, int index)
    {
        if (side == Side.Input) return index > 0;
        return index > 1;
    }

    public override bool CanRemoveParameter(Side side, int index)
    {
        if (side == Side.Input)
            return Parameters.InputCount >= 2 && index > 0;
        return Parameters.OutputCount >= 3 && index > 1;
    }

    public override void DoCreateParameter(Side side, int index)
    {
        var inputIndex = (side == Side.Input) ? index : index - 1;
        var outputIndex = inputIndex + 1;
        Parameters.AddInput(new GenericParameter(Nomen.Empty), inputIndex);
        Parameters.AddOutput(new GenericParameter(Nomen.Empty), outputIndex);
    }
    public override void DoRemoveParameter(Side side, int index)
    {
        var inputIndex = (side == Side.Input) ? index : index - 1;
        var outputIndex = inputIndex + 1;
        Parameters.RemoveInput(inputIndex);
        Parameters.RemoveOutput(outputIndex);
    }
    public override void VariableParameterMaintenance()
    {
        for (var i = 1; i < Parameters.InputCount; i++)
        {
            var p1 = Parameters.Input(i);
            p1.Requirement = Requirement.MayBeMissing;
            var englishOrdinal = i.ToEnglishOrdinal();
            p1.FallbackName = $"V{i}";
            p1.ModifyNameAndInfo("Value", englishOrdinal + " optional value used to control the field's behavior.");
        }
        for (var i = 2; i < Parameters.OutputCount; i++)
        {
            var p2 = Parameters.Output(i);
            var englishOrdinal = (i - 1).ToEnglishOrdinal();
            p2.FallbackName = $"V{i - 1}";
            p2.ModifyNameAndInfo("Values", englishOrdinal + " optional value used to control the field's behavior");
        }
    }
    protected override void Process(IDataAccess access)
    {
        if (Document.State == DocumentState.Active)
            access.GetItem(0, out Point);
        access.SetItem(1, Point);
        for (var i = 1; i < Parameters.InputCount; i++)
        {
            access.GetTree(i, out Tree<object> tree);
            access.SetTree(i + 1, tree);
        }
    }
}

