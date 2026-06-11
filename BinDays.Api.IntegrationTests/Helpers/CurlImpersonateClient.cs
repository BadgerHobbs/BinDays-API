namespace BinDays.Api.IntegrationTests.Helpers;

using BinDays.Api.Collectors.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

/// <summary>
/// Executes a client-side request using a bundled <c>curl-impersonate</c> binary, which replays
/// the TLS, HTTP/2, and header fingerprint of a real browser.
/// </summary>
/// <remarks>
/// Some councils sit behind a Cloudflare TLS-fingerprint challenge that blocks the Dart/Dio
/// transport (HTTP/1.1, non-browser JA3) even though real devices pass it. This transport is
/// used as an automatic fallback for those councils so the integration tests reflect production,
/// where real users already succeed. It only affects the test transport; the production client is
/// untouched.
/// </remarks>
internal sealed class CurlImpersonateClient
{
	/// <summary>
	/// The curl-impersonate release version to download.
	/// </summary>
	private const string _version = "1.5.6";

	/// <summary>
	/// The browser fingerprint to impersonate.
	/// </summary>
	private const string _impersonateTarget = "chrome131";

	/// <summary>
	/// Request headers that are part of the browser fingerprint and are therefore left to
	/// curl-impersonate, rather than being overridden by the collector's values.
	/// </summary>
	private static readonly HashSet<string> _fingerprintHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		"user-agent",
		"accept",
		"accept-encoding",
		"accept-language",
		"upgrade-insecure-requests",
		"priority",
		"connection",
		"host",
		"sec-fetch-dest",
		"sec-fetch-mode",
		"sec-fetch-site",
		"sec-fetch-user",
		"sec-ch-ua",
		"sec-ch-ua-mobile",
		"sec-ch-ua-platform",
	};

	private static readonly SemaphoreSlim _binaryLock = new(1, 1);
	private static (string ExePath, string? LibDir)? _cachedBinary;

	private readonly ITestOutputHelper _outputHelper;

	/// <summary>
	/// Initializes a new instance of the <see cref="CurlImpersonateClient"/> class.
	/// </summary>
	/// <param name="outputHelper">The xUnit test output helper.</param>
	public CurlImpersonateClient(ITestOutputHelper outputHelper)
	{
		_outputHelper = outputHelper;
	}

	/// <summary>
	/// Sends a client-side request through curl-impersonate and returns the parsed response.
	/// </summary>
	/// <param name="request">The client-side request details provided by the main API.</param>
	/// <returns>A <see cref="ClientSideResponse"/> parsed from the curl-impersonate output.</returns>
	public async Task<ClientSideResponse> SendAsync(ClientSideRequest request)
	{
		var (exePath, libDir) = await EnsureBinaryAsync();

		var headerFile = Path.GetTempFileName();
		var bodyFile = Path.GetTempFileName();
		var dataFile = string.IsNullOrEmpty(request.Body) ? null : Path.GetTempFileName();

		try
		{
			if (dataFile != null)
			{
				await File.WriteAllTextAsync(dataFile, request.Body);
			}

			var statusCode = await RunCurlAsync(exePath, libDir, request, headerFile, bodyFile, dataFile);
			var (headers, reasonPhrase) = ParseHeaders(headerFile);
			var content = await File.ReadAllTextAsync(bodyFile);

			return new ClientSideResponse
			{
				RequestId = request.RequestId,
				StatusCode = statusCode,
				Headers = headers,
				Content = content,
				ReasonPhrase = reasonPhrase,
				Options = request.Options,
			};
		}
		finally
		{
			File.Delete(headerFile);
			File.Delete(bodyFile);
			if (dataFile != null)
			{
				File.Delete(dataFile);
			}
		}
	}

	/// <summary>
	/// Runs the curl-impersonate process for the given request, writing response headers and body
	/// to the supplied files, and returns the HTTP status code.
	/// </summary>
	private async Task<int> RunCurlAsync(
		string exePath,
		string? libDir,
		ClientSideRequest request,
		string headerFile,
		string bodyFile,
		string? dataFile)
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = exePath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		var args = process.StartInfo.ArgumentList;
		args.Add("--impersonate");
		args.Add(_impersonateTarget);
		args.Add("--insecure");
		args.Add("--silent");
		args.Add("--show-error");
		args.Add("--compressed");
		args.Add("--max-time");
		args.Add("30");

		if (request.Options.FollowRedirects)
		{
			args.Add("--location");
		}

		if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
		{
			args.Add("--request");
			args.Add(request.Method);
		}

		foreach (var header in request.Headers)
		{
			if (!_fingerprintHeaders.Contains(header.Key))
			{
				args.Add("--header");
				args.Add($"{header.Key}: {header.Value}");
			}
		}

		if (dataFile != null)
		{
			args.Add("--data-binary");
			args.Add($"@{dataFile}");
		}

		args.Add("--dump-header");
		args.Add(headerFile);
		args.Add("--output");
		args.Add(bodyFile);
		args.Add("--write-out");
		args.Add("%{response_code}");
		args.Add(request.Url);

		// The binary resolves libcurl-impersonate from the extracted lib directory; the dynamic
		// linker variable differs between macOS and Linux.
		if (libDir != null)
		{
			var libPathVariable = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "DYLD_LIBRARY_PATH" : "LD_LIBRARY_PATH";
			process.StartInfo.Environment[libPathVariable] = libDir;
		}

		process.Start();

		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();

		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			throw new TimeoutException("curl-impersonate process timed out after 60 seconds.");
		}

		var stdout = await stdoutTask;
		var stderr = await stderrTask;

		if (process.ExitCode != 0)
		{
			throw new HttpRequestException($"curl-impersonate failed (exit code {process.ExitCode}): {stderr}");
		}

		_outputHelper.WriteLine($"[curl-impersonate] {request.Method} {request.Url} -> {stdout.Trim()}");

		return int.Parse(stdout.Trim());
	}

	/// <summary>
	/// Parses the dumped header file into a case-normalised dictionary, matching the Dart client's
	/// behaviour of lower-casing keys and comma-joining repeated headers. Only the final response
	/// block is used (after any followed redirects).
	/// </summary>
	private static (Dictionary<string, string> Headers, string ReasonPhrase) ParseHeaders(string headerFile)
	{
		var lines = File.ReadAllLines(headerFile);

		var lastStatusIndex = -1;
		for (var i = 0; i < lines.Length; i++)
		{
			if (lines[i].StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
			{
				lastStatusIndex = i;
			}
		}

		var headers = new Dictionary<string, string>();
		var reasonPhrase = string.Empty;

		if (lastStatusIndex >= 0)
		{
			// e.g. "HTTP/1.1 302 Found" (HTTP/2 has no reason phrase).
			var statusParts = lines[lastStatusIndex].Split(' ', 3);
			if (statusParts.Length == 3)
			{
				reasonPhrase = statusParts[2].Trim();
			}

			for (var i = lastStatusIndex + 1; i < lines.Length; i++)
			{
				var line = lines[i];
				if (string.IsNullOrWhiteSpace(line))
				{
					break;
				}

				var separator = line.IndexOf(':');
				if (separator <= 0)
				{
					continue;
				}

				var key = line[..separator].Trim().ToLowerInvariant();
				var value = line[(separator + 1)..].Trim();

				headers[key] = headers.TryGetValue(key, out var existing)
					? $"{existing},{value}"
					: value;
			}
		}

		return (headers, reasonPhrase);
	}

	/// <summary>
	/// Ensures the curl-impersonate binary for the current platform is downloaded and extracted,
	/// returning the executable path and (on Linux) the directory holding the shared library.
	/// </summary>
	private async Task<(string ExePath, string? LibDir)> EnsureBinaryAsync()
	{
		if (_cachedBinary is { } cached)
		{
			return cached;
		}

		await _binaryLock.WaitAsync();
		try
		{
			if (_cachedBinary is { } existing)
			{
				return existing;
			}

			var (asset, exeName) = ResolveAsset();
			var installDir = Path.Combine(AppContext.BaseDirectory, "curl-impersonate", _version);
			var exePath = Directory.Exists(installDir)
				? FindExecutable(installDir, exeName)
				: null;

			if (exePath == null)
			{
				_outputHelper.WriteLine($"[curl-impersonate] Downloading {asset}...");
				await DownloadAndExtractAsync(asset, installDir);
				exePath = FindExecutable(installDir, exeName)
					?? throw new FileNotFoundException($"'{exeName}' not found after extracting {asset}.");

				// The extracted binary needs the executable bit on Unix-like systems.
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					File.SetUnixFileMode(
						exePath,
						UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
						| UnixFileMode.GroupRead | UnixFileMode.GroupExecute
						| UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
				}
			}

			var libDir = Directory.Exists(Path.Combine(installDir, "lib"))
				? Path.Combine(installDir, "lib")
				: null;

			_cachedBinary = (exePath, libDir);
			return _cachedBinary.Value;
		}
		finally
		{
			_binaryLock.Release();
		}
	}

	/// <summary>
	/// Resolves the release asset name and executable file name for the current platform.
	/// </summary>
	private static (string Asset, string ExeName) ResolveAsset()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return ($"libcurl-impersonate-v{_version}.x86_64-win32.tar.gz", "curl-impersonate.exe");
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";
			return ($"libcurl-impersonate-v{_version}.{arch}-macos.tar.gz", "curl-impersonate");
		}

		return ($"libcurl-impersonate-v{_version}.x86_64-linux-gnu.tar.gz", "curl-impersonate");
	}

	/// <summary>
	/// Recursively locates the curl-impersonate executable within the install directory.
	/// </summary>
	private static string? FindExecutable(string installDir, string exeName)
	{
		var direct = Path.Combine(installDir, "bin", exeName);
		if (File.Exists(direct))
		{
			return direct;
		}

		foreach (var path in Directory.EnumerateFiles(installDir, exeName, SearchOption.AllDirectories))
		{
			return path;
		}

		return null;
	}

	/// <summary>
	/// Downloads the release asset and extracts its gzip-compressed tar archive into the install
	/// directory using the built-in gzip and tar support.
	/// </summary>
	private static async Task DownloadAndExtractAsync(string asset, string installDir)
	{
		var url = $"https://github.com/lexiforest/curl-impersonate/releases/download/v{_version}/{asset}";
		var archivePath = Path.Combine(Path.GetTempPath(), asset);

		try
		{
			using (var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
			using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();
				await using var fileStream = File.Create(archivePath);
				await response.Content.CopyToAsync(fileStream);
			}

			// Start from a clean directory so a failed run can't leave a partial install that a
			// later run mistakes for a complete one.
			if (Directory.Exists(installDir))
			{
				Directory.Delete(installDir, recursive: true);
			}

			Directory.CreateDirectory(installDir);
			await using var archiveStream = File.OpenRead(archivePath);
			await using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
			await TarFile.ExtractToDirectoryAsync(gzipStream, installDir, overwriteFiles: true);
		}
		catch
		{
			if (Directory.Exists(installDir))
			{
				try
				{
					Directory.Delete(installDir, recursive: true);
				}
				catch
				{
					// Preserve the original failure rather than masking it with cleanup errors.
				}
			}

			throw;
		}
		finally
		{
			if (File.Exists(archivePath))
			{
				File.Delete(archivePath);
			}
		}
	}
}
