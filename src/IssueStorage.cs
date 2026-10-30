using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;

namespace RoBIM.Feature;

public sealed class IssueStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string IssueDirectory { get; }

    public IssueStorage(Document doc)
    {
        IssueDirectory = ResolveIssueDirectory(doc);
        if (!Directory.Exists(IssueDirectory))
        {
            Directory.CreateDirectory(IssueDirectory);
        }
    }

    public static string ResolveIssueDirectory(Document doc)
    {
        string? baseFolder = null;

        try
        {
            if (doc.IsWorkshared)
            {
                var centralPath = doc.GetWorksharingCentralModelPath();
                if (centralPath != null)
                {
                    string visiblePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
                    if (!string.IsNullOrWhiteSpace(visiblePath) && Path.IsPathRooted(visiblePath))
                    {
                        baseFolder = Path.GetDirectoryName(visiblePath);
                    }
                }
            }
        }
        catch
        {
            // Fallback to local PathName if CentralModelPath resolution fails (e.g. detached or disconnected)
        }

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(doc.PathName) && Path.IsPathRooted(doc.PathName))
                {
                    baseFolder = Path.GetDirectoryName(doc.PathName);
                }
            }
            catch
            {
                // Ignored
            }
        }

        // If still empty (e.g. unsaved project or cloud model without local path), use Documents folder
        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            string safeDocName = string.Join("_", doc.Title.Split(Path.GetInvalidFileNameChars()));
            baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RoBIM_Issues", safeDocName);
        }

        return Path.Combine(baseFolder, "issue");
    }

    public IssueProjectConfig LoadConfig()
    {
        string configPath = Path.Combine(IssueDirectory, "fields_schema.json");
        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<IssueProjectConfig>(json, JsonOptions);
                if (config != null)
                {
                    if (config.Discipline1List == null || config.Discipline1List.Count == 0)
                        config.Discipline1List = ["건축", "구조", "기계", "전기", "통신", "소방", "토목", "조경", "기타"];
                    if (config.Discipline2List == null || config.Discipline2List.Count == 0)
                        config.Discipline2List = ["일반", "골조", "마감", "배관", "덕트", "케이블트레이", "소화배관", "외부토공", "기타"];
                    if (config.TypeList == null || config.TypeList.Count == 0)
                        config.TypeList = ["설계검토", "품질", "간섭(Clash)", "안전", "시공성", "법규/인허가", "기타"];
                    if (config.RootCauseList == null || config.RootCauseList.Count == 0)
                        config.RootCauseList = ["미지정", "설계 누락", "모델링 오류", "공종 간섭", "기준 미달", "발주처 요청", "현장 여건", "기타"];
                    if (config.PriorityList == null || config.PriorityList.Count == 0)
                        config.PriorityList = ["긴급", "높음", "보통", "낮음"];
                    if (config.Users == null)
                        config.Users = [];
                    return config;
                }
            }
            catch
            {
                // Return default on parse failure
            }
        }

        var defaultConfig = new IssueProjectConfig
        {
            NextNumber = 1,
            CustomFields =
            [
                new CustomFieldDefinition { Name = "도면 번호", FieldType = "Text", DefaultValue = "" }
            ]
        };
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    public void SaveConfig(IssueProjectConfig config)
    {
        string configPath = Path.Combine(IssueDirectory, "fields_schema.json");
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);
    }

    public List<IssueItem> LoadAllIssues()
    {
        var list = new List<IssueItem>();
        if (!Directory.Exists(IssueDirectory)) return list;

        foreach (var file in Directory.GetFiles(IssueDirectory, "ISSUE-*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var item = JsonSerializer.Deserialize<IssueItem>(json, JsonOptions);
                if (item != null)
                {
                    list.Add(item);
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return list.OrderByDescending(x => x.Number).ToList();
    }

    public void SaveIssue(IssueItem item, IssueProjectConfig config)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            item.Number = config.NextNumber++;
            item.Id = $"ISSUE-{item.Number:D4}";
            SaveConfig(config);
        }

        item.UpdatedAt = DateTime.Now;
        string path = Path.Combine(IssueDirectory, $"{item.Id}.json");
        string json = JsonSerializer.Serialize(item, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void DeleteIssue(string issueId)
    {
        string jsonPath = Path.Combine(IssueDirectory, $"{issueId}.json");
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
        }

        string imagePath = Path.Combine(IssueDirectory, $"{issueId}_snapshot.png");
        if (File.Exists(imagePath))
        {
            File.Delete(imagePath);
        }
    }

    public string GetSnapshotFullPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
        return Path.Combine(IssueDirectory, fileName);
    }
}
