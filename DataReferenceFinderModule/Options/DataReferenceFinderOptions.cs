using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	internal partial class OptionsProvider
	{
		// Register the options with this attribute on your package class:
		// [ProvideOptionPage(typeof(OptionsProvider.DataReferenceFinderOptionsOptions), "GameCodersToolkit.DataReferenceFinderModule.Options", "DataReferenceFinderOptions", 0, 0, true, SupportsProfiles = true)]
		[ComVisible(true)]
		public class DataReferenceFinderOptionsOptions : BaseOptionPage<DataReferenceFinderOptions> { }
	}

	public class DataReferenceFinderOptions : BaseOptionModel<DataReferenceFinderOptions>
	{
		[Category("Data Editor Connection")]
		[DisplayName("Enable Socket Keep Alive Pong")]
		[Description("If active enables sending keep alive pongs when the Data Editor Socket is connected")]
		[DefaultValue(false)]
		public bool DataEditorSocketEnableKeepAlivePong { get; set; } = false;

		[Category("Data Editor Connection")]
		[DisplayName("Socket Keep Alive Interval")]
		[Description("If sending keep alive pongs define the interval between them in seconds")]
		[DefaultValue(30.0)]
		public double DataEditorSocketKeepAliveInterval { get; set; } = 30;

		[Category("Data Editor Connection")]
		[DisplayName("Auto Connect")]
		[Description("If enabled automatically try periodically to connect to the Data Edtior Socket defined in the configuration")]
		[DefaultValue(true)]
		public bool DataEditorSocketAutoConnect { get; set; } = true;

		[Category("Data Editor Connection")]
		[DisplayName("Auto Connect Interval")]
		[Description("If auto connect is enabled defines the interval in seconds in which connection is tried to establish")]
		[DefaultValue(5.0)]
		public double DataEditorSocketAutoConnectInterval { get; set; } = 5.0f;
	}
}
