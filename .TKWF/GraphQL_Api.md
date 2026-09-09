# XiaoShuTong WebH5 API 契约 — 前端/Agent 消费版

> 自动生成（@tkwf/tsclient v1.0.6）｜生成时间：2026-09-09 23:06:01
> 源：schema.graphql（禁止手改；缺失/过时报告补齐）

## 一、操作概览（全部域）

| 域 | Query 数 | Mutation 数 | 已暴露 | 未暴露 |
|---|:---:|:---:|:---:|:---:|
| auth | 0 | 3 | 3 | 0 |
| bank_Execute | 1 | 0 | 1 | 0 |
| banks_Execute | 1 | 0 | 1 | 0 |
| buddies_Execute | 1 | 0 | 1 | 0 |
| buddy_Execute | 1 | 0 | 1 | 0 |
| children_Execute | 1 | 0 | 1 | 0 |
| group_Execute | 1 | 0 | 1 | 0 |
| groups_Execute | 1 | 0 | 1 | 0 |
| member | 0 | 1 | 1 | 0 |
| myTasks_Execute | 1 | 0 | 1 | 0 |
| other | 40 | 1 | 41 | 0 |
| parentRelation_Execute | 1 | 0 | 1 | 0 |
| pkMatch_Execute | 1 | 0 | 1 | 0 |
| studySession_Execute | 1 | 0 | 1 | 0 |
| subscriptions_Execute | 1 | 0 | 1 | 0 |
| task_Execute | 1 | 0 | 1 | 0 |
| tasks_Execute | 1 | 0 | 1 | 0 |
| **合计** | **54** | **5** | **59** | **0** |

## 二、按域操作清单

