using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class ModelDesigner : Form
	{
		private readonly Dictionary<string, string> _connectionStrings;
		private DalConfig _config;
		private int _currentTabControlIndex;

		internal String ConnectionStringName
		{
			get
			{
				return appConnectionTab.ConnectionStringName;
			}
		}

		internal String ConnectionString
		{
			get
			{
				// Here I will need to build a different connection string
				if (appConnectionTab.ConnectionStringChoice == ApplicationConnectionTab.ConnectionStringChoiceType.Existing)
				{
					return _connectionStrings[appConnectionTab.ConnectionStringName];
				}
				else
				{
				    var sb = new SqlConnectionStringBuilder
				    {
				        DataSource = appConnectionTab.ServerName,
				        IntegratedSecurity = appConnectionTab.WindowsAuthentication
				    };

				    if (!appConnectionTab.WindowsAuthentication)
					{
						sb.UserID = appConnectionTab.Username;
						sb.Password = appConnectionTab.Password;
					}

					sb.InitialCatalog = databaseSelectionTab.SelectedDatabase;
					sb.AsynchronousProcessing = appConnectionTab.AsynchronousProcessing;

					return sb.ConnectionString;
				}


			}
		}
		internal DalConfig Config
		{
			get
			{
				return _config;
			}
		}



		public ModelDesigner(Dictionary<string, string> connectionStrings, DalConfig config)
		{
			_connectionStrings = connectionStrings;
			_config = config;

			InitializeComponent();

			CustomizeComponent();

			WireUpEventHandlers();
		}

		private void CustomizeComponent()
		{
			Text = "Model Designer - " + tabContainer.SelectedTab.Text;
			appConnectionTab.SetStaticData(_connectionStrings, _config);
			designerInformationTab.SetStaticData(_config);
			enumsTab.SetStaticData(_config);
			proceduresTab.SetStaticData(_config);
		}

		private void WireUpEventHandlers()
		{
			tabContainer.SelectedIndexChanged += VisibleTabChanged;
			
			appConnectionTab.CanContinueChanged += OnCanContinue;
			designerInformationTab.CanContinueChanged += OnCanContinue;
			databaseSelectionTab.CanContinueChanged += OnCanContinue;
			enumsTab.CanContinueChanged += OnCanContinue;
			proceduresTab.CanContinueChanged += OnCanContinue;
		}

		private void OnCanContinue(object o, CanContinueEventArgs e)
		{
			Control ctl = o as Control;

			// Make sure the control that raised this event is a child of a currently selected tab
			if (ctl != null && tabContainer.SelectedTab.Contains(ctl))
				nextButton.Enabled = (tabContainer.SelectedIndex < tabContainer.TabCount - 1) && e.CanContinue;
		}


		void VisibleTabChanged(object sender, EventArgs e)
		{
			previousButton.Enabled = tabContainer.SelectedIndex > 0;
			Text = "Model Designer - " + tabContainer.SelectedTab.Text;

			// do only when clicked on "Next"
			if (_currentTabControlIndex < tabContainer.SelectedIndex)
			{
				switch (tabContainer.SelectedTab.Name)
				{
					case "ApplicationConnection":
						break;

					case "DesignerConnection":
						break;

					case "DatabaseSelection":
						UpdateDatabaseSelectionData();
						break;

					case "Enums":
						UpdateEnumsData();
						break;

					case "Procedures":
						UpdateProceduresData();
						break;
				}
			}

			_currentTabControlIndex = tabContainer.SelectedIndex;
			finishButton.Enabled = tabContainer.SelectedIndex == tabContainer.TabCount - 1;
		}

		private void UpdateProceduresData()
		{
			proceduresTab.UpdateData(GetDesignerConnectionString());
		}

		private void UpdateEnumsData()
		{
			enumsTab.UpdateData(GetDesignerConnectionString());
		}

		private String GetDesignerConnectionString()
		{
			// Here I will need to build a different connection string
		    var sb = appConnectionTab.ConnectionStringChoice == ApplicationConnectionTab.ConnectionStringChoiceType.Existing
		        ? new SqlConnectionStringBuilder(_connectionStrings[appConnectionTab.ConnectionStringName])
		        : new SqlConnectionStringBuilder {DataSource = appConnectionTab.ServerName};

			sb.IntegratedSecurity = designerInformationTab.WindowsAuthentication;

			if (!designerInformationTab.WindowsAuthentication)
			{
				sb.UserID = designerInformationTab.Username;
				sb.Password = designerInformationTab.Password;
			}
			if (tabContainer.SelectedTab.Name != "DatabaseSelection")
			{
				sb.InitialCatalog = databaseSelectionTab.SelectedDatabase;
			}

			return sb.ConnectionString;
		}

		private void UpdateDatabaseSelectionData()
		{
			List<string> databasesCollection = new List<string>();

			Cursor savedCursor = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			String currentConnectionString = GetDesignerConnectionString();

			String selectedDatabase = new SqlConnectionStringBuilder(currentConnectionString).InitialCatalog;

			try
			{
				using (SqlConnection conn = new SqlConnection(currentConnectionString))
				{
					conn.Open();
					using (SqlCommand cmd = conn.CreateCommand())
					{
						cmd.CommandType = CommandType.StoredProcedure;
						cmd.CommandText = "sp_executesql";
						cmd.Parameters.AddWithValue("@stmt", "SELECT [name] FROM [sys].[databases];");
						using (var reader = cmd.ExecuteReader())
						{
							while (reader.Read())
							{
								string databaseName = (String)reader["name"];
								if ((appConnectionTab.ConnectionStringChoice == ApplicationConnectionTab.ConnectionStringChoiceType.Existing && selectedDatabase == databaseName) || appConnectionTab.ConnectionStringChoice == ApplicationConnectionTab.ConnectionStringChoiceType.New)
									databasesCollection.Add(databaseName);
							}
						}

					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Connection Error");
			}
			finally
			{
				Cursor.Current = savedCursor;
			}



			databaseSelectionTab.UpdateData(databasesCollection, appConnectionTab.ConnectionStringChoice == ApplicationConnectionTab.ConnectionStringChoiceType.New);
		}

		private void NextButton_Click(object sender, EventArgs e)
		{
			if (tabContainer.SelectedIndex < tabContainer.TabCount - 1)
			{
				tabContainer.SelectedIndex++;
			}
		}

		private void PreviousButton_Click(object sender, EventArgs e)
		{
			if (tabContainer.SelectedIndex > 0)
			{
				tabContainer.SelectedIndex--;
			}
		}

		private void cancelButton_Click(object sender, EventArgs e)
		{

		}

		private void finishButton_Click(object sender, EventArgs e)
		{
			var config = new DalConfig()
			{
				ApplicationConnectionString = appConnectionTab.ConnectionStringName,
				DesignerConnection = new DesignerConnection()
				{
					Authentication = designerInformationTab.WindowsAuthentication ? new WindowsAuthentication() : new SqlAuthentication(designerInformationTab.Username, designerInformationTab.Password) as Authentication
				},
				Namespace = designerInformationTab.Namespace,
				Enums = enumsTab.SelectedEnums,
				Procedures = proceduresTab.SelectedProcedures
			};

			_config = config;
		}
	}
}
