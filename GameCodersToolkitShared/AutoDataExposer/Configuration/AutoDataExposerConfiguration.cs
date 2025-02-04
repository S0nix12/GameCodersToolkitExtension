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
        public string Regex { get; set; }
    }

    public class CAutoDataExposerConfig
    {
        public List<CAutoDataExposerEntry> AutoDataExposerEntries { get; set; } = new List<CAutoDataExposerEntry>();
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

        private void DynamicItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            if (GameCodersToolkitPackage.Package.GetService<IMenuCommandService, OleMenuCommandService>() is OleMenuCommandService commandService)
            {
                foreach (OleMenuCommand command in RegisteredCommands)
                {
                    commandService.RemoveCommand(command);
                }

                RegisteredCommands.Clear();
            }

            ThreadHelper.ThrowIfNotOnUIThread();
            var cmd = sender as OleMenuCommand;
            if (cmd == null)
                return;

            cmd.Visible = false;

            if (GameCodersToolkitPackage.Package.GetService<IMenuCommandService, OleMenuCommandService>() is OleMenuCommandService cmdService)
            {
                for (int i = 0; i < ExposerConfig.AutoDataExposerEntries.Count; i++)
                {
                    CAutoDataExposerEntry entry = ExposerConfig.AutoDataExposerEntries[i];

                    CommandID dynamicStartCmdID = new CommandID(PackageGuids.guidMyCmdSet, 0x2000 + i + 1);
                    OleMenuCommand dynamicItem = new OleMenuCommand(DynamicItem_Invoke, dynamicStartCmdID);
                    dynamicItem.Enabled = true;
                    dynamicItem.Visible = true;
                    dynamicItem.Text = entry.Name;

                    cmdService.AddCommand(dynamicItem);
                    RegisteredCommands.Add(dynamicItem);
                }
            }
        }

        private void DynamicItem_Invoke(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var cmd = sender as OleMenuCommand;
            if (cmd == null)
                return;

            int commandIndex = cmd.CommandID.ID - 0x1000;
            if (commandIndex >= 0 && commandIndex < ExposerConfig.AutoDataExposerEntries.Count)
            {
                ExecuteCommand(ExposerConfig.AutoDataExposerEntries[commandIndex]);
            }
        }

        private bool IsLineMatchingRegex(string pattern)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(ServiceProvider.GlobalProvider.GetService(typeof(SVsTextManager)) is IVsTextManager textManager))
                return false;

            textManager.GetActiveView(1, null, out IVsTextView textView);
            if (textView == null)
                return false;

            textView.GetSelection(out int startLine, out _, out _, out _);

            if (!(textView.GetBuffer(out IVsTextLines textLines) == VSConstants.S_OK && textLines != null))
                return false;

            textLines.GetLineText(startLine, 0, startLine, int.MaxValue, out string lineText);

            return Regex.IsMatch(lineText.Trim(), pattern);
        }
        private void ExecuteCommand(CAutoDataExposerEntry commandConfig)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VsShellUtilities.ShowMessageBox(
                GameCodersToolkitPackage.Package,
                $"Command {commandConfig.Name} executed!",
                "Dynamic Command",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
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

                    //Create context menu items
                    {
                        await GameCodersToolkitPackage.Package.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (await GameCodersToolkitPackage.Package.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService cmdService)
                        {
                            CommandID dynamicStartCmdID = new CommandID(PackageGuids.guidMyCmdSet, 0x2000);
                            OleMenuCommand dynamicItem = new OleMenuCommand(null, dynamicStartCmdID);
                            dynamicItem.BeforeQueryStatus += DynamicItem_BeforeQueryStatus;
                            dynamicItem.MatchedCommandId = 0x2000; // Ensures dynamic commands match correctly

                            cmdService.AddCommand(dynamicItem);
                            RegisteredCommands.Add(dynamicItem);
                        }
                    }

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
