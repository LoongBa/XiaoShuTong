import { Tkwf } from '@tkwf/tsclient';
import { operationSelection, operationVariableTypes } from '@/gql/ts-client.g';

/**
 * SDK 前缀启发式绕行执行器（V0.7.7，G16 缺陷临时方案）
 *
 * 背景：@tkwf/tsclient ServiceProxy.createUse() 用**前缀启发式**判定 GraphQL 操作类型
 * （MUTATION_PREFIXES = create/update/delete/add/remove/lock/unlock/reset，命中即 mutation）；
 * 但服务端 SG 的 IsMutationMethod 把 `Xxx_Execute` 方法**全部判定为非 mutation → 放 Query 根**。
 * 前后端判定不一致 → `removeBuddy_Execute`/`createTask_Execute`/`createStudySession_Execute`
 * 等前缀命中的方法被 SDK 误判 mutation → 发 `mutation { ... }` → 服务端 Mutation 根无此字段 → HTTP 400
 * （走查实证 2026-10-01，详见 docs/草稿/框架问题单-...G16.md）。
 *
 * 绕行：直接调 `Tkwf.User.getTransport().execute()`，**强制 type:'query'**（与服务端 Query 根一致），
 * 从 codegen 产物 operationSelection/operationVariableTypes 取 selection/variableTypes
 * （同名 main.tsx Tkwf.configure selectionMap/variableTypesMap 注册的数据）。
 *
 * 注意：该方法不经 ServiceProxy 的 userErrorHandler 事件分发（onAuthRequired 等），
 * 调用方需自行 try/catch 处理 DomainClientError（本仓 3 处调用均已有 catch，无需额外处理）。
 *
 * 临时方案：待框架侧落地 G16 explicitQueries 根治（SDK 消费 codegen type 标记）后移除。
 */
export async function executeQuery<TResult = unknown>(
  field: string,
  variables?: Record<string, unknown>,
  signal?: AbortSignal,
): Promise<TResult> {
  const transport = Tkwf.User.getTransport();
  const selection = operationSelection[field];
  const variableTypes = operationVariableTypes[field];
  const result = await transport.execute<{ [k: string]: TResult }>({
    field,
    type: 'query',
    variables,
    variableTypes,
    selection,
    sessionKey: Tkwf.User.sessionKey ?? undefined,
    signal,
  });
  // 解包 GraphQL data 键：{ [field]: 数据 } → 裸数据（对齐 ServiceProxy 解包语义）
  const value = result?.[field];
  return (value === undefined ? result : value) as TResult;
}