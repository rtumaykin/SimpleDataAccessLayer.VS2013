using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using EnvDTE;
using Microsoft.SqlServer.Management.Common;
using SimpleDataAccessLayer_vs2013.CodeBuilder;

namespace SimpleDataAccessLayer_vs2013
{
    public class Helper
    {
        private readonly DTE _dte;
        private readonly String _templateFileName;

        public Helper(DTE dte, String templateFileName)
        {
            _dte = dte;
            _templateFileName = templateFileName;
        }

        private ProjectItem _templateFile;

        public ProjectItem TemplateFile
        {
            get
            {
                System.Threading.LazyInitializer.EnsureInitialized(
                    ref _templateFile,
                    () =>
                    {
                        try
                        {
                            return _dte.Solution.FindProjectItem(_templateFileName);
                        }
                        catch
                        {
                            return (ProjectItem) null;
                        }
                    }
                    );
                return _templateFile;
            }
        }

        private DalConfig _config;

        public DalConfig Config
        {
            get
            {
                System.Threading.LazyInitializer.EnsureInitialized(
                    ref _config,
                    () =>
                    {
                        try
                        {
                            var xmlProjectItem = (ProjectItem) TemplateFile.Collection.Parent;
                            var ser = new DataContractSerializer(typeof (DalConfig));
                            var fileName = xmlProjectItem.FileNames[0];
                            var dalConfig = (DalConfig) ser.ReadObject(XmlReader.Create(fileName));
                            return dalConfig;
                        }
                        catch
                        {
                            return new DalConfig();
                        }
                    }
                    );
                return _config;
            }
        }

        private string _designerConnectionString;

        public string DesignerConnectionString
        {
            get
            {
                System.Threading.LazyInitializer.EnsureInitialized(
                    ref _designerConnectionString,
                    () =>
                    {
                        try
                        {
                            var activeProject = TemplateFile.ContainingProject;

                            var configurationFilename = (from ProjectItem item in activeProject.ProjectItems
                                where Regex.IsMatch(item.Name, "(app|web).config", RegexOptions.IgnoreCase)
                                select item.FileNames[0]).FirstOrDefault();

                            if (!string.IsNullOrEmpty(configurationFilename))
                            {
                                // found it, map it and expose salient members as properties
                                var configFile = new ExeConfigurationFileMap {ExeConfigFilename = configurationFilename};
                                var configuration =
                                    System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configFile,
                                        ConfigurationUserLevel.None);
                                var configConnectionString =
                                    configuration.ConnectionStrings.ConnectionStrings[Config.ApplicationConnectionString
                                        ].ConnectionString;
                                var sb = new SqlConnectionStringBuilder(configConnectionString);
                                if (Config.DesignerConnection.Authentication is WindowsAuthentication)
                                {
                                    sb.IntegratedSecurity = true;
                                }
                                else
                                {
                                    var auth = Config.DesignerConnection.Authentication as SqlAuthentication;
                                    sb.IntegratedSecurity = false;
                                    if (auth != null)
                                    {
                                        sb.UserID = auth.UserName;
                                        sb.Password = auth.Password;
                                    }
                                }
                                return sb.ConnectionString;

                            }
                            else
                            {
                                return (string) null;
                            }
                        }
                        catch
                        {
                            return (string) null;
                        }
                    }
                    );
                return _designerConnectionString;
            }
        }


        public string GetCode()
        {
            return string.Format("{0}{1}{2}{3}",
                new CodeBuilder.Common(Config, DesignerConnectionString).GetCode(),
                new CodeBuilder.TableValuedParameter(Config, DesignerConnectionString).GetCode(),
                new CodeBuilder.Enum(Config, DesignerConnectionString).GetCode(),
                new CodeBuilder.Procedure(Config, DesignerConnectionString).GetCode()
                );
        }
    }
}
