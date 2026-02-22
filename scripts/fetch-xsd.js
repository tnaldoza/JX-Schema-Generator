/**
 * Downloads the latest JXChange TPG XSD zip from JackHenry's public
 * GitHub-hosted developer assets - NO authentication required.
 *
 * Zip structure inside the download:
 *   TPG_R<VER>_XSD/
 *     R<VER>TPGPub/
 *       Validation/
 *         TPG_CustomerMaster.xsd   <- only these *Master.xsd files are needed
 *         TPG_DepositMaster.xsd
 *         TPG_Customer.wsdl        <- ignored
 *         oasis-*.xsd              <- ignored
 *         w3-*.xsd / w3-*.dtd     <- ignored
 *         ...
 *
 * Only `*Master.xsd` files are extracted; everything else is discarded.
 *
 * Usage:
 *   node scripts/fetch-xsd.js                   # latest only (default)
 *   node scripts/fetch-xsd.js --all             # all available versions
 *   node scripts/fetch-xsd.js --version 2026.0.00
 *
 * Env vars:
 *   OUTPUT_DIR   Extraction target (default: ./schemas)
 */

const https = require("https");
const fs = require("fs");
const path = require("path");
const zlib = require("zlib");
const { execSync } = require("child_process");

const OUTPUT_DIR =
  process.env.OUTPUT_DIR || path.join(process.cwd(), "schemas");
const DOWNLOAD_ALL = process.argv.includes("--all");
const VERSION_ARG = (() => {
  const idx = process.argv.findIndex((a) => a === "--version");
  return idx !== -1 ? process.argv[idx + 1] : null;
})();

// ! LATEST RELEASED VERSIONS AS OF 02/21/2026
// Hardcoded fallback list - update when JackHenry publishes new releases
const FALLBACK_ZIPS = [
  "https://jkhy.github.io/devrel-assets//cms-files/soap/TPG_R2026.0.00_XSD.zip",
  "https://jkhy.github.io/devrel-assets//cms-files/soap/TPG_R2025.1.05_XSD.zip",
  "https://jkhy.github.io/devrel-assets//cms-files/soap/TPG_R2025.1.04_XSD.zip",
];

// Only extract files matching this pattern; everything else is ignored
const MASTER_XSD_RE = /TPG_\w+Master\.xsd$/i;

function fetchText(url) {
  return new Promise((resolve, reject) => {
    const follow = (u, hops = 0) => {
      if (hops > 5) return reject(new Error("Too many redirects"));
      https
        .get(
          u,
          { headers: { "User-Agent": "JXSchemaGenerator/1.0" } },
          (res) => {
            if (
              res.statusCode >= 300 &&
              res.statusCode < 400 &&
              res.headers.location
            ) {
              res.resume();
              return follow(
                new URL(res.headers.location, u).toString(),
                hops + 1,
              );
            }
            const chunks = [];
            res.on("data", (c) => chunks.push(c));
            res.on("end", () =>
              resolve(Buffer.concat(chunks).toString("utf-8")),
            );
          },
        )
        .on("error", reject);
    };
    follow(url);
  });
}

function fetchJson(url, extraHeaders = {}) {
  return new Promise((resolve, reject) => {
    const follow = (u, hops = 0) => {
      if (hops > 5) return reject(new Error("Too many redirects"));
      https
        .get(
          u,
          {
            headers: {
              "User-Agent": "JXSchemaGenerator/1.0",
              Accept: "application/vnd.github+json",
              ...extraHeaders,
            },
          },
          (res) => {
            if (
              res.statusCode >= 300 &&
              res.statusCode < 400 &&
              res.headers.location
            ) {
              res.resume();
              return follow(
                new URL(res.headers.location, u).toString(),
                hops + 1,
              );
            }
            const chunks = [];
            res.on("data", (c) => chunks.push(c));
            res.on("end", () => {
              const text = Buffer.concat(chunks).toString("utf-8");
              if (res.statusCode !== 200) {
                return reject(
                  new Error(`HTTP ${res.statusCode}: ${text.slice(0, 200)}`),
                );
              }
              try {
                resolve(JSON.parse(text));
              } catch (e) {
                reject(new Error(`Invalid JSON from GitHub: ${e.message}`));
              }
            });
          },
        )
        .on("error", reject);
    };
    follow(url);
  });
}

