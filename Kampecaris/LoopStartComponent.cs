using Grasshopper2.Components;
using Grasshopper2.Data;
using Grasshopper2.Doc;
using Grasshopper2.Extensions;
using Grasshopper2.Parameters;
using Grasshopper2.Parameters.Standard;
using Grasshopper2.UI;
using GrasshopperIO;

namespace Kampecaris;
[IoId("C4B2EF2B-4F11-4790-BAA8-D7C57C0C2A9F")]
public sealed class LoopStartComponent : Component
{
    internal int I;
    internal int Count;
    internal Tree<object>[] Trees;
    public LoopStartComponent() : base(new Nomen("Loop Start", "Initializes the loop with input data and parameters to begin iteration.", "Kampecaris", "Loop"))
    {
        if (Parameters is not null)
            Parameters.ParameterRenamed += ParametersOnParameterRenamed;
    }
    public LoopStartComponent(IReader reader) : base(reader)
    {
        if (Parameters is not null)
            Parameters.ParameterRenamed += ParametersOnParameterRenamed;
    }
    private void ParametersOnParameterRenamed(object sender, ParameterEventArgs e)
    {
        switch (e.Side)
        {
            case Side.Input when e.Index > 0:
                Parameters.Output(e.Index + 1).UserName = e.Parameter.UserName;
                break;
            case Side.Output when e.Index > 1:
                Parameters.Input(e.Index - 1).UserName = e.Parameter.UserName;
                break;
        }
    }
    
    protected override void AddInputs(InputAdder inputs)
    {
        inputs.AddInteger("Iterations", "I", "Loop Iterations").Set(0);
        inputs.AddGeneric("Data", "D0", "Zeroth data for the first iteration of the loop", Access.Tree,
            Requirement.MayBeMissing);
    }

    protected override void AddOutputs(OutputAdder outputs)
    {
        outputs.AddGeneric(">", ">", "Connect to Loop End.");
        outputs.AddInteger("Counter", "C", "Counter");
        outputs.AddGeneric("Data", "D0", "Zeroth data for the first iteration of the loop", Access.Tree);
    }
    public override bool CanCreateParameter(Side side, int index)
    {
        if (side == Side.Input) return index > 0;
        return index > 2;
    }

    public override bool CanRemoveParameter(Side side, int index)
    {
        if (side == Side.Input)
            return Parameters.InputCount > 2 && index > 0;
        return Parameters.OutputCount > 3 && index > 1;
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
        for (var i = 2; i < Parameters.InputCount; i++)
        {
            var p1 = Parameters.Input(i);
            p1.Requirement = Requirement.MayBeMissing;
            var englishOrdinal = (i-1).ToEnglishOrdinal();
            p1.FallbackName = $"D{i-1}";
            p1.ModifyNameAndInfo("Data", englishOrdinal + " data for the first iteration of the loop.");
        }
        for (var i = 3; i < Parameters.OutputCount; i++)
        {
            var p2 = Parameters.Output(i);
            var englishOrdinal = (i - 2).ToEnglishOrdinal();
            p2.FallbackName = $"D{i - 2}";
            p2.ModifyNameAndInfo("Data", englishOrdinal + " data for the first iteration of the loop.");
        }
    }
    protected override void Process(IDataAccess access)
    {
        access.GetItem(0, out I);
        if (Document.State == DocumentState.Active)
        {
            Trees = new Tree<object>[Parameters.InputCount - 1];
            for (var i = 1; i < Parameters.InputCount; i++)
                access.GetTree(i, out Trees[i - 1]);
        }
        for (var i = 2; i < Parameters.OutputCount; i++)
            access.SetTree(i, Trees[i - 2]);
        access.SetItem(1, Count);
    }
}