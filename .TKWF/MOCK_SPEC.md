# Mock 数据规格 (MOCK_SPEC)

> 本文件定义每张 mock 数据表填什么数据、填多少、字段取什么值。
> **Agent 或开发者填充 `<项目>/src/mock/data.ts` 时，以本文件为数据策略依据。**
>
> 维护方式：
> - **一、API 映射表**：由 `gen-mock-handlers --mock-spec` 自动生成（幂等更新标记段，勿手改）
> - **二、数据策略**：手写，随需求变更更新（依据 Business.md 业务规则推导字段约束）
> - **三、表间一致性**：同业务实体的 ResDto 表间字段对齐指引

> ✅ **当前状态（2026-09-08）**：消费端已就绪（ts-client.g.ts 60 ops / 58 services，ts-client.mock.g.ts 57 张 ResDto 表）。
> 本文件 §2/§3 已改写为 **ResDto 级**（与 `ts-client.mock.g.ts` 的 `db` 表名完全对齐）——UI Agent 填充 `data.ts` 时按 §1 映射表的"需要的数据表"列直接取表名。

---

## 一、API → 数据表映射

> 每张表对应哪些 API。Agent 填充某张表时，知道会影响哪些页面。
> 自动生成，与 `ts-client.mock.g.ts` 的 `// → API:` 注释对齐。

<!-- auto-generated: mapping-table -->
| API 操作 | 类型 | 需要的数据表 | 说明 |
|----------|------|------------|------|
| `acceptBuddyInvite_Execute` | Query | `acceptBuddyInviteResDtos` |  |
| `activateMember_Execute` | Query | `activateMemberResDtos` |  |
| `bankDetail_Execute` | Query | `getBankDetailResDtos` |  |
| `buddyRank_Execute` | Query | `getBuddyRankResDtos` |  |
| `cancelSubscription_Execute` | Query | `cancelSubscriptionResDtos` |  |
| `changePasswordSecure` | Mutation | `registerResults` |  |
| `createBank_Execute` | Query | `createBankResDtos` |  |
| `createGroup_Execute` | Query | `createGroupResDtos` |  |
| `createParentRelation_Execute` | Query | `createParentRelationResDtos` |  |
| `createPkMatch_Execute` | Query | `createPkMatchResDtos` |  |
| `createStudySession_Execute` | Query | `createStudySessionResDtos` |  |
| `createTask_Execute` | Query | `createTaskResDtos` |  |
| `dashboardReport_Execute` | Query | `getDashboardReportResDtos` |  |
| `removeMember` | Mutation | `removeMemberResDtos` |  |
| `exportRosterCsv_Execute` | Query | `exportRosterCsvResDtos` |  |
| `generateInviteCodes_Execute` | Query | `generateInviteCodesResDtos` |  |
| `heatmap_Execute` | Query | `getHeatmapResDtos` |  |
| `hint_Execute` | Query | `getHintResDtos` |  |
| `importQuestions_Execute` | Query | `importQuestionsResDtos` |  |
| `importRoster_Execute` | Query | `importRosterResDtos` |  |
| `inviteBuddy_Execute` | Query | `inviteBuddyResDtos` |  |
| `joinPkMatch_Execute` | Query | `joinPkMatchResDtos` |  |
| `knowledgeCard_Execute` | Query | `getKnowledgeCardResDtos` |  |
| `listBanks_Execute` | Query | `listBanksResDtos` |  |
| `listBuddies_Execute` | Query | `listBuddiesResDtos` |  |
| `listChildren_Execute` | Query | `listChildrenResDtos` |  |
| `listGroups_Execute` | Query | `listGroupsResDtos` |  |
| `listMyTasks_Execute` | Query | `listMyTasksResDtos` |  |
| `listSubscriptions_Execute` | Query | `listSubscriptionsResDtos` |  |
| `listTasks_Execute` | Query | `listTasksResDtos` |  |
| `loginByContext` | Mutation | `loginPayloads` |  |
| `loginByPassword` | Mutation | `loginPayloads` |  |
| `logout` | Mutation | `loginPayloads` |  |
| `members` | Query | `groupDetailResDtos` |  |
| `memoryStates_Execute` | Query | `getMemoryStatesResDtos` |  |
| `myRanking_Execute` | Query | `getMyRankingResDtos` |  |
| `ownerDashboard_Execute` | Query | `getOwnerDashboardResDtos` |  |
| `periodReport_Execute` | Query | `getPeriodReportResDtos` |  |
| `pkResult_Execute` | Query | `getPkResultResDtos` |  |
| `pkStats_Execute` | Query | `getPkStatsResDtos` |  |
| `progressReport_Execute` | Query | `getProgressReportResDtos` |  |
| `rankings_Execute` | Query | `getRankingsResDtos` |  |
| `registerSecure` | Query | `registerResults` |  |
| `rejectBuddyInvite_Execute` | Query | `rejectBuddyInviteResDtos` |  |
| `removeBuddy_Execute` | Query | `removeBuddyResDtos` |  |
| `requestChallenge` | Query | `challengeResponses` |  |
| `reviewBackingPoints_Execute` | Query | `reviewBackingPointsResDtos` |  |
| `reviewQueue_Execute` | Query | `getReviewQueueResDtos` |  |
| `rosterPreview_Execute` | Query | `rosterPreviewResDtos` |  |
| `sessionResult_Execute` | Query | `getSessionResultResDtos` |  |
| `setRankEnabled_Execute` | Query | `setRankEnabledResDtos` |  |
| `startTrial_Execute` | Query | `startTrialResDtos` |  |
| `statsWrongQuestions_Execute` | Query | `statsGetWrongQuestionsResDtos` |  |
| `streak_Execute` | Query | `getStreakResDtos` |  |
| `submitAttempt_Execute` | Query | `submitAttemptResDtos` |  |
| `submitJudgmentFeedback_Execute` | Query | `submitJudgmentFeedbackResDtos` |  |
| `submitPkAnswer_Execute` | Query | `submitPkAnswerResDtos` |  |
| `taskDetail_Execute` | Query | `getTaskDetailResDtos` |  |
| `weaknessReport_Execute` | Query | `getWeaknessReportResDtos` |  |
| `wrongQuestions_Execute` | Query | `getWrongQuestionsResDtos` |  |
<!-- end-auto-generated -->

