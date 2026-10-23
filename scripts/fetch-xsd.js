/**
 * Downloads jXchange TPG XSD zips listed on the Jack Henry Downloads page, with no authentication
 *
 * Zip layout:
 *   R<VER>TPGPub/
 *     Validation/
 *       TPG_CustomerMaster.xsd   <- extracted (*Master.xsd)
 *       oasis-*.xsd              <- extracted (dependency schemas)
 *       w3-*.xsd                 <- extracted (dependency schemas)
 *       w3-*.dtd                 <- extracted (dependency schemas)
 *       TPG_Customer.wsdl        <- ignored
 *       ...
 *
 * Extracted files:
 *   - `*Master.xsd`  : JXChange domain schemas
 *   - `oasis-*.xsd`  : OASIS WS-Security schemas (required by XmlSchemaSet compiler)
 *   - `w3-*.xsd`     : W3C XML/XMLDSig/XMLEnc schemas (required by XmlSchemaSet compiler)
 *   - `w3-*.dtd`     : W3C DTD files (required by XmlSchemaSet compiler)
 *
 * Usage:
 *   node scripts/fetch-xsd.js                   # newest listed release into ./schemas
 *   node scripts/fetch-xsd.js --all             # every listed release into ./schemas/R<VER>/
 *   node scripts/fetch-xsd.js --version 2026.1.00
 *
 * Env vars:
 *   OUTPUT_DIR   Extraction target (default: ./schemas)
 */

const https = require("https");
const fs = require("fs");
const path = require("path");
const { pipeline } = require("stream/promises");
const { parseArgs } = require("util");
const zlib = require("zlib");

const OUTPUT_DIR = process.env.OUTPUT_DIR || path.join(process.cwd(), "schemas");

const DOWNLOADS_PAGE = "https://jackhenry.dev/jxchange-soap/documentation/";
const ZIP_BASE = "https://jkhy.github.io/devrel-assets/cms-files/soap/";

// ! Jack Henry lists only the two most recent releases and deletes older zips
// Used when the downloads page lists no TPG zips, as of 2026-10-22
const FALLBACK_ZIPS = [`${ZIP_BASE}TPG_R2026.1.00_XSD.zip`, `${ZIP_BASE}TPG_R2026.0.06_XSD.zip`];

const REQUEST_TIMEOUT_MS = 60_000;

// JXChange domain schemas
const MASTER_XSD_RE = /^TPG_\w+Master\.xsd$/i;

// ! OASIS and W3C dependency schemas must sit next to the Master XSDs, since LocalFolderXmlResolver only looks in that folder
const DEPS_RE = /^(?:oasis-[^/\\]+\.xsd|w3-[^/\\]+\.xsd|w3-[^/\\]+\.dtd)$/i;

/**
 * Resolves with the response for url after following redirects
 *
 * @throws {Error} On a non-2xx status, too many redirects, or an idle socket past REQUEST_TIMEOUT_MS
 */
function get(url, hops = 0) {
	return new Promise((resolve, reject) => {
		if (hops > 5) return reject(new Error("Too many redirects"));
		const req = https.get(url, { headers: { "User-Agent": "JXSchemaGenerator/1.0" }, timeout: REQUEST_TIMEOUT_MS }, (res) => {
			if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
				res.resume();
				return resolve(get(new URL(res.headers.location, url).toString(), hops + 1));
			}
			if (res.statusCode < 200 || res.statusCode >= 300) {
				res.resume();
				return reject(new Error(`HTTP ${res.statusCode}: ${url}`));
			}
			resolve(res);
		});
		req.on("timeout", () => req.destroy(new Error(`Timed out after ${REQUEST_TIMEOUT_MS} ms: ${url}`)));
		req.on("error", reject);
	});
}

async function fetchText(url) {
	const res = await get(url);
	const chunks = [];
	for await (const chunk of res) chunks.push(chunk);
	return Buffer.concat(chunks).toString("utf-8");
}

