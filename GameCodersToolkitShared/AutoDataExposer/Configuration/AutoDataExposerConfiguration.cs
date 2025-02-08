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

	public class CAutoDataExposerConfig
	{
		public List<CAutoDataExposerEntry> AutoDataExposerEntries { get; set; } = new List<CAutoDataExposerEntry>();
		public List<CAutoDataExposerDefaultValue> DefaultValues { get; set; }
	}

	public class CAutoDataExposerUserConfig
	{
		[Editable(allowEdit: true)]
		public string AuthorName { get; set; }
		[Editable(allowEdit: true)]
		public bool JumpToGeneratedCode { get; set; }
	}

	public class CAutoDataExposerConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "AutoDataExposer/AutoDataExposerConfig.json";
		public const string cUserConfigFilePath = "AutoDataExposer/AutoDataExposerUserConfig.json";

		public event EventHandler OnPreConfigLoad;

		public async Task InitAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			bool isSolutionOpen = await VS.Solutions.IsOpenAsync();

			if (isSolutionOpen)
			{
				HandleOpenSolution(await VS.Solutions.GetCurrentSolutionAsync());
			}

			VS.Events.SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;
		}

		public void Reload()
		{
			ThreadHelper.JoinableTaskFactory.Run(LoadSolutionConfigAsync);
		}

		private void HandleOpenSolution(Community.VisualStudio.Toolkit.Solution solution = null)
		{
			if (solution != null)
			{
				lock (SolutionFolder)
				{
					SolutionFolder = Path.GetDirectoryName(solution.FullPath);
				}
			}
			ThreadHelper.JoinableTaskFactory.Run(LoadSolutionConfigAsync);
		}

		public async Task LoadSolutionConfigAsync()
		{
			string[] configFilePaths = GetConfigFilePaths();
			await LoadConfigsAsync(configFilePaths);

			string configDirectory = Path.GetDirectoryName(configFilePaths[0]);

			ConfigFileWatcher?.Dispose();

			ConfigFileWatcher = new FileSystemWatcher(configDirectory)
			{
				EnableRaisingEvents = true
			};
			ConfigFileWatcher.Changed += OnConfigFileChanged;
			ConfigFileWatcher.IncludeSubdirectories = false;
			ConfigFileWatcher.Filter = "*.json";
			ConfigFileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
		}

		private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await LoadConfigsAsync(GetConfigFilePaths());
			});
		}

		public string[] GetConfigFilePaths()
		{
			lock (SolutionFolder)
			{
				return [Path.Combine(SolutionFolder, cConfigFilePath), Path.Combine(SolutionFolder, cUserConfigFilePath)];
			}
		}

		public CAutoDataExposerEntry FindExposerEntryByName(string name)
		{
			foreach (CAutoDataExposerEntry entry in ExposerConfig.AutoDataExposerEntries)
			{
				if (entry.Name == name)
				{
					return entry;
				}
			}

			return null;
		}


		private async Task LoadConfigsAsync(string[] exposerConfigFilePaths)
		{
			OnPreConfigLoad?.Invoke(this, new EventArgs());

			try
			{
				if (ExposerConfig != null || ExposerUserConfig != null)
				{
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Reloading AutoDataExposer config after change");
				}
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Attempting to load AutoDataExposer config at '{exposerConfigFilePaths[0]}' and '{exposerConfigFilePaths[1]}'");

				// Global config
				if (File.Exists(exposerConfigFilePaths[0]))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
						exposerConfigFilePaths[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					ExposerConfig = await JsonSerializer.DeserializeAsync<CAutoDataExposerConfig>(fileStream);
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Finished loading config.");
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(exposerConfigFilePaths[0]));
					File.Create(exposerConfigFilePaths[0]).Dispose();
				}

				if (File.Exists(exposerConfigFilePaths[1]))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
						exposerConfigFilePaths[1], FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					ExposerUserConfig = await JsonSerializer.DeserializeAsync<CAutoDataExposerUserConfig>(fileStream);

					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Finished loading user config.");
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(exposerConfigFilePaths[1]));
					await SaveConfigAsync();
				}
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Exception while loading AutoDataExposer Config File",
					ex);
			}
		}

		public async Task<bool> SaveConfigAsync()
		{
			var options = new JsonSerializerOptions { WriteIndented = true };
			string[] configFilePaths = GetConfigFilePaths();

			bool isWritable = !File.Exists(configFilePaths[1]) || configFilePaths[1].IsFileWritable();

			if (isWritable)
			{
				using FileStream fileStream = File.Create(configFilePaths[1]);
				JsonSerializer.Serialize(fileStream, ExposerUserConfig, options);
			}

			return isWritable;
		}

		public CAutoDataExposerConfig ExposerConfig { get; set; } = new CAutoDataExposerConfig();
		public CAutoDataExposerUserConfig ExposerUserConfig { get; set; } = new CAutoDataExposerUserConfig();

		private FileSystemWatcher ConfigFileWatcher { get; set; }

		private string SolutionFolder { get; set; } = "";

		private List<OleMenuCommand> RegisteredCommands { get; set; } = new List<OleMenuCommand>();
	}
}
