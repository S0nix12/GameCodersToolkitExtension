using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio.Threading;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebsocket;

namespace GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication
{
	public class ConnectionStatusChangedEventArgs : EventArgs
	{
		public ConnectionStatusChangedEventArgs(bool isConnected) { IsConnected = isConnected; }
		public bool IsConnected { get; set; }
	}

	public class DataEditorClientSocket : IDisposable
	{
		public DataEditorClientSocket(string serverAddress)
		{
			m_socket = new WatsonWsClient(new Uri(serverAddress));
			m_socket.ConfigureOptions(options => options.AddSubProtocol("GameCodersToolkit.DataReferenceFinder"));
			m_socket.ServerDisconnected += ServerDisconnected;
			m_socket.ServerConnected += ServerConnected;
			ServerAddress = serverAddress;
		}

		public void StartBackgroundAutoConnection(bool restartWhenClosed)
		{
			lock (m_mutex)
			{
				m_restartConnectionWhenClosed = restartWhenClosed;
				if (IsAutoConnecting)
					return;

				m_autoConnectionTask = Task.Run(() => AutoConnectionAsync(m_autoConnectionCancelSource.Token), m_autoConnectionCancelSource.Token);
			}
		}

		public async Task StartSocketAsync()
		{
			if (!m_socket.Connected)
			{
				await m_socket.StartAsync();
			}
		}

		public async Task SendMessageToDataEditorAsync<TValue>(TValue jsonObject)
		{
			if (!m_socket.Connected)
			{
				await m_socket.StartAsync();
				if (!m_socket.Connected)
				{
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("Could not send Message to DataEditor. No connection possible");
					return;
				}
			}

			var options = new JsonSerializerOptions { WriteIndented = true };

			byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(jsonObject, options);
			await m_socket.SendAsync(new ArraySegment<byte>(jsonBytes), WebSocketMessageType.Text, CancellationToken.None);
		}

		public async Task CloseDataEditorConnectionAsync()
		{
			m_autoConnectionCancelSource.Cancel();
			m_restartConnectionWhenClosed = false;
			await m_socket.StopAsync();
		}

		public void Dispose()
		{
			m_autoConnectionCancelSource.Cancel();
			if (m_socket.Connected)
			{
				m_socket.Stop();
			}
			m_socket.Dispose();
		}

		async Task AutoConnectionAsync(CancellationToken cancelToken)
		{
			while (!m_socket.Connected && !cancelToken.IsCancellationRequested)
			{
				if (await m_socket.StartWithTimeoutAsync(token: cancelToken))
				{
					break;
				}

				await Task.Delay(2000);
			}

			lock (m_mutex)
			{
				m_autoConnectionTask = null;
			}
		}

		void ServerConnected(object sender, EventArgs e)
		{
			ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(true));
		}

		void ServerDisconnected(object sender, EventArgs args)
		{
			if (m_restartConnectionWhenClosed)
			{
				StartBackgroundAutoConnection(m_restartConnectionWhenClosed);
			}
			ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false));
		}

		public EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged { get; set; }
		public bool IsConnected { get => m_socket.Connected; }
		public bool IsAutoConnecting { get => m_autoConnectionTask != null; }
		public string ServerAddress { get; private set; }

		CancellationTokenSource m_autoConnectionCancelSource = new CancellationTokenSource();

		Task m_autoConnectionTask = null;
		bool m_restartConnectionWhenClosed = false;
		WatsonWsClient m_socket;

		object m_mutex = new();
	}

	public class DataEditorConnection
	{
		public DataEditorConnection()
		{
			GameCodersToolkitPackage.DataLocationsConfig.ConfigLoaded += OnConfigLoaded;
			CreateSocket();
			m_clientSocket?.StartBackgroundAutoConnection(true);
		}

		public async Task OpenInDataEditorAsync(DataEntry dataEntry)
		{
			if (m_clientSocket == null || !m_clientSocket.IsConnected)
				return;

			OpenDataEntryMessage message = new OpenDataEntryMessage(dataEntry);
			try
			{
				await m_clientSocket.SendMessageToDataEditorAsync(message);
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("Send Open Command to Editor for: " + dataEntry.Identifier);
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Exception Opening Data Entry in Editor",
					ex);
			}
		}

		void CreateSocket()
		{
			try
			{
				bool wasConnected = IsConnectedToDataEditor;
				m_clientSocket?.Dispose();
				if (wasConnected)
				{
					DataEditorConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false));
				}

				string serverAddress = GameCodersToolkitPackage.DataLocationsConfig.GetDataEditorServerUri();
				if (!string.IsNullOrWhiteSpace(serverAddress))
				{
					m_clientSocket = new DataEditorClientSocket(serverAddress);
					m_clientSocket.ConnectionStatusChanged += OnConnectionStatusChanged;
					m_clientSocket?.StartBackgroundAutoConnection(true);
				}
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory.Run(() => DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Failed to create DateEditorConnection Socket",
					ex));
			}
		}

		void OnConfigLoaded(object sender, EventArgs e)
		{
			string serverAddress = GameCodersToolkitPackage.DataLocationsConfig.GetDataEditorServerUri();
			if (m_clientSocket == null || m_clientSocket.ServerAddress != serverAddress)
			{
				CreateSocket();
			}
		}

		void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
		{
			DataEditorConnectionStatusChanged?.Invoke(this, args);
		}

		public EventHandler<ConnectionStatusChangedEventArgs> DataEditorConnectionStatusChanged { get; set; }
		public bool IsConnectedToDataEditor { get => m_clientSocket != null ? m_clientSocket.IsConnected : false; }

		static DataEditorClientSocket? m_clientSocket;
	}
}