async function downloadFile(url, dest) {
	const res = await get(url);
	const total = parseInt(res.headers["content-length"] || "0", 10);
	let received = 0;

	res.on("data", (chunk) => {
		received += chunk.length;
		if (total && process.stdout.isTTY) {
			const pct = Math.round((received / total) * 100);
			process.stdout.write(`\r    ${pct}% (${(received / 1024).toFixed(0)} / ${(total / 1024).toFixed(0)} KB)`);
		}
	});

	// ! pipeline rejects on a dropped connection, where pipe would leave the promise pending and exit 0
	await pipeline(res, fs.createWriteStream(dest));
	if (process.stdout.isTTY) process.stdout.write("\n");
	if (total && received !== total) throw new Error(`Truncated download: ${received} of ${total} bytes from ${url}`);
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

async function discoverZips() {
	const found = new Set();

	// ! The downloads page writes unquoted href attributes
	const HREF_RE = /href\s*=\s*(?:"([^"]+)"|'([^']+)'|([^\s>]+))/gi;

	try {
		console.log(`  Scanning: ${DOWNLOADS_PAGE}`);
		const html = await fetchText(DOWNLOADS_PAGE);

		let m;
		while ((m = HREF_RE.exec(html)) !== null) {
			const href = (m[1] || m[2] || m[3] || "").trim();
			const name = href.match(/TPG_R[\d.]+_XSD\.zip$/i);
			if (name) found.add(`${ZIP_BASE}${name[0]}`);
		}

		console.log(`  Found ${found.size} TPG XSD zip(s)`);
	} catch (e) {
		console.log(`  Scan failed (${e.message})`);
	}

	if (found.size === 0) {
		console.log("  Using hardcoded fallback URLs");
		FALLBACK_ZIPS.forEach((u) => found.add(u));
	}

	return Array.from(found).sort(newerFirst);
}

/**
 * Yields every file entry in a ZIP buffer as { filename, inflate }, where inflate() returns the entry's bytes
 *
 * ! Sizes come from the central directory because Jack Henry's zips set the data-descriptor flag and leave every local header size at 0
 * @see https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
 */
function* iterZipEntries(buf) {
	let eocd = -1;
	for (let i = buf.length - 22; i >= Math.max(0, buf.length - 22 - 0xffff); i--) {
		// ! A signature inside the archive comment is not the record unless the comment runs exactly to the end of the file
		if (buf.readUInt32LE(i) === 0x06054b50 && i + 22 + buf.readUInt16LE(i + 20) === buf.length) {
			eocd = i;
			break;
		}
	}
	if (eocd === -1) throw new Error("Invalid ZIP: end of central directory not found");

	const count = buf.readUInt16LE(eocd + 10);
	let pos = buf.readUInt32LE(eocd + 16);

	for (let n = 0; n < count; n++) {
		if (buf.readUInt32LE(pos) !== 0x02014b50) throw new Error("Invalid ZIP: bad central directory entry");

		const compression = buf.readUInt16LE(pos + 10);
		const compressedSize = buf.readUInt32LE(pos + 20);
		const uncompressedSize = buf.readUInt32LE(pos + 24);
		const nameLen = buf.readUInt16LE(pos + 28);
		const extraLen = buf.readUInt16LE(pos + 30);
		const commentLen = buf.readUInt16LE(pos + 32);
		const localOffset = buf.readUInt32LE(pos + 42);
		const filename = buf.toString("utf-8", pos + 46, pos + 46 + nameLen);
		pos += 46 + nameLen + extraLen + commentLen;

		if (filename.endsWith("/")) continue;

		const inflate = () => {
			if (compression !== 0 && compression !== 8) throw new Error(`Unsupported compression method ${compression} for ${filename}`);
			const start = localOffset + 30 + buf.readUInt16LE(localOffset + 26) + buf.readUInt16LE(localOffset + 28);
			const compressed = buf.subarray(start, start + compressedSize);
			// ! maxOutputLength caps a lying entry at its declared size
			const data = compression === 8 ? zlib.inflateRawSync(compressed, { maxOutputLength: Math.max(1, uncompressedSize) }) : compressed;
			if (data.length !== uncompressedSize) throw new Error(`Size mismatch for ${filename}: ${data.length} of ${uncompressedSize} bytes`);
			return data;
		};
		yield { filename, inflate };
	}
}

