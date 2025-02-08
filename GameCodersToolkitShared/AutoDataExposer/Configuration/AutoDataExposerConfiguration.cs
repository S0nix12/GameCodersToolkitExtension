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

	public class CAutoDataExposerConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "AutoDataExposer/AutoDataExposerConfig.json";

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
			string configFilePath = GetConfigFilePath();
			await LoadConfigAsync(configFilePath);

			string configDirectory = Path.GetDirectoryName(configFilePath);

			ConfigFileWatcher?.Dispose();

			ConfigFileWatcher = new FileSystemWatcher(configDirectory)
			{
				EnableRaisingEvents = true
			};
			ConfigFileWatcher.Changed += OnConfigFileChanged;
			ConfigFileWatcher.IncludeSubdirectories = false;
			ConfigFileWatcher.Filter = "*.json";
			ConfigFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
		}

		private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate
			{
				await LoadConfigAsync(GetConfigFilePath());
			});
		}

		public string GetConfigFilePath()
		{
			lock (SolutionFolder)
			{
				return Path.Combine(SolutionFolder, cConfigFilePath);
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


		private async Task LoadConfigAsync(string exposerConfigFilePath)
		{
			try
			{
				if (ExposerConfig != null)
				{
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Reloading AutoDataExposer config after change");
				}
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Attempting to load AutoDataExposer config at '{exposerConfigFilePath}'");

				if (File.Exists(exposerConfigFilePath))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
						exposerConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					ExposerConfig = await JsonSerializer.DeserializeAsync<CAutoDataExposerConfig>(fileStream);
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Finished loading config.");
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(exposerConfigFilePath));
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
			string configFilePath = GetConfigFilePath();

			bool isWritable = !File.Exists(configFilePath) || configFilePath.IsFileWritable();

			if (isWritable)
			{
				using FileStream fileStream = File.Create(configFilePath);
				JsonSerializer.Serialize(fileStream, ExposerConfig, options);
			}

			return isWritable;
		}

		public CAutoDataExposerConfig ExposerConfig { get; set; } = new CAutoDataExposerConfig();
		private FileSystemWatcher ConfigFileWatcher { get; set; }

		private string SolutionFolder { get; set; } = "";

		private List<OleMenuCommand> RegisteredCommands { get; set; } = new List<OleMenuCommand>();
	}
}