function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    const follow = (u, hops = 0) => {
      if (hops > 5) return reject(new Error("Too many redirects"));
      https
        .get(
          u,
          { headers: { "User-Agent": "JXSchemaGenerator/1.0" } },
          (res) => {
            if (
              res.statusCode >= 300 &&
              res.statusCode < 400 &&
              res.headers.location
            ) {
              res.resume();
              return follow(
                new URL(res.headers.location, u).toString(),
                hops + 1,
              );
            }
            if (res.statusCode !== 200)
              return reject(new Error(`HTTP ${res.statusCode}: ${u}`));

            const total = parseInt(res.headers["content-length"] || "0");
            let received = 0;
            const file = fs.createWriteStream(dest);

            res.on("data", (chunk) => {
              received += chunk.length;
              if (total && process.stdout.isTTY) {
                const pct = Math.round((received / total) * 100);
                process.stdout.write(
                  `\r    ${pct}% (${(received / 1024).toFixed(0)} / ${(total / 1024).toFixed(0)} KB)`,
                );
              }
            });
            res.pipe(file);
            file.on("finish", () => {
              if (process.stdout.isTTY) process.stdout.write("\n");
              file.close(resolve);
            });
            file.on("error", reject);
          },
        )
        .on("error", reject);
    };
    follow(url);
  });
}

function parseVer(filename) {
  const m = filename.match(/TPG_R(\d+)\.(\d+)\.(\d+)_XSD/i);
  return m ? [+m[1], +m[2], +m[3]] : [0, 0, 0];
}
function newerFirst(a, b) {
  const va = parseVer(path.basename(a)),
    vb = parseVer(path.basename(b));
  for (let i = 0; i < 3; i++) if (va[i] !== vb[i]) return vb[i] - va[i];
  return 0;
}

async function discoverZipsFromGithubContents() {
  const apiUrl =
    "https://api.github.com/repos/jkhy/devrel-assets/contents/cms-files/soap";
  const token = process.env.JX_GH_READ_TOKEN || process.env.GITHUB_TOKEN;

  const headers = token ? { Authorization: `Bearer ${token}` } : {};

  const items = await fetchJson(apiUrl, headers);

  if (!Array.isArray(items)) {
    throw new Error("Unexpected GitHub API response (expected an array).");
  }

  const found = items
    .filter((x) => x && x.type === "file" && typeof x.name === "string")
    .filter((x) => /TPG_R[\d.]+_XSD\.zip/i.test(x.name))
    .map((x) => x.download_url)
    .filter(Boolean);

  return found;
}

async function discoverZips() {
  const found = new Set();

  // 1) GitHub Contents API
  try {
    console.log("  Scanning: GitHub Contents API");
    const zips = await discoverZipsFromGithubContents();
    zips.forEach((u) => found.add(u));
    if (found.size > 0) {
      console.log(`  Found ${found.size} TPG XSD zip(s) via GitHub API`);
      return Array.from(found).sort(newerFirst);
    }
  } catch (e) {
    console.log(
      `  GitHub API scan failed (${e.message}), trying HTML scans...`,
    );
  }

  // 2) HTML scans
  const HREF_RE = /href\s*=\s*(?:"([^"]+)"|'([^']+)'|([^\s>]+))/gi;

  for (const url of [
    "https://jackhenry.dev/jxchange-soap/documentation/",
    "https://jkhy.github.io/devrel-assets/cms-files/soap/",
  ]) {
    try {
      console.log(`  Scanning: ${url}`);
      const html = await fetchText(url);

      let m;
      while ((m = HREF_RE.exec(html)) !== null) {
        const href = (m[1] || m[2] || m[3] || "").trim();
        if (!/TPG_R[\d.]+_XSD\.zip/i.test(href)) continue;

        found.add(
          href.startsWith("http")
            ? href
            : `https://jkhy.github.io/devrel-assets/${href.replace(/^\/+/, "")}`,
        );
      }

      if (found.size > 0) {
        console.log(`  Found ${found.size} TPG XSD zip(s)`);
        break;
      }
    } catch (e) {
      console.log(`  Scan failed (${e.message}), trying next...`);
    }
  }

  // 3) Last resort: hardcoded
  if (found.size === 0) {
    console.log("  Using hardcoded fallback URLs");
    FALLBACK_ZIPS.forEach((u) => found.add(u));
  }

  return Array.from(found).sort(newerFirst);
}

/**
 * Reads a ZIP buffer and yields each entry as:
 *   { filename, data: Buffer }
 * Uses the local file header sequence (works for flat or nested zips).
 */
function* iterZipEntries(buf) {
  let offset = 0;
  while (offset < buf.length - 30) {
    const sig = buf.readUInt32LE(offset);
    if (sig !== 0x04034b50) break; // local file header magic

    const compression = buf.readUInt16LE(offset + 8);
    const compressedSz = buf.readUInt32LE(offset + 18);
    const filenameLen = buf.readUInt16LE(offset + 26);
    const extraLen = buf.readUInt16LE(offset + 28);
    const filename = buf.toString(
      "utf-8",
      offset + 30,
      offset + 30 + filenameLen,
    );
    const dataStart = offset + 30 + filenameLen + extraLen;
    const compressed = buf.slice(dataStart, dataStart + compressedSz);

    if (!filename.endsWith("/")) {
      const data =
        compression === 8 ? zlib.inflateRawSync(compressed) : compressed;
      yield { filename, data };
    }

    offset = dataStart + compressedSz;
  }
}