| Domain | GraphQL 操作 | 分类 | 说明 | 参数 (TS) | 返回 | 状态 |
|---|---|---|---|---|---|---|
| auth | `loginByPassword` | Mutation |  | { userName?: string | null; password?: string | null } | `LoginPayload` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `userName`: string | null | | | | | |
| | ⤷ `displayName`: string | null | | | | | |
| | ⤷ `sessionKey`: string | null | | | | | |
| | ⤷ `accessToken`: string | null | | | | | |
| | ⤷ `refreshToken`: string | null | | | | | |
| | ⤷ `expiresAt`: string | null | | | | | |
| | ⤷ `deviceId`: string | null | | | | | |
| | ⤷ `extensions`: `ExtensionEntry` | | | | | |
| auth | `loginByContext` | Mutation |  | { input?: LoginContextInput | null } | `LoginPayload` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `userName`: string | null | | | | | |
| | ⤷ `displayName`: string | null | | | | | |
| | ⤷ `sessionKey`: string | null | | | | | |
| | ⤷ `accessToken`: string | null | | | | | |
| | ⤷ `refreshToken`: string | null | | | | | |
| | ⤷ `expiresAt`: string | null | | | | | |
| | ⤷ `deviceId`: string | null | | | | | |
| | ⤷ `extensions`: `ExtensionEntry` | | | | | |
| auth | `logout` | Mutation |  | { broadcast: boolean } | `LoginPayload` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `userName`: string | null | | | | | |
| | ⤷ `displayName`: string | null | | | | | |
| | ⤷ `sessionKey`: string | null | | | | | |
| | ⤷ `accessToken`: string | null | | | | | |
| | ⤷ `refreshToken`: string | null | | | | | |
| | ⤷ `expiresAt`: string | null | | | | | |
| | ⤷ `deviceId`: string | null | | | | | |
| | ⤷ `extensions`: `ExtensionEntry` | | | | | |
| bank_Execute | `createBank_Execute` | Query |  | { request?: CreateBankReqDtoInput | null } | `CreateBankResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `bankUid`: string | | | | | |
| | ⤷ `bankId`: string | | | | | |
| banks_Execute | `listBanks_Execute` | Query |  | { request?: ListBanksReqDtoInput | null } | `ListBanksResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `BankListItemDto` | | | | | |
| | ⤷ `total`: number | | | | | |
| buddies_Execute | `listBuddies_Execute` | Query |  | — | `ListBuddiesResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `BuddyListItemDto` | | | | | |
| buddy_Execute | `removeBuddy_Execute` | Query |  | { request?: RemoveBuddyReqDtoInput | null } | `RemoveBuddyResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| children_Execute | `listChildren_Execute` | Query |  | — | `ListChildrenResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `ChildItemDto` | | | | | |
| group_Execute | `createGroup_Execute` | Query |  | { request?: CreateGroupReqDtoInput | null } | `CreateGroupResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `groupId`: number | | | | | |
| groups_Execute | `listGroups_Execute` | Query |  | { request?: ListGroupsReqDtoInput | null } | `ListGroupsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `GroupListItemDto` | | | | | |
| | ⤷ `pageIndex`: number | | | | | |
| | ⤷ `pageSize`: number | | | | | |
| | ⤷ `totalCount`: number | | | | | |
| member | `removeMember` | Mutation |  | { request?: RemoveMemberReqDtoInput | null } | `RemoveMemberResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `removed`: boolean | | | | | |
| myTasks_Execute | `listMyTasks_Execute` | Query |  | { request?: ListMyTasksReqDtoInput | null } | `ListMyTasksResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `MyTaskItemDto` | | | | | |
| other | `requestChallenge` | Query |  | { userName?: string | null } | `ChallengeResponse` | ✅ |
| | ⤷ `challengeToken`: string | | | | | |
| | ⤷ `salt`: string | | | | | |
| | ⤷ `iterations`: number | | | | | |
| other | `registerSecure` | Query |  | { input?: RegisterSecureInput | null } | `RegisterResult` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `message`: string | null | | | | | |
| other | `bankDetail_Execute` | Query |  | { request?: GetBankDetailReqDtoInput | null } | `GetBankDetailResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `bank`: `BanksDto` | | | | | |
| | ⤷ `topics`: `TopicNodeDto` | | | | | |
| | ⤷ `previewQuestions`: `QuestionsDto` | | | | | |
| other | `knowledgeCard_Execute` | Query |  | { request?: GetKnowledgeCardReqDtoInput | null } | `GetKnowledgeCardResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `cardType`: string | | | | | |
| | ⤷ `content`: string | | | | | |
| other | `importQuestions_Execute` | Query |  | { request?: ImportQuestionsReqDtoInput | null } | `ImportQuestionsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `imported`: number | | | | | |
| | ⤷ `failed`: number | | | | | |
| | ⤷ `failures`: string[] | | | | | |
| other | `reviewBackingPoints_Execute` | Query |  | { request?: ReviewBackingPointsReqDtoInput | null } | `ReviewBackingPointsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `imported`: number | | | | | |
| | ⤷ `skipped`: number | | | | | |
| other | `acceptBuddyInvite_Execute` | Query |  | { request?: AcceptBuddyInviteReqDtoInput | null } | `AcceptBuddyInviteResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `buddyId`: string | | | | | |
| other | `buddyRank_Execute` | Query |  | { request?: GetBuddyRankReqDtoInput | null } | `GetBuddyRankResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `rank`: number | | | | | |
| | ⤷ `metricValue`: number | | | | | |
| | ⤷ `trend`: string | | | | | |
| | ⤷ `snapshotDate`: string | | | | | |
| other | `inviteBuddy_Execute` | Query |  | { request?: InviteBuddyReqDtoInput | null } | `InviteBuddyResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `inviteId`: string | | | | | |
| | ⤷ `expiresAt`: string | | | | | |
| other | `rejectBuddyInvite_Execute` | Query |  | { request?: RejectBuddyInviteReqDtoInput | null } | `RejectBuddyInviteResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| other | `activateMember_Execute` | Query |  | { request?: ActivateMemberReqDtoInput | null } | `ActivateMemberResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `groupId`: number | | | | | |
| | ⤷ `groupName`: string | | | | | |
| | ⤷ `role`: string | | | | | |
| | ⤷ `memberId`: number | | | | | |
| other | `exportRosterCsv_Execute` | Query |  | { request?: ExportRosterCsvReqDtoInput | null } | `ExportRosterCsvResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `csvFileUrl`: string | null | | | | | |
| | ⤷ `expiresAt`: string | null | | | | | |
| | ⤷ `columns`: string[] | | | | | |
| other | `generateInviteCodes_Execute` | Query |  | { request?: GenerateInviteCodesReqDtoInput | null } | `GenerateInviteCodesResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `generatedCount`: number | | | | | |
| other | `rosterPreview_Execute` | Query |  | { request?: GetRosterPreviewReqDtoInput | null } | `RosterPreviewResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `importId`: number | | | | | |
| | ⤷ `status`: string | null | | | | | |
| | ⤷ `sourceCount`: number | | | | | |
| | ⤷ `cleanedCount`: number | | | | | |
| | ⤷ `duplicateCount`: number | | | | | |
| | ⤷ `invalidCount`: number | | | | | |
| | ⤷ `preview`: string[] | | | | | |
| other | `importRoster_Execute` | Query |  | { request?: ImportRosterReqDtoInput | null } | `ImportRosterResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `importId`: number | | | | | |
| | ⤷ `status`: string | null | | | | | |
| | ⤷ `sourceCount`: number | | | | | |
| other | `members` | Query |  | { request?: GetMembersReqDtoInput | null } | `GroupDetailResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `groupId`: number | | | | | |
| | ⤷ `members`: `MemberItemDto` | | | | | |
| other | `setRankEnabled_Execute` | Query |  | { request?: SetRankEnabledReqDtoInput | null } | `SetRankEnabledResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `groupId`: number | | | | | |
| | ⤷ `rankEnabled`: boolean | | | | | |
| other | `submitJudgmentFeedback_Execute` | Query |  | { request?: SubmitJudgmentFeedbackReqDtoInput | null } | `SubmitJudgmentFeedbackResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `feedbackUid`: string | | | | | |
| | ⤷ `status`: string | | | | | |
| other | `hint_Execute` | Query |  | { request?: GetHintReqDtoInput | null } | `GetHintResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `hint`: string | | | | | |
| | ⤷ `difficultySlot`: string | | | | | |
| | ⤷ `hintSource`: string | | | | | |
| other | `memoryStates_Execute` | Query |  | { request?: GetMemoryStatesReqDtoInput | null } | `GetMemoryStatesResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `MemoryStatesDto` | | | | | |
| | ⤷ `totalCount`: number | | | | | |
| | ⤷ `pageIndex`: number | | | | | |
| | ⤷ `pageSize`: number | | | | | |
| other | `reviewQueue_Execute` | Query |  | { request?: GetReviewQueueReqDtoInput | null } | `GetReviewQueueResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `MemoryStatesDto` | | | | | |
| | ⤷ `overdueCount`: number | | | | | |
| other | `sessionResult_Execute` | Query |  | { request?: GetSessionResultReqDtoInput | null } | `GetSessionResultResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `correctCount`: number | | | | | |
| | ⤷ `totalCount`: number | | | | | |
| | ⤷ `newStarCount`: number | | | | | |
| | ⤷ `blockedPoints`: `BlockedPointDto` | | | | | |
| other | `wrongQuestions_Execute` | Query |  | { request?: GetWrongQuestionsReqDtoInput | null } | `GetWrongQuestionsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `WrongQuestionsDto` | | | | | |
| | ⤷ `total`: number | | | | | |
| other | `submitAttempt_Execute` | Query |  | { request?: SubmitAttemptReqDtoInput | null } | `SubmitAttemptResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `result`: string | | | | | |
| | ⤷ `confidence`: number | null | | | | | |
| | ⤷ `matchedKeywords`: string[] | | | | | |
| | ⤷ `missingKeywords`: string[] | | | | | |
| | ⤷ `hint`: string | | | | | |
| | ⤷ `preState`: string | | | | | |
| | ⤷ `postState`: string | | | | | |
| | ⤷ `nextReviewAt`: string | | | | | |
| other | `cancelSubscription_Execute` | Query |  | { request?: CancelSubscriptionReqDtoInput | null } | `CancelSubscriptionResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| other | `dashboardReport_Execute` | Query |  | { request?: GetDashboardReportReqDtoInput | null } | `GetDashboardReportResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `subscription`: `SubscriptionBriefDto` | | | | | |
| | ⤷ `todayCompleted`: boolean | null | | | | | |
| | ⤷ `streakDays`: number | null | | | | | |
| | ⤷ `weekProgress`: `WeekProgressDto` | | | | | |
| | ⤷ `subjectsMastery`: `SubjectMasteryDto` | | | | | |
| | ⤷ `locked`: boolean | | | | | |
| other | `progressReport_Execute` | Query |  | { request?: GetProgressReportReqDtoInput | null } | `GetProgressReportResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `trend`: `DailyStatsDto` | | | | | |
| | ⤷ `vsLastWeek`: `VsLastWeekDto` | | | | | |
| other | `weaknessReport_Execute` | Query |  | { request?: GetWeaknessReportReqDtoInput | null } | `GetWeaknessReportResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `weakPoints`: `ParentWeakPointDto` | | | | | |
| other | `startTrial_Execute` | Query |  | { request?: StartTrialReqDtoInput | null } | `StartTrialResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `subscriptionUid`: string | | | | | |
| | ⤷ `status`: string | | | | | |
| | ⤷ `trialEndAt`: string | null | | | | | |
| other | `pkResult_Execute` | Query |  | { request?: GetPkResultReqDtoInput | null } | `GetPkResultResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `status`: string | | | | | |
| | ⤷ `winnerId`: number | null | | | | | |
| | ⤷ `finishReason`: string | | | | | |
| | ⤷ `winReason`: string | null | | | | | |
| | ⤷ `players`: `PkPlayerResultDto` | | | | | |
| other | `pkStats_Execute` | Query |  | { request?: GetPkStatsReqDtoInput | null } | `GetPkStatsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `totalMatches`: number | | | | | |
| | ⤷ `wins`: number | | | | | |
| | ⤷ `draws`: number | | | | | |
| | ⤷ `winRate`: number | | | | | |
| | ⤷ `totalScore`: number | | | | | |
| other | `joinPkMatch_Execute` | Query |  | { request?: JoinPkMatchReqDtoInput | null } | `JoinPkMatchResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `matchUid`: string | | | | | |
| | ⤷ `mode`: string | | | | | |
| | ⤷ `questionCount`: number | | | | | |
| | ⤷ `perQuestionTimeS`: number | | | | | |
| other | `submitPkAnswer_Execute` | Query |  | { request?: SubmitPkAnswerReqDtoInput | null } | `SubmitPkAnswerResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `isCorrect`: boolean | | | | | |
| | ⤷ `result`: string | | | | | |
| | ⤷ `confidence`: number | null | | | | | |
| | ⤷ `score`: number | | | | | |
| other | `myRanking_Execute` | Query |  | { request?: GetMyRankingReqDtoInput | null } | `GetMyRankingResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `rank`: number | | | | | |
| | ⤷ `value`: number | | | | | |
| | ⤷ `trend`: string | | | | | |
| | ⤷ `rankChange`: number | | | | | |
| | ⤷ `rankEnabled`: boolean | | | | | |
| other | `rankings_Execute` | Query |  | { request?: GetRankingsReqDtoInput | null } | `GetRankingsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `snapshotDate`: string | | | | | |
| | ⤷ `rankEnabled`: boolean | | | | | |
| | ⤷ `items`: `RankingItemDto` | | | | | |
| other | `heatmap_Execute` | Query |  | { request?: GetHeatmapReqDtoInput | null } | `GetHeatmapResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `days`: `DailyStatsDto` | | | | | |
| other | `periodReport_Execute` | Query |  | { request?: GetPeriodReportReqDtoInput | null } | `GetPeriodReportResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `learnedCount`: number | | | | | |
| | ⤷ `accuracy`: number | null | | | | | |
| | ⤷ `starredCount`: number | | | | | |
| | ⤷ `weakPoints`: `WeakPointDto` | | | | | |
| other | `streak_Execute` | Query |  | — | `GetStreakResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `currentStreak`: number | | | | | |
| | ⤷ `longestStreak`: number | | | | | |
| other | `ownerDashboard_Execute` | Query |  | { request?: GetOwnerDashboardReqDtoInput | null } | `GetOwnerDashboardResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `todayExecutionRate`: number | | | | | |
| | ⤷ `avgProgress`: number | | | | | |
| | ⤷ `overdueCount`: number | | | | | |
| | ⤷ `weakPointsTop5`: `WeakPointDto` | | | | | |
| | ⤷ `taskList`: `DashboardTaskDto` | | | | | |
| other | `taskDetail_Execute` | Query |  | { request?: GetTaskDetailReqDtoInput | null } | `GetTaskDetailResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `task`: `TasksDto` | | | | | |
| | ⤷ `members`: `TaskAssignmentsDto` | | | | | |
| other | `changePasswordSecure` | Mutation |  | { input?: ChangePasswordSecureInput | null } | `RegisterResult` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `message`: string | null | | | | | |
| parentRelation_Execute | `createParentRelation_Execute` | Query |  | { request?: CreateParentRelationReqDtoInput | null } | `CreateParentRelationResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `relationUid`: string | | | | | |
| pkMatch_Execute | `createPkMatch_Execute` | Query |  | { request?: CreatePkMatchReqDtoInput | null } | `CreatePkMatchResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `matchUid`: string | | | | | |
| | ⤷ `inviteCode`: string | | | | | |
| studySession_Execute | `createStudySession_Execute` | Query |  | { request?: CreateStudySessionReqDtoInput | null } | `CreateStudySessionResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `sessionUid`: string | | | | | |
| | ⤷ `questionCount`: number | | | | | |
| subscriptions_Execute | `listSubscriptions_Execute` | Query |  | — | `ListSubscriptionsResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `SubscriptionItemDto` | | | | | |
| task_Execute | `createTask_Execute` | Query |  | { request?: CreateTaskReqDtoInput | null } | `CreateTaskResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `taskUid`: string | | | | | |
| | ⤷ `assignedCount`: number | | | | | |
| tasks_Execute | `listTasks_Execute` | Query |  | { request?: ListTasksReqDtoInput | null } | `ListTasksResDto` | ✅ |
| | ⤷ `success`: boolean | | | | | |
| | ⤷ `errorCode`: string | null | | | | | |
| | ⤷ `items`: `TaskListItemDto` | | | | | |
| | ⤷ `total`: number | | | | | |

