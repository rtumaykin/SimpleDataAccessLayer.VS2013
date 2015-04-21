using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EnvDTE;

namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
#if DEBUG
    public class Main
#else 
    internal class Main
#endif
    {
        public Main(DalConfig dalConfig, ProjectItem projectItem)
        {
            _config = dalConfig;
            _projectItem = projectItem;
        }

        private DalConfig _config;
        private ProjectItem _projectItem;


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
                            var activeProject = _projectItem.ContainingProject;

                            var configurationFilename = (from ProjectItem item in activeProject.ProjectItems
                                                         where Regex.IsMatch(item.Name, "(app|web).config", RegexOptions.IgnoreCase)
                                                         select item.FileNames[0]).FirstOrDefault();

                            if (!string.IsNullOrEmpty(configurationFilename))
                            {
                                // found it, map it and expose salient members as properties
                                var configFile = new ExeConfigurationFileMap { ExeConfigFilename = configurationFilename };
                                var configuration =
                                    System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configFile,
                                        ConfigurationUserLevel.None);
                                var configConnectionString =
                                    configuration.ConnectionStrings.ConnectionStrings[_config.ApplicationConnectionString
                                        ].ConnectionString;
                                var sb = new SqlConnectionStringBuilder(configConnectionString);
                                if (_config.DesignerConnection.Authentication is WindowsAuthentication)
                                {
                                    sb.IntegratedSecurity = true;
                                }
                                else
                                {
                                    var auth = _config.DesignerConnection.Authentication as SqlAuthentication;
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
                                return (string)null;
                            }
                        }
                        catch
                        {
                            return (string)null;
                        }
                    }
                    );
                return _designerConnectionString;
            }
        }


        public string GetCode()
        {
            ISqlRepository sqlRepository = new SqlRepository(DesignerConnectionString);

            return string.Format("{0}{1}{2}{3}",
                new CodeBuilder.Common(_config).GetCode(),
                new CodeBuilder.TableValuedParameter(_config, sqlRepository).GetCode(),
                new CodeBuilder.Enum(_config, sqlRepository).GetCode(),
                new CodeBuilder.Procedure(_config, sqlRepository).GetCode()
                );
        }
    }
}
