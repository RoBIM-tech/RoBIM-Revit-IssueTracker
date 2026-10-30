using System;
using System.Collections.Generic;

namespace RoBIM.Feature;

public sealed class IssueItem
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Type { get; set; } = "설계검토";
    public string AssignedTo { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string RootCause { get; set; } = "미지정";
    public List<long> ElementIds { get; set; } = [];
    public List<string> UniqueIds { get; set; } = [];
    public string SnapshotFileName { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = Environment.UserName;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public Dictionary<string, string> CustomFields { get; set; } = [];

    public string ElementsSummary =>
        ElementIds.Count > 0 ? $"{ElementIds.Count}개 객체 ({string.Join(", ", ElementIds)})" : "(선택 없음)";

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
    public List<CustomFieldDefinition> CustomFields { get; set; } = [];
}
