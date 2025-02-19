using System;
using System.Reflection;
using Grasshopper2.Framework;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;


namespace Kampecaris.Kampecaris;

public sealed class KampecarisPluginInfo : Plugin
{
    private static T GetAttribute<T>() where T : Attribute => typeof(KampecarisPluginInfo).Assembly.GetCustomAttribute<T>();
    public KampecarisPluginInfo()
        : base(new Guid("c3ced770-34da-4144-8e60-9a96d679b9ac"), new Nomen("Kampecaris", 
                "Kampecaris"), new Version(2, 0, 1))
    {
        Icon = AbstractIcon.FromResource("KampecarisPlugin", typeof(KampecarisPluginInfo));
    }
    public override string Author => "Mahdiyar";
    public override IIcon Icon { get; }
    public override string Copyright => "© 2025 Mahdiyar. Licensed under the MIT License.";
    public override string Website => "https://mahdiyar.io";
    public override string Contact => "info@mahdiyar.io";
}
