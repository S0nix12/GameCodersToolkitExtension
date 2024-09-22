using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameCodersToolkit.QuickAttach
{
	internal partial class OptionsProvider
	{
		[ComVisible(true)]
		public class QuickAttachOptionsOptions : BaseOptionPage<QuickAttachOptions> { }
	}

	public class QuickAttachOptions : BaseOptionModel<QuickAttachOptions>
	{
		[Category("Quick Attach")]
		[DisplayName("Process Filters")]
		[Description("String a process name should include to get listed in the quick attach options. Multiple can be added separated by commas")]
		[DefaultValue("")]
		public string ProcessFilters { get; set; } = "";
	}
}
