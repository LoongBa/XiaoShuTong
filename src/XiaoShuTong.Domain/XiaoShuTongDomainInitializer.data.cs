using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Threading.Tasks;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Learning;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Entities.Rank;
using XiaoShuTong.Entities.TaskManagement;
using XiaoShuTong.Tools;

namespace XiaoShuTong;

// ══════════════════════════════════════════════════════════════════
// 实体同步与种子数据维护文件
//
// ⚠ 规则：增加/删除 Entity 后必须同步此文件。
//    - 新增 Entity → 在此添加种子数据（测试/初始化用）
//    - 删除 Entity → 移除此处对应的种子数据逻辑
//
// 此文件为 partial class，与 XiaoShuTongDomainInitializer.cs 配对。
// 修改此文件无需触及主初始化器。Agent 按此规则执行。
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// 实体同步与种子数据。增加/删除 Entity 时同步更新此处。
/// </summary>
public partial class XiaoShuTongDomainInitializer
{
    /// <summary>
    /// Data 分部类：只包含实体同步 + 种子数据逻辑。
    /// </summary>
    internal static class Data
    {
        // ── V0.6.3 联调身份常量（与 XiaoShuTongUserHelper 白名单对齐，无 Users 表，裸 long 外键） ──
        private const long SeedStudentId = 10001;   // xiaoming（学生）
        private const long SeedOwnerId = 10002;     // owner01（群主）
        private const long SeedParentId = 10003;    // parent01（家长）
        private const string SeedBankId = "bank-ch-7a";
        private static readonly string[] SeedQuestionIds = ["Q-demo-7a-0001", "Q-demo-7a-0002", "Q-demo-7a-0003"];

        // ── V0.6.4 战绩榜联调常量（群组 UId 固定供前端 rank.tsx 硬编码 scopeId；榜内补充 2 名学生） ──
        private const string SeedGroupUid = "group-demo-73";
        private const long SeedStudentBId = 10011;  // xiaomei（学生，战绩榜）
        private const long SeedStudentCId = 10012;  // xiaogang（学生，战绩榜）

