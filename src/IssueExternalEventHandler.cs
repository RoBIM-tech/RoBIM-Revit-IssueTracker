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
    PickElementsByRectangle,
    GetSelectedElements,
    HighlightElements,
    SelectElementsOnly,
    SwitchToViewAndHighlight,
    CaptureView,
    GetCurrentViewInfo
}

public sealed class IssueExternalEventHandler : IExternalEventHandler
{
    public IssueRevitAction CurrentAction { get; set; } = IssueRevitAction.None;

    // Parameters
    public List<long> TargetElementIds { get; set; } = [];
    public long TargetViewId { get; set; }
    public string CaptureTargetFolder { get; set; } = string.Empty;
    public string CaptureTargetBaseName { get; set; } = string.Empty;

    // Callbacks
    public Action<List<long>, List<string>>? OnElementsPicked { get; set; }
    public Action<long, string, string>? OnViewInfoRetrieved { get; set; } // id, uniqueId, name
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

                case IssueRevitAction.PickElementsByRectangle:
                    ExecutePickElementsByRectangle(uidoc);
                    break;

                case IssueRevitAction.GetSelectedElements:
                    ExecuteGetSelectedElements(uidoc);
                    break;

                case IssueRevitAction.HighlightElements:
                    ExecuteHighlightElements(uidoc, zoom: true);
                    break;

                case IssueRevitAction.SelectElementsOnly:
                    ExecuteHighlightElements(uidoc, zoom: false);
                    break;

                case IssueRevitAction.SwitchToViewAndHighlight:
                    ExecuteSwitchToViewAndHighlight(uidoc);
                    break;

                case IssueRevitAction.GetCurrentViewInfo:
                    ExecuteGetCurrentViewInfo(uidoc);
                    break;

                case IssueRevitAction.CaptureView:
                    ExecuteCaptureView(doc);
                    break;
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // User cancelled picking - do nothing
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
        var refs = uidoc.Selection.PickObjects(ObjectType.Element, "객체들을 선택하세요 (Ctrl을 누르고 다중 클릭 가능, 완료 시 상단 Finish/완료 클릭)");
        if (refs == null || refs.Count == 0) return;

        var elementIds = new List<long>();
        var uniqueIds = new List<string>();

        foreach (var r in refs)
        {
            var elem = doc.GetElement(r);
            if (elem != null && !elementIds.Contains(elem.Id.Value))
            {
                elementIds.Add(elem.Id.Value);
                uniqueIds.Add(elem.UniqueId);
            }
        }

        OnElementsPicked?.Invoke(elementIds, uniqueIds);
    }

    private void ExecutePickElementsByRectangle(UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var elems = uidoc.Selection.PickElementsByRectangle("마우스로 사각형 영역을 드래그하여 객체들을 한 번에 선택하세요");
        if (elems == null || elems.Count == 0) return;

        var elementIds = new List<long>();
        var uniqueIds = new List<string>();

        foreach (var elem in elems)
        {
            if (elem != null && !elementIds.Contains(elem.Id.Value))
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
            if (elem != null && !elementIds.Contains(elem.Id.Value))
            {
                elementIds.Add(elem.Id.Value);
                uniqueIds.Add(elem.UniqueId);
            }
        }

        OnElementsPicked?.Invoke(elementIds, uniqueIds);
    }

    private void ExecuteHighlightElements(UIDocument uidoc, bool zoom = true)
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
            if (zoom)
            {
                uidoc.ShowElements(elementIds);
            }
        }
        else
        {
            OnError?.Invoke("해당 ID의 객체를 현재 열려 있는 모델에서 찾을 수 없습니다. 타 공종 모델에서 생성된 객체인 경우 해당 원본 모델 파일을 열어 확인하세요.");
        }
    }

    private void ExecuteSwitchToViewAndHighlight(UIDocument uidoc)
    {
        var doc = uidoc.Document;

        if (TargetViewId > 0)
        {
            var viewElem = doc.GetElement(new ElementId(TargetViewId)) as View;
            if (viewElem != null && !viewElem.IsTemplate)
            {
                if (uidoc.ActiveView.Id != viewElem.Id)
                {
                    uidoc.ActiveView = viewElem;
                }
            }
            else
            {
                OnError?.Invoke($"연계된 뷰(ID: {TargetViewId})를 현재 모델에서 찾을 수 없습니다. 타 모델의 뷰인 경우 해당 원본 모델 파일을 확인하세요.");
            }
        }

        // Highlight elements after view transition
        ExecuteHighlightElements(uidoc);
    }

    private void ExecuteGetCurrentViewInfo(UIDocument uidoc)
    {
        var view = uidoc.ActiveView;
        if (view != null)
        {
            OnViewInfoRetrieved?.Invoke(view.Id.Value, view.UniqueId, view.Name);
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
            ImageResolution = ImageResolution.DPI_300,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = 2560 // 고화질 2.5K 원본 해상도 그대로 캡처 저장
        };

        doc.ExportImage(options);

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
