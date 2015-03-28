using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using EnvDTE;
using VSLangProj;
using Configuration = System.Configuration.Configuration;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class MyEditor : UserControl
	{
	    private SimpleDataAccessLayer_vs2013Package _package;
	    private Dictionary<string, string> _connectionStrings;
		string _fileName;

		internal DalConfig Config { get; private set; }

	    public MyEditor()
		{
			//this.package = package;
			InitializeComponent();
		}

        private Dictionary<string, string> InitializeConectionStringsCollection(SimpleDataAccessLayer_vs2013Package package, string fileName)
		{
			Project project = package.GetEnvDTE().Solution.FindProjectItem(fileName).ContainingProject;

            var configurationFilename = (from ProjectItem item in project.ProjectItems
                where Regex.IsMatch(item.Name, "(app|web).config", RegexOptions.IgnoreCase)
                select item.FileNames[0]).FirstOrDefault();

            // examine each project item's filename looking for app.config or web.config
            var returnValue = new Dictionary<string, string>();

			if (!string.IsNullOrEmpty(configurationFilename))
			{
				// found it, map it and expose salient members as properties
			    var configFile = new ExeConfigurationFileMap {ExeConfigFilename = configurationFilename};
			    var configuration = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);

				foreach (ConnectionStringSettings connStringSettings in configuration.ConnectionStrings.ConnectionStrings)
				{
					returnValue.Add(connStringSettings.Name, connStringSettings.ConnectionString);
				}
			}

			return returnValue;
		}

		private void Edit_Click(object sender, EventArgs e)
		{
			var modelDesignerDialog = new ModelDesigner(_connectionStrings, Config);
			var dialogResult = modelDesignerDialog.ShowDialog();
			if (dialogResult == DialogResult.OK)
			{
				Config = modelDesignerDialog.Config;
				var connectionStringName = modelDesignerDialog.ConnectionStringName;
				if (!_connectionStrings.ContainsKey(connectionStringName))
				{
					_connectionStrings.Add(connectionStringName, modelDesignerDialog.ConnectionString);
					//AddConnectionStringToProject(_connectionStringName, _modelDesignerDialog.ConnectionString);
				}
			}

			InitControls();
		}

		internal void SaveConfig(string fileName)
		{
			var ser = new DataContractSerializer(typeof(DalConfig));
			var settings = new XmlWriterSettings { Indent = true, Encoding=Encoding.Unicode };
			using (var writer = XmlWriter.Create(fileName, settings))
			{
				ser.WriteObject(writer, Config);
			}
				// by now the connection string is already in the collection
			AddConnectionStringToProject(Config.ApplicationConnectionString, _connectionStrings[Config.ApplicationConnectionString]);
			ProjectItem dalProjectItem = _package.GetEnvDTE().Solution.FindProjectItem(fileName);
			var dalProjectItemChildren = dalProjectItem.ProjectItems;
			foreach (ProjectItem item in dalProjectItemChildren)
			{
				// there is only one child item with this extension
				if (item.Name.ToUpper().EndsWith(".tt".ToUpper()))
				{
					var pi = item.Object as VSProjectItem;

					var prop = item.Properties.OfType<Property>().FirstOrDefault(p => p.Name == "CustomTool");

					if (prop != null && pi != null)
						pi.RunCustomTool();
				}
			}


		}

		private void AddConnectionStringToProject(string connectionStringName, string connectionString)
		{
			Project project = _package.GetEnvDTE().Solution.FindProjectItem(_fileName).ContainingProject;

		    var configurationFilename = (from ProjectItem item in project.ProjectItems
		        where Regex.IsMatch(item.Name, "(app|web).config", RegexOptions.IgnoreCase)
		        select item.FileNames[0]).FirstOrDefault();
		    // examine each project item's filename looking for app.config or web.config

		    if (string.IsNullOrEmpty(configurationFilename))
			{
				MessageBox.Show("The configuration file for this project does not exist. A new app.config file will be created in the project root in order to save the connection strings. Please make sure you copy the connection strings to the application which will be using this project.", "Warning!", MessageBoxButtons.OK, MessageBoxIcon.Warning);

				configurationFilename = Path.Combine(Path.GetDirectoryName(project.FullName), "app.config");

				var configText = new[] { "<?xml version=\"1.0\" encoding=\"utf-8\" ?>", "<configuration>", "</configuration>" };
				File.WriteAllLines(configurationFilename, configText);
				project.ProjectItems.AddFromFile(configurationFilename);
			}
			// found it, map it and expose salient members as properties
		    var configFile = new ExeConfigurationFileMap {ExeConfigFilename = configurationFilename};
		    var configuration = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);
		    if (
		        configuration.ConnectionStrings.ConnectionStrings.Cast<ConnectionStringSettings>()
		            .Any(connStringSettings => connStringSettings.Name == connectionStringName))
		    {
		        return;
		    }
		    configuration.ConnectionStrings.ConnectionStrings.Add(new ConnectionStringSettings(connectionStringName, connectionString));
			configuration.Save();
		}

		public void Init(DalConfig config, string fileName, SimpleDataAccessLayer_vs2013Package package)
		{
			_connectionStrings = InitializeConectionStringsCollection(package, fileName);
			_package = package;
			Config = config;
			_fileName = fileName;

			if (config == null)
			{
				HandleInvalidFileFormat();
			}
			else
			{
				InitControls();
			}
		}

		private void InitControls()
		{
			ConnectionStringName.Text = Config.ApplicationConnectionString;
			Namespace.Text = Config.Namespace;

			if (!String.IsNullOrWhiteSpace(Config.ApplicationConnectionString))
			{
				var connectionString = _connectionStrings[Config.ApplicationConnectionString];
				var sb = new SqlConnectionStringBuilder(connectionString);

				Server.Text = sb.DataSource;
				Database.Text = sb.InitialCatalog;
			}
			EnumsGridView.Rows.Clear();
			ProcsGrid.Rows.Clear();

			foreach (var procedure in Config.Procedures)
			{
				ProcsGrid.Rows.Add(procedure.Schema, procedure.ProcedureName, procedure.Alias);
			}

			foreach (var _enum in Config.Enums)
			{
				EnumsGridView.Rows.Add(_enum.Schema, _enum.TableName, _enum.Alias, _enum.KeyColumn, _enum.ValueColumn);
			}
		}

		void HandleInvalidFileFormat()
		{
			ConnectionStringName.Text = "";
			Server.Text = "";
			Database.Text = "";
			Namespace.Text = "";
			EnumsGridView.Rows.Clear();
			ProcsGrid.Rows.Clear();

			MessageBox.Show("Invalid file format. Ignore and continue? Warning! All data in the file will be overwritten!", "Invalid file format", MessageBoxButtons.YesNo);

			Edit.Enabled = false;
		}
	}
}
