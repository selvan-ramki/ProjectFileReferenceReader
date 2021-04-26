using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Configuration;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace ProjectFileReferenceReader
{
    public class ReferenceFinder
    {
        private static readonly string IgnoreReferencePatternString = ConfigUtil.GetAppConfigValue("IgnoreReferencePattern");
        private static readonly Regex IgnoreReferencePattern = new Regex
            (IgnoreReferencePatternString, RegexOptions.IgnoreCase);

        public static void Run()
        {
            try
            {
                ConsoleUtility.WriteInfo("-- Started running reference finder tool --", true);

                string fileSearchPattern = ConfigurationManager.AppSettings["FileSearchPattern"];
                GeneralUtility.ThrowIfInvalid(fileSearchPattern, "file search pattern");

                bool exportFullFileName = Convert.ToBoolean(ConfigurationManager.AppSettings["ExportFullFileName"]);

                DataTable referenceTable = ExportUtility.GetReferenceTable();

                // Process all project base paths.
                List<string> projectBasePaths = ConfigUtil.GetSectionValues("ProjectBasePath");

                if (projectBasePaths == null || !projectBasePaths.Any())
                {
                    ConsoleUtility.WriteWarning("No project base paths found to process");
                    return;
                }

                int tableRowNo = 1;

                foreach (string projectPath in projectBasePaths)
                {
                    string projectPathFolderName = new DirectoryInfo(projectPath).Name;
                    List<string> files = FileManager.GetFiles(projectPath, fileSearchPattern);

                    ConsoleUtility.WriteInfo($"Processing project path '{projectPathFolderName}'");

                    if (files == null || !files.Any())
                    {
                        ConsoleUtility.WriteWarning($"No files found to process for project path '{projectPathFolderName}'");
                        continue;
                    }

                    ConsoleUtility.WriteInfo($"{files.Count} files found for project path '{projectPathFolderName}'");
                    int cursorTop = Console.CursorTop;
                    int fileIndex = 0;
                    
                    foreach (string file in files)
                    {
                        fileIndex++;

                        ConsoleUtility.ClearLine(cursorTop);
                        ConsoleUtility.GoToLine(cursorTop);
                        ConsoleUtility.WriteStep($"Processing file {fileIndex} of {files.Count} - {Path.GetFileName(file)} of project path '{projectPathFolderName}'");

                        // Delay.
                        GeneralUtility.ProcessDelay();

                        TryConstructDotNetFrameworkReferences(projectPath, file, ref tableRowNo, referenceTable);

                        TryConstructDotNetCoreReferences(projectPath, file, ref tableRowNo, referenceTable);
                    }

                    // Clear the processing line.
                    ConsoleUtility.ClearLine(cursorTop);
                    ConsoleUtility.GoToLine(cursorTop);
                
                    ConsoleUtility.WriteInfo($"Completed processing {files.Count} files for project path '{projectPathFolderName}'");
                    ConsoleUtility.NewLine();
                }

                if (referenceTable.Rows.Count > 0)
                {
                    ConsoleUtility.WriteInfo($"{referenceTable.Rows.Count} rows identified to export");

                    string fileName = $"{Guid.NewGuid()}.xlsx";
                    ExportUtility.ExportTable(referenceTable, fileName);
                }

                ConsoleUtility.NewLine();
                ConsoleUtility.WriteInfo("-- Reference finder tool ran successfully --");
            }
            catch (Exception exp)
            {
                ConsoleUtility.WriteError(exp.Message);
            }
        }

        /// <summary>
        /// Construct references for dotnet framework project.
        /// </summary>
        private static void TryConstructDotNetFrameworkReferences(string projectPath, string file, ref int tableRowNo, DataTable referenceTable)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(file);


            XmlNamespaceManager mgr = new XmlNamespaceManager(xmldoc.NameTable);
            mgr.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

            foreach (XmlNode item in xmldoc.SelectNodes("//x:Reference", mgr))
            {
                try
                {
                    string reference = item.Attributes[0].Value;

                    ConstructRow(projectPath, file, ref tableRowNo, reference, referenceTable);
                }
                catch (Exception exp)
                {
                    ConsoleUtility.WriteError(exp.Message);
                }
            }

            foreach (XmlNode item in xmldoc.SelectNodes("//x:ProjectReference", mgr))
            {
                try
                {
                    string reference = item.Attributes[0].Value;

                    ConstructRow(projectPath, file, ref tableRowNo, reference, referenceTable);
                }
                catch (Exception exp)
                {
                    ConsoleUtility.WriteError(exp.Message);
                }
            }
        }

        /// <summary>
        /// Construct references for dotnet core project.
        /// </summary>
        private static void TryConstructDotNetCoreReferences(string projectPath, string file, ref int tableRowNo, DataTable referenceTable)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(file);

            XmlNodeList nodes = xmldoc.SelectNodes("//Project/ItemGroup/PackageReference");
            if (nodes != null)
            {
                foreach (XmlNode node in nodes)
                {
                    try
                    {
                        string reference = $"{node.Attributes["Include"].Value}, Version={node.Attributes["Version"].Value}";

                        ConstructRow(projectPath, file, ref tableRowNo, reference, referenceTable);
                    }
                    catch (Exception exp)
                    {
                        ConsoleUtility.WriteError(exp.Message);
                    }
                }
            }
        }

        private static void ConstructRow(string projectPath, string file, ref int tableRowNo, string reference, DataTable referenceTable)
        {
            if (string.IsNullOrWhiteSpace(IgnoreReferencePatternString) || !IgnoreReferencePattern.IsMatch(reference))
            {
                // Add the row to the reference table.
                DataRow referenceTableRow = referenceTable.NewRow();
                referenceTableRow["SNo"] = tableRowNo.ToString();
                referenceTableRow["ProjectPath"] = new DirectoryInfo(projectPath).Name;
                referenceTableRow["FileName"] = new FileInfo(file).Name;
                referenceTableRow["Reference"] = reference;
                referenceTable.Rows.Add(referenceTableRow);

                tableRowNo++;
            }
        }
    }
}
