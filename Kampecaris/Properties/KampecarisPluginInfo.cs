using System;
using System.Reflection;
using Grasshopper.Framework;
using Grasshopper.UI;
using Grasshopper.UI.Icon;


namespace Kampecaris.Kampecaris;

public sealed class KampecarisPluginInfo : Plugin
{
    private static T GetAttribute<T>() where T : Attribute => typeof(KampecarisPluginInfo).Assembly.GetCustomAttribute<T>();
    public KampecarisPluginInfo()
        : base(new Guid("c3ced770-34da-4144-8e60-9a96d679b9ac"), new Nomen(
                GetAttribute<AssemblyTitleAttribute>().Title,
                GetAttribute<AssemblyDescriptionAttribute>().Description),
            typeof(KampecarisPluginInfo).Assembly.GetName().Version)
    => Icon = AbstractIcon.FromResource("KampecarisPlugin", typeof(KampecarisPluginInfo));
    public override string Author => GetAttribute<AssemblyCompanyAttribute>().Company;
    public override IIcon Icon { get; }
    public override string Copyright => GetAttribute<AssemblyCopyrightAttribute>().Copyright;
    public override string Website => "https://mahdiyar.io";
    public override string Contact => "info@mahdiyar.io";
}
