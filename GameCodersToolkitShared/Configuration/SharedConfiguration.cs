using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EnvDTE80;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using GameCodersToolkitShared.Utils;

namespace GameCodersToolkit.Configuration
{
	public class CSharedUserConfig : BaseConfig
	{
		[Editable(allowEdit: true)]
		public string P4Server { get; set; }
		[Editable(allowEdit: true)]
		public string P4UserName { get; set; }
		[Editable(allowEdit: true)]
		public string P4Workspace { get; set; }
		[Editable(allowEdit: true)]
		public string AuthorName { get; set; }

		public string PostChangeScriptPath { get; set; }
		[JsonIgnore]
		public string PostChangeScriptAbsolutePath { get; set; }

		[Editable(allowEdit: true)]
		public string CMakeUpdateProject { get; set; } = "ZERO_CHECK";

		[Editable(allowEdit: true)]
		public bool UpdateCMakeAfterChange { get; set; }
	}

	public class CSharedConfiguration : ModuleBaseConfiguration
	{
		public const string cUserConfigFilePath = "GameCodersToolkit/SharedUserConfig.json";

		public CSharedUserConfig UserConfig { get { return GetConfig<CSharedUserConfig>(); } }

		public CSharedConfiguration()
		{
			ModuleName = "GameCodersToolkit";

			AddConfigFile<CSharedUserConfig>("UserConfig", cUserConfigFilePath);

			OnConfigLoadSucceeded += (s, e) => EstablishPerforceConnectionAsync();
		}

		public async Task<bool> EstablishPerforceConnectionAsync()
		{
			if (!string.IsNullOrWhiteSpace(UserConfig.P4Server))
			{
				PerforceID id = new PerforceID(UserConfig.P4Server, UserConfig.P4UserName, UserConfig.P4Workspace);
				return await PerforceConnection.InitAsync(id);
			}

			return false;
		}

		public async Task ExecutePostBuildStepsAsync()
		{
			if (File.Exists(UserConfig.PostChangeScriptAbsolutePath))
			{
				System.Diagnostics.Process.Start(UserConfig.PostChangeScriptAbsolutePath);
			}

			if (UserConfig.UpdateCMakeAfterChange)
			{
				await BuildPostChangeProjectAsync();
			}
		}

		public async Task BuildPostChangeProjectAsync()
		{
			await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				EnvDTE80.DTE2 dte = GameCodersToolkitPackage.Package.GetService<EnvDTE.DTE, DTE2>();
				if (dte != null)
				{
					bool isBuildingAlready = dte.Solution.SolutionBuild.BuildState == EnvDTE.vsBuildState.vsBuildStateInProgress;

					if (!isBuildingAlready && !string.IsNullOrWhiteSpace(UserConfig.CMakeUpdateProject))
					{
						var project = await VS.Solutions.FindProjectsAsync(UserConfig.CMakeUpdateProject);
						if (project != null)
						{
							await project.BuildAsync(BuildAction.Build);
						}
					}
				}
			});
		}

		protected override async Task PostConfigLoad(string name, Type type, object configObject)
		{
			if (type == typeof(CSharedUserConfig))
			{
				// Post-Change Script
				{
					if (!string.IsNullOrWhiteSpace(UserConfig.PostChangeScriptPath) && !Path.IsPathRooted(UserConfig.PostChangeScriptPath))
					{
						lock (SolutionFolder)
						{
							UserConfig.PostChangeScriptAbsolutePath = Path.Combine(SolutionFolder, UserConfig.PostChangeScriptPath);
						}

						UserConfig.PostChangeScriptAbsolutePath = Path.GetFullPath(UserConfig.PostChangeScriptAbsolutePath);
					}

					bool exists = File.Exists(UserConfig.PostChangeScriptAbsolutePath);
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[GameCodersToolkit] PostChangeScript at '{UserConfig.PostChangeScriptAbsolutePath}' (File exists: {exists})");
				}

				// CMake update project
				{
					bool exists = (await VS.Solutions.FindProjectsAsync(UserConfig.CMakeUpdateProject)) != null;
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[GameCodersToolkit] CMakeUpdateProject is '{UserConfig.CMakeUpdateProject}' (Exists: {exists}), UpdateCMakeAfterChange: {UserConfig.UpdateCMakeAfterChange}");
				}
			}
		}
	}
}
