import 'dart:convert';
import 'dart:io';
import 'package:dio/dio.dart' as dio;
import 'package:dio_impersonate/dio_impersonate.dart';

import 'package:bindays_client/client.dart';
import 'package:bindays_client/models/client_side_request.dart';

Future<void> main() async {
  try {
    final input = await stdin.transform(utf8.decoder).join();
    final json = jsonDecode(input) as Map<String, dynamic>;
    final request = ClientSideRequest.fromJson(json);

    // Dummy base URL — we only use sendClientSideRequest, not the API methods.
    final client = Client(Uri.parse('http://localhost'));

    // When the harness flags this request (councils behind a Cloudflare
    // TLS-fingerprint challenge), route it through libcurl-impersonate so the
    // Dio client presents a real browser's JA3/HTTP-2 fingerprint. The harness
    // only passes a boolean; the target and native library are resolved here.
    final impersonate =
        Platform.environment['BINDAYS_IMPERSONATE']?.toLowerCase() == 'true';
    if (impersonate) {
      client.httpClient.httpClientAdapter = ImpersonateAdapter(
        target: ImpersonateTarget.chrome131,
        // Mirrors curl-impersonate's --insecure; the prebuilt library ships
        // without a CA bundle on some platforms.
        validateCertificates: false,
      );
    }

    final enableLogging = Platform.environment['BINDAYS_ENABLE_HTTP_LOGGING']?.toLowerCase() == 'true';
    if (enableLogging) {
      client.httpClient.interceptors.add(dio.LogInterceptor(
        requestBody: true,
        responseBody: true,
        logPrint: (message) => stderr.writeln(message),
      ));
    }

    final response = await client.sendClientSideRequest(request, validateStatus: false);

    stdout.write(jsonEncode(response.toJson()));
    client.httpClient.close();
  } on dio.DioException catch (e, s) {
    stderr.writeln('DioException: ${e.toString()}');
    stderr.writeln(s);
    exit(1);
  } catch (e, s) {
    stderr.writeln('Error: $e');
    stderr.writeln(s);
    exit(1);
  }
}
