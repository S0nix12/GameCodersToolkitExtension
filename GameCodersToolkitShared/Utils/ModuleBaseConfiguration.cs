using GameCodersToolkit;
using GameCodersToolkit.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameCodersToolkitShared.Utils
{
	public class ConfigFile
	{
		public string Name { get; set; }
		public string RelativePath { get; set; }
		public FileSystemWatcher FileSystemWatcher { get; set; }
		public Type Type { get; set; }
		public object ConfigObject { get; set; }
	}

	public class BaseConfig { }

	public class ConfigFileEventArgs : EventArgs
	{
		public ConfigFileEventArgs(ConfigFile configFile)
		{
			ConfigFile = configFile;
		}

		public ConfigFile ConfigFile { get; }
	}

	public class ModuleBaseConfiguration
	{
		protected void AddConfigFile<T>(string name, string relativePath) where T : BaseConfig, new()
		{
			ConfigFiles.Add(new ConfigFile() { Name = name, RelativePath = relativePath, ConfigObject = new T(), Type = typeof(T) });
		}

		// Returns the first config with the given name and type
		public T GetConfig<T>(string name) where T : BaseConfig
		{
			foreach (var config in ConfigFiles)
			{
				if (config.Name == name && config.Type == typeof(T))
				{
					return config.ConfigObject as T;
				}
			}

			return null;
		}

		// Returns the first config of given type
		public T GetConfig<T>() where T : BaseConfig
		{
			foreach (var config in ConfigFiles)
			{
				if (config.Type == typeof(T))
				{
					return config.ConfigObject as T;
				}
			}

			return null;
		}

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

		private void HandleOpenSolution(Solution solution = null)
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
			//Clear any potential old watchers
			foreach (var config in ConfigFiles)
			{
				config.FileSystemWatcher?.Dispose();
				config.FileSystemWatcher = null;
			}

			await LoadConfigsAsync();

			List<string> watchedDirectories = [];
			foreach (var configFile in ConfigFiles)
			{
				string configFilePath = GetConfigFilePath(configFile);
				string configDirectory = Path.GetDirectoryName(configFilePath);

				if (!watchedDirectories.Contains(configDirectory))
				{
					configFile.FileSystemWatcher = new FileSystemWatcher(configDirectory)
					{
						EnableRaisingEvents = true
					};
					configFile.FileSystemWatcher.Changed += OnConfigFileChanged;
					configFile.FileSystemWatcher.IncludeSubdirectories = false;
					configFile.FileSystemWatcher.Filter = "*.json";
					configFile.FileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
					watchedDirectories.Add(configDirectory);
				}
			}
		}

		private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
		{
			ThreadHelper.JoinableTaskFactory.Run(LoadConfigsAsync);
		}

		public string GetConfigFilePath(ConfigFile configFile)
		{
			string result = "";

			lock (SolutionFolder)
			{
				result = Path.Combine(SolutionFolder, configFile.RelativePath);
			}

			return result;
		}

		protected virtual async Task PostConfigLoad(string name, Type type, object configObject)
		{

		}

		private async Task LoadConfigsAsync()
		{
			foreach (var configFile in ConfigFiles)
			{
				bool shouldSaveConfigAfterLoad = false;
				string configFilePath = GetConfigFilePath(configFile);

				OnPreConfigLoad?.Invoke(this, new ConfigFileEventArgs(configFile));

				try
				{
					if (configFile.ConfigObject != null)
					{
						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[{ModuleName}] Reloading {ModuleName}::{configFile.Name} config...");
					}
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[{ModuleName}] Attempting to load {ModuleName}::{configFile.Name} config at '{configFile}'...");

					if (File.Exists(configFilePath))
					{
						FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
						using var fileStream = new FileStream(
							configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

						try
						{
							configFile.ConfigObject = await JsonSerializer.DeserializeAsync(fileStream, configFile.Type);
						}
						catch (JsonException e)
                        {
							// If the file couldn't be read, create a blank slate
							configFile.ConfigObject = Activator.CreateInstance(configFile.Type);
							shouldSaveConfigAfterLoad = true;
                        }

						await PostConfigLoad(configFile.Name, configFile.Type, configFile.ConfigObject);
						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[AutoDataExposer] Finished loading config.");
					}
					else
					{
						shouldSaveConfigAfterLoad = true;
                    }

					OnConfigLoadSucceeded?.Invoke(this, new ConfigFileEventArgs(configFile));
				}
				catch (Exception ex)
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
						$"Exception while loading {ModuleName}::{configFile.Name} config file!",
						ex);

					OnConfigLoadFailed?.Invoke(this, new ConfigFileEventArgs(configFile));
				}

				if (shouldSaveConfigAfterLoad)
				{
                    Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
                    SaveConfig(configFile);
                }
			}
		}

		public void SaveAllConfigs()
		{
			var options = new JsonSerializerOptions { WriteIndented = true };
			foreach (var configFile in ConfigFiles)
			{
				SaveConfig(configFile);
			}
		}

		public void SaveConfig(string name)
		{
			ConfigFile configFile = ConfigFiles.Where(file => file.Name == name).FirstOrDefault();
			if (configFile != null)
			{
				SaveConfig(configFile);
			}
		}

		public void SaveConfig<T>()
		{
			ConfigFile configFile = ConfigFiles.Where(file => file.Type == typeof(T)).FirstOrDefault();
			if (configFile != null)
			{
				SaveConfig(configFile);
			}
		}

		private void SaveConfig(ConfigFile configFile)
		{
			string configFilePath = GetConfigFilePath(configFile);
			bool isWritable = !File.Exists(configFilePath) || configFilePath.IsFileWritable();

			if (isWritable)
			{
				using FileStream fileStream = File.Create(configFilePath);

				var options = new JsonSerializerOptions { WriteIndented = true };
				JsonSerializer.Serialize(fileStream, configFile.ConfigObject, options);
			}
		}

		public event EventHandler<ConfigFileEventArgs> OnPreConfigLoad;
		public event EventHandler<ConfigFileEventArgs> OnConfigLoadFailed;
		public event EventHandler<ConfigFileEventArgs> OnConfigLoadSucceeded;

		public string ModuleName { get; protected set; }
		protected string SolutionFolder { get; private set; } = "";
		private List<ConfigFile> ConfigFiles { get; set; } = [];
	}
}
