using GameCodersToolkit.Utils;
using Perforce.P4;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.SourceControl
{
	public class PerforceWorkspace
	{
		public string Name { get; set; } = string.Empty;
		public string Root { get; set; } = string.Empty;
		public bool IsValid { get; set; } = false;
	}

	public class PerforceID
	{
		public PerforceID(string serverURI, string user_name, string client_spec)
		{
			mServerURI = serverURI;
			mUserName = user_name;
			mClientSpec = client_spec;
		}
		private string mServerURI;
		public string ServerURI
		{
			get { return mServerURI; }
		}
		private string mUserName;
		public string UserName
		{
			get { return mUserName; }
		}
		private string mClientSpec;
		public string ClientSpec
		{
			get { return mClientSpec; }
		}

		public bool IsIdenticalTo(PerforceID other)
		{
			return ServerURI == other.ServerURI && UserName == other.UserName && ClientSpec == other.ClientSpec;
		}
	}

	public class PerforceConnection
	{
		private static Connection s_perforceConnection;
		private static Repository s_repository;
		private static PerforceID s_currentId;

		public static bool IsEnabled { get; set; } = true;

		public static async Task<bool> InitAsync(PerforceID id)
		{
			if (!IsEnabled)
				return false;

            bool hasConnection = s_perforceConnection != null && s_currentId != null && s_perforceConnection.Status == ConnectionStatus.Connected;
            bool isConnectionDifferent = !hasConnection || !s_currentId.IsIdenticalTo(id);

            if (hasConnection && !isConnectionDifferent)
                return true;

            return await Task.Run(async () =>
			{
				await ShutdownAsync();

				Server server = new Server(new ServerAddress(id.ServerURI));
				s_repository = new Repository(server);
				s_perforceConnection = s_repository.Connection;

				s_perforceConnection.UserName = id.UserName;
				s_perforceConnection.Client = new Client();
				s_perforceConnection.Client.Name = id.ClientSpec;

				string path = Directory.GetCurrentDirectory();

				Options opconnect = new Options();
				try
				{
					bool result = s_perforceConnection.Connect(opconnect);
					if (result)
					{
						s_currentId = id;
					}
					return result;
				}
				catch (Exception ex)
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
						"Exception initializing Perforce connection", 
						ex);

					return false;
				}
			});
		}

		public static async Task<bool> ShutdownAsync()
        {
            if (!IsEnabled)
                return false;

            return await Task.Run(() =>
			{
				if (s_perforceConnection != null && s_perforceConnection.Status == ConnectionStatus.Connected)
				{
					bool result = s_perforceConnection.Disconnect();
					s_perforceConnection = null;

					return result;
				}

				return true;
			});
		}

		public static async Task<bool> TryAddFilesAsync(IList<string> filePaths)
        {
            if (!IsEnabled)
                return false;

            return await Task.Run(async () =>
			{
				if (s_perforceConnection == null || s_perforceConnection.Status == ConnectionStatus.Disconnected)
					return false;

				Options options = new Options();
				FileSpec[] fileSpecs = new FileSpec[filePaths.Count];

				for (int i = 0; i < filePaths.Count; i++)
				{
					fileSpecs[i] = new FileSpec(new ClientPath(filePaths[i]));
				}

				try
				{
					IList<FileSpec> files = s_perforceConnection.Client.AddFiles(options, fileSpecs);
					return files != null && files.Count > 0;
				}
				catch (Exception ex)
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
						"Exception adding files to Perforce",
						ex);

					return false;
				}
			});
		}

		public static async Task<bool> TryCheckoutFilesAsync(IList<string> filePaths)
        {
            if (!IsEnabled)
                return false;

            return await Task.Run(async () =>
			{
				if (s_perforceConnection == null || s_perforceConnection.Status == ConnectionStatus.Disconnected)
					return false;

				Options options = new Options();
				FileSpec[] fileSpecs = new FileSpec[filePaths.Count];

				for (int i = 0; i < filePaths.Count; i++)
				{
					fileSpecs[i] = new FileSpec(new ClientPath(filePaths[i]));
				}

				try
				{
					IList<FileSpec> files = s_perforceConnection.Client.EditFiles(options, fileSpecs);
					return true;
				}
				catch (Exception ex)
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
						"Exception checking out file from Perforce",
						ex);

					return false;
				}
			});
		}

		public static async Task<PerforceWorkspace> FindWorkspaceAsync(string currentDirectory)
        {
            if (!IsEnabled)
                return null;

            return await Task.Run(() =>
			{
				PerforceWorkspace foundWorkspace = new PerforceWorkspace();

				ClientsCmdOptions opts = new ClientsCmdOptions(ClientsCmdFlags.None, null, null, 0, "");
				foreach (Client client in s_repository?.GetClients(opts))
				{
					if (!currentDirectory.ToLower().StartsWith(client.Root.ToLower()))
					{
						continue;
					}

					foundWorkspace.Name = client.Name;
					foundWorkspace.Root = client.Root;
					break;
				}

				return foundWorkspace;
			});
		}
	}
}
