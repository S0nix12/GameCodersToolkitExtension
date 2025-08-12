using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameCodersToolkit.QuickAutotest
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class QuickAutotestOptionsOptions : BaseOptionPage<QuickAutotestOptions> { }
    }

    public class QuickAutotestOptions : BaseOptionModel<QuickAutotestOptions>
    {
        [Category("Quick Autotest")]
        [DisplayName("Run Tests on Select")]
        [Description("If enabled, an autotest script will be run when selected in the list, skipping the need to confirm by pressing the button.")]
        [DefaultValue(false)]
        public bool RunTestsOnSelect { get; set; } = false;
    }
}
