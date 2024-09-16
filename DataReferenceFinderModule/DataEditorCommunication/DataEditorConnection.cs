using EnvDTE;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio.Threading;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication
{
	public class ConnectionStatusChangedEventArgs : EventArgs
	{
		public ConnectionStatusChangedEventArgs(bool isConnected) { IsConnected = isConnected; }
		public bool IsConnected { get; set; }
	}

	public enum ESocketStatus
	{
		Inactive,
		Connecting,
		Open
	}

	public class DataEditorClientSocket : IDisposable
	{
		public DataEditorClientSocket()
		{

		}

		public void Dispose()
		{
			m_tryReconnect = false;
			m_pendingConnectionCancellationSource?.Cancel();
			m_reciveLoopCancellationSource?.Cancel();

			m_pendingConnectionLoop?.Wait();
			m_receiveLoop?.Wait();
			m_socket?.Dispose();
			SocketStatus = ESocketStatus.Inactive;
		}

		public async Task StartAsync(Uri address, bool tryReconnect)
		{
			if (SocketStatus != ESocketStatus.Inactive)
			{
				await StopAsync();
			}

			lock (m_mutex)
			{
				m_tryReconnect = tryReconnect;
				m_requestedAddress = address;
				m_socket = new ClientWebSocket();
				m_socket.Options.AddSubProtocol("GameCodersToolkit.DataReferenceFinder");
				SocketStatus = ESocketStatus.Connecting;

				m_pendingConnectionCancellationSource = new CancellationTokenSource();
				m_pendingConnectionLoop = Task.Run(PendingConnectLoopAsync).ContinueWith(StartReceiveLoop, TaskScheduler.Default);
			}
		}

		public async Task SendMessageToDataEditorAsync<TValue>(TValue jsonObject)
		{
			if (SocketStatus != ESocketStatus.Open)
			{
				return;
			}

			var options = new JsonSerializerOptions { WriteIndented = true };

			byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(jsonObject, options);
			await m_socket.SendAsync(new ArraySegment<byte>(jsonBytes), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		public async Task StopAsync()
		{
			lock (m_mutex)
			{
				m_tryReconnect = false;
				m_pendingConnectionCancellationSource?.Cancel();
			}

			if (SocketStatus == ESocketStatus.Open)
			{
				var timeout = new CancellationTokenSource(2000);
				try
				{
					// after this, the socket state which change to CloseSent
					await m_socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
					// now we wait for the server response, which will close the socket
					while (m_socket.State != WebSocketState.Closed && !timeout.Token.IsCancellationRequested) ;
				}
				catch (OperationCanceledException)
				{

				}
				catch (Exception ex)
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
						"DataEditorSocket Exception while stopping socket",
						ex);
				}

				lock (m_mutex)
				{
					m_reciveLoopCancellationSource?.Cancel();
				}
				await Task.Delay(50);
			}

			lock (m_mutex)
			{
				m_socket?.Dispose();
				m_socket = null;
				SocketStatus = ESocketStatus.Inactive;
			}
		}

		private async Task ReceiveLoopAsync()
		{
			var cancellationToken = m_reciveLoopCancellationSource.Token;
			try
			{
				var buffer = WebSocket.CreateClientBuffer(4096, 4096);
				while (m_socket.State != WebSocketState.Closed && !cancellationToken.IsCancellationRequested)
				{
					var receiveResult = await m_socket.ReceiveAsync(buffer, cancellationToken);
					// if the token is cancelled while ReceiveAsync is blocking, the socket state changes to aborted and it can't be used
					if (!cancellationToken.IsCancellationRequested)
					{
						// the server is notifying us that the connection will close; send acknowledgement
						if (m_socket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
						{
							await m_socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
						}

						// Nothing else is interesting for us. We only use the Socket for sending;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// normal upon task/token cancellation, disregard
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"DataEditorSocket Exception during Receive Loop",
					ex);
			}
			finally
			{
				lock (m_mutex)
				{
					m_socket.Dispose();
					m_socket = null;
					SocketStatus = ESocketStatus.Inactive;
				}
			}
		}

		private async Task PendingConnectLoopAsync()
		{
			var cancellationToken = m_pendingConnectionCancellationSource.Token;
			try
			{
				while (m_socket.State != WebSocketState.Open && !cancellationToken.IsCancellationRequested)
				{
					await Task.Delay(2000, cancellationToken);
					if (m_socket.State == WebSocketState.Open || cancellationToken.IsCancellationRequested)
					{
						break;
					}

					try
					{
						await m_socket.ConnectAsync(m_requestedAddress, cancellationToken);
					}
					catch (WebSocketException)
					{
						// Retry connection, the exception results in our socket being disposed so create a new one
						m_socket = new ClientWebSocket();
						m_socket.Options.AddSubProtocol("GameCodersToolkit.DataReferenceFinder");
					}
				}

				lock (m_mutex)
				{
					if (!cancellationToken.IsCancellationRequested && m_socket.State == WebSocketState.Open)
					{
						SocketStatus = ESocketStatus.Open;
					}
				}
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"DataEditorSocket Exception during Connect Loop",
					ex);
			}
		}

		private void StartReceiveLoop(Task connectionTask)
		{
			if (m_socket != null && SocketStatus == ESocketStatus.Open && (m_reciveLoopCancellationSource == null || !m_reciveLoopCancellationSource.IsCancellationRequested))
			{
				m_reciveLoopCancellationSource = new CancellationTokenSource();
				m_receiveLoop = Task.Run(ReceiveLoopAsync).ContinueWith(RestartPendingConnectionLoop, TaskScheduler.Default);
			}
		}

		private void RestartPendingConnectionLoop(Task receiveTask)
		{
			if (!m_tryReconnect || m_pendingConnectionCancellationSource.IsCancellationRequested)
				return;

			m_socket = new ClientWebSocket();
			m_socket.Options.AddSubProtocol("GameCodersToolkit.DataReferenceFinder");
			SocketStatus = ESocketStatus.Connecting;

			m_pendingConnectionCancellationSource = new CancellationTokenSource();
			m_pendingConnectionLoop = Task.Run(PendingConnectLoopAsync).ContinueWith(StartReceiveLoop, TaskScheduler.Default);
		}

		public ESocketStatus SocketStatus { get; private set; }

		Uri m_requestedAddress;
		bool m_tryReconnect = false;

		ClientWebSocket m_socket;
		ClientWebSocketOptions m_options;

		Task m_receiveLoop;
		CancellationTokenSource m_reciveLoopCancellationSource;

		Task m_pendingConnectionLoop;
		CancellationTokenSource m_pendingConnectionCancellationSource;

		object m_mutex = new object();
	}

	public class DataEditorConnection
	{
		public DataEditorConnection()
		{
			GameCodersToolkitPackage.DataLocationsConfig.ConfigLoaded += OnConfigLoaded;
			m_clientSocket = new DataEditorClientSocket();
		}

		public async Task OpenInDataEditorAsync(DataEntry dataEntry)
		{
			if (m_clientSocket.SocketStatus != ESocketStatus.Open)
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

		void OnConfigLoaded(object sender, EventArgs e)
		{
			string serverAddress = GameCodersToolkitPackage.DataLocationsConfig.GetDataEditorServerUri();
			if (Uri.TryCreate(serverAddress, UriKind.RelativeOrAbsolute, out Uri serverUri))
			{
				ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
				{
					await m_clientSocket.StopAsync();
					await m_clientSocket.StartAsync(serverUri, true);
				}).FireAndForget();
			}
		}

		void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
		{
			DataEditorConnectionStatusChanged?.Invoke(this, args);
		}

		public EventHandler<ConnectionStatusChangedEventArgs> DataEditorConnectionStatusChanged { get; set; }
		public bool IsConnectedToDataEditor { get => m_clientSocket.SocketStatus == ESocketStatus.Open; }

		DataEditorClientSocket m_clientSocket;
	}
}
