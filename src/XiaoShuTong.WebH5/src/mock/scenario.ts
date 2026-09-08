// ── mock 场景切换单例 ──
// 导入后可在任何页面/组件中 setScenario 切换状态
// 调试用：window.__mockScenario = scenario;

import { createScenarioContext, MockTransport } from "@tkwf/tsclient-mock";
import { db, handlers } from "@/gql/ts-client.mock.g";
import { scenarioOverrides } from "@/mock/data";

const transport = new MockTransport(handlers, scenarioOverrides);
export const scenario = createScenarioContext({ db, transport });

// 调试用：window.__mockScenario = scenario;
