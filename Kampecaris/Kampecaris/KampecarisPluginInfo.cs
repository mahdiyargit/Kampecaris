using Grasshopper.Framework;
using Grasshopper.UI;
using Grasshopper.UI.Icon;
using Kampecaris.Properties;

namespace Kampecaris.Kampecaris
{
    public sealed class KampecarisPluginInfo : StandardRmaPlugin
    {
        public KampecarisPluginInfo() : base(
            new Guid("782A7EC3-1F5A-4A86-A429-2B658F4CB64E"), new Nomen("Kampecaris", "Kampecaris", "Kampecaris"), AbstractIcon.FromStream(new MemoryStream(Resources.Kampecaris)))
        {
        }
    }
}