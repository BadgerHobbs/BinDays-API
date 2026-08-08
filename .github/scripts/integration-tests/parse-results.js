/**
 * Parse TRX test results into a structured JSON file and job summary.
 *
 * Reads the primary test results and optional retry results, merges them
 * (retry results override primary for the same council), and produces:
 *   - test-results.json: { passed: [...], failed: [{ name, message, duration }] }
 *   - GitHub Actions job summary with a per-council pass/fail table
 *
 * Expected to run after `dotnet test --logger "trx;LogFileName=results.trx"`.
 * If a retry step produced retry-results.trx, those results take precedence.
 *
 * @param {Object} core - GitHub Actions core utilities
 */
module.exports = async ({ core }) => {
  const fs = require('fs');
  const path = require('path');

  const resultsDir = 'TestResults';
  const primaryFile = path.join(resultsDir, 'results.trx');

  /** Extract a named XML attribute value from a tag string. */
  function attr(tag, name) {
    const m = tag.match(new RegExp(`${name}="([^"]*)"`));
    return m ? m[1] : null;
  }

  /** Unescape XML/HTML entities in a string. */
  function unescapeXml(str) {
    return str
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/&quot;/g, '"')
      .replace(/&apos;/g, "'")
      .replace(/&amp;/g, '&');
  }

  function parseTrx(trxPath) {
    const xml = fs.readFileSync(trxPath, 'utf8');
    const results = new Map();

    // Build a map from testId -> className from TestDefinitions.
    // Match each UnitTest element (which contains a TestMethod child).
    const classMap = new Map();
    const unitTestRe = /<UnitTest\b[^>]*>[\s\S]*?<\/UnitTest>/g;
    let utMatch;
    while ((utMatch = unitTestRe.exec(xml)) !== null) {
      const block = utMatch[0];
      const id = attr(block, 'id');
      const methodTag = block.match(/<TestMethod\b[^>]*>/);
      const className = methodTag ? attr(methodTag[0], 'className') : null;
      if (id && className) {
        classMap.set(id, className);
      }
    }
    core.info(`classMap: ${classMap.size} entries`);

    // Parse UnitTestResult elements (attributes may appear in any order).
    // Use (?:[^/>]|\/(?!>))* instead of [^>]* so the trailing / of a self-closing
    // tag is not consumed before the \/>|> alternation is attempted.
    const resultRe = /<UnitTestResult\b(?:[^/>]|\/(?!>))*(?:\/>|>[\s\S]*?<\/UnitTestResult>)/g;
    let resMatch;
    let unmatchedCount = 0;
    while ((resMatch = resultRe.exec(xml)) !== null) {
      const element = resMatch[0];
      const openTag = element.match(/<UnitTestResult\b[^>]*/)[0];

      const testId = attr(openTag, 'testId');
      const testName = attr(openTag, 'testName');
      const outcome = attr(openTag, 'outcome');
      const durationStr = attr(openTag, 'duration');

      if (!outcome || outcome === 'NotExecuted') continue;

      // Resolve fully-qualified class name: prefer classMap lookup, fall back to testName.
      // Only include council tests (namespace contains .Collectors.Councils.).
      const COUNCIL_NS = '.Collectors.Councils.';
      let fqn = testId ? classMap.get(testId) : null;
      if (!fqn) {
        // testName is fully qualified: "Ns.Ns.ClassName.MethodName(params)"
        // Reconstruct fqn from segments up to and including the Tests class.
        if (testName) {
          const parts = testName.split('.');
          const classIdx = parts.findIndex(p => p.endsWith('Tests'));
          if (classIdx >= 0) fqn = parts.slice(0, classIdx + 1).join('.');
        }
      }
      if (!fqn) {
        unmatchedCount++;
        core.warning(`No class mapping for testId=${testId}, testName=${testName}`);
        continue;
      }
      if (!fqn.includes(COUNCIL_NS)) continue; // skip non-council test classes

      const councilName = fqn.split('.').pop().replace(/Tests$/, '');

      // Parse duration (TimeSpan format: HH:MM:SS.fffffff)
      const durParts = (durationStr || '0:0:0').split(':');
      const duration = durParts.length === 3
        ? parseInt(durParts[0], 10) * 3600 + parseInt(durParts[1], 10) * 60 + parseFloat(durParts[2])
        : 0;

      const passed = outcome === 'Passed';

      // Extract error message for failed tests
      let failureMessage = null;
      if (!passed) {
        const msgMatch = element.match(/<Message>([\s\S]*?)<\/Message>/);
        const traceMatch = element.match(/<StackTrace>([\s\S]*?)<\/StackTrace>/);
        failureMessage = [
          msgMatch ? unescapeXml(msgMatch[1].trim()) : null,
          traceMatch ? unescapeXml(traceMatch[1].trim()) : null,
        ].filter(Boolean).join('\n') || 'Test failed';
      }

      // Worst-case wins: failed beats passed
      const existing = results.get(councilName);

      if (!existing || (!passed && existing.passed)) {
        results.set(councilName, {
          passed,
          duration: Math.max(duration, existing?.duration ?? 0),
          message: failureMessage,
        });
      } else if (existing) {
        existing.duration = Math.max(existing.duration, duration);
      }
    }

    if (unmatchedCount > 0) {
      core.warning(`${unmatchedCount} test result(s) had no class mapping`);
    }
    return results;
  }

  if (!fs.existsSync(primaryFile)) {
    core.setFailed(`Primary results file not found: ${primaryFile}`);
    return;
  }

  const merged = parseTrx(primaryFile);
  core.info(`Results: ${merged.size} test(s)`);

  // Build structured output
  const passed = [];
  const failed = [];

  for (const [council, result] of [...merged].sort((a, b) => a[0].localeCompare(b[0]))) {
    if (result.passed) {
      passed.push(council);
    } else {
      failed.push({
        name: council,
        message: result.message || 'Test failed',
        duration: result.duration,
      });
    }
  }

  const output = { passed, failed };
  fs.writeFileSync('test-results.json', JSON.stringify(output, null, 2));
  core.info(`Results: ${passed.length} passed, ${failed.length} failed`);

  // Write job summary
  let summary = '## Integration Test Results\n\n';
  summary += `**${passed.length}** passed, **${failed.length}** failed\n\n`;

  if (failed.length > 0) {
    summary += '### Failed\n\n';
    summary += '| Council | Duration |\n|---------|----------|\n';
    for (const result of failed) {
      summary += `| ${result.name} | ${result.duration.toFixed(1)}s |\n`;
    }
    summary += '\n';
  }

  summary += '### Passed\n\n';
  summary += '| Council | Duration |\n|---------|----------|\n';
  for (const council of passed) {
    const dur = merged.get(council).duration.toFixed(1);
    summary += `| ${council} | ${dur}s |\n`;
  }

  await core.summary.addRaw(summary).write();
  core.info(summary);

  // Write badge JSON (shields.io endpoint format)
  const badge = {
    schemaVersion: 1,
    label: 'Integration Tests',
    message: `${passed.length} passed, ${failed.length} failed`,
    color: failed.length === 0 ? 'brightgreen' : 'red',
  };
  fs.writeFileSync('badge.json', JSON.stringify(badge));
};
