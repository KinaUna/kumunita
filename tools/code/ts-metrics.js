#!/usr/bin/env node
// ts-metrics.js — the IMPROVE-CODE lane's TypeScript metrics (C00).
//
// Usage:
//   node tools/code/ts-metrics.js <root> [output-dir]
//
// Walks src/Kumunita.Web/client/**/*.ts (skipping node_modules) and writes
// ts.csv into <output-dir> (default: <root>/.tmp/metrics) with rows:
//
//   file,kind,name,code_lines,cyclomatic
//   <file>,file,,<code lines>,0
//   <file>,function,<name>,<body lines>,<cc>
//
// Approach (documented choice, per the C00 plan note "a plain
// regex/line-based approach is fine for C00 (the goal is the baseline, not a
// full parse)"): a LINE-BASED heuristic, no full TS parse. A named function
// is a line matching any of:
//   - `function name(...)`, `function* name(...)` (with optional
//     `export` / `async` prefix)
//   - `const|let|var name = function ...`, `const|let|var name = (args) =>`
//     (with optional `async`)
// whose body is the block of lines from the first `{` that raises the
// brace depth above 0 to the line where it returns to 0 (strings and
// comments masked per line, so a `{` inside a literal does not count).
// The body's `code_lines` is that line span. The `cyclomatic` count is
// 1 + the number of decision tokens inside the body: `if (`, `for (`,
// `while (`, `&&`, `||`, and ternary `? :` (a `case` label does not count,
// mirroring the C# method rule in CodeMetrics/Program.cs).
//
// Expression arrows with no block body (e.g. `const L = (a,b) => a || b`)
// have no measurable body and are skipped — they are one-liners, always
// under the ceiling, and not the targets this baseline exists to track.
//
// Dependency-free (node >= 14) so it runs on a bare CI box without the
// project's typescript package. The plan's NODE_PATH invocation is a hint
// for a fuller parser later; C00 does not need it.

"use strict";

const fs = require("fs");
const path = require("path");

const args = process.argv.slice(2);
if (args.length < 1) {
  console.error("usage: ts-metrics.js <root> [output-dir]");
  process.exit(2);
}
const root = path.resolve(args[0]);
const outDir = path.resolve(args.length >= 2 ? args[1] : path.join(root, ".tmp", "metrics"));

function clientDir() {
  return path.join(root, "src", "Kumunita.Web", "client");
}

function* walkTs(dir) {
  if (!fs.existsSync(dir)) { return; }
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === "node_modules") { continue; }
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) { yield* walkTs(full); }
    else if (entry.isFile() && entry.name.endsWith(".ts")) { yield full; }
  }
}

// mask string/char/template literals and line comments (keep newlines so the
// line structure is preserved; replace masked chars with NUL)
function maskLine(line) {
  let out = "";
  let i = 0;
  const n = line.length;
  while (i < n) {
    const c = line[i];
    if (c === "/" && line[i + 1] === "/") { break; } // rest is a comment
    if (c === '"' || c === "'" || c === "`") {
      const quote = c;
      i++;
      while (i < n && line[i] !== quote) {
        if (line[i] === "\\") { out += "\u0000\u0000"; i += 2; continue; }
        out += "\u0000";
        i++;
      }
      i++; // closing quote
      continue;
    }
    out += c;
    i++;
  }
  return out;
}

function isDeclaration(line, nameOut) {
  const t = line.trim();
  let m;
  // function name(...)  /  function* name(...)   (export/async optional)
  m = /^(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s*\*?\s*([A-Za-z_$][\w$]*)\s*\(/.exec(t);
  if (m) { nameOut[0] = m[1]; return true; }
  // const|let|var name = function ... / = (args) => / = async (args) => / = async function
  m = /^(?:export\s+)?(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(?:async\s+)?(?:function\b|\(\s*[\w$]+\s*[,)]|[\w$]+\s*[,)]\s*=>|[\w$]+\s*=>)/.exec(t);
  if (m) { nameOut[0] = m[1]; return true; }
  return false;
}

function bodyLineSpan(lines, startIdx) {
  // From startIdx, find the first line that raises brace depth > 0, then the
  // line where it returns to 0. Returns [bodyStart, bodyEnd] (0-based) or
  // null if there is no block body.
  let depth = 0;
  let bodyStart = -1;
  for (let i = startIdx; i < lines.length; i++) {
    const masked = maskLine(lines[i]);
    for (let j = 0; j < masked.length; j++) {
      const c = masked[j];
      if (c === "{") {
        depth++;
        if (bodyStart < 0) { bodyStart = i; }
      } else if (c === "}") {
        depth--;
      }
    }
    if (bodyStart >= 0 && depth <= 0) { return [bodyStart, i]; }
  }
  if (bodyStart >= 0) { return [bodyStart, lines.length - 1]; }
  return null;
}

function cyclomatic(lines, startIdx, endIdx) {
  let cc = 1;
  const re = /\b(if|for|while)\s*\(|&&|\|\||\?\s*:(?![\w])/g;
  for (let i = startIdx; i <= endIdx && i < lines.length; i++) {
    const masked = maskLine(lines[i]);
    let m;
    re.lastIndex = 0;
    while ((m = re.exec(masked)) !== null) { cc++; }
  }
  return cc;
}

function rel(root, p) {
  const r = root.split(path.sep).join("/");
  let out = p.split(path.sep).join("/");
  if (out.startsWith(r + "/")) { out = out.slice(r.length + 1); }
  return out;
}

function main() {
  const files = [];
  const functions = [];

  for (const file of walkTs(clientDir())) {
    let src;
    try { src = fs.readFileSync(file, "utf8"); } catch { continue; }
    const lines = src.split("\n");

    let codeLines = 0;
    let inBlock = false;
    for (const line of lines) {
      const t = line.trim();
      if (t.length === 0) { continue; }
      if (inBlock) {
        if (t.includes("*/")) { inBlock = false; }
        continue;
      }
      if (t.startsWith("//")) { continue; }
      if (t.startsWith("/*") && !t.includes("*/")) { inBlock = true; continue; }
      codeLines++;
    }
    const relFile = rel(root, file);
    files.push({ file: relFile, codeLines });

    const nameOut = [""];
    for (let i = 0; i < lines.length; i++) {
      if (!isDeclaration(lines[i], nameOut)) { continue; }
      const span = bodyLineSpan(lines, i);
      if (!span) { continue; } // expression arrow, no block body
      functions.push({
        file: relFile,
        name: nameOut[0],
        lines: span[1] - span[0] + 1,
        cc: cyclomatic(lines, span[0], span[1]),
      });
    }
  }

  fs.mkdirSync(outDir, { recursive: true });
  const out = path.join(outDir, "ts.csv");
  const esc = (s) => (s.includes(",") || s.includes('"') ? '"' + s.replace(/"/g, '""') + '"' : s);
  const rows = ["file,kind,name,code_lines,cyclomatic"];
  for (const f of files) { rows.push(esc(f.file) + ",file,," + f.codeLines + ",0"); }
  for (const f of functions) { rows.push(esc(f.file) + ",function," + esc(f.name) + "," + f.lines + "," + f.cc); }
  fs.writeFileSync(out, rows.join("\n") + "\n");
  console.log("ts-metrics: wrote " + out + " (files=" + files.length + " functions=" + functions.length + ")");
}

main();