## 三、DTO 字段速查

| DTO | 关键字段 |
|-----|---------|
| `AcceptBuddyInviteResDto` | success, errorCode, buddyId |
| `ActivateMemberResDto` | success, errorCode, groupId, groupName, role, memberId |
| `CancelSubscriptionResDto` | success, errorCode |
| `ChallengeResponse` | challengeToken, salt, iterations |
| `CreateBankResDto` | success, errorCode, bankUid, bankId |
| `CreateGroupResDto` | success, errorCode, groupId |
| `CreateParentRelationResDto` | success, errorCode, relationUid |
| `CreatePkMatchResDto` | success, errorCode, matchUid, inviteCode |
| `CreateStudySessionResDto` | success, errorCode, sessionUid, questionCount |
| `CreateTaskResDto` | success, errorCode, taskUid, assignedCount |
| `ExportRosterCsvResDto` | success, errorCode, csvFileUrl, expiresAt, columns |
| `GenerateInviteCodesResDto` | success, errorCode, generatedCount |
| `GetBankDetailResDto` | success, errorCode, bank → 见 \`BanksDto\`, topics → 见 \`TopicNodeDto\`, previewQuestions → 见 \`QuestionsDto\` |
| `GetBuddyRankResDto` | success, errorCode, rank, metricValue, trend, snapshotDate |
| `GetDashboardReportResDto` | success, errorCode, subscription → 见 \`SubscriptionBriefDto\`, todayCompleted, streakDays, weekProgress → 见 \`WeekProgressDto\`, subjectsMastery → 见 \`SubjectMasteryDto\`, locked |
| `GetHeatmapResDto` | success, errorCode, days → 见 \`DailyStatsDto\` |
| `GetHintResDto` | success, errorCode, hint, difficultySlot, hintSource |
| `GetKnowledgeCardResDto` | success, errorCode, cardType, content |
| `GetMemoryStatesResDto` | success, errorCode, items → 见 \`MemoryStatesDto\`, totalCount, pageIndex, pageSize |
| `GetMyRankingResDto` | success, errorCode, rank, value, trend, rankChange, rankEnabled |
| `GetOwnerDashboardResDto` | success, errorCode, todayExecutionRate, avgProgress, overdueCount, weakPointsTop5 → 见 \`WeakPointDto\`, taskList → 见 \`DashboardTaskDto\` |
| `GetPeriodReportResDto` | success, errorCode, learnedCount, accuracy, starredCount, weakPoints → 见 \`WeakPointDto\` |
| `GetPkResultResDto` | success, errorCode, status, winnerId, finishReason, winReason, players → 见 \`PkPlayerResultDto\` |
| `GetPkStatsResDto` | success, errorCode, totalMatches, wins, draws, winRate, totalScore |
| `GetProgressReportResDto` | success, errorCode, trend → 见 \`DailyStatsDto\`, vsLastWeek → 见 \`VsLastWeekDto\` |
| `GetRankingsResDto` | success, errorCode, snapshotDate, rankEnabled, items → 见 \`RankingItemDto\` |
| `GetReviewQueueResDto` | success, errorCode, items → 见 \`MemoryStatesDto\`, overdueCount |
| `GetSessionResultResDto` | success, errorCode, correctCount, totalCount, newStarCount, blockedPoints → 见 \`BlockedPointDto\` |
| `GetStreakResDto` | success, errorCode, currentStreak, longestStreak |
| `GetTaskDetailResDto` | success, errorCode, task → 见 \`TasksDto\`, members → 见 \`TaskAssignmentsDto\` |
| `GetWeaknessReportResDto` | success, errorCode, weakPoints → 见 \`ParentWeakPointDto\` |
| `GetWrongQuestionsResDto` | success, errorCode, items → 见 \`WrongQuestionsDto\`, total |
| `GroupDetailResDto` | success, errorCode, groupId, members → 见 \`MemberItemDto\` |
| `ImportQuestionsResDto` | success, errorCode, imported, failed, failures |
| `ImportRosterResDto` | success, errorCode, importId, status, sourceCount |
| `InviteBuddyResDto` | success, errorCode, inviteId, expiresAt |
| `JoinPkMatchResDto` | success, errorCode, matchUid, mode, questionCount, perQuestionTimeS |
| `ListBanksResDto` | success, errorCode, items → 见 \`BankListItemDto\`, total |
| `ListBuddiesResDto` | success, errorCode, items → 见 \`BuddyListItemDto\` |
| `ListChildrenResDto` | success, errorCode, items → 见 \`ChildItemDto\` |
| `ListGroupsResDto` | success, errorCode, items → 见 \`GroupListItemDto\`, pageIndex, pageSize, totalCount |
| `ListMyTasksResDto` | success, errorCode, items → 见 \`MyTaskItemDto\` |
| `ListSubscriptionsResDto` | success, errorCode, items → 见 \`SubscriptionItemDto\` |
| `ListTasksResDto` | success, errorCode, items → 见 \`TaskListItemDto\`, total |
| `LoginPayload` | success, userName, displayName, sessionKey, accessToken, refreshToken, expiresAt, deviceId, extensions → 见 \`ExtensionEntry\` |
| `RegisterResult` | success, message |
| `RejectBuddyInviteResDto` | success, errorCode |
| `RemoveBuddyResDto` | success, errorCode |
| `RemoveMemberResDto` | success, errorCode, removed |
| `ReviewBackingPointsResDto` | success, errorCode, imported, skipped |
| `RosterPreviewResDto` | success, errorCode, importId, status, sourceCount, cleanedCount, duplicateCount, invalidCount, preview |
| `SetRankEnabledResDto` | success, errorCode, groupId, rankEnabled |
| `StartTrialResDto` | success, errorCode, subscriptionUid, status, trialEndAt |
| `SubmitAttemptResDto` | success, errorCode, result, confidence, matchedKeywords, missingKeywords, hint, preState, postState, nextReviewAt |
| `SubmitJudgmentFeedbackResDto` | success, errorCode, feedbackUid, status |
| `SubmitPkAnswerResDto` | success, errorCode, isCorrect, result, confidence, score |

## 四、暴露状态与缺口

| 操作 | 契约来源 | 问题 | 阻塞页面 |
|------|---------|------|---------|
| — | — | ✅ 全部已注册 | — |

## 五、页面→操作映射

> 待补充（由消费端在 codegen-config.json 中配置 pageMapping）

---
## 附录：变更记录

| 版本 | 日期 | 修改内容 |
|------|------|---------|
| v1.0.6 | 09/09/2026 | 自动生成初版 |
