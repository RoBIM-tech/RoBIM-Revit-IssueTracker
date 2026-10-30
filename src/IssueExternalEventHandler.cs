using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RoBIM.Feature;

public enum IssueRevitAction
{
    None,
    PickElements,
    GetSelectedElements,
    HighlightElements,
    CaptureView
}

public sealed class IssueExternalEventHandler : IExternalEventHandler
{
    public IssueRevitAction CurrentAction { get; set; } = IssueRevitAction.None;

    // Action Parameters & Callbacks
    public List<long> TargetElementIds { get; set; } = [];
    public string CaptureTargetFolder { get; set; } = string.Empty;
    public string CaptureTargetBaseName { get; set; } = string.Empty;

    public Action<List<long>, List<string>>? OnElementsPicked { get; set; }
    public Action<string>? OnViewCaptured { get; set; }
    public Action<string>? OnError { get; set; }

    public void Execute(UIApplication app)
    {
        var uidoc = app.ActiveUIDocument;
        if (uidoc == null)
        {
            OnError?.Invoke("활성화된 Revit 문서가 없습니다.");
            return;
        }

        var doc = uidoc.Document;

        try
        {
            switch (CurrentAction)
            {
                case IssueRevitAction.PickElements:
                    ExecutePickElements(uidoc);
                    break;

                case IssueRevitAction.GetSelectedElements:
                    ExecuteGetSelectedElements(uidoc);
                    break;

                case IssueRevitAction.HighlightElements:
                    ExecuteHighlightElements(uidoc);
                    break;

                case IssueRevitAction.CaptureView:
                    ExecuteCaptureView(doc);
                    break;
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // User cancelled selection - do nothing
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Revit 작업 중 오류: {ex.Message}");
        }
        finally
        {
            CurrentAction = IssueRevitAction.None;
        }
    }

    private void ExecutePickElements(UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var refs = uidoc.Selection.PickObjects(ObjectType.Element, "이슈와 연계할 객체들을 선택하세요 (완료 시 Finish 클릭)");
        if (refs == null || refs.Count == 0) return;

        var elementIds = new List<long>();
        var uniqueIds = new List<string>();

        foreach (var r in refs)
        {
            var elem = doc.GetElement(r);
            if (elem != null)
            {
                elementIds.Add(elem.Id.Value);
                uniqueIds.Add(elem.UniqueId);
            }
        }

        OnElementsPicked?.Invoke(elementIds, uniqueIds);
    }

    private void ExecuteGetSelectedElements(UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var selIds = uidoc.Selection.GetElementIds();
        if (selIds == null || selIds.Count == 0)
        {
            OnError?.Invoke("현재 선택된 객체가 없습니다. Revit 뷰에서 객체를 선택 후 다시 시도하세요.");
            return;
        }

        var elementIds = new List<long>();
        var uniqueIds = new List<string>();

        foreach (var id in selIds)
        {
            var elem = doc.GetElement(id);
            if (elem != null)
            {
                elementIds.Add(elem.Id.Value);
                uniqueIds.Add(elem.UniqueId);
            }
        }

        OnElementsPicked?.Invoke(elementIds, uniqueIds);
    }

    private void ExecuteHighlightElements(UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var elementIds = new List<ElementId>();

        foreach (var idVal in TargetElementIds)
        {
            var elemId = new ElementId(idVal);
            if (doc.GetElement(elemId) != null)
            {
                elementIds.Add(elemId);
            }
        }

        if (elementIds.Count > 0)
        {
            uidoc.Selection.SetElementIds(elementIds);
            uidoc.ShowElements(elementIds);
        }
        else
        {
            OnError?.Invoke("해당 ID의 객체를 현재 모델에서 찾을 수 없습니다.");
        }
    }

    private void ExecuteCaptureView(Document doc)
    {
        if (string.IsNullOrWhiteSpace(CaptureTargetFolder))
        {
            OnError?.Invoke("캡처 저장 폴더가 지정되지 않았습니다.");
            return;
        }

        if (!Directory.Exists(CaptureTargetFolder))
        {
            Directory.CreateDirectory(CaptureTargetFolder);
        }

        string tempBase = Path.Combine(CaptureTargetFolder, "temp_capture_" + Guid.NewGuid().ToString("N")[..8]);
        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.VisibleRegionOfCurrentView,
            FilePath = tempBase,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG,
            ImageResolution = ImageResolution.DPI_150,
            ZoomType = ZoomFitType.FitToPage
        };

        doc.ExportImage(options);

        // Revit ExportImage appends view name or extension
        string? exportedFile = Directory.GetFiles(CaptureTargetFolder, Path.GetFileName(tempBase) + "*")
            .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));

        if (exportedFile != null)
        {
            string finalFileName = (string.IsNullOrWhiteSpace(CaptureTargetBaseName) ? "snap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") : CaptureTargetBaseName) + ".png";
            string finalPath = Path.Combine(CaptureTargetFolder, finalFileName);

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(exportedFile, finalPath);

            OnViewCaptured?.Invoke(finalFileName);
        }
        else
        {
            OnError?.Invoke("화면 캡처 파일 생성에 실패했습니다.");
        }
    }

    public string GetName() => "RoBIM Issue Tracker External Event";
}
