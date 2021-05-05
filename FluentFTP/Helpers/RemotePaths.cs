﻿using System;
using System.Text.RegularExpressions;

namespace FluentFTP.Helpers {
	/// <summary>
	/// Extension methods related to FTP tasks
	/// </summary>
	public static class RemotePaths {

		/// <summary>
		/// Checks if this FTP path is a top level path
		/// </summary>
		public static bool IsAbsolutePath(this string path) {
			return path.StartsWith("/") || path.StartsWith("./") || path.StartsWith("../");
		}

		/// <summary>
		/// Converts the specified path into a valid FTP file system path
		/// </summary>
		/// <param name="path">The file system path</param>
		/// <returns>A path formatted for FTP</returns>
		public static string GetFtpPath(this string path) {
			if (string.IsNullOrEmpty(path)) {
				return "./";
			}

			path = path.Replace('\\', '/');
			path = Regex.Replace(path, "[/]+", "/");
			path = path.TrimEnd('/');

			if (path.Length == 0) {
				path = "/";
			}

			return path;
		}

		/// <summary>
		/// Creates a valid FTP path by appending the specified segments to this string
		/// </summary>
		/// <param name="path">This string</param>
		/// <param name="segments">The path segments to append</param>
		/// <returns>A valid FTP path</returns>
		public static string GetFtpPath(this string path, params string[] segments) {
			if (string.IsNullOrEmpty(path)) {
				path = "./";
			}

			foreach (var part in segments) {
				if (part != null) {
					if (path.Length > 0 && !path.EndsWith("/")) {
						path += "/";
					}

					path += Regex.Replace(part.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
				}
			}

			path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/").TrimEnd('/');
			if (path.Length == 0) {
				path = "/";
			}

			return path;
		}

		/// <summary>
		/// Gets the parent directory path (formatted for a FTP server)
		/// </summary>
		/// <param name="path">The path</param>
		/// <returns>The parent directory path</returns>
		public static string GetFtpDirectoryName(this string path) {
			var tpath = path == null ? "" : path.GetFtpPath();

			if (tpath.Length == 0 || tpath == "/") {
				return "/";
			}

			var lastslash = tpath.LastIndexOf('/');
			if (lastslash < 0) {
				return ".";
			}

			if (lastslash == 0) {
				return "/";
			}

			return tpath.Substring(0, lastslash);
		}

		/// <summary>
		/// Gets the file name and extension from the path
		/// </summary>
		/// <param name="path">The full path to the file</param>
		/// <returns>The file name</returns>
		public static string GetFtpFileName(this string path) {
			var tpath = path == null ? null : path;
			var lastslash = -1;

			if (tpath == null) {
				return null;
			}

			lastslash = tpath.LastIndexOf('/');
			if (lastslash < 0) {
				return tpath;
			}

			lastslash += 1;
			if (lastslash >= tpath.Length) {
				return tpath;
			}

			return tpath.Substring(lastslash, tpath.Length - lastslash);
		}

		/// <summary>
		/// Checks if the given path is a root directory or working directory path
		/// </summary>
		/// <param name="ftppath"></param>
		/// <returns></returns>
		public static bool IsFtpRootDirectory(this string ftppath) {
			return ftppath == "." || ftppath == "./" || ftppath == "/";
		}

		/// <summary>
		/// Converts a Windows or Unix-style path into its segments for segment-wise processing
		/// </summary>
		/// <returns></returns>
		public static string[] GetPathSegments(this string path) {
			if (path.Contains("/")) {
				return path.Split('/');
			}
			else if (path.Contains("\\")) {
				return path.Split('\\');
			}
			else {
				return new string[] { path };
			}
		}

		/// <summary>
		/// Get the full path of a given FTP Listing entry
		/// </summary>
		public static void CalculateFullFtpPath(this FtpListItem item, FtpClient client, string path, bool isVMS) {
			// EXIT IF NO DIR PATH PROVIDED
			if (path == null) {
				// check if the path is absolute
				if (IsAbsolutePath(item.Name)) {
					item.FullName = item.Name;
					item.Name = item.Name.GetFtpFileName();
				}

				return;
			}


			// ONLY IF DIR PATH PROVIDED

			// if this is a vax/openvms file listing
			// there are no slashes in the path name
			if (isVMS) {
				item.FullName = path + item.Name;
			}
			else {
				//this.client.LogStatus(item.Name);

				// remove globbing/wildcard from path
				if (path.GetFtpFileName().Contains("*")) {
					path = path.GetFtpDirectoryName();
				}

				if (item.Name != null) {
					// absolute path? then ignore the path input to this method.
					if (IsAbsolutePath(item.Name)) {
						item.FullName = item.Name;
						item.Name = item.Name.GetFtpFileName();
					}
					else if (path != null) {
						item.FullName = path.GetFtpPath(item.Name); //.GetFtpPathWithoutGlob();
					}
					else {
						client.LogStatus(FtpTraceLevel.Warn, "Couldn't determine the full path of this object: " +
															 Environment.NewLine + item.ToString());
					}
				}


				// if a link target is set and it doesn't include an absolute path
				// then try to resolve it.
				if (item.LinkTarget != null && !item.LinkTarget.StartsWith("/")) {
					if (item.LinkTarget.StartsWith("./")) {
						item.LinkTarget = path.GetFtpPath(item.LinkTarget.Remove(0, 2)).Trim();
					}
					else {
						item.LinkTarget = path.GetFtpPath(item.LinkTarget).Trim();
					}
				}
			}
		}


	}
}