---

## 二、数据策略（ResDto 级，与 ts-client.mock.g.ts 表名对齐）

> ⚠️ **填写指引**：
> 1. 每行 = `ts-client.mock.g.ts` 中 `db` 的一张表（`{Xxx}ResDto` 级，非 Entity 级）
> 2. **最少条数**：确保该 API 页面不空即可
> 3. **关键字段约束**：字段值必须满足的条件（来源 Business.md BR）
> 4. 边界态（空/错误）由 `scenarioOverrides` 覆盖，不在 `initialData` 模拟
> 5. 时间字段用相对当前时间 ISO 字符串
> 6. 所有 ResDto 公共字段：`success=true`、`errorCode=null`（错误态走 scenarioOverrides）

### 2.1 群组管理域（Groups）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `listGroupsResDtos` | 2 | items[].groupId 唯一；name ≤64；rankEnabled 一 true 一 false | 群组-BR-03/06/12 |
| `groupDetailResDtos` | 1 | items[].members ≥3；role 覆盖 owner+student；userId 对齐主用户 | 群组-BR-08/09/11 |
| `createGroupResDtos` | 1 | success=true；groupId 非空 | 群组-BR-01~04 |
| `activateMemberResDtos` | 1 | success=true；role=student | 群组-BR-26~30 |
| `importRosterResDtos` | 1 | importId 非空；status=Ready | 群组-BR-14~18 |
| `rosterPreviewResDtos` | 1 | preview 行含 phoneLast4（脱敏）；duplicateCount=0 | 群组-BR-19 |
| `generateInviteCodesResDtos` | 1 | generatedCount>0 | 群组-BR-20~23 |
| `exportRosterCsvResDtos` | 1 | csvFileUrl 非空；columns 含后四位+码 | 群组-BR-24/25 |
| `setRankEnabledResDtos` | 1 | rankEnabled 反映开关状态 | 群组-BR-12/13 |
| `removeMemberResDtos` | 1 | removed=true | 群组-BR-09/10 |

