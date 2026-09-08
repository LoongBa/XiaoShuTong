// ── mock HTTP server（tkwf-tsclient-mock-server 启动脚本）──
// 独立 Node 进程，纯 ESM，无浏览器依赖
// 数据来源：src/mock/data.ts（MOCK_SPEC.md §2 填充）
// 端口 5157（不与真实 WebApi 5156 冲突）

import { MockHttpServer } from "@tkwf/tsclient-mock/server";
import { MockTransport } from "@tkwf/tsclient-mock";
import { db, handlers } from "../src/gql/ts-client.mock.g";
import { initialData, scenarioOverrides } from "../src/mock/data";

const PORT = 5157;

// buildDataset 注入数据到骨架 db 实例，handlers 闭包引用自动感知
db.buildDataset(initialData, { strict: true });

const transport = new MockTransport(handlers, scenarioOverrides);

const server = new MockHttpServer({
  transport,
  port: PORT,
  cors: true,
  auth: true,
});

await server.start();
console.log(`[mock-server] http://localhost:${PORT}/graphql`);

// 优雅退出
process.on("SIGINT", async () => { await server.stop(); process.exit(0); });
process.on("SIGTERM", async () => { await server.stop(); process.exit(0); });
