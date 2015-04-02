using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.Win32;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class ApplicationConnectionTab : UserControl
	{
		public enum ConnectionStringChoiceType
		{
			New,
			Existing
		}
#region Public Properties

		public ConnectionStringChoiceType ConnectionStringChoice
		{
			get
			{
				return connectionString_Existing.Checked ? ConnectionStringChoiceType.Existing : ConnectionStringChoiceType.New;
			}
		}
		public string ConnectionStringName
		{
			get
			{
				return connectionString_Existing.Checked ? existingConnectionStringComboBox.Text : newConnectionStringTextBox.Text;
			}
		}

		public string ServerName
		{
			get
			{
				return serverNameComboBox.Text;
			}
		}

		public bool WindowsAuthentication
		{
			get
			{
				return authenticationComboBox.SelectedIndex == 0;
			}
		}
		public string Username
		{
			get
			{
				return userNameTextBox.Text;
			}
		}
		public string Password
		{
			get
			{
				return passwordTextBox.Text;
			}
		}
		public bool AsynchronousProcessing
		{
			get
			{
				return asynchronousComboBox.SelectedIndex == 0;
			}
		}

#endregion

		private Dictionary<string, string> _connectionStrings;
		private DalConfig _dalConfig;

		private String _savedUserNameText = "", _savedPassword = "", _savedSqlServerName = "", _savedNewConnectionStringName = "";
		int _savedAuthenticationIndex, _savedAsyncChoice;
		private bool? _savedCanContinue;

		/// <summary>
		/// Declare the delegate that will be used to notify parent container
		/// </summary>
		/// <param name="o"></param>
		/// <param name="e"></param>
		public delegate void CanContinueHandler (object o, CanContinueEventArgs e);

		/// <summary>
		/// Declare the event
		/// </summary>
		public event CanContinueHandler CanContinueChanged;
		private bool _serverDropdownInitialized;

		/// <summary>
		/// Public constructor
		/// </summary>
		public ApplicationConnectionTab()
		{
			// This will create all child controls
			InitializeComponent();

			// Once child controls have been created, I can attach event handlers to them
			WireUpEventHandlers();
		}

		private void WireUpEventHandlers()
		{
			// Local handlers
			connectionString_Existing.CheckedChanged += OnChangeConnectionStringChoice;
			connectionString_New.CheckedChanged += OnChangeConnectionStringChoice;

			authenticationComboBox.SelectedIndexChanged += OnAuthenticationMethodChange;
			existingConnectionStringComboBox.SelectedIndexChanged += PopulateFieldsFromConnectionString;

			serverNameComboBox.DropDown += PopulateSqlServersDropdown;

			// notifications to the main form
			connectionString_Existing.CheckedChanged += SetNextButtonEnabledState;
			connectionString_New.CheckedChanged += SetNextButtonEnabledState;
			// Will need to disable "existing" if there is no connection strings in the config file
			// once I do so, if the "existing" option is selected, I don't need to check any further. Therefore no handler 
			// is necessary for the "existing name" dropdown.
			newConnectionStringTextBox.TextChanged += SetNextButtonEnabledState;
			serverNameComboBox.TextChanged += SetNextButtonEnabledState;
			authenticationComboBox.SelectedIndexChanged += SetNextButtonEnabledState;
			userNameTextBox.TextChanged += SetNextButtonEnabledState;
			passwordTextBox.TextChanged += SetNextButtonEnabledState;
			VisibleChanged += OnShow;
		}

	    /// <summary>
	    /// Sets the initial data - existing connection strings and current config
	    /// </summary>
	    /// <param name="connectionStrings"></param>
	    /// <param name="config"></param>
	    internal void SetStaticData (Dictionary<string, string> connectionStrings, DalConfig config)
		{
			// this is called after all components of this control are initialized, so it is safe to populate all data
			_connectionStrings = connectionStrings;
			_dalConfig = config;

			PopulateFormFields();
		}

		private void PopulateFormFields()
		{
			PopulateExistingConnectionStringDropdown();

			InitializeConnectionStringRadioButtonChoice();

		}

	    private void PopulateExistingConnectionStringDropdown()
	    {
	        foreach (var index in from connectionString in _connectionStrings.Keys.ToList()
	            let index = existingConnectionStringComboBox.Items.Add(connectionString)
	            where _dalConfig.ApplicationConnectionString == connectionString
	            select index)
	        {
	            existingConnectionStringComboBox.SelectedIndex = index;
	        }
	    }

	    private void InitializeConnectionStringRadioButtonChoice()
		{
			if (existingConnectionStringComboBox.Items.Count > 0)
			{
				// this needs to be enabled first
				if (existingConnectionStringComboBox.SelectedIndex < 0)
				{
					existingConnectionStringComboBox.SelectedIndex = 0;
				}
				// this will fire the event
				connectionString_Existing.Checked = true;
			}
			else
			{
				connectionString_New.Checked = true;
				// disable since there is no connection string in the config file
				connectionString_Existing.Enabled = false;
			}
		}


		private void PopulateFieldsFromConnectionString(object sender, EventArgs e)
		{
			if (connectionString_Existing.Checked && existingConnectionStringComboBox.Enabled)
			{
				var cb = new SqlConnectionStringBuilder(_connectionStrings[existingConnectionStringComboBox.Text]);
				newConnectionStringTextBox.Text = existingConnectionStringComboBox.Text;
				authenticationComboBox.SelectedIndex = cb.IntegratedSecurity ? 0 : 1;
				serverNameComboBox.Text = cb.DataSource;
				userNameLabel.Text = cb.IntegratedSecurity ? "User Name" : "Login";
				userNameTextBox.Text = cb.IntegratedSecurity ? "" : cb.UserID;
				passwordTextBox.Text = cb.IntegratedSecurity ? "" : cb.Password;
				asynchronousComboBox.SelectedIndex = cb.AsynchronousProcessing ? 0 : 1;
			}
		}

		private void OnShow(object sender, EventArgs e)
		{
			if (!DesignMode && Visible)
			{
				_savedCanContinue = null;
				SetNextButtonEnabledState(sender, e);
			}
		}


		private void PopulateSqlServersDropdown(object sender, EventArgs e)
		{
			if (!_serverDropdownInitialized)
			{

				// capture what's now typed in the box
				var typedServerName = serverNameComboBox.Text;

				var currentCursor = Cursor.Current;
				Cursor.Current = Cursors.WaitCursor;

				var serverList = SmoApplication.EnumAvailableSqlServers();

				if (serverList.Rows.Count > 0)
				{
					// Load server names into combo box
					foreach (DataRow dr in serverList.Rows)
					{
						//only add if it doesn't exist
						if (serverNameComboBox.FindStringExact((String)dr["Name"]) == -1)
							serverNameComboBox.Items.Add(dr["Name"]);
					}
				}

				//Registry for local
				var rk = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server");
			    if (rk != null)
			    {
			        var instances = (String[])rk.GetValue("InstalledInstances");
			        if (instances != null && instances.Length > 0)
			        {
			            foreach (var element in instances)
			            {
			                string name;
			                //only add if it doesn't exist
			                if (element == "MSSQLSERVER")
			                    name = Environment.MachineName;
			                else
			                    name = Environment.MachineName + @"\" + element;

			                if (serverNameComboBox.FindStringExact(name) == -1)
			                    serverNameComboBox.Items.Add(name);
			            }
			        }
			    }

			    serverNameComboBox.Text = typedServerName;
				Cursor.Current = currentCursor;
			}
			_serverDropdownInitialized = true;
		}

		private void SetNextButtonEnabledState(object sender, EventArgs e)
		{
		    bool canContinue = connectionString_Existing.Checked || (!String.IsNullOrWhiteSpace(newConnectionStringTextBox.Text) && !String.IsNullOrWhiteSpace(serverNameComboBox.Text) && (authenticationComboBox.SelectedIndex == 0 || (authenticationComboBox.SelectedIndex == 1 && !String.IsNullOrWhiteSpace(userNameTextBox.Text) && !String.IsNullOrWhiteSpace(passwordTextBox.Text))));
		    // only fire if it changed
			if (_savedCanContinue == null ||  canContinue != _savedCanContinue.Value)
			{
				_savedCanContinue = canContinue;
				if (CanContinueChanged != null)
					CanContinueChanged(this, new CanContinueEventArgs(canContinue));
			}
		}

		private void OnAuthenticationMethodChange(object sender, EventArgs e)
		{
			if (authenticationComboBox.SelectedIndex == 0)
			{
				// only if the control is enabled. If it is disabled then it this shouldn't save/restore
				if (authenticationComboBox.Enabled)
				{
					_savedPassword = passwordTextBox.Text;
					_savedUserNameText = userNameTextBox.Text;
				}
				userNameLabel.Text = "User Name";
				userNameTextBox.Text = passwordTextBox.Text = "";
				userNameTextBox.Enabled = passwordTextBox.Enabled = false;
			}
			else
			{
				if (authenticationComboBox.Enabled)
				{
					userNameTextBox.Text = _savedUserNameText;
					passwordTextBox.Text = _savedPassword;
				}
				userNameLabel.Text = "Login";
				userNameTextBox.Enabled = passwordTextBox.Enabled = connectionString_New.Checked;
			}

		}

		private void OnChangeConnectionStringChoice(object sender, EventArgs e)
		{
			var selectedConnectionStringOption = sender as RadioButton;
			if (selectedConnectionStringOption == null || (selectedConnectionStringOption != connectionString_New && selectedConnectionStringOption != connectionString_Existing))
			{
				throw new Exception("Passed object is neither one of 2 available radio button choices");
			}

			// Only want to know the checked radiobutton
			if (!selectedConnectionStringOption.Checked)
				return;

			if (selectedConnectionStringOption == connectionString_New)
			{
				// restore saved data:
				newConnectionStringTextBox.Text = _savedNewConnectionStringName;
				authenticationComboBox.SelectedIndex = _savedAuthenticationIndex;
				serverNameComboBox.Text = _savedSqlServerName;
				userNameTextBox.Text = _savedUserNameText;
				passwordTextBox.Text = _savedPassword;
				asynchronousComboBox.SelectedIndex = _savedAsyncChoice;

				existingConnectionStringComboBox.Enabled = false;
				newConnectionStringTextBox.Enabled = serverNameComboBox.Enabled = authenticationComboBox.Enabled = asynchronousComboBox.Enabled = true;
				OnAuthenticationMethodChange(null, null);
			}
			else
			{
				existingConnectionStringComboBox.Enabled = true;
				// All other fields are populated from the connection string, but are disabled. Previous data is saved and should be restored when the selection changes
				_savedNewConnectionStringName = newConnectionStringTextBox.Text;
				_savedAuthenticationIndex = authenticationComboBox.SelectedIndex < 0 ? 0 : authenticationComboBox.SelectedIndex;
				_savedSqlServerName = serverNameComboBox.Text;
				_savedUserNameText = userNameTextBox.Text;
				_savedPassword = passwordTextBox.Text;
				_savedAsyncChoice = asynchronousComboBox.SelectedIndex < 0 ? 0 : asynchronousComboBox.SelectedIndex;

				newConnectionStringTextBox.Enabled = serverNameComboBox.Enabled = authenticationComboBox.Enabled = userNameTextBox.Enabled = passwordTextBox.Enabled = asynchronousComboBox.Enabled = false;
				PopulateFieldsFromConnectionString(null, null);
				// should add here a failover partner and async setting, and 
			}
			
		}

	}

	public class CanContinueEventArgs : EventArgs
	{
	    public bool CanContinue { get; private set; }

	    public CanContinueEventArgs(bool canContinue)
		{
			CanContinue = canContinue;
		}
	}


}