### 2.2 学习Session域（Learning）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `createStudySessionResDtos` | 1 | sessionUid 非空；questionCount 10/20/30/50 | 学习-BR-01~05 |
| `submitAttemptResDtos` | 3 | 覆盖 result=Correct/Partial/Wrong；preState/postState 展示四阶迁移；nextReviewAt 相对当前 | 学习-BR-07~25 |
| `getHintResDtos` | 1 | hint ≤20 字；不含答案 | 学习-BR-28~30 |
| `getReviewQueueResDtos` | 2 | items[].nextReviewAt ≤ 今日（到期）；不含答案字段 | 学习-BR-31~34 |
| `getMemoryStatesResDtos` | 4 | items[] 覆盖 ✕/△/○/★ 四阶；totalCount 正确 | 学习-BR-36~38 |
| `getSessionResultResDtos` | 2 | 一空一满；correctCount ≤ totalCount；blockedPoints 含知识点 | 学习-BR-39~42 |
| `getWrongQuestionsResDtos` | 3 | items[].questionId 唯一；mastered 混合（false 为主）；knowledgePoint 非空 | 学习-BR-43~46 |

### 2.3 家校任务闭环域（Tasks）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `createTaskResDtos` | 1 | taskUid 非空；assignedCount>0 | 任务-BR-01~05 |
| `listTasksResDtos` | 2 | items[].taskUid 唯一；一 Active 一 Closed；completionRate 0~1 | 任务-BR-07~10 |
| `listMyTasksResDtos` | 2 | 一正常 + 一 Overdue（红标）；属主用户 | 任务-BR-11~13 |
| `getTaskDetailResDtos` | 1 | task 头字段完整；members[].progress 0~100；**无正确率排名列** | 任务-BR-07~10 |
| `getOwnerDashboardResDtos` | 1 | todayExecutionRate 0~1；overdueCount≥0；weakPointsTop5 升序；taskList 完成率 | 任务-BR-14~19 |

### 2.4 题库判题域（Bank + Judging）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `listBanksResDtos` | 2 | items[].bankId 唯一；privacy 覆盖 Public/Private；含官方（ownerId=null） | 题库-BR-01~03 |
| `getBankDetailResDtos` | 2 | 一公有（全 items）+ 一私有（含 topics/previewQuestions 预览）；**previewQuestions 不含答案** | 题库-BR-04~06 |
| `createBankResDtos` | 1 | success=true；bankId 非空 | 题库-BR-07~10 |
| `importQuestionsResDtos` | 2 | 一全成功（imported>0, failed=0）+ 一含失败明细（failures 带行号） | 题库-BR-11~16 |
| `getHintResDtos` | （复用 2.2） | — | 学前-Hint |
| `getKnowledgeCardResDtos` | 1 | cardType=authorCard；content 非空 | 题库-BR-21~23 |
| `reviewBackingPointsResDtos` | 1 | imported/skipped 计数；含未校验确认逻辑 | 题库-BR-26~31 |
| `submitJudgmentFeedbackResDtos` | 1 | feedbackUid 非空；status=Pending | 题库-BR-37~39 |

### 2.5 可视化激励域（Stats）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `getHeatmapResDtos` | 2 | 一空数组 + 一条含 days[] 覆盖近 7 天（statDate/starredCount/learnedCount）；starredCount 0~3 | 激励-BR-01~04 |
| `getStreakResDtos` | 1 | currentStreak≥1；longestStreak≥current | 激励-BR-05~07 |
| `getPeriodReportResDtos` | 1 | learnedCount>0；accuracy 0.5~1.0；weakPoints 升序取 5；**无群组排名** | 激励-BR-08~11 |
| `statsGetWrongQuestionsResDtos` | 3 | items[] 含 summary（题干摘要，无答案）；mastered 混合 | 激励-BR-12~15 |

### 2.6 排行榜搭子域（Rank + Buddy）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `getRankingsResDtos` | 2 | 一战力榜（rankEnabled=true, items 含 rank/userId/value/trend）+ 一战绩榜已关闭（rankEnabled=false） | 搭子-BR-01~05 |
| `getMyRankingResDtos` | 1 | rank 数值；trend=Up/Down/Flat；rankChange 反映变化 | 搭子-BR-06~08 |
| `inviteBuddyResDtos` | 1 | inviteId 非空；expiresAt=+7 天 | 搭子-BR-14~18 |
| `acceptBuddyInviteResDtos` | 1 | buddyId 非空 | 搭子-BR-19~21 |
| `rejectBuddyInviteResDtos` | 1 | success=true | 搭子-BR-22/23 |
| `listBuddiesResDtos` | 2 | 一含 2 个 accepted（双向）+ 一空；items[].rank 含 rank/metricValue | 搭子-BR-24~27 |
| `getBuddyRankResDtos` | 1 | rank/metricValue/trend；**无答题明细** | 搭子-BR-28/29 |
| `removeBuddyResDtos` | 1 | success=true | 搭子-BR-30~32 |

