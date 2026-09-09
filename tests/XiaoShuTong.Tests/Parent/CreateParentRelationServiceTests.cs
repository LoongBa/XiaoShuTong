using XiaoShuTong.DataServices.Parent;
using XiaoShuTong.Entities.Parent;
using XiaoShuTong.Services.Parent;
using XiaoShuTong.Tools;
using TKW.Framework.Domain.Testing.xUnit;
using Xunit;

namespace XiaoShuTong.Tests.Parent;

/// <summary>
/// UC-10.1 家长关联孩子（CreateParentRelationService）Contract 测试
/// 覆盖 BR：BR-02 同一 (家长,孩子) 幂等（UNIQUE）| 关系枚举解析（默认 Parent / Guardian）
/// </summary>
[Collection("XiaoShuTongDomain")]
[Trait("Category", "Contract")]
public class CreateParentRelationServiceTests(XiaoShuTongDomainTestFixture fixture, ITestOutputHelper output)
    : DomainXunitTestBase<XiaoShuTongUserInfo, XiaoShuTongDomainTestFixture>(fixture, output)
{
    private long SetUser(long id)
    {
        User.UserInfo!.Id = id;
        User.UserInfo.UserIdString = id.ToString();
        return id;
    }

    /// <summary>主流程：建立关联 → RelationUid 非空 + 已写入（默认关系 Parent）</summary>
    [Fact]
    public async Task CreateRelation_MainFlow_CreatesRelation()
    {
        var parentId = SetUser(48201);
        var svc = User.Use<CreateParentRelationService>();

        var result = await svc.ExecuteAsync(new CreateParentRelationReqDto { StudentId = 48311 }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.RelationUid));

        var ds = User.Use<ParentStudentRelationsDataService>();
        var relations = await ds.EntitySelectAsync(x => x.ParentId == parentId, ct: TestContext.Current.CancellationToken);
        var relation = Assert.Single(relations);
        Assert.Equal(48311, relation.StudentId);
        Assert.Equal(ParentRelation.Parent, relation.Relation); // 默认 Parent
    }

    /// <summary>BR-02：重复关联 → 幂等返回原记录，不重复写入（UNIQUE）</summary>
    [Fact]
    public async Task CreateRelation_Duplicate_Idempotent()
    {
        var parentId = SetUser(48202);
        var svc = User.Use<CreateParentRelationService>();
        var request = new CreateParentRelationReqDto { StudentId = 48321 };

        var first = await svc.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var second = await svc.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.RelationUid, second.RelationUid); // 幂等返回原记录

        var ds = User.Use<ParentStudentRelationsDataService>();
        var relations = await ds.EntitySelectAsync(x => x.ParentId == parentId, ct: TestContext.Current.CancellationToken);
        Assert.Single(relations); // 仅一条
    }

    /// <summary>关系枚举解析：Relation=Guardian → 存储 Guardian</summary>
    [Fact]
    public async Task CreateRelation_CustomRelation_Parsed()
    {
        var parentId = SetUser(48203);
        var svc = User.Use<CreateParentRelationService>();

        var result = await svc.ExecuteAsync(new CreateParentRelationReqDto { StudentId = 48331, Relation = "Guardian" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var ds = User.Use<ParentStudentRelationsDataService>();
        var relations = await ds.EntitySelectAsync(x => x.ParentId == parentId, ct: TestContext.Current.CancellationToken);
        var relation = Assert.Single(relations);
        Assert.Equal(ParentRelation.Guardian, relation.Relation);
    }
}
