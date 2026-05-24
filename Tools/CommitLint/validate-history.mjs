import { execFileSync, spawnSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const specs = process.argv.slice(2);
const revisions = specs.length > 0 ? specs : ["HEAD"];
const commitlintCli = resolve("node_modules", "@commitlint", "cli", "cli.js");

function git(args) {
  return execFileSync("git", args, {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  }).trim();
}

function gitRaw(args) {
  return execFileSync("git", args, {
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  });
}

const hashes = [];
const seen = new Set();

for (const revision of revisions) {
  const output = gitRaw(["rev-list", "--reverse", revision]).trim();
  if (!output) {
    continue;
  }

  for (const hash of output.split(/\r?\n/)) {
    if (!seen.has(hash)) {
      seen.add(hash);
      hashes.push(hash);
    }
  }
}

if (hashes.length === 0) {
  console.log("No commits found for commitlint history validation.");
  process.exit(0);
}

const tmp = mkdtempSync(join(tmpdir(), "commitlint-history-"));
let failed = false;

try {
  for (const hash of hashes) {
    const message = gitRaw(["log", "-1", "--format=%B", hash]);
    const subject = git(["log", "-1", "--format=%s", hash]);
    const messagePath = join(tmp, `${hash}.txt`);
    writeFileSync(messagePath, message, "utf8");

    const result = spawnSync(
      process.execPath,
      [commitlintCli, "--edit", messagePath, "--verbose"],
      {
        encoding: "utf8",
        stdio: ["ignore", "pipe", "pipe"]
      }
    );

    if (result.status !== 0) {
      failed = true;
      console.error(`\nCommit ${hash.slice(0, 7)} failed commitlint: ${subject}`);
      if (result.stdout) {
        console.error(result.stdout.trimEnd());
      }
      if (result.stderr) {
        console.error(result.stderr.trimEnd());
      }
      if (result.error) {
        console.error(result.error.message);
      }
    }
  }
} finally {
  rmSync(tmp, { recursive: true, force: true });
}

if (failed) {
  process.exit(1);
}

console.log(`Validated ${hashes.length} commit message(s).`);
