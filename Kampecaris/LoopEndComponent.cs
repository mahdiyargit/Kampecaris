using Grasshopper.Components;
using Grasshopper.Data;
using Grasshopper.Doc;
using Grasshopper.Extensions;
using Grasshopper.Parameters;
using Grasshopper.Parameters.Standard;
using Grasshopper.UI;
using Grasshopper.UI.Icon;
using Grasshopper.UI.InputPanel;
using Grasshopper.UI.Toolbar;
using GrasshopperIO;
using GrasshopperIO.DataBase;

namespace Kampecaris;
[IoId("EE23160E-DAEE-4007-83A6-AEFF53D6CC1C")]
public class LoopEndComponent : Component
{
    private Tree<object>[] _trees;
    private Tree<object>[] _recorded;
    private bool _exit;
    private bool _record;
    public LoopEndComponent() : base(new Nomen("Loop End", "Collects loop results and feeds them back for the next iteration or final output.", "Kampecaris", "Loop"))
    {
        if (Parameters is not null)
            Parameters.ParameterRenamed += ParametersOnParameterRenamed;
    }

    public LoopEndComponent(IReader reader) : base(reader)
    {
        if (Parameters is not null)
            Parameters.ParameterRenamed += ParametersOnParameterRenamed;
        _record = reader.Boolean("Record");
    }

    public override void AppendToInputPanel(InputPanel panel)
    {
        base.AppendToInputPanel(panel);
        using (panel.BeginCategory("Record Data"))
        {
            var nomen = new Nomen("", "", "Record Data", "Record Data");
            var spacer = new Spacer(nomen, 0, 0);
            var pushOption = new PushOption(StandardIcons.PreviewIcon(),
                nomen.WithName("RecordData").WithInfo("When enabled, data from each iteration is stores separately."),
                Record, b => Record = b);
            pushOption.SetSizeLimits(100, 1000);
            panel.AddBar(true, spacer, pushOption);
        }
    }
    public bool Record
    {
        get => _record;
        internal set
        {
            if (value == _record) return;
            _record = value;
            var document = Document;
            if (Document == null)
                Expire();
            else
                document.Solution.DelayedExpire(this);
        }
    }
    private void ParametersOnParameterRenamed(object? sender, ParameterEventArgs e)
    {
        switch (e.Side)
        {
            case Side.Input when e.Index > 1:
                Parameters.Output(e.Index - 2).UserName = e.Parameter.UserName;
                break;
            case Side.Output:
                Parameters.Input(e.Index + 2).UserName = e.Parameter.UserName;
                break;
        }
    }
    protected override void AddInputs(InputAdder inputs)
    {
        inputs.AddTopological("<", "<", "Connect to Field Input.").DoCollect = true;
        inputs.AddBoolean("Exit", "E", "Set true to exit the loop.").Set(false);
        inputs.AddGeneric("Data", "D0", "Zeroth data", Access.Tree,
            Requirement.MayBeMissing);
    }

    protected override void AddOutputs(OutputAdder outputs)
    {
        outputs.AddGeneric("Data", "D0", "Zeroth data result after looping.", Access.Tree);
    }
    public override bool CanCreateParameter(Side side, int index)
    {
        if (side == Side.Input) return index > 1;
        return true;
    }

    public override bool CanRemoveParameter(Side side, int index)
    {
        if (side == Side.Input)
            return Parameters.InputCount > 3 && index > 1;
        return Parameters.InputCount > 0;
    }

