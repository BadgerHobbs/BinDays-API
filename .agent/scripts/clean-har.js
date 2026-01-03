const fs = require("fs");

const inputFile = process.argv[2];
const outputFile = process.argv[3];

if (!inputFile || !outputFile) {
  console.error("Usage: node clean-har.js <input-file> <output-file>");
  process.exit(1);
}

// List of headers to strip (lowercase for comparison)
// We explicitly DO NOT include: 'cookie', 'set-cookie', 'authorization', 'accesstoken', 'content-type', 'location'
const HEADERS_TO_REMOVE = new Set([
  // Standard Request Headers
  "host",
  "accept",
  "accept-language",
  "accept-encoding",
  "user-agent",
  "connection",
  "upgrade-insecure-requests",
  "sec-fetch-dest",
  "sec-fetch-mode",
  "sec-fetch-site",
  "sec-fetch-user",
  "sec-ch-ua",
  "sec-ch-ua-mobile",
  "sec-ch-ua-platform",
  "referer",
  "origin",
  // Standard Response / Caching / Transport Headers
  "date",
  "etag",
  "via",
  "server",
  "vary",
  "pragma",
  "expires",
  "last-modified",
  "cache-control",
  "content-length",
  "content-encoding",
  "accept-ranges",
  "alt-svc",
  // Security Policies (Browser enforcement, not auth)
  "content-security-policy",
  "strict-transport-security",
  "x-content-type-options",
  "x-frame-options",
  "referrer-policy",
  "permissions-policy",
  // Cloud / CDN / Load Balancer Noise (AWS, Azure, Cloudfront)
  "x-amz-cf-id",
  "x-amz-cf-pop",
  "x-cache",
  "x-azure-ref",
  "x-fd-int-roxy-purgeid",
  "x-powered-by",
  "x-aspnet-version",
  "x-aspnetmvc-version",
  // Varnish & Server Caching Specifics
  "x-varnish",
  "x-varnish-authentication",
  "x-age",
  "x-grace",
  "x-backend-ttl",
  "surrogate-control",
  // Server Diagnostics & Metadata
  "x-origin-server",
  "x-host",
  "x-server-name",
  "x-is-iis-fallback",
  "request-handler-metrics",
  "x-version-no",
  "origin-content-api-version",
  "x-geoip-country-code",
  "x-firefox-spdy",
  // Tracing & Correlation IDs (Useless for replay)
  "x-arcgis-trace-id",
  "x-arcgis-correlation-id",
  "x-arcgis-instance",
  "request-context",
  "x-contensis-viewer-groups",
  "x-request-id",
  "x-correlation-id",
  "x-trace-id",
]);

// Helper to filter headers
const cleanHeaders = (headers) => {
  if (!headers) return [];
  return headers.filter((h) => {
    const name = h.name.toLowerCase();

    // Remove specific blocked headers
    if (HEADERS_TO_REMOVE.has(name)) return false;

    // Remove any access-control headers (CORS)
    if (name.startsWith("access-control")) return false;

    return true;
  });
};

fs.readFile(inputFile, { encoding: "utf-8" }, (err, data) => {
  if (err) {
    console.error(`Error reading file ${inputFile}:`, err);
    process.exit(1);
  }

  try {
    const har = JSON.parse(data);

    // 1. Filter Entries (Remove static assets/analytics)
    const filteredEntries = har.log.entries.filter((entry) => {
      const status = entry.response.status;
      const mimeType = entry.response.content.mimeType || "";
      const url = entry.request.url;

      if (
        status > 400 ||
        status < 0 ||
        mimeType.includes("image/") ||
        mimeType.includes("font/") ||
        mimeType.includes("text/css") ||
        mimeType.includes("javascript") ||
        mimeType.includes("binary/octet-stream") ||
        mimeType.includes("application/octet-stream") ||
        mimeType.includes("font-sfnt") ||
        mimeType.includes("x-protobuf") ||
        url.includes("www.gov.uk") ||
        url.includes("google.com") ||
        url.includes("google.co.uk") ||
        url.includes("googleapis.com") ||
        url.includes("google-analytics.com") ||
        url.includes("googletagmanager.com") ||
        url.includes("gstatic.com") ||
        url.includes("doubleclick.net") ||
        url.includes("cookiebot.net") ||
        url.includes("cookiebot.com") ||
        url.includes("facebook.com") ||
        url.includes("hotjar.com") ||
        url.includes("silktide.com") ||
        url.includes("clarity.ms") ||
        url.includes("bing.com") ||
        url.includes("microsoft.com") ||
        url.includes("newrelic.com") ||
        url.includes("sentry.io")
      ) {
        return false;
      }
      return true;
    });

    // 2. Strip Useless Properties from remaining entries
    const cleanedEntries = filteredEntries.map((entry) => {
      // Top level entry properties
      delete entry.startedDateTime;
      delete entry.time;
      delete entry.cache;
      delete entry.timings;
      delete entry.serverIPAddress;
      delete entry._serverPort;
      delete entry._securityDetails;
      delete entry.pageref;
      delete entry._initiator;
      delete entry._priority;
      delete entry._resourceType;

      // Request properties
      if (entry.request) {
        delete entry.request.httpVersion;
        delete entry.request.queryString;
        delete entry.request.headersSize;
        delete entry.request.bodySize;
        entry.request.headers = cleanHeaders(entry.request.headers);

        // Clean cookies (keep only name and value)
        if (entry.request.cookies) {
          entry.request.cookies = entry.request.cookies.map((c) => ({
            name: c.name,
            value: c.value,
          }));
        }
      }

      // Response properties
      if (entry.response) {
        delete entry.response.httpVersion;
        delete entry.response.headersSize;
        delete entry.response.bodySize;
        delete entry.response._transferSize;
        entry.response.headers = cleanHeaders(entry.response.headers);

        // Clean cookies (keep only name, value, and essential attributes)
        if (entry.response.cookies) {
          entry.response.cookies = entry.response.cookies.map((c) => ({
            name: c.name,
            value: c.value,
            path: c.path,
            domain: c.domain,
          }));
        }

        // Content properties
        if (entry.response.content) {
          delete entry.response.content.size;
          delete entry.response.content.compression;
          delete entry.response.content.headersSize;
          delete entry.response.content.bodySize;
          delete entry.response.content._transferSize;
        }
      }

      return entry;
    });

    // 3. Clean Pages array
    if (har.log.pages) {
      har.log.pages.forEach((page) => {
        delete page.startedDateTime;
        delete page.pageTimings;
      });
    }

    // 4. Clean creator/browser info
    if (har.log.creator) {
      delete har.log.creator.comment;
    }
    delete har.log.browser;

    // Assign back to HAR
    har.log.entries = cleanedEntries;

    fs.writeFile(outputFile, JSON.stringify(har, null, 2), (err) => {
      if (err) {
        console.error(`Error writing file ${outputFile}:`, err);
        process.exit(1);
      }
      console.log(`HAR file cleaned and saved to ${outputFile}`);
      console.log(
        `Reduced from ${har.log.entries.length} to ${cleanedEntries.length} entries`
      );
    });
  } catch (e) {
    console.error("Error parsing HAR file:", e);
    process.exit(1);
  }
});
