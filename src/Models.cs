using System;
using System.Collections.Generic;

namespace RoBIM.Feature;

public sealed class ProjectUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string RevitUsername { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;

    public string DisplayText =>
        string.IsNullOrWhiteSpace(RevitUsername)
            ? DisplayName
            : $"{DisplayName} ({RevitUsername})";
}

public sealed class IssueItem
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Type { get; set; } = "설계검토";

    // 기본 격상 필드
    public string Priority { get; set; } = "보통";
    public string Discipline1 { get; set; } = "건축";
    public string Discipline2 { get; set; } = "일반";

    public string AssignedTo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string RootCause { get; set; } = "미지정";

    // 연계 뷰 정보
    public long LinkedViewId { get; set; }
    public string LinkedViewUniqueId { get; set; } = string.Empty;
    public string LinkedViewName { get; set; } = string.Empty;

    // 연계 객체 정보
    public List<long> ElementIds { get; set; } = [];
    public List<string> UniqueIds { get; set; } = [];

    public string SnapshotFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public Dictionary<string, string> CustomFields { get; set; } = [];

    public string ElementsSummary =>
        ElementIds.Count > 0 ? $"{ElementIds.Count}개 객체" : "(선택 없음)";

    public string DisplayNumber => $"#{Number:D3}";
}

public sealed class CustomFieldDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text"; // Text, Dropdown, Radio, Checkbox
    public List<string> Options { get; set; } = [];
    public string DefaultValue { get; set; } = string.Empty;
}

public sealed class IssueProjectConfig
{
    public int NextNumber { get; set; } = 1;
    public List<ProjectUser> Users { get; set; } = [];
    public List<string> Discipline1List { get; set; } = ["건축", "구조", "기계", "전기", "통신", "소방", "토목", "조경", "기타"];
    public List<string> Discipline2List { get; set; } = ["일반", "골조", "마감", "배관", "덕트", "케이블트레이", "소화배관", "외부토공", "기타"];
    public List<string> TypeList { get; set; } = ["설계검토", "품질", "간섭(Clash)", "안전", "시공성", "법규/인허가", "기타"];
    public List<string> RootCauseList { get; set; } = ["미지정", "설계 누락", "모델링 오류", "공종 간섭", "기준 미달", "발주처 요청", "현장 여건", "기타"];
    public List<string> PriorityList { get; set; } = ["긴급", "높음", "보통", "낮음"];
    public List<CustomFieldDefinition> CustomFields { get; set; } = [];
}
