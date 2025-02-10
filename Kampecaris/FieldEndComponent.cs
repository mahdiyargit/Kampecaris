using System;
using System.Linq;
using Grasshopper.Components;
using Grasshopper.Data;
using Grasshopper.Doc;
using Grasshopper.Parameters;
using Grasshopper.Types.Fields;
using Grasshopper.UI;
using GrasshopperIO;
using GrasshopperIO.DataBase;
using Rhino.Geometry;

namespace Kampecaris;

[IoId("384EEECA-B305-417F-9449-ABAD5E451877")]
public sealed class FieldEndComponent : Component
{
    private Document? _document;
    internal double Scalar;
    internal Vector3d Vector;
    public FieldEndComponent() : base(new Nomen("Field End", "Create a user - defined field based on a simple explicit definition.", "Kampecaris", "Fields"))
    {
        DocumentChanged += OnDocumentChanged;
        ActivityChanged += FieldEndComponentActivityChanged;
    }
    public FieldEndComponent(IReader reader) : base(reader)
    {
        DocumentChanged += OnDocumentChanged;
        ActivityChanged += FieldEndComponentActivityChanged;
    }
    private void FieldEndComponentActivityChanged(object? sender, ObjectEventArgs e)
    {
        if (Activity == ObjectActivity.Disabled)
            _document?.Close();
    }
    private void OnDocumentChanged(object? sender, ObjectDocumentEventArgs e) =>
        _document?.Close();
    protected override void AddInputs(InputAdder inputs)
    {
        inputs.AddTopological("<", "<", "Connect to Field Input").DoCollect = true;
        inputs.AddNumber("Scalar", "Sc", "Field scalar magnitude at sampling location.", Access.Item,
            Requirement.MayBeMissing);
        inputs.AddVector("Vector", "Vc", "Field vector at sampling location", Access.Item,
            Requirement.MayBeMissing);
        inputs.AddText("Name", "Nm", "Field name.", Access.Item, Requirement.MayBeMissing);
    }
    protected override void AddOutputs(OutputAdder outputs) =>
        outputs.AddField("Field", "Fl", "User-defined field.");

    protected override void Process(IDataAccess access)
    {
        var isScalar = access.GetItem(1, out Scalar);
        var isVector = access.GetItem(2, out Vector);
        if (Document.State != DocumentState.Active) return;
        _document?.Close();
        access.GetTree(0, out Tree<Guid> tree);
        access.GetItem(3, out string name);
        if (!tree.AllItems.Any())
        {
            access.AddWarning("Field Input Component required.",
                "This component must be connected to a Field Input Component.");
            return;
        }
        if (tree.AllItems.Count() > 1)
        {
            access.AddWarning("Multiple Field Input Components connected.",
                "This component must be connected to a single Field Input Component.");
            return;
        }
        if (Document.Objects.Find(tree.AllItems.First()).ParentObject is not FieldStartComponent startComp)
        {
            access.AddWarning("Invalid Field Input Component connected.",
                "This component must be connected to a Field Input Component.");
            return;
        }
        var root = Node.Create("Document");
        Document.Store(root, FileContents.Small);
        var bytes = IO.WriteNodeToByteArray(root);
        var reader = IO.ReadNodeFromByteArray(bytes);
        _document = new Document(reader);
        var fieldStart = (FieldStartComponent)_document.Objects.Find(startComp.InstanceId);
        var fieldEnd = (FieldEndComponent)_document.Objects.Find(InstanceId);

        FieldType ft;
        if (isScalar && isVector)
            ft = FieldType.Both;
        else if (isScalar)
            ft = FieldType.Scalar;
        else if (isVector)
            ft = FieldType.Vector;
        else
            ft = FieldType.None;
        access.SetItem(0, new UserDefinedField(_document, name, ft, fieldStart, fieldEnd));
    }
}