        /// <summary>
        /// 实体同步 + 种子数据插入入口。
        /// Agent：按业务需求在此添加/移除实体对应的种子数据（开发/测试环境）。
        /// </summary>
        public static async Task OnEnsureDataReadyAsync(IServiceProvider sp)
        {
            // IFreeSql 为 Scoped 注册（框架 XML：按请求租户解析连接串），root provider 直接解析会抛
            // "Cannot resolve scoped service 'IFreeSql' from root provider"——须 CreateScope 包裹
            using var scope = sp.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IFreeSql>();

            // 幂等：任一已有即跳过（现有性判断，重复启动/多环境安全）
            var hasGroup = await repo.Select<Groups>().AnyAsync();
            if (hasGroup)
                return;

            var now = DateTime.UtcNow;
            // UTC+8 业务日（与 RankSnapshotFreezeJobTests.Today() 口径一致，DailyStats/RankSnapshots 共用）
            var businessDay = DateOnly.FromDateTime(now.AddHours(8));

            // 1. 群组链路（群主 10002 → 群 → 学生 10001/10011/10012、家长 10003 成员）
            var group = new Groups
            {
                UId = SeedGroupUid,
                OwnerId = SeedOwnerId,
                Name = "七(3)班（联调）",
                Subject = "语文",
                Grade = "七年级",
                Status = GroupStatus.Active,
                RankEnabled = true,
                CreateTime = now,
                UpdateTime = now,
            };
            await repo.Insert(group).ExecuteAffrowsAsync();

            await repo.Insert(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = SeedStudentId,
                Role = MemberRole.Student,
                Nickname = "小明",
                JoinedAt = now,
            } as GroupMembers).ExecuteAffrowsAsync();
            await repo.Insert(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = SeedParentId,
                Role = MemberRole.Parent,
                Nickname = "小明家长",
                JoinedAt = now,
            } as GroupMembers).ExecuteAffrowsAsync();
            await repo.Insert(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = SeedStudentBId,
                Role = MemberRole.Student,
                Nickname = "小美",
                JoinedAt = now,
            } as GroupMembers).ExecuteAffrowsAsync();
            await repo.Insert(new GroupMembers
            {
                UId = UidGenerator.NewId(),
                GroupId = group.Id,
                UserId = SeedStudentCId,
                Role = MemberRole.Student,
                Nickname = "小刚",
                JoinedAt = now,
            } as GroupMembers).ExecuteAffrowsAsync();

            // 2. 题库链路（语文示例题库 + 3 题 R1 补全，Keywords 判题元数据对齐测试范式）
            await repo.Insert(new Banks
            {
                UId = UidGenerator.NewId(),
                BankId = SeedBankId,
                Name = "语文示例题库（联调）",
                Subject = Subject.Chinese,
                Version = "V1.0",
                Purpose = BankPurpose.Memorize,
                Privacy = BankPrivacy.Public,
                OwnerId = null,
                JsonPath = $"bank.{SeedBankId}.json",
                Tags = [],
                Status = BankStatus.Active,
                CreateTime = now,
                UpdateTime = now,
            } as Banks).ExecuteAffrowsAsync();

            await InsertDemoQuestionsAsync(repo, now);

            // 3. 任务链路（群主 10002 布置 → 学生 10001 分配）
            var task = new Tasks
            {
                UId = UidGenerator.NewId(),
                OwnerId = SeedOwnerId,
                GroupId = group.Id,
                BankId = SeedBankId,
                Title = "《观沧海》背诵（联调）",
                Description = "V0.6.3 端到端联调任务：3 题",
                QuestionIds = SeedQuestionIds,
                QuestionCount = SeedQuestionIds.Length,
                Scenario = TaskScenario.Memorize,
                SessionType = TaskSessionType.Progressive,
                AllowRedo = false,
                StartedAt = now,
                DeadlineAt = now.AddDays(7),
                Status = TaskStatus.Active,
                CreateTime = now,
                UpdateTime = now,
            };
            await repo.Insert(task).ExecuteAffrowsAsync();

            await repo.Insert(new TaskAssignments
            {
                UId = UidGenerator.NewId(),
                TaskId = task.Id,
                UserId = SeedStudentId,
                Status = AssignmentStatus.Pending,
                Progress = 0,
                ConsumedQuestionIds = string.Empty,
                AssignedAt = now,
                CreateTime = now,
                UpdateTime = now,
            } as TaskAssignments).ExecuteAffrowsAsync();

            // 4. 家长学生授权链（10003 → 10001）
            await repo.Insert(new ParentStudentRelations
            {
                UId = UidGenerator.NewId(),
                ParentId = SeedParentId,
                StudentId = SeedStudentId,
                Relation = ParentRelation.Parent,
                CreateTime = now,
                UpdateTime = now,
            } as ParentStudentRelations).ExecuteAffrowsAsync();

            // 5. 战绩榜链路（群组固定 UId 供前端 rank.tsx 硬编码 scopeId；
            //    DailyStats/KnowledgeMastery 为业务底座，RankSnapshots 直插免 FreezeJob 立即可查）

            // 5.1 每日统计（一人一日一行，Accuracy 为当日正确率，对齐 SeedDailyAsync 字段形状）
            await repo.Insert(new DailyStats
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                StatDate = businessDay,
                LearnedCount = 10,
                StarredCount = 3,
                ReviewCount = 5,
                Accuracy = 0.9,
                StudySeconds = 1800,
            } as DailyStats).ExecuteAffrowsAsync();
            await repo.Insert(new DailyStats
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                StatDate = businessDay,
                LearnedCount = 8,
                StarredCount = 2,
                ReviewCount = 4,
                Accuracy = 0.8,
                StudySeconds = 1500,
            } as DailyStats).ExecuteAffrowsAsync();
            await repo.Insert(new DailyStats
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                StatDate = businessDay,
                LearnedCount = 6,
                StarredCount = 1,
                ReviewCount = 3,
                Accuracy = 0.7,
                StudySeconds = 1200,
            } as DailyStats).ExecuteAffrowsAsync();

            // 5.2 知识点掌握度（一人一行，Accuracy 聚合正确率，对齐 SeedMasteryAsync 字段形状）
            await repo.Insert(new KnowledgeMastery
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                Subject = "chinese",
                KnowledgePoint = "观沧海-背诵",
                State = MemoryState.Fuzzy,
                Accuracy = 0.8,
                AttemptCount = 1,
            } as KnowledgeMastery).ExecuteAffrowsAsync();
            await repo.Insert(new KnowledgeMastery
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                Subject = "chinese",
                KnowledgePoint = "观沧海-背诵",
                State = MemoryState.Fuzzy,
                Accuracy = 0.7,
                AttemptCount = 1,
            } as KnowledgeMastery).ExecuteAffrowsAsync();
            await repo.Insert(new KnowledgeMastery
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                Subject = "chinese",
                KnowledgePoint = "观沧海-背诵",
                State = MemoryState.Fuzzy,
                Accuracy = 0.6,
                AttemptCount = 1,
            } as KnowledgeMastery).ExecuteAffrowsAsync();

            // 5.3 排名快照直插（3 用户 × Accuracy/Mastery，ScopeType=Group + SeedGroupUid 供战绩榜查询）
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Accuracy,
                MetricValue = 0.75m,
                Rank = 1,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Mastery,
                MetricValue = 0.6m,
                Rank = 1,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Accuracy,
                MetricValue = 0.70m,
                Rank = 2,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Mastery,
                MetricValue = 0.55m,
                Rank = 2,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Accuracy,
                MetricValue = 0.60m,
                Rank = 3,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Mastery,
                MetricValue = 0.40m,
                Rank = 3,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();

            // 5.4 战力榜快照直插（3 用户 × Streak/Volume/PkWins，供战力榜查询；PkWins 切片 07 未实施 → 0）
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Streak,
                MetricValue = 3m,
                Rank = 1,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Volume,
                MetricValue = 10m,
                Rank = 1,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.PkWins,
                MetricValue = 0m,
                Rank = 1,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Streak,
                MetricValue = 2m,
                Rank = 2,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Volume,
                MetricValue = 8m,
                Rank = 2,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentBId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.PkWins,
                MetricValue = 0m,
                Rank = 2,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Streak,
                MetricValue = 1m,
                Rank = 3,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.Volume,
                MetricValue = 6m,
                Rank = 3,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
            await repo.Insert(new RankSnapshots
            {
                UId = UidGenerator.NewId(),
                UserId = SeedStudentCId,
                ScopeType = RankScopeType.Group,
                ScopeId = SeedGroupUid,
                Subject = "All",
                MetricType = RankMetricType.PkWins,
                MetricValue = 0m,
                Rank = 3,
                SnapshotDate = businessDay,
            } as RankSnapshots).ExecuteAffrowsAsync();
        }

        /// <summary>插入 3 道《观沧海》R1 补全题（Keywords 组感知 JSON，对齐 SubmitAttemptServiceTests 范式）</summary>
        private static async Task InsertDemoQuestionsAsync(IFreeSql repo, DateTime now)
        {
            // 观沧海四句（与测试 FullKeywords 同构）
            (string QuestionId, string Stem, string[] Keywords, string Hint)[] demo =
            [
                ("Q-demo-7a-0001", "东临碣石，___", ["若出其中", "星汉灿烂", "幸甚至哉", "歌以咏志"], "首字：若"),
                ("Q-demo-7a-0002", "水何澹澹，___", ["山岛竦峙", "秋风萧瑟", "洪波涌起"], "首字：山"),
                ("Q-demo-7a-0003", "日月之行，___", ["若出其中", "星汉灿烂", "若出其里"], "首字：若"),
            ];

            foreach (var (qid, stem, keywords, hint) in demo)
            {
                var keywordsJson = JsonSerializer.Serialize(
                    keywords.Select(k => new KeywordGroup([k])).ToList());

                await repo.Insert(new Questions
                {
                    UId = UidGenerator.NewId(),
                    QuestionId = qid,
                    BankId = SeedBankId,
                    ChapterId = "7a",
                    QType = QuestionType.R1,
                    Content = $"{{\"questionId\":\"{qid}\",\"stem\":\"{stem}\"}}",
                    Keywords = keywordsJson,
                    KnowledgePoints = ["观沧海-背诵"],
                    Difficulty = 0,
                    Status = QuestionStatus.Active,
                    Hint = hint,
                    CreateTime = now,
                    UpdateTime = now,
                } as Questions).ExecuteAffrowsAsync();
            }
        }
    }
}