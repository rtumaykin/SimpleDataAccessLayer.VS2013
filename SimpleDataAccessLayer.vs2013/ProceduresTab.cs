using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class ProceduresTab : UserControl
	{
		private bool _isLoading;
		private bool _isRowUpdating;
		private string _currentConnectionString = "";
		private DalConfig _dalConfig;
		private readonly Dictionary<string, Procedure> _configProceduresCollection = new Dictionary<string, Procedure>();

		internal List<Procedure> SelectedProcedures
		{
			get
			{
			    return (from DataGridViewRow row in proceduresGrid.Rows
			        where (bool) row.Cells["GenerateInterface"].Value
			        select new Procedure()
			        {
			            Schema = (String) row.Cells["Schema"].Value,
			            ProcedureName = (String) row.Cells["ProcedureName"].Value,
			            Alias = (String) row.Cells["Alias"].Value
			        }).ToList();
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

		public ProceduresTab()
		{
			InitializeComponent();

			WireUpEventHandlers();
		}

		internal void SetStaticData(DalConfig dalConfig)
		{
			_dalConfig = dalConfig;
			PrepareConfigProceduresCollection();
		}

		private void PrepareConfigProceduresCollection()
		{
			foreach (var procedure in _dalConfig.Procedures)
			{
				_configProceduresCollection.Add(QuoteName(procedure.Schema) + "." + QuoteName(procedure.ProcedureName), procedure);
			}
		}

		internal void UpdateData(string connectionString)
		{
			var reloadRequired = false;

			if (String.IsNullOrWhiteSpace(_currentConnectionString))
			{
				reloadRequired = true;
			}
			else
			{
				var sb = new SqlConnectionStringBuilder(connectionString);
				var currentSb = new SqlConnectionStringBuilder(_currentConnectionString);
				if (!(sb.DataSource == currentSb.DataSource && sb.InitialCatalog == currentSb.InitialCatalog))
				{
					reloadRequired = true;
				}
			}

			_currentConnectionString = connectionString;

			if (reloadRequired)
			{
				PopulateProceduresGrid();
			}
		}

		private void WireUpEventHandlers()
		{
			proceduresGrid.CellValueChanged += ProceduresGrid_CellValueChanged;
			VisibleChanged += SetNextButtonState;
		}

		private void SetNextButtonState(object sender, EventArgs e)
		{
			if (CanContinueChanged != null)
				CanContinueChanged(this, new CanContinueEventArgs(true));
		}

		void ProceduresGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			// this is happening within the same thread
			if (_isLoading)
				return;

			// This row is already updating 
			if (_isRowUpdating)
				return;

			// if none of the above let's start row updating;
			_isRowUpdating = true;
			try
			{
				var row = proceduresGrid.Rows[e.RowIndex];

				// this was a check box
				if (proceduresGrid.Columns[e.ColumnIndex].Name == "GenerateInterface")
				{
					// if it was set to true, then need to make sure all columns are selected
					if (!((bool)((DataGridViewCheckBoxCell)(row.Cells[e.ColumnIndex])).Value))
					{
						// remove all data from the row
						row.Cells["Alias"].Value = "";
					}
				}
				else
				{
					// Generate is already checked - do nothing
				    if (((DataGridViewCheckBoxCell) (row.Cells["GenerateInterface"])).Value != null &&
				        !(bool) ((DataGridViewCheckBoxCell) (row.Cells["GenerateInterface"])).Value)
				    {
				        ((DataGridViewCheckBoxCell) (row.Cells["GenerateInterface"])).Value = true;
				    }
				}
			}
			finally
			{
				_isRowUpdating = false;
			}
		}
		
		private void PopulateProceduresGrid()
		{
		    const string query = @"
				SELECT 
					OBJECT_SCHEMA_NAME([object_id]) AS SchemaName, 
					OBJECT_NAME([object_id]) AS ProcedureName
				FROM [sys].[objects] o
				WHERE [type] = 'P'
				ORDER BY 
					OBJECT_SCHEMA_NAME([object_id]) ASC, 
					OBJECT_NAME([object_id]) ASC;";

		    using (var conn = new SqlConnection(_currentConnectionString))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.CommandText = "sys.sp_executesql";
					cmd.Parameters.AddWithValue("@stmt", query);

					using (var reader = cmd.ExecuteReader())
					{
						// since this happens only when connection server and database changes, I can wipe old items
						proceduresGrid.Rows.Clear();

						_isLoading = true;
						try
						{
							while (reader.Read())
							{
								AddRow(reader.GetFieldValue<string>(0), reader.GetFieldValue<string>(1));
							}
						}
						finally
						{
							_isLoading = false;
						}
					}
				}
			}
		}

	    private void AddRow(string procedureSchema, string procedureName)
		{
			// Create new row and get the cell templates
			var row = proceduresGrid.Rows[proceduresGrid.Rows.Add()];

			var procedureSchemaCell = (DataGridViewTextBoxCell)row.Cells["Schema"];
			var procedureNameCell = (DataGridViewTextBoxCell)row.Cells["ProcedureName"];
			var alias = (DataGridViewTextBoxCell)row.Cells["Alias"];
			var generate = (DataGridViewCheckBoxCell)row.Cells["GenerateInterface"];

			var quotedName = QuoteName(procedureSchema) + "." + QuoteName(procedureName);

			procedureSchemaCell.Value = procedureSchema;
			procedureNameCell.Value = procedureName;
			var isEnumInConfig = _configProceduresCollection.ContainsKey(quotedName);
			alias.Value = isEnumInConfig ? _configProceduresCollection[quotedName].Alias : "";
			generate.Value = isEnumInConfig;
		}
		private string QuoteName(string name)
		{
			if (name == null)
				return null;
			else
				return ("[" + name.Replace("]", "]]") + "]");
		}
	}
}
