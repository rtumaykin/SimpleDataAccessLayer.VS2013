using System;
using System.Windows.Forms;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class DesignerInformationTab : UserControl
	{
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
		public string Namespace
		{
			get
			{
				return namespaceTextBox.Text;
			}
		}


		private String _savedUserNameText = "", _savedPassword = "";
		private bool? _savedCanContinue;
		private int? _savedAuthenticationMethodIndex;

		private DalConfig _dalConfig;

		/// <summary>
		/// Declare the delegate that will be used to notify parent container
		/// </summary>
		/// <param name="o"></param>
		/// <param name="e"></param>
		public delegate void CanContinueHandler(object o, CanContinueEventArgs e);

		/// <summary>
		/// Declare the event
		/// </summary>
		public event CanContinueHandler CanContinueChanged;

		public DesignerInformationTab()
		{
			InitializeComponent();

			WireUpEventHandlers();

		}

		internal void SetStaticData(DalConfig config)
		{
			// this is called after all components of this control are initialized, so it is safe to populate all data
			_dalConfig = config;

			PopulateFormFields();
		}

		private void PopulateFormFields()
		{
			authenticationComboBox.SelectedIndex = _dalConfig.DesignerConnection.Authentication is WindowsAuthentication ? 0 : 1;

		    var sqlAuthentication = _dalConfig.DesignerConnection.Authentication as SqlAuthentication;
		    if (sqlAuthentication != null)
			{
				var auth = sqlAuthentication;
				userNameTextBox.Text = auth.UserName;
				passwordTextBox.Text = auth.Password;
			}

			namespaceTextBox.Text = _dalConfig.Namespace;
		}


		private void WireUpEventHandlers()
		{
			authenticationComboBox.SelectedIndexChanged += OnAuthenticationMethodChange;
			authenticationComboBox.SelectedIndexChanged += SetNextButtonEnabledState;
			userNameTextBox.TextChanged += SetNextButtonEnabledState;
			passwordTextBox.TextChanged += SetNextButtonEnabledState;
			namespaceTextBox.TextChanged += SetNextButtonEnabledState;
			VisibleChanged += OnShow;
		}

		private void OnShow(object sender, EventArgs e)
		{
			if (!DesignMode && Visible)
			{
				_savedCanContinue = null;
				SetNextButtonEnabledState(sender, e);
			}
		}

		private void SetNextButtonEnabledState(object sender, EventArgs e)
		{
		    var canContinue = (authenticationComboBox.SelectedIndex == 0 || (authenticationComboBox.SelectedIndex == 1 && !String.IsNullOrWhiteSpace(userNameTextBox.Text) && !String.IsNullOrWhiteSpace(passwordTextBox.Text))) && !String.IsNullOrWhiteSpace(namespaceTextBox.Text);
		    // only fire if it changed
			if (_savedCanContinue == null || canContinue != _savedCanContinue.Value)
			{
				_savedCanContinue = canContinue;
				if (CanContinueChanged != null)
					CanContinueChanged(this, new CanContinueEventArgs(canContinue));
			}
		}

		private void OnAuthenticationMethodChange(object sender, EventArgs e)
		{
			// change only if the new method is different from the previous one.
			// If you drop down and then choose again the same item, this event foires anyway even if the index did not change
			if (_savedAuthenticationMethodIndex != null && authenticationComboBox.SelectedIndex == _savedAuthenticationMethodIndex)
				return;

			_savedAuthenticationMethodIndex = authenticationComboBox.SelectedIndex;

			if (authenticationComboBox.SelectedIndex == 0)
			{
				_savedPassword = passwordTextBox.Text;
				_savedUserNameText = userNameTextBox.Text;

				userNameLabel.Text = "User Name";
				userNameTextBox.Text = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
				passwordTextBox.Text = "";
				userNameTextBox.Enabled = passwordTextBox.Enabled = false;
			}
			else
			{
				userNameTextBox.Text = _savedUserNameText;
				passwordTextBox.Text = _savedPassword;

				userNameLabel.Text = "Login";
				userNameTextBox.Enabled = passwordTextBox.Enabled = true;
			}
		}
	}
}
