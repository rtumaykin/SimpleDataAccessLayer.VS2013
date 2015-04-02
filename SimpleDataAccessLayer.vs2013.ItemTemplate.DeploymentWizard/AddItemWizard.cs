using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TemplateWizard;

namespace SimpleDataAccessLayer.vs2013.ItemTemplate.DeploymentWizard
{
    public class AddItemWizard : IWizard
    {
        private ProjectItem _dal, _template;
        bool _canAdd = true;

        internal static Project GetActiveProject(DTE2 dte)
        {
            Project project = null;
            try
            {
                var activeSolutionProjects = (Array)dte.ActiveSolutionProjects;
                if (activeSolutionProjects.Length > 0)
                {
                    project = (Project)activeSolutionProjects.GetValue(0);
                }
            }
            catch (COMException)
            {
            }
            if (project == null)
            {
                if (((dte.ActiveDocument != null) && (dte.ActiveDocument.ProjectItem != null)) && (dte.ActiveDocument.ProjectItem.ContainingProject != null))
                {
                    return dte.ActiveDocument.ProjectItem.ContainingProject;
                }
                if (dte.Solution.Projects.Count > 0)
                {
                    project = dte.Solution.Projects.Item(1);
                }
            }
            return project;
        }

        // Taken from Entity Framework implementation. Since you have to have an item selected when you 
        // either right click or choose a menu, then it is possible to find the path where all items will be generated
        private static string GetFolderNameForNewItems(DTE2 dte)
        {
            var activeProject = GetActiveProject(dte);

            var item = dte.SelectedItems.Item(1);
            string fullName = null;
            if (item.Project != null)
            {
                fullName = GetProjectRoot(item.Project).FullName;
            }
            else if (item.ProjectItem != null)
            {
                var parent = new DirectoryInfo(item.ProjectItem.FileNames[1]);
                while (parent != null && (parent.Attributes & FileAttributes.Directory) != FileAttributes.Directory)
                {
                    parent = parent.Parent;
                }
                if (parent != null) fullName = parent.FullName;
            }
            else
            {
                fullName = GetProjectRoot(activeProject).FullName;
            }

            return fullName;
        }

        internal static DirectoryInfo GetProjectRoot(Project project)
        {
            DirectoryInfo info = null;

            var str4 = project.Properties.Item("FullPath").Value as string;
            if (!string.IsNullOrEmpty(str4))
            {
                info = new DirectoryInfo(str4);
            }

            return info ?? (new DirectoryInfo(@".\"));
        }

        public void BeforeOpeningFile(ProjectItem projectItem)
        {

        }

        public void ProjectFinishedGenerating(Project project)
        {
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
            try
            {
                if (projectItem.Name.Substring(projectItem.Name.Length - 3, 3) == "dal")
                {
                    _dal = projectItem;
                }
                else
                {
                    _template = projectItem;
                }
            }
            catch (Exception)
            {
                _canAdd = false;
            }
        }

        public void RunFinished()
        {
            if (_canAdd)
                _dal.ProjectItems.AddFromFile(_template.FileNames[1]);
        }


        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            var dte = (DTE2)automationObject;

            var folderNameForNewItems = GetFolderNameForNewItems(dte);
            var mainFileName = folderNameForNewItems + "\\" + replacementsDictionary["$safeitemname$"];

            var index = 0;
            var keepNameSearching = false;
            string ttNewPath;
            do
            {
                // if this item exists in solution - keep searching
                while (dte.Solution.FindProjectItem(mainFileName + (index == 0 ? "" : index.ToString()) + ".tt") != null)
                {
                    index++;
                }
                ttNewPath = mainFileName + (index == 0 ? "" : index.ToString()) + ".tt";
                if (File.Exists(ttNewPath))
                {
                    var result = MessageBox.Show(String.Format("File {0} exists, but it is not a part of the project. Overwrite?", ttNewPath), "File conflict", MessageBoxButtons.YesNoCancel);
                    if (result == DialogResult.Yes)
                    {
                        File.Delete(ttNewPath);
                        keepNameSearching = false;
                    }
                    else if (result == DialogResult.No)
                    {
                        //increase index and keep searching
                        index++;
                        keepNameSearching = true;
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        _canAdd = false;
                        break;
                    }
                }
                else
                {
                    keepNameSearching = false;
                }

            } while (keepNameSearching);

            replacementsDictionary.Add("$fileinputname_randomized$", Path.GetFileNameWithoutExtension(ttNewPath));

            // make sure that the "D:\Users\rtumaykin\AppData\Local\Microsoft\VisualStudio\11.0Exp\VTC\86e5fb2ca5ba64738f07b472c28dca98\~IC\ItemTemplates\Data\CSharp\1033\DataAccessLayerItemTemplate.zip\DataAccessLayerItemTemplate.vstemplate"
            // path is always passed as the first parameter to the customParams (http://msdn.microsoft.com/en-us/library/ms247063(v=vs.100).aspx)
            // and if this is always true, then I can read the file content into the string, then at the end save straight to the disk
            // this will be used for the temporary file name for tt template until it is renamed to something else.




        }

        public bool ShouldAddProjectItem(string filePath)
        {
            // don't add the tt file
            return _canAdd; // filePath.Substring(filePath.Length - 3) == ".tt" ? false : true;
        }
    }
}
