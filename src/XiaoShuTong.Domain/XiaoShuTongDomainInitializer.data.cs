using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Threading.Tasks;
using XiaoShuTong.Entities.Bank;
using XiaoShuTong.Entities.GroupManagement;
using XiaoShuTong.Entities.Parent;
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

            // 1. 群组链路（群主 10002 → 群 → 学生 10001/家长 10003 成员）
            var group = new Groups
            {
                UId = UidGenerator.NewId(),
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