using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using System.Linq;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using EnvDTE80;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading;
using Microsoft.VisualStudio.RpcContracts.Commands;
using System.ComponentModel.DataAnnotations;
using GameCodersToolkitShared.ModuleUtils;

namespace GameCodersToolkit.Configuration
{
	public class CAutoDataExposerEntry
	{
		public string Name { get; set; }
		public string LineValidityRegex { get; set; }
		public string TargetFunctionRegex { get; set; }
		public string FunctionNameRegex { get; set; }
		public string FunctionReturnValueRegex { get; set; }
		public string FunctionArgumentsRegex { get; set; }
		public string OutParamLine { get; set; }
		public string InParamLine { get; set; }
		public string ExposeString { get; set; }
		public string DefaultValueFormat { get; set; }
	}

	public class CAutoDataExposerDefaultValue
	{
		public string TypeName { get; set; }
		public string DefaultValue { get; set; }
	}

	public class CAutoDataExposerConfig : BaseConfig
	{
		public List<CAutoDataExposerEntry> AutoDataExposerEntries { get; set; } = new List<CAutoDataExposerEntry>();
		public List<CAutoDataExposerDefaultValue> DefaultValues { get; set; }

		public CAutoDataExposerEntry FindExposerEntryByName(string name)
		{
			foreach (CAutoDataExposerEntry entry in AutoDataExposerEntries)
			{
				if (entry.Name == name)
				{
					return entry;
				}
			}

			return null;
		}
	}

	public class CAutoDataExposerUserConfig : BaseConfig
	{
		[Editable(allowEdit: true)]
		public string AuthorName { get; set; }
		[Editable(allowEdit: true)]
		public bool JumpToGeneratedCode { get; set; }
	}

	public class CAutoDataExposerConfiguration : ModuleBaseConfiguration
	{
		public const string cConfigFilePath = "AutoDataExposer/AutoDataExposerConfig.json";
		public const string cUserConfigFilePath = "AutoDataExposer/AutoDataExposerUserConfig.json";

		public CAutoDataExposerConfiguration()
		{
			ModuleName = "AutoDataExposer";

			AddConfigFile<CAutoDataExposerConfig>("MainConfig", cConfigFilePath);
			AddConfigFile<CAutoDataExposerUserConfig>("UserConfig", cUserConfigFilePath);
		}
	}
}
