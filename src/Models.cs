using System;
using System.Collections.Generic;

namespace RoBIM.Feature;

public sealed class ProjectUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string RevitUsername { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } // 관리자 권한 (댓글/활동내역 삭제 권한 등)

    public string AdminDisplay => IsAdmin ? "관리자" : "일반";

    // 이름 (회사) 형식
    public string DisplayText =>
        string.IsNullOrWhiteSpace(Company)
            ? DisplayName
            : $"{DisplayName} ({Company})";
}

public sealed class IssueComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Content { get; set; } = string.Empty;
    public bool IsSystem { get; set; } // 시스템 자동 생성(수정 내역 등) 여부
}

public sealed class IssueItem
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Type { get; set; } = "설계검토";

    // 간섭 대비 공종 1 & 공종 2 (동일한 공종 목록에서 선택)
    public string Priority { get; set; } = "보통";
    public string Discipline1 { get; set; } = "건축";
    public string Discipline2 { get; set; } = "(해당 없음)";

    public string AssignedTo { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string RootCause { get; set; } = "미지정";

    // 작성 모델 (파일명, 예: Project_ARC.rvt)
    public string SourceModel { get; set; } = string.Empty;
    public string SourceModelDisplay => string.IsNullOrWhiteSpace(SourceModel) ? "(공통)" : SourceModel;

    // 연계 뷰 정보
    public long LinkedViewId { get; set; }
    public string LinkedViewUniqueId { get; set; } = string.Empty;
    public string LinkedViewName { get; set; } = string.Empty;

    // 연계 객체 정보
    public List<long> ElementIds { get; set; } = [];
    public List<string> UniqueIds { get; set; } = [];

    // 다중 스냅샷 지원
    public List<string> SnapshotFileNames { get; set; } = [];
    public string SnapshotFileName { get; set; } = string.Empty; // 하위 호환용

    // 댓글 및 수정 이력
    public List<IssueComment> Comments { get; set; } = [];

    // 아카이브 여부
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public Dictionary<string, string> CustomFields { get; set; } = [];

    public string ElementsSummary =>
        ElementIds.Count > 0 ? $"{ElementIds.Count}개 객체" : "(선택 없음)";

    public string DisplayNumber => $"#{Number:D3}";

    public string CommentsSummary => Comments.Count > 0 ? $"💬 {Comments.Count}" : "";

    public string DisciplinesDisplay =>
        string.IsNullOrWhiteSpace(Discipline2) || Discipline2 == "(해당 없음)"
            ? Discipline1
            : $"{Discipline1} ⚡ {Discipline2}";
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
    // 간섭 검토를 위해 공종 1과 공종 2가 공유하는 단일 공종 목록
    public List<string> DisciplineList { get; set; } = ["건축", "구조", "기계", "전기", "통신", "소방", "토목", "조경", "골조", "마감", "배관", "덕트", "케이블트레이", "기타"];
    public List<string> TypeList { get; set; } = ["설계검토", "품질", "간섭(Clash)", "안전", "시공성", "법규/인허가", "기타"];
    public List<string> RootCauseList { get; set; } = ["미지정", "설계 누락", "모델링 오류", "공종 간섭", "기준 미달", "발주처 요청", "현장 여건", "기타"];
    public List<string> PriorityList { get; set; } = ["긴급", "높음", "보통", "낮음"];
    public List<CustomFieldDefinition> CustomFields { get; set; } = [];

    // 관리자 마스터 비밀번호 (SHA-256 해시 및 Salt)
    public string AdminPasswordHash { get; set; } = string.Empty;
    public string AdminPasswordSalt { get; set; } = string.Empty;
}

public static class PasswordHelper
{
    public static string GenerateSalt()
    {
        byte[] bytes = new byte[16];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string HashPassword(string password, string salt)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] input = System.Text.Encoding.UTF8.GetBytes(salt + password);
        byte[] hash = sha.ComputeHash(input);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string salt, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt)) return false;
        string computed = HashPassword(password, salt);
        return string.Equals(computed, hash, StringComparison.Ordinal);
    }
}
