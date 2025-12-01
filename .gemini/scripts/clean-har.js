// .gemini/scripts/clean-har.js

const fs = require("fs");

const inputFile = process.argv[2];
const outputFile = process.argv[3];

if (!inputFile || !outputFile) {
  console.error("Usage: node clean-har.js <input-file> <output-file>");
  process.exit(1);
}

// List of headers to strip (lowercase for comparison)
const HEADERS_TO_REMOVE = new Set([
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
]);

// Helper to filter headers
const cleanHeaders = (headers) => {
  if (!headers) return [];
  return headers.filter((h) => {
    const name = h.name.toLowerCase();
    // Remove exact matches
    if (HEADERS_TO_REMOVE.has(name)) return false;
    // Remove any access-control headers (allow-origin, etc.)
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

    // 1. Filter Entries (Existing Logic)
    const filteredEntries = har.log.entries.filter((entry) => {
      const status = entry.response.status;
      const mimeType = entry.response.content.mimeType;
      const url = entry.request.url;

      // Filter out common static assets and analytics trackers
      if (
        status > 400 ||
        status < 0 ||
        mimeType.includes("image/") ||
        mimeType.includes("font/") ||
        mimeType.includes("text/css") ||
        mimeType.includes("javascript") ||
        mimeType.includes("binary/octet-stream") ||
        mimeType.includes("font-sfnt") ||
        mimeType.includes("x-protobuf") ||
        url.includes("www.gov.uk") ||
        url.includes("google.com") ||
        url.includes("doubleclick.net") ||
        url.includes("cookiebot.net") ||
        url.includes("cookiebot.com") ||
        url.includes("facebook.com") ||
        url.includes("google-analytics.com") ||
        url.includes("googletagmanager.com") ||
        url.includes("hotjar.com")
      ) {
        return false;
      }
      return true;
    });

    // 2. Strip Useless Properties (New Logic)
    const cleanedEntries = filteredEntries.map((entry) => {
      // Top level entry properties
      delete entry.startedDateTime;
      delete entry.time;
      delete entry.cache;
      delete entry.timings;
      delete entry.serverIPAddress;
      delete entry._serverPort;
      delete entry._securityDetails;

      // Request properties
      if (entry.request) {
        delete entry.request.httpVersion;
        delete entry.request.queryString;
        delete entry.request.headersSize;
        delete entry.request.bodySize;
        entry.request.headers = cleanHeaders(entry.request.headers);
      }

      // Response properties
      if (entry.response) {
        delete entry.response.httpVersion;
        delete entry.response.headersSize;
        delete entry.response.bodySize;
        delete entry.response._transferSize;
        entry.response.headers = cleanHeaders(entry.response.headers);

        // Content properties
        if (entry.response.content) {
          delete entry.response.content.size;
          delete entry.response.content.compression;
          // Some HARs duplicate these inside content as well
          delete entry.response.content.headersSize;
          delete entry.response.content.bodySize;
          delete entry.response.content._transferSize;
        }
      }

      return entry;
    });

    // 3. Clean Pages (optional, but good for removing dates)
    if (har.log.pages) {
      har.log.pages.forEach((page) => {
        delete page.startedDateTime;
      });
    }

    // Assign back to HAR
    har.log.entries = cleanedEntries;

    fs.writeFile(outputFile, JSON.stringify(har, null, 2), (err) => {
      if (err) {
        console.error(`Error writing file ${outputFile}:`, err);
        process.exit(1);
      }
      console.log(`HAR file cleaned and saved to ${outputFile}`);
    });
  } catch (e) {
    console.error("Error parsing HAR file:", e);
    process.exit(1);
  }
});
