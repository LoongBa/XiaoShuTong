using System.ComponentModel.DataAnnotations;
using TKW.Framework;
using TKW.Framework.CodeGeneration;
namespace XiaoShuTong.Entities.Learning.DTOs;

/// <summary>错题本（物化派生表，一人一题一行） 的手写 DTO 扩展</summary>
public partial record WrongQuestionsDto
{
    /// <summary>题意知识点（跨题库域注册表映射，非 SQL 列，Service 赋值）</summary>
    [DtoField(IsComputed = true)]
    public string KnowledgePoint { get; init; } = string.Empty;

    /// <summary>题目摘要（题库域关联，非 SQL 列，Service 赋值）</summary>
    [DtoField(IsComputed = true)]
    public string Summary { get; init; } = string.Empty;

    /// <summary>根据需要添加自定义验证逻辑</summary>
    partial void OnCustomValidate(EnumSceneFlags scene, List<ValidationResult> results)
    {
        // 示例：非数据库依赖的字段交叉验证
        // if (this.StartTime > this.EndTime) 
        //    results.Add(new ValidationResult("开始时间不能晚于结束时间", new[] { nameof(StartTime) }));
    }
}