    public override void DoCreateParameter(Side side, int index)
    {
        if (side == Side.Input)
        {
            Parameters.AddInput(new GenericParameter(Nomen.Empty), index);
            Parameters.AddOutput(new GenericParameter(Nomen.Empty), index - 2);
        }
        else
        {
            Parameters.AddInput(new GenericParameter(Nomen.Empty), index + 2);
            Parameters.AddOutput(new GenericParameter(Nomen.Empty), index);
        }
    }
    public override void DoRemoveParameter(Side side, int index)
    {
        if (side == Side.Input)
        {
            Parameters.RemoveInput(index);
            Parameters.RemoveOutput(index - 2);
        }
        else
        {
            Parameters.RemoveInput(index + 2);
            Parameters.RemoveOutput(index);
        }
    }
    public override void VariableParameterMaintenance()
    {
        for (var i = 2; i < Parameters.InputCount; i++)
        {
            var p1 = Parameters.Input(i);
            p1.Requirement = Requirement.MayBeMissing;
            var englishOrdinal = (i-2).ToEnglishOrdinal();
            p1.FallbackName = $"D{i - 2}";
            p1.ModifyNameAndInfo("Data", englishOrdinal + " data for the first iteration of the loop.");
        }
        for (var i = 0; i < Parameters.OutputCount; i++)
        {
            var p1 = Parameters.Output(i);
            p1.Requirement = Requirement.MayBeMissing;
            var englishOrdinal = i.ToEnglishOrdinal();
            p1.FallbackName = $"D{i}";
            p1.ModifyNameAndInfo("Data", englishOrdinal + " data for the first iteration of the loop.");
        }
    }

    protected override void Process(IDataAccess access)
    {
        _trees = new Tree<object>[Parameters.InputCount - 2];
        for (var i = 2; i < Parameters.InputCount; i++)
            access.GetTree(i, out _trees[i - 2]);
        access.GetItem(1, out _exit);
        if (Document.State != DocumentState.Active) return;
        access.GetTree(0, out Tree<Guid> tree);
        if (!tree.AllItems.Any())
        {
            access.AddWarning("Loop Start Component required.",
                "This component must be connected to a Loop Start Component.");
            return;
        }

        if (tree.AllItems.Count() > 1)
        {
            access.AddWarning("Multiple Loop Start Components connected.",
                "This component must be connected to a Loop Start Component.");
            return;
        }

        if (Document.Objects.Find(tree.AllItems.First()).ParentObject is not LoopStartComponent startComp)
        {
            access.AddWarning("Invalid Loop Start Component connected.",
                "This component must be connected to a Loop Start Component.");
            return;
        }

        if (startComp.Parameters.InputCount != Parameters.OutputCount + 1)
        {
            access.AddWarning("Invalid Loop Start Component connected.",
                "Loop Start and Loop End must have the same amount of data streams.");
            return;
        }

        var root = Node.Create("Document");
        Document.Store(root, FileContents.Small);
        var bytes = IO.WriteNodeToByteArray(root);
        var reader = IO.ReadNodeFromByteArray(bytes);
        var document = new Document(reader);
        var loopStart = (LoopStartComponent)document.Objects.Find(startComp.InstanceId);
        var loopEnd = (LoopEndComponent)document.Objects.Find(InstanceId);
        loopStart.Trees = startComp.Trees;
        document.Solution.StartWait();
        loopEnd._recorded = new Tree<object>[loopStart.Trees.Length];
        if (_record)
        {
            for (var i = 0; i < loopEnd._trees.Length; i++)
                loopEnd._recorded[i] = loopEnd._trees[i].ModifyPaths(null, path => path.PrependElement(loopStart.Count));
        }
        while (!loopEnd._exit && loopStart.Count < loopStart.I)
        {
            loopStart.Trees = loopEnd._trees;
            loopStart.Expire();
            document.Solution.StartWait();
            loopStart.Count++;
            if (!_record) continue;
            for (var i = 0; i < loopEnd._trees.Length; i++)
            {
                var mt = loopEnd._trees[i].ModifyPaths(null, path => path.PrependElement(loopStart.Count));
                for (var j = 0; j < mt.PathCount; j++)
                    loopEnd._recorded[i] = loopEnd._recorded[i].Add(mt.Paths[j], mt.Twigs[j]);
            }
        }
        if (_record)
            for (var i = 0; i < loopEnd._trees.Length; i++)
                access.SetTree(i, loopEnd._recorded[i]);
        else
            for (var i = 0; i < loopEnd._trees.Length; i++)
                access.SetTree(i, loopEnd._trees[i]);
        document.Close();
    }
    public override void Store(IWriter writer)
    {
        base.Store(writer);
        writer.Boolean((Name)"Record", Record);
    }
}