/**
 * Writes the Master XSDs and their dependency schemas from the zip flat into outDir
 *
 * ! Only basenames are written, which also blocks zip-slip paths
 */
function extractFromZip(zipPath, outDir) {
	const masterFiles = [];
	const depFiles = [];
	const written = new Set();

	for (const { filename, inflate } of iterZipEntries(fs.readFileSync(zipPath))) {
		// ! Older zips were built on Windows with backslash separators, which path.basename ignores on Linux
		const normalized = filename.replaceAll("\\", "/");
		if (normalized.startsWith("__MACOSX/") || normalized.includes("/__MACOSX/")) continue;

		const basename = path.basename(normalized);
		const bucket = MASTER_XSD_RE.test(basename) ? masterFiles : DEPS_RE.test(basename) ? depFiles : null;
		if (!bucket) continue;

		if (written.has(basename.toLowerCase())) throw new Error(`Duplicate file name in zip: ${basename}`);
		written.add(basename.toLowerCase());

		fs.writeFileSync(path.join(outDir, basename), inflate());
		bucket.push(basename);
	}

	return { masterFiles, depFiles };
}

/**
 * Deletes schema files a previous fetch left at the top of dir, so releases never mix
 *
 * ! Only names the fetcher itself writes are removed, since OUTPUT_DIR can point anywhere
 */
function clearPreviousSchemas(dir) {
	for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
		if (entry.isFile() && (MASTER_XSD_RE.test(entry.name) || DEPS_RE.test(entry.name))) fs.rmSync(path.join(dir, entry.name));
	}
}

async function main() {
	console.log("");
	console.log("JXChange TPG XSD Fetcher");
	console.log("");

	const { values: args } = parseArgs({
		options: {
			all: { type: "boolean", default: false },
			version: { type: "string" },
		},
		strict: true,
	});

	let targets;
	if (args.version !== undefined) {
		if (!/^\d{4}\.\d+\.\d{2}$/.test(args.version)) {
			throw new Error(`Invalid version "${args.version}", expected e.g. 2026.1.00`);
		}
		targets = [`${ZIP_BASE}TPG_R${args.version}_XSD.zip`];
	} else {
		console.log("Discovering available TPG XSD zips...");
		const allZips = await discoverZips();
		targets = args.all ? allZips : [allZips[0]];
	}

	const multiVersion = targets.length > 1;
	fs.mkdirSync(OUTPUT_DIR, { recursive: true });

	const tmpDir = path.join(process.cwd(), ".tmp-fetch-xsd");
	fs.mkdirSync(tmpDir, { recursive: true });

	try {
		for (const url of targets) {
			const filename = decodeURIComponent(path.basename(url));
			const verMatch = filename.match(/R([\d.]+)_XSD/);
			const version = verMatch ? verMatch[1] : filename;

			console.log("");
			console.log(`Downloading: ${filename}`);

			const tmpZip = path.join(tmpDir, filename);
			await downloadFile(url, tmpZip);
			console.log(`\t${(fs.statSync(tmpZip).size / 1024).toFixed(0)} KB`);

			const extractDir = multiVersion ? path.join(OUTPUT_DIR, `R${version}`) : OUTPUT_DIR;
			fs.mkdirSync(extractDir, { recursive: true });
			clearPreviousSchemas(extractDir);

			console.log(`  Extracting XSD files -> ${extractDir}`);
			const { masterFiles, depFiles } = extractFromZip(tmpZip, extractDir);

			if (masterFiles.length === 0) {
				throw new Error(`No *Master.xsd files found in ${filename}`);
			}

			console.log(`\t${masterFiles.length} Master XSD file(s):`);
			masterFiles.sort().forEach((f) => console.log(`\t\t${f}`));

			if (depFiles.length > 0) {
				console.log(`\t${depFiles.length} dependency file(s):`);
				depFiles.sort().forEach((f) => console.log(`\t\t${f}`));
			}
		}
	} finally {
		fs.rmSync(tmpDir, { recursive: true, force: true });
	}

	console.log("");
	console.log(`\tDone. XSD files are in: ${OUTPUT_DIR}`);
}

main().catch((err) => {
	console.error("\nERROR:", err.message);
	process.exit(1);
});
