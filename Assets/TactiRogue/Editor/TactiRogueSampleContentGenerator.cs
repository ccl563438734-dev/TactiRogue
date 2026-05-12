using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TactiRogue
{
    public static class TactiRogueSampleContentGenerator
    {
        [MenuItem("Tools/TactiRogue/Generate Sample Content")]
        public static void Generate()
        {
            try
            {
                var workbookPath = TactiRogueExcelPaths.ToAbsolutePath(TactiRogueExcelPaths.WorkbookAssetPath);
                if (!File.Exists(workbookPath))
                {
                    TactiRogueExcelExporter.ExportCurrentDataToWorkbook(workbookPath);
                }

                var report = TactiRogueExcelImporter.ImportWorkbookToGameData(workbookPath);
                if (!report.IsValid)
                {
                    throw new InvalidOperationException(report.ToDisplayString());
                }

                EditorUtility.DisplayDialog("TactiRogue", "Workbook data was imported and runtime assets were regenerated.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("TactiRogue", $"Generate Sample Content failed.\n{exception.Message}", "OK");
            }
        }
    }
}