### 2.7 搭子PK竞技域（Pk）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `createPkMatchResDtos` | 1 | matchUid 非空；inviteCode 4 位 | Pk-BR-01~05 |
| `joinPkMatchResDtos` | 1 | matchUid；questionCount 5/10/20；perQuestionTimeS=30 | Pk-BR-06~10 |
| `submitPkAnswerResDtos` | 2 | 一 Correct（score+10）+ 一 Wrong/Partial（score 0） | Pk-BR-11~16 |
| `getPkResultResDtos` | 2 | 一 Finished(Score, winnerId 非空, players 含 aiComment) + 一 Forfeit；**无正确率对比榜** | Pk-BR-17~20 |
| `getPkStatsResDtos` | 1 | totalMatches≥1；winRate 0~1 | Pk-BR-24~27 |

### 2.8 家长报告订阅域（Parent）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `createParentRelationResDtos` | 1 | relationUid 非空 | 家长-BR-01/02 |
| `listChildrenResDtos` | 1 | items[].studentUid；hasSubscription 混合 | 家长-BR-03/04 |
| `startTrialResDtos` | 1 | status=Trialing；trialEndAt=+7 天 | 家长-BR-05~07 |
| `listSubscriptionsResDtos` | 2 | 一 Trialing + 一 Active；status 语义正确 | 家长-BR-11/12 |
| `cancelSubscriptionResDtos` | 1 | success=true（权益至周期末） | 家长-BR-13/14/28 |
| `getDashboardReportResDtos` | 2 | 一 locked=true（未订阅仅前 2 项）+ 一完整（weekProgress/subjectsMastery）；**无群组排名** | 家长-BR-15~19 |
| `getProgressReportResDtos` | 1 | trend[].statDate 连续；vsLastWeek.learnedDelta 非 0 | 家长-BR-20~24 |
| `getWeaknessReportResDtos` | 2 | 一含 weakPoints（4 字段含 stateText）+ 一空；弱→强升序 | 家长-BR-25~27 |

### 2.9 会话/通用（Auth）

| 表 | 最少条数 | 关键字段约束 | 填写参考 |
|----|---------|-------------|---------|
| `loginPayloads` | 1 | userKey 标识主用户；**平铺结构**（success+sessionKey+userName+displayName，勿嵌套 user 对象）— 主包读取硬性要求 | tkwf-tsclient-mock 登录约束 |
| `registerResults` | 1 | success=true | Auth |
| `challengeResponses` | 1 | challenge 令牌非空（registerSecure 前置） | Auth |

---

## 三、表间一致性（ResDto 级跨表对齐）

> ResDto 表为 API 响应切片，同一业务实体的字段需在关联 ResDto 表间保持一致（UI 跳转/详情依赖）。
> 无传统外键（mock DB 无 JOIN），一致性靠填充时保证。

| 业务实体 | 主键字段 | 需一致的 ResDto 表 | 一致性要求 |
|---------|---------|-------------------|-----------|
| Groups | groupId | `listGroupsResDtos` / `groupDetailResDtos` / `getOwnerDashboardResDtos` | 同一 groupId 的 name/subject/rankEnabled 一致 |
| Tasks | taskUid | `listTasksResDtos` / `listMyTasksResDtos` / `getTaskDetailResDtos` / `getOwnerDashboardResDtos.taskList` | 同一 taskUid 的 title/completionRate/status 一致 |
| Banks | bankId | `listBanksResDtos` / `getBankDetailResDtos` | 同一 bankId 的 name/subject/privacy 一致 |
| Questions | questionId | `getWrongQuestionsResDtos` / `statsGetWrongQuestionsResDtos` / `getSessionResultResDtos.blockedPoints` | 同一 questionId 的知识点/状态一致 |
| MemoryStates | questionId | `getMemoryStatesResDtos` / `getReviewQueueResDtos` | 同一 questionId 的 state/nextReviewAt 一致 |
| StudyBuddies | userId 对 | `listBuddiesResDtos` / `getBuddyRankResDtos` | 搭子对的 rank/metricValue 一致 |
| RankSnapshots | userId+scope | `getRankingsResDtos.items` / `getMyRankingResDtos` | 同一用户同范围的 rank/value 一致 |
| Subscriptions | subscriptionUid | `listSubscriptionsResDtos` / `getDashboardReportResDtos` | 同一订阅的 status/trialEndAt 一致 |