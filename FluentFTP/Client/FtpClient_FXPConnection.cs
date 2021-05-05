﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using System.Security.Authentication;
using System.Net;
using FluentFTP.Proxy;
using FluentFTP.Servers;
using FluentFTP.Helpers;
#if !CORE
using System.Web;
#endif
#if (CORE || NETFX)
using System.Threading;
using FluentFTP.Rules;
#endif
#if (CORE || NET45)
using System.Threading.Tasks;

#endif
namespace FluentFTP {
	public partial class FtpClient : IDisposable {

		/// <summary>
		/// Opens a FXP PASV connection between the source FTP Server and the destination FTP Server
		/// </summary>
		/// <param name="remoteClient">FtpClient instance of the destination FTP Server</param>
		/// <returns>A data stream ready to be used</returns>
		private FtpFxpSession OpenPassiveFXPConnection(FtpClient remoteClient, bool trackProgress) {
			FtpReply reply;
			Match m;
			FtpClient sourceClient = null;
			FtpClient destinationClient = null;
			FtpClient progressClient = null;

			// create a new connection to the source FTP server if EnableThreadSafeDataConnections is set
			if (EnableThreadSafeDataConnections) {
				sourceClient = CloneConnection();
				sourceClient._AutoDispose = true;
				sourceClient.CopyStateFlags(this);
				sourceClient.Connect();
				sourceClient.SetWorkingDirectory(GetWorkingDirectory());
			}
			else {
				sourceClient = this;
			}

			// create a new connection to the target FTP server if EnableThreadSafeDataConnections is set
			if (remoteClient.EnableThreadSafeDataConnections) {
				destinationClient = remoteClient.CloneConnection();
				destinationClient._AutoDispose = true;
				destinationClient.CopyStateFlags(remoteClient);
				destinationClient.Connect();
				destinationClient.SetWorkingDirectory(remoteClient.GetWorkingDirectory());
			}
			else {
				destinationClient = remoteClient;
			}

			// create a new connection to the target FTP server to track progress
			// if progress tracking is enabled during this FXP transfer
			if (trackProgress) {
				progressClient = remoteClient.CloneConnection();
				progressClient._AutoDispose = true;
				progressClient.CopyStateFlags(remoteClient);
				progressClient.Connect();
				progressClient.SetWorkingDirectory(remoteClient.GetWorkingDirectory());
			}

			sourceClient.SetDataType(sourceClient.FXPDataType);
			destinationClient.SetDataType(destinationClient.FXPDataType);

			// send PASV command to destination FTP server to get passive port to be used from source FTP server
			if (!(reply = destinationClient.Execute("PASV")).Success) {
				throw new FtpCommandException(reply);
			}
			m = Regex.Match(reply.Message, @"(?<quad1>\d+)," + @"(?<quad2>\d+)," + @"(?<quad3>\d+)," + @"(?<quad4>\d+)," + @"(?<port1>\d+)," + @"(?<port2>\d+)");
			if (!m.Success || m.Groups.Count != 7) {
				throw new FtpException("Malformed PASV response: " + reply.Message);
			}

			// Instruct source server to open a connection to the destination Server

			if (!(reply = sourceClient.Execute($"PORT {m.Value}")).Success) {
				throw new FtpCommandException(reply);
			}

			// the FXP session stores the active connections used for this FXP transfer
			return new FtpFxpSession {
				SourceServer = sourceClient,
				TargetServer = destinationClient,
				ProgressServer = progressClient,
			};
		}

#if ASYNC

		/// <summary>
		/// Opens a FXP PASV connection between the source FTP Server and the destination FTP Server
		/// </summary>
		/// <param name="remoteClient">Valid FTP connection to the destination FTP Server</param>
		/// <returns>A data stream ready to be used</returns>
		private async Task<FtpFxpSession> OpenPassiveFXPConnectionAsync(FtpClient remoteClient, bool trackProgress, CancellationToken token) {
			FtpReply reply;
			Match m;
			FtpClient sourceClient = null;
			FtpClient destinationClient = null;
			FtpClient progressClient = null;

			// create a new connection to the source FTP server if EnableThreadSafeDataConnections is set
			if (m_threadSafeDataChannels) {
				sourceClient = CloneConnection();
				sourceClient._AutoDispose = true;
				sourceClient.CopyStateFlags(this);
				await sourceClient.ConnectAsync(token);
				await sourceClient.SetWorkingDirectoryAsync(await GetWorkingDirectoryAsync(token), token);
			}
			else {
				sourceClient = this;
			}

			// create a new connection to the target FTP server if EnableThreadSafeDataConnections is set
			if (remoteClient.EnableThreadSafeDataConnections) {
				destinationClient = remoteClient.CloneConnection();
				destinationClient._AutoDispose = true;
				destinationClient.CopyStateFlags(remoteClient);
				await destinationClient.ConnectAsync(token);
				await destinationClient.SetWorkingDirectoryAsync(await remoteClient.GetWorkingDirectoryAsync(token), token);
			}
			else {
				destinationClient = remoteClient;
			}

			// create a new connection to the target FTP server to track progress
			// if progress tracking is enabled during this FXP transfer
			if (trackProgress) {
				progressClient = remoteClient.CloneConnection();
				progressClient._AutoDispose = true;
				progressClient.CopyStateFlags(remoteClient);
				await progressClient.ConnectAsync(token);
				await progressClient.SetWorkingDirectoryAsync(await remoteClient.GetWorkingDirectoryAsync(token), token);
			}

			await sourceClient.SetDataTypeAsync(sourceClient.FXPDataType, token);
			await destinationClient.SetDataTypeAsync(destinationClient.FXPDataType, token);

			// send PASV command to destination FTP server to get passive port to be used from source FTP server
			if (!(reply = await destinationClient.ExecuteAsync("PASV", token)).Success) {
				throw new FtpCommandException(reply);
			}

			m = Regex.Match(reply.Message, @"(?<quad1>\d+)," + @"(?<quad2>\d+)," + @"(?<quad3>\d+)," + @"(?<quad4>\d+)," + @"(?<port1>\d+)," + @"(?<port2>\d+)");

			if (!m.Success || m.Groups.Count != 7) {
				throw new FtpException("Malformed PASV response: " + reply.Message);
			}

			// Instruct source server to open a connection to the destination Server

			if (!(reply = await sourceClient.ExecuteAsync($"PORT {m.Value}", token)).Success) {
				throw new FtpCommandException(reply);
			}

			// the FXP session stores the active connections used for this FXP transfer
			return new FtpFxpSession {
				SourceServer = sourceClient,
				TargetServer = destinationClient,
				ProgressServer = progressClient,
			};
		}

#endif

		/// <summary>
		/// Disposes and disconnects this FTP client if it was auto-created for an internal operation.
		/// </summary>
		public void AutoDispose() {
			if (_AutoDispose) {
				Dispose();
			}
		}

	}
}