const fs = require('fs');

if (process.argv.length !== 4) {
    console.log('Usage: node filter-har.js <input.har> <output.har>');
    process.exit(1);
}

const inputFile = process.argv[2];
const outputFile = process.argv[3];

const allowedMimeTypes = new Set([
    "text/html",
    "application/json",
    "application/javascript",
    "text/javascript",
    "application/x-www-form-urlencoded"
]);

try {
    const harContent = fs.readFileSync(inputFile, 'utf8');
    const har = JSON.parse(harContent);

    if (!har.log || !har.log.entries) {
        console.error('Error: Could not find "log.entries" array in the HAR file.');
        process.exit(1);
    }

    const initialCount = har.log.entries.length;

    const filteredEntries = har.log.entries
        .filter(entry => {
            const mimeType = entry.response.content.mimeType;
            if (!mimeType) {
                return false;
            }
            for (const allowed of allowedMimeTypes) {
                if (mimeType.startsWith(allowed)) {
                    return true;
                }
            }
            return false;
        })
        .map(entry => ({
            request: entry.request,
            response: entry.response
        }));

    har.log.entries = filteredEntries;

    fs.writeFileSync(outputFile, JSON.stringify(har, null, 2));

    console.log(`Successfully filtered ${initialCount} entries down to ${filteredEntries.length} and saved to ${outputFile}`);

} catch (err) {
    console.error(`An error occurred: ${err.message}`);
    process.exit(1);
}
