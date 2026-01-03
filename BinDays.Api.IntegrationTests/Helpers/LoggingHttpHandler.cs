namespace BinDays.Api.IntegrationTests.Helpers
{
	using System;
	using System.Net.Http;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Xunit.Abstractions;

	/// <summary>
	/// An <see cref="HttpMessageHandler"/> that logs request and response details to the test output.
	/// </summary>
	internal sealed class LoggingHttpHandler : DelegatingHandler
	{
		private readonly ITestOutputHelper _outputHelper;
		private const int _borderWidth = 80;

		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingHttpHandler"/> class.
		/// </summary>
		/// <param name="outputHelper">The xUnit test output helper.</param>
		/// <param name="innerHandler">The inner handler to which requests are delegated.</param>
		public LoggingHttpHandler(ITestOutputHelper outputHelper, HttpMessageHandler innerHandler)
			: base(innerHandler)
		{
			_outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
		}

		/// <summary>
		/// Sends an HTTP request, logging details before and after the request is sent.
		/// </summary>
		/// <param name="request">The HTTP request message to send.</param>
		/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
		/// <returns>The HTTP response message.</returns>
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await LogRequestAsync(request);
			var response = await base.SendAsync(request, cancellationToken);
			await LogResponseAsync(response);
			return response;
		}

		/// <summary>
		/// Logs the details of the outgoing <see cref="HttpRequestMessage"/>.
		/// </summary>
		/// <param name="request">The request to log.</param>
		private async Task LogRequestAsync(HttpRequestMessage request)
		{
			var logBuilder = new StringBuilder();
			var requestTitle = $" HTTP Request Sent ";
			logBuilder.AppendLine(TestOutput.CreateCenteredHeader(requestTitle, _borderWidth, '='));
			logBuilder.AppendLine();

			logBuilder.AppendLine($"-> {request.Method.Method.ToUpper()} {request.RequestUri}");
			logBuilder.AppendLine($"-> Host: {request.RequestUri?.Host}");
			logBuilder.AppendLine();

			logBuilder.AppendLine("Headers:");
			foreach (var header in request.Headers)
			{
				logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
			}
			if (request.Content != null)
			{
				foreach (var header in request.Content.Headers)
				{
					logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
				}
			}

			if (request.Content != null)
			{
				logBuilder.AppendLine();
				logBuilder.AppendLine("Body:");
				var content = await request.Content.ReadAsStringAsync();
				logBuilder.AppendLine(content);
			}

			logBuilder.AppendLine(new string('=', _borderWidth));
			_outputHelper.WriteLine(logBuilder.ToString());
		}

		/// <summary>
		/// Logs the details of the incoming <see cref="HttpResponseMessage"/>.
		/// </summary>
		/// <param name="response">The response to log.</param>
		private async Task LogResponseAsync(HttpResponseMessage response)
		{
			var logBuilder = new StringBuilder();
			var responseTitle = $" HTTP Response Received ";
			logBuilder.AppendLine(TestOutput.CreateCenteredHeader(responseTitle, _borderWidth, '='));
			logBuilder.AppendLine();

			logBuilder.AppendLine($"<- {(int)response.StatusCode} {response.ReasonPhrase}");
			logBuilder.AppendLine($"<- Request: {response.RequestMessage?.Method.Method.ToUpper()} {response.RequestMessage?.RequestUri}");
			logBuilder.AppendLine();

			logBuilder.AppendLine("Headers:");
			foreach (var header in response.Headers)
			{
				logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
			}
			foreach (var header in response.Content.Headers)
			{
				logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
			}

			logBuilder.AppendLine();
			logBuilder.AppendLine("Body:");
			var content = await response.Content.ReadAsStringAsync();
			logBuilder.AppendLine(content);

			logBuilder.AppendLine(new string('=', _borderWidth));
			_outputHelper.WriteLine(logBuilder.ToString());
		}
	}
}