function extractMasterXsds(zipPath, outDir) {
  const buf = fs.readFileSync(zipPath);
  const extracted = [];
  let skipped = 0;

  for (const { filename, data } of iterZipEntries(buf)) {
    const basename = path.basename(filename);
    if (MASTER_XSD_RE.test(basename)) {
      fs.writeFileSync(path.join(outDir, basename), data);
      extracted.push(basename);
    } else {
      skipped++;
    }
  }

  return { extracted, skipped };
}

/**
 * Extraction strategy:
 * 1. Try system `unzip` piped through a filter - fast and handles large zips
 * 2. Fall back to pure-Node entry-by-entry read (iterZipEntries)
 */
function extractFromZip(zipPath, outDir) {
  // Try system unzip - extract only *Master.xsd from anywhere in the zip
  try {
    const result = execSync(
      `unzip -o "${zipPath}" "*Master.xsd" -d "${outDir}"`,
      { stdio: "pipe" },
    );
    // unzip preserves the nested folder structure; flatten it into outDir
    flattenMasterXsds(outDir);
    return countXsds(outDir);
  } catch {
    // system unzip not available or pattern match failed - use Node fallback
  }

  return extractMasterXsds(zipPath, outDir).extracted;
}

/** Move any *Master.xsd files found in sub-directories up to outDir and remove empty dirs. */
function flattenMasterXsds(dir) {
  const walk = (d) => {
    for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
      const full = path.join(d, entry.name);
      if (entry.isDirectory()) {
        walk(full);
        try {
          fs.rmdirSync(full);
        } catch {
          /* not empty, leave it */
        }
      } else if (MASTER_XSD_RE.test(entry.name) && d !== dir) {
        fs.renameSync(full, path.join(dir, entry.name));
      } else if (!MASTER_XSD_RE.test(entry.name) && d !== dir) {
        fs.unlinkSync(full); // delete non-Master files from sub-dirs
      }
    }
  };
  walk(dir);
}

function countXsds(dir) {
  return fs.readdirSync(dir).filter((f) => MASTER_XSD_RE.test(f));
}

async function main() {
  console.log("");
  console.log("JXChange TPG XSD Fetcher");
  console.log("");
  console.log("Discovering available TPG XSD zips...");

  const allZips = await discoverZips();

  // Select targets
  let targets;
  if (VERSION_ARG) {
    targets = allZips.filter((u) => u.includes(VERSION_ARG));
    if (targets.length === 0) {
      throw new Error(
        `No zip found matching version "${VERSION_ARG}".\nAvailable:\n` +
          allZips.map((u) => `  ${path.basename(u)}`).join("\n"),
      );
    }
  } else if (DOWNLOAD_ALL) {
    targets = allZips;
  } else {
    targets = [allZips[0]]; // latest only
  }

  const multiVersion = targets.length > 1;
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  const tmpDir = path.join(process.cwd(), ".tmp-fetch-xsd");
  fs.mkdirSync(tmpDir, { recursive: true });

  for (const url of targets) {
    const filename = decodeURIComponent(path.basename(url));
    const verMatch = filename.match(/R([\d.]+)_XSD/);
    const version = verMatch ? verMatch[1] : filename;

    console.log("");
    console.log(`Downloading: ${filename}`);

    const tmpZip = path.join(tmpDir, filename);
    await downloadFile(url, tmpZip);
    console.log(`	${(fs.statSync(tmpZip).size / 1024).toFixed(0)} KB`);

    // Multi-version mode: extract into versioned sub-folders
    const extractDir = multiVersion
      ? path.join(OUTPUT_DIR, `R${version}`)
      : OUTPUT_DIR;
    fs.mkdirSync(extractDir, { recursive: true });

    console.log(`  Extracting *Master.xsd files -> ${extractDir}`);
    const extracted = extractFromZip(tmpZip, extractDir);
    fs.unlinkSync(tmpZip);

    if (extracted.length === 0) {
      throw new Error(`No *Master.xsd files found in ${filename}`);
    }

    console.log(`	${extracted.length} Master XSD file(s):`);
    extracted.sort().forEach((f) => console.log(`		${f}`));
  }

  fs.rmSync(tmpDir, { recursive: true, force: true });

  console.log("");
  console.log(`	Done. XSD files are in: ${OUTPUT_DIR}`);
}

main().catch((err) => {
  console.error("\nERROR:", err.message);
  process.exit(1);
});
