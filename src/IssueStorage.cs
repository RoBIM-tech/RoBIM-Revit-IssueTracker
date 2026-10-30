using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    public string ArchiveDirectory => Path.Combine(IssueDirectory, "archive");

    public IssueStorage(Document doc)
    {
        IssueDirectory = ResolveIssueDirectory(doc);
        if (!Directory.Exists(IssueDirectory))
        {
            Directory.CreateDirectory(IssueDirectory);
        }
        if (!Directory.Exists(ArchiveDirectory))
        {
            Directory.CreateDirectory(ArchiveDirectory);
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
            // Fallback
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

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            string safeDocName = string.Join("_", doc.Title.Split(Path.GetInvalidFileNameChars()));
            baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RoBIM_Issues", safeDocName);
        }

        return Path.Combine(baseFolder, "issue");
    }

    public static string GetModelName(Document doc)
    {
        try
        {
            if (doc.IsWorkshared)
            {
                var centralPath = doc.GetWorksharingCentralModelPath();
                if (centralPath != null)
                {
                    string visiblePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
                    if (!string.IsNullOrWhiteSpace(visiblePath))
                    {
                        return Path.GetFileName(visiblePath);
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
            {
                return Path.GetFileName(doc.PathName);
            }
        }
        catch
        {
            // Ignored
        }

        string title = doc.Title ?? "Untitled";
        return title.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? title : $"{title}.rvt";
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
                    if (config.DisciplineList == null || config.DisciplineList.Count == 0)
                        config.DisciplineList = ["건축", "구조", "기계", "전기", "통신", "소방", "토목", "조경", "골조", "마감", "배관", "덕트", "케이블트레이", "기타"];
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
                // Fallback
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

    public List<IssueItem> LoadAllIssues(bool includeArchived = false)
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
                    item.IsArchived = false;
                    NormalizeSnapshots(item);
                    list.Add(item);
                }
            }
            catch
            {
                // Skip
            }
        }

        if (includeArchived && Directory.Exists(ArchiveDirectory))
        {
            foreach (var file in Directory.GetFiles(ArchiveDirectory, "ISSUE-*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var item = JsonSerializer.Deserialize<IssueItem>(json, JsonOptions);
                    if (item != null)
                    {
                        item.IsArchived = true;
                        NormalizeSnapshots(item);
                        list.Add(item);
                    }
                }
                catch
                {
                    // Skip
                }
            }
        }

        return list.OrderByDescending(x => x.Number).ToList();
    }

    private static void NormalizeSnapshots(IssueItem item)
    {
        if (item.SnapshotFileNames == null) item.SnapshotFileNames = [];
        if (!string.IsNullOrWhiteSpace(item.SnapshotFileName) && !item.SnapshotFileNames.Contains(item.SnapshotFileName))
        {
            item.SnapshotFileNames.Insert(0, item.SnapshotFileName);
        }
    }

    public void SaveIssue(IssueItem item, IssueProjectConfig config)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            int maxExisting = GetMaxIssueNumber();
            item.Number = Math.Max(config.NextNumber, maxExisting + 1);
            config.NextNumber = item.Number + 1;
            item.Id = $"ISSUE-{item.Number:D4}";
            SaveConfig(config);
        }

        item.UpdatedAt = DateTime.Now;
        if (item.SnapshotFileNames.Count > 0)
        {
            item.SnapshotFileName = item.SnapshotFileNames[0];
        }

        string targetDir = item.IsArchived ? ArchiveDirectory : IssueDirectory;
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        string path = Path.Combine(targetDir, $"{item.Id}.json");
        string json = JsonSerializer.Serialize(item, JsonOptions);
        File.WriteAllText(path, json);
    }

    private int GetMaxIssueNumber()
    {
        int max = 0;
        var dirs = new[] { IssueDirectory, ArchiveDirectory };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "ISSUE-*.json"))
            {
                string fn = Path.GetFileNameWithoutExtension(file);
                var match = Regex.Match(fn, @"ISSUE-(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int n))
                {
                    if (n > max) max = n;
                }
            }
        }
        return max;
    }

    public void ArchiveIssue(string issueId)
    {
        if (!Directory.Exists(ArchiveDirectory)) Directory.CreateDirectory(ArchiveDirectory);

        string srcJson = Path.Combine(IssueDirectory, $"{issueId}.json");
        if (File.Exists(srcJson))
        {
            try
            {
                string json = File.ReadAllText(srcJson);
                var item = JsonSerializer.Deserialize<IssueItem>(json, JsonOptions);
                if (item != null)
                {
                    item.IsArchived = true;
                    item.ArchivedAt = DateTime.Now;
                    string destJson = Path.Combine(ArchiveDirectory, $"{issueId}.json");
                    File.WriteAllText(destJson, JsonSerializer.Serialize(item, JsonOptions));
                    File.Delete(srcJson);

                    // Move snapshots
                    foreach (var snap in item.SnapshotFileNames)
                    {
                        string srcSnap = Path.Combine(IssueDirectory, snap);
                        string destSnap = Path.Combine(ArchiveDirectory, snap);
                        if (File.Exists(srcSnap))
                        {
                            if (File.Exists(destSnap)) File.Delete(destSnap);
                            File.Move(srcSnap, destSnap);
                        }
                    }
                }
            }
            catch
            {
                // Fallback direct move
                string destJson = Path.Combine(ArchiveDirectory, $"{issueId}.json");
                if (File.Exists(destJson)) File.Delete(destJson);
                File.Move(srcJson, destJson);
            }
        }
    }

    public void RestoreIssue(string issueId)
    {
        string srcJson = Path.Combine(ArchiveDirectory, $"{issueId}.json");
        if (File.Exists(srcJson))
        {
            try
            {
                string json = File.ReadAllText(srcJson);
                var item = JsonSerializer.Deserialize<IssueItem>(json, JsonOptions);
                if (item != null)
                {
                    item.IsArchived = false;
                    item.ArchivedAt = null;
                    string destJson = Path.Combine(IssueDirectory, $"{issueId}.json");
                    File.WriteAllText(destJson, JsonSerializer.Serialize(item, JsonOptions));
                    File.Delete(srcJson);

                    // Restore snapshots
                    foreach (var snap in item.SnapshotFileNames)
                    {
                        string srcSnap = Path.Combine(ArchiveDirectory, snap);
                        string destSnap = Path.Combine(IssueDirectory, snap);
                        if (File.Exists(srcSnap))
                        {
                            if (File.Exists(destSnap)) File.Delete(destSnap);
                            File.Move(srcSnap, destSnap);
                        }
                    }
                }
            }
            catch
            {
                // Fallback direct move
                string destJson = Path.Combine(IssueDirectory, $"{issueId}.json");
                if (File.Exists(destJson)) File.Delete(destJson);
                File.Move(srcJson, destJson);
            }
        }
    }

    public string GetSnapshotFullPath(string? fileName, bool isArchived = false)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;
        string activePath = Path.Combine(IssueDirectory, fileName);
        if (File.Exists(activePath)) return activePath;
        string archivePath = Path.Combine(ArchiveDirectory, fileName);
        if (File.Exists(archivePath)) return archivePath;
        return isArchived ? archivePath : activePath;
    }
}
