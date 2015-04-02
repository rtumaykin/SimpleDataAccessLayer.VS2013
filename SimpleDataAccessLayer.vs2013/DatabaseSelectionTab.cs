using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class DatabaseSelectionTab : UserControl
	{
		private List<String> _databasesCollection = new List<String>();

		public string SelectedDatabase
		{
			get
			{
				return databaseNameComboBox.Text;
			}
		}

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

		public DatabaseSelectionTab()
		{
			InitializeComponent();
		}

		internal void UpdateData(List<String> databasesCollection, bool isChoosingAllowed)
		{
			_databasesCollection = databasesCollection;
			UpdateDatabasesDropdown(isChoosingAllowed);
		}

		private void UpdateDatabasesDropdown(bool isChoosingAllowed)
		{
			string currentDatabaseText = databaseNameComboBox.Text;

			databaseNameComboBox.Items.Clear();
		    if (_databasesCollection != null) databaseNameComboBox.Items.AddRange(_databasesCollection.ToArray());

		    int newIndex = isChoosingAllowed ? databaseNameComboBox.Items.IndexOf(currentDatabaseText) : 0;
			if (databaseNameComboBox.Items.Count > 0)
				databaseNameComboBox.SelectedIndex = newIndex < 0 ? 0 : newIndex;

			SetNextButtonEnabledState(databaseNameComboBox.Items.Count > 0);
		}

		private void SetNextButtonEnabledState(bool enable)
		{
			if (CanContinueChanged != null)
				CanContinueChanged(this, new CanContinueEventArgs(enable));
		}

	}
}