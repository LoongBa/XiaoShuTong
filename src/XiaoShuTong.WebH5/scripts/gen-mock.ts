#!/usr/bin/env tsx
/**
 * gen-mock.ts — 生成 tsp-client.mock.g.ts（MockTransport handlers + 内存 DB 骨架）
 *
 * 底层调用 @tkwf/tsclient-mock 的 CLI（gen-mock-handlers）：
 *   gen-mock-handlers --input <ts-client.g.ts> --output <ts-client.mock.g.ts>
 *
 * 注意：本脚本是"薄封装"——修复/扩展生成逻辑应改 TKWF-tsclient-mock 仓库源码
 * （src/codegen/*），再 build 后重跑本脚本。
 */

import { execSync } from "child_process";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const input = resolve(__dirname, "..", "src", "gql", "ts-client.g.ts");
const output = resolve(__dirname, "..", "src", "gql", "ts-client.mock.g.ts");

try {
  execSync(
    `npx gen-mock-handlers --input "${input}" --output "${output}"`,
    { stdio: "inherit", cwd: resolve(__dirname, "..") },
  );
  console.log("[gen-mock] OK");
} catch (err) {
  console.error("[gen-mock] FAILED:", (err as Error).message);
  process.exit(1);
}