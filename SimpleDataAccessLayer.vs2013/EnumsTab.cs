using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SimpleDataAccessLayer_vs2013
{
	public partial class EnumsTab : UserControl
	{
		private bool _isLoading;
		private bool _isRowUpdating;
		private string _currentConnectionString = "";
		private DalConfig _dalConfig;
		private readonly Dictionary<string, Enum> _configEnumsCollection = new Dictionary<string,Enum>();

		internal List<Enum> SelectedEnums
		{
		    get
		    {
		        return (from DataGridViewRow row in enumsGrid.Rows
		            where (bool) row.Cells["GenerateInterface"].Value
		            select new Enum()
		            {
		                Schema = (String) row.Cells["Schema"].Value,
		                TableName = (String) row.Cells["TableName"].Value,
		                KeyColumn = (String) row.Cells["KeyColumn"].Value,
		                ValueColumn = (String) row.Cells["ValueColumn"].Value,
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


		public EnumsTab()
		{
			InitializeComponent();

			WireUpEventHandlers();
		}

		internal void SetStaticData (DalConfig dalConfig)
		{
			_dalConfig = dalConfig;
			PrepareConfigEnumsCollection();
		}

		private void PrepareConfigEnumsCollection()
		{
			foreach (var _enum in _dalConfig.Enums)
			{
				_configEnumsCollection.Add(QuoteName(_enum.Schema) + "." + QuoteName(_enum.TableName), _enum);
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
				PopulateEnumsGrid();
			}
		}

		private void WireUpEventHandlers()
		{
			enumsGrid.CellValueChanged += EnumsGrid_CellValueChanged;
			VisibleChanged += SetNextButtonState;
		}

		private void SetNextButtonState(object sender, EventArgs e)
		{
			if (CanContinueChanged != null)
				CanContinueChanged(this, new CanContinueEventArgs(true));
		}

		void EnumsGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
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
				var row = enumsGrid.Rows[e.RowIndex];

				// this was a check box
				if (enumsGrid.Columns[e.ColumnIndex].Name == "GenerateInterface")
				{
					// if it was set to true, then need to make sure all columns are selected
					if ((bool)((DataGridViewCheckBoxCell)(row.Cells[e.ColumnIndex])).Value)
					{
						SetDefaultsForDropDownCells(row);
					}
					else
					{
						// remove all data from the row
						row.Cells["KeyColumn"].Value = row.Cells["ValueColumn"].Value = row.Cells["Alias"].Value = "";
					}
				}
				else
				{
					// Generate is already checked - do nothing
				    if (((DataGridViewCheckBoxCell) (row.Cells["GenerateInterface"])).Value != null &&
				        !(bool) ((DataGridViewCheckBoxCell) (row.Cells["GenerateInterface"])).Value)
				    {
				        SetDefaultsForDropDownCells(row);

				        ((DataGridViewCheckBoxCell) (row.Cells["GenerateInterface"])).Value = true;
				    }
				}
			}
			finally
			{
				_isRowUpdating = false;
			}
		}

		private static void SetDefaultsForDropDownCells(DataGridViewRow row)
		{
			if (String.IsNullOrWhiteSpace((String)(row.Cells["KeyColumn"].Value)))
				row.Cells["KeyColumn"].Value = ((DataGridViewComboBoxCell)row.Cells["KeyColumn"]).Items[0];

			if (String.IsNullOrWhiteSpace((String)(row.Cells["ValueColumn"].Value)))
				row.Cells["ValueColumn"].Value = ((DataGridViewComboBoxCell)row.Cells["ValueColumn"]).Items[0];
		}

		private void PopulateEnumsGrid()
		{
		    const string query = @"
				SELECT	[SchemaName],
						[TableName],
						CONVERT(xml, [ValueColumnsXml]) AS [ValueColumnsXml],
						CONVERT(xml, [KeyColumnsXml]) AS [KeyColumnsXml]
				FROM (
					SELECT 
						OBJECT_SCHEMA_NAME([object_id]) AS SchemaName,
						OBJECT_NAME([object_id]) AS TableName, 
						[object_id], 
					(
						SELECT [column_id] AS Value, [name] AS [Key]
						FROM [sys].[columns] ValueColumns
						WHERE 
							[object_id] = o.[object_id]
							AND [system_type_id] IN (48, 52, 56, 104, 127)
						FOR XML AUTO, ROOT('data')
					) AS ValueColumnsXml,
					(
						SELECT [column_id] AS Value, [name] AS [Key]
						FROM [sys].[columns] KeyColumns
						WHERE 
							[object_id] = o.[object_id]
							AND [system_type_id] IN (167, 175, 231, 239)
						FOR XML AUTO, ROOT('data')
					) AS KeyColumnsXml
					FROM [sys].[objects] o
					WHERE [type] IN ('U', 'V')
				) x
				WHERE [ValueColumnsXml] IS NOT NULL AND [KeyColumnsXml] IS NOT NULL
				ORDER BY 
					[SchemaName] ASC,
					[TableName] ASC;";

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
						enumsGrid.Rows.Clear();

						_isLoading = true;
						try
						{
							while (reader.Read())
							{
								AddRow(reader.GetFieldValue<string>(0), reader.GetFieldValue<string>(1), reader.GetFieldValue<string>(2), reader.GetFieldValue<string>(3));
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

	    private void AddRow(string schemaName, string tableName, string valueColumnsXml, string keyColumnsXml)
		{
			// Create new row and get the cell templates
			var row = enumsGrid.Rows[enumsGrid.Rows.Add()];
			var tableSchemaCell = (DataGridViewTextBoxCell)row.Cells["Schema"];
			var tableNameCell = (DataGridViewTextBoxCell)row.Cells["TableName"];
			var keysCell = (DataGridViewComboBoxCell)row.Cells["KeyColumn"];
			var valuesCell = (DataGridViewComboBoxCell)row.Cells["ValueColumn"];
			var alias = (DataGridViewTextBoxCell)row.Cells["Alias"];
			var generate = (DataGridViewCheckBoxCell)row.Cells["GenerateInterface"];

			tableSchemaCell.Value = schemaName;
			tableNameCell.Value = tableName;
			var quotedName = QuoteName(schemaName) + "." + QuoteName(tableName);
			var isEnumInConfig = _configEnumsCollection.ContainsKey(quotedName);
			alias.Value = isEnumInConfig ? _configEnumsCollection[quotedName].Alias : "";
			generate.Value = isEnumInConfig;

			var keysDataSet = new DataSet();
			keysDataSet.ReadXml(new StringReader(keyColumnsXml));

			foreach (var key in (from DataRow dataRow in keysDataSet.Tables["KeyColumns"].Rows select (String)dataRow["Key"]).Where(key => keysCell != null))
			{
			    keysCell.Items.Add(key);
			}

	        var valuesDataSet = new DataSet();
			valuesDataSet.ReadXml(new StringReader(valueColumnsXml));

			foreach (DataRow dataRow in valuesDataSet.Tables["ValueColumns"].Rows)
			{
				valuesCell.Items.Add((String)dataRow["Key"]);
			}

			if (isEnumInConfig)
			{
			    if (keysCell != null) keysCell.Value = _configEnumsCollection[quotedName].KeyColumn;
			    valuesCell.Value = _configEnumsCollection[quotedName].ValueColumn;
			}
		}

		private string QuoteName (string name)
		{
			if (name == null)
				return null;
			else
				return ("[" + name.Replace("]", "]]") + "]");
		}
	}
}
