using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RoBIM.Common;

namespace RoBIM.Feature;

[Transaction(TransactionMode.Manual)]
public sealed class Command : IExternalCommand
{
    private static ExternalEvent? revitEvent;
    private static IssueExternalEventHandler? eventHandler;

    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        var uiApp = data.Application;
        var doc = uiApp.ActiveUIDocument?.Document;

        if (doc is null)
        {
            TaskDialog.Show("BIM 이슈 검토", "열려 있는 Revit 프로젝트 문서가 없습니다.");
            return Result.Cancelled;
        }

        var status = ReleaseMetadata.Resolve(null, "issue-tracker", "0.1.1-test.1");

        try
        {
            if (IssueTrackerWindow.Instance != null)
            {
                if (IssueTrackerWindow.Instance.WindowState == System.Windows.WindowState.Minimized)
                {
                    IssueTrackerWindow.Instance.WindowState = System.Windows.WindowState.Normal;
                }
                IssueTrackerWindow.Instance.Activate();
                return Result.Succeeded;
            }

            eventHandler = new IssueExternalEventHandler();
            revitEvent = ExternalEvent.Create(eventHandler);

            var window = new IssueTrackerWindow(doc, revitEvent, eventHandler, status.Version);
            window.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
