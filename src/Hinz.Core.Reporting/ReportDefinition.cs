using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Hinz.Core.Reporting
{
	/// <summary>
	/// Represents the report definition of the created for Reporting Service.
	/// </summary>
	public class ReportDefinition : IDisposable
	{

		#region Constant

		public const string DefaultReportDefinitionNamespace = "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition";

		#endregion

		#region DataSet subclass

		/// <summary>
		/// Represents the dataset for the report.
		/// </summary>
		public class DataSet
		{

			#region Properties

			/// <summary>
			/// Gets or sets the name of the <see cref="DataSet"/>.
			/// </summary>
			public string Name { get; set; }

			/// <summary>
			/// Gets or sets the command text for the <see cref="DataSet"/>.
			/// </summary>
			public string CommandText { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="bool"/> flag that indicate whether the
			/// current <see cref="DataSet"/> is the data source for the report used
			/// to generate the report's content.
			/// </summary>
			public bool IsReportDataSource { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="DataSource"/> of the <see cref="DataSet"/>.
			/// </summary>
			public DataTable DataSource { get; set; }

			#endregion

			#region Constructor

			/// <summary>
			/// Initialize a new instance of <see cref="DataSet"/>.
			/// </summary>
			public DataSet()
			{
				DataSource = null;
				Name = CommandText = string.Empty;
			}

			#endregion

		}

		#endregion

		#region DataSetReference subclass

		/// <summary>
		/// Represents the reference of <see cref="DataSet"/> by <see cref="ReportParameter"/>.
		/// </summary>
		public class DataSetReference
		{

			#region Properties

			/// <summary>
			/// Gets or sets the name of the <see cref="DataSet"/> that the <see cref="ReportParameter"/>
			/// refers to.
			/// </summary>
			public string DataSetName { get; set; }

			/// <summary>
			/// Gets or sets the field name used as the value for the <see cref="ReportParameter"/>.
			/// </summary>
			public string ValueField { get; set; }

			/// <summary>
			/// Gets or sets the field name used as display text.
			/// </summary>
			public string LabelField { get; set; }

			#endregion

			#region Constructor

			/// <summary>
			/// Initialize a new instance of <see cref="DataSetReference"/>.
			/// </summary>
			public DataSetReference()
			{
				DataSetName = ValueField = LabelField = string.Empty;
			}

			#endregion

		}

		#endregion

		#region ReportParameter subclass

		/// <summary>
		/// Represents the report parameter for the report.
		/// </summary>
		public class ReportParameter
		{

			#region Properties

			/// <summary>
			/// Gets or sets the name of the <see cref="ReportParameter"/>.
			/// </summary>
			public string Name { get; set; }

			/// <summary>
			/// Gets or sets the data type of the <see cref="ReportParameter"/>.
			/// </summary>
			public string DataType { get; set; }

			/// <summary>
			/// Gets or sets display text for the <see cref="ReportParameter"/>.
			/// </summary>
			public string Prompt { get; set; }

			/// <summary>
			/// Gets or sets the values for the <see cref="ReportParameter"/>.
			/// </summary>
			public List<string> Values { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="bool"/> flag that indicate whether the parameter
			/// is visible.
			/// </summary>
			public bool Visible { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="bool"/> flag that indicate whether the parameter
			/// accepts blank text as value.
			/// </summary>
			public bool AllowBlank { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="bool"/> flag that indicate whether the parameter
			/// accepts NULL value.
			/// </summary>
			public bool Nullable { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="bool"/> flag that indicate whether the parameter
			/// accepts multiple selection as value.
			/// </summary>
			public bool MultiValue { get; set; }

			/// <summary>
			/// Gets or sets the <see cref="DataSetReference"/> that provide information about
			/// the <see cref="DataSet"/> that the parameter refers.
			/// </summary>
			public DataSetReference DataSetReference { get; set; }

			#endregion

			#region Constructor

			/// <summary>
			/// Initialize a new instance of <see cref="ReportParameter"/>.
			/// </summary>
			public ReportParameter()
			{
				Name = DataType = Prompt = string.Empty;
				Values = new List<string>();
				Visible = true;
				DataSetReference = null;
			}

			#endregion

		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the list of <see cref="DataSet"/> for the <see cref="ReportDefinition"/>.
		/// </summary>
		private List<DataSet> DataSets { get; set; }

		/// <summary>
		/// Gets the MIME type of the report content.
		/// </summary>
		public string MIMEType { get; private set; }

		/// <summary>
		/// Gets or sets the list of <see cref="ReportParameter"/> for the <see cref="ReportDefinition"/>.
		/// </summary>
		public List<ReportParameter> ReportParameters { get; private set; }

		private Microsoft.Reporting.WinForms.LocalReport LocalReport { get; set; }

		#endregion

		#region Constructor

		/// <summary>
		/// Initialize a new instance of <see cref="ReportDefinition"/>.
		/// </summary>
		private ReportDefinition()
		{
			DataSets = new List<DataSet>();
			ReportParameters = new List<ReportParameter>();
		}

		#endregion

		#region Methods

		/// <summary>
		/// Release all resources.
		/// </summary>
		public void Dispose()
		{
			LocalReport.Dispose();
			DataSets.Clear();
			DataSets = null;
			ReportParameters.Clear();
			ReportParameters = null;
		}

		private void ExploreParameters(SqlCommand command)
		{
			command.Parameters.Clear();

			foreach (Match match in Regex.Matches(command.CommandText, "@[a-zA-Z0-9]{1,}"))
			{
				if (!command.Parameters.Contains(match.Value)) command.Parameters.Add(match.Value, SqlDbType.VarChar);
			}
		}

		/// <summary>
		/// Returns the <see cref="DataSet"/> with the name specified.
		/// </summary>
		/// <param name="dataSetName">The name of the <see cref="DataSet"/>.</param>
		/// <returns>The <see cref="DataSet"/> with the name specified.</returns>
		private DataSet GetDataSet(string dataSetName)
		{
			return DataSets.SingleOrDefault(d => d.Name.Equals(dataSetName));
		}

		/// <summary>
		/// Returns the <see cref="DataTable"/> that contains the data reference by the 
		/// <see cref="ReportParameter"/>.
		/// </summary>
		/// <param name="dataSetName">The name of the <see cref="DataSet"/>.</param>
		/// <returns>The <see cref="DataTable"/> that contains the reference data.</returns>
		public DataTable GetReferenceData(string dataSetName)
		{
			DataTable referenceData = null;
			DataSet dataSet = GetDataSet(dataSetName);
			if (dataSet != null)
			{
				referenceData = dataSet.DataSource.Copy();
			}
			return referenceData;
		}

		/// <summary>
		/// Initialize all report's data source, which are used for generating report.
		/// </summary>
		/// <param name="connection">The <see cref="SqlConnection"/> used to retrieve data.</param>
		/// <param name="commandTimeout">The number of seconds to execute the command before it times out.</param>
		public void InitializeReportDataSource(SqlConnection connection, int commandTimeout = 30)
		{
			if (connection == null) throw new ArgumentNullException("connection");

			if (DataSets != null)
			{
				using (SqlDataAdapter adapter = new SqlDataAdapter(connection.CreateCommand()))
				{
					adapter.SelectCommand.CommandTimeout = commandTimeout;
					adapter.SelectCommand.CommandType = CommandType.Text;

					foreach (DataSet dataSet in DataSets.Where(ds => !string.IsNullOrWhiteSpace(ds.CommandText)))
					{
						adapter.SelectCommand.CommandText = dataSet.CommandText;
						ExploreParameters(adapter.SelectCommand);
						foreach (SqlParameter sqlParam in adapter.SelectCommand.Parameters)
						{
							ReportParameter reportParam = ReportParameters.Single(p => p.Name.Equals(sqlParam.ParameterName.Substring(1)));
							if (reportParam != null)
							{
								if (reportParam.Nullable && reportParam.Values == null)
								{
									sqlParam.Value = DBNull.Value;
								}
								else
								{
									switch (reportParam.DataType)
									{
										case "String":
											sqlParam.Value = reportParam.MultiValue ? string.Concat(",", string.Join(",", reportParam.Values), ",") : reportParam.Values[0];
											break;
										case "Integer":
											sqlParam.SqlDbType = SqlDbType.BigInt;
											sqlParam.Value = Convert.ToInt64(reportParam.Values[0]);
											break;
										case "Float":
											sqlParam.SqlDbType = SqlDbType.Decimal;
											sqlParam.Value = Convert.ToDecimal(reportParam.Values[0]);
											break;
										case "DateTime":
											if (reportParam.Name.EndsWith("DateTime"))
											{
												sqlParam.SqlDbType = SqlDbType.DateTime;
												sqlParam.Value = Convert.ToDateTime(reportParam.Values[0]);
											}
											else if (reportParam.Name.EndsWith("Date"))
											{
												sqlParam.SqlDbType = SqlDbType.Date;
												sqlParam.Value = Convert.ToDateTime(reportParam.Values[0]).ToString("yyyy-MM-dd");
											}
											else if (reportParam.Name.EndsWith("Time"))
											{
												sqlParam.SqlDbType = SqlDbType.Time;
												sqlParam.Value = Convert.ToDateTime(reportParam.Values[0]).TimeOfDay;
											}
											else
											{
												sqlParam.SqlDbType = SqlDbType.DateTime;
												sqlParam.Value = Convert.ToDateTime(reportParam.Values[0]);
											}
											break;
										case "Boolean":
											sqlParam.SqlDbType = SqlDbType.Bit;
											sqlParam.Value = Convert.ToBoolean(reportParam.Values[0]) ? 1 : 0;
											break;
									}
								}
							}
							else
							{
								sqlParam.Value = DBNull.Value;
							}
						}

						dataSet.DataSource = new DataTable(dataSet.Name);
						adapter.FillSchema(dataSet.DataSource, SchemaType.Source);
						adapter.Fill(dataSet.DataSource);
					}
				}
			}
		}

		/// <summary>
		/// Loads the report definition based on the <see cref="Stream"/> of the report definition file.
		/// </summary>
		/// <param name="stream">The <see cref="Stream"/> of report definition file.</param>
		/// <param name="connection">The <see cref="SqlConnection"/> used to initialize the reference 
		/// data for <see cref="ReportParameter"/>.</param>
		/// <param name="commandTimeout">The number of seconds to execute the command before it times out.</param>
		/// <returns>An instance of <see cref="ReportDefinition"/>.</returns>
		public static ReportDefinition LoadReportDefinition(Stream stream, SqlConnection connection, int commandTimeout = 30)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			ReportDefinition definition = new ReportDefinition();
			string reportDefinitionNamespace = DefaultReportDefinitionNamespace;

			// Reads the report definition into XDocument.
			XDocument document = XDocument.Load(stream);

			// Attempts to detect the report definition namespace;
			foreach (XAttribute attribute in document.Root.Attributes())
			{
				if (attribute.Value.Contains("/reportdefinition"))
				{
					reportDefinitionNamespace = attribute.Value;
					break;
				}
			}

			// Reads the list of DataSet defined.
			foreach (XElement element in document.Descendants(XName.Get("DataSet", reportDefinitionNamespace)))
			{
				definition.DataSets.Add
				(
					new DataSet
					{
						Name = element.Attribute("Name").Value,
						CommandText = element
							.Element(XName.Get("Query", reportDefinitionNamespace))
							.Element(XName.Get("CommandText", reportDefinitionNamespace))
							.Value,
						IsReportDataSource = true
					}
				);
			}

			// Reads the list of ReportParameter defined.
			foreach (XElement element in document.Descendants(XName.Get("ReportParameter", reportDefinitionNamespace)))
			{
				ReportParameter parameter = new ReportParameter
				{
					Name = element.Attribute("Name").Value,
					DataType = element.Element(XName.Get("DataType", reportDefinitionNamespace)).Value,
					Prompt = element.Element(XName.Get("Prompt", reportDefinitionNamespace)).Value
				};

				XElement childElement;

				// Gets the flag of whether the parameter is visible.
				childElement = element.Element(XName.Get("Hidden", reportDefinitionNamespace));
				if (childElement != null) parameter.Visible = !bool.Parse(childElement.Value);

				// Gets the flag of whether the parameter allow blank text as value.
				childElement = element.Element(XName.Get("AllowBlank", reportDefinitionNamespace));
				if (childElement != null) parameter.AllowBlank = bool.Parse(childElement.Value);

				// Gets the flag of whether the parameter allow NULL as value.
				childElement = element.Element(XName.Get("Nullable", reportDefinitionNamespace));
				if (childElement != null) parameter.Nullable = bool.Parse(childElement.Value);

				// Gets the flag of whether the parameter allow multiple values.
				childElement = element.Element(XName.Get("MultiValue", reportDefinitionNamespace));
				if (childElement != null) parameter.MultiValue = bool.Parse(childElement.Value);

				// Gets the DataSetReference of the parameter.
				childElement = element.Element(XName.Get("ValidValues", reportDefinitionNamespace));
				if (childElement != null)
				{
					XElement dataSetReferenceElement = childElement.Element(XName.Get("DataSetReference", reportDefinitionNamespace));
					if (dataSetReferenceElement != null)
					{
						parameter.DataSetReference = new DataSetReference
						{
							DataSetName = dataSetReferenceElement.Element(XName.Get("DataSetName", reportDefinitionNamespace)).Value,
							ValueField = dataSetReferenceElement.Element(XName.Get("ValueField", reportDefinitionNamespace)).Value,
							LabelField = dataSetReferenceElement.Element(XName.Get("LabelField", reportDefinitionNamespace)).Value
						};

						DataSet dataSet = definition.GetDataSet(parameter.DataSetReference.DataSetName);
						if (dataSet != null)
						{
							dataSet.IsReportDataSource = false; // Set it to FALSE to indicate it's not the actual data source for generating report.
							if (dataSet.DataSource == null)
							{
								dataSet.DataSource = new DataTable(dataSet.Name);
								using (SqlDataAdapter adapter = new SqlDataAdapter(dataSet.CommandText, connection))
								{
									adapter.SelectCommand.CommandTimeout = commandTimeout;
									adapter.FillSchema(dataSet.DataSource, System.Data.SchemaType.Source);
									adapter.Fill(dataSet.DataSource);
								}
							}
						}
					}
					else
					{
						XElement parameterValuesElement = childElement.Element(XName.Get("ParameterValues", reportDefinitionNamespace));
						if (parameterValuesElement != null)
						{
							string dataSetName = string.Format("{0}AvailableValues", parameter.Name);
							DataSet dataSet = definition.GetDataSet(dataSetName);
							if (dataSet == null)
							{
								dataSet = new DataSet
								{
									Name = dataSetName,
									CommandText = string.Empty,
									IsReportDataSource = false
								};
								dataSet.DataSource = new DataTable(dataSet.Name);
								dataSet.DataSource.Columns.Add("ValueField", typeof(string));
								dataSet.DataSource.Columns.Add("LabelField", typeof(string));

								foreach (XElement parameterValueElement in parameterValuesElement.Elements(XName.Get("ParameterValue", reportDefinitionNamespace)))
								{
									System.Data.DataRow newRow = dataSet.DataSource.NewRow();
									newRow["ValueField"] = parameterValueElement.Element(XName.Get("Value", reportDefinitionNamespace)).Value;
									newRow["LabelField"] = parameterValueElement.Element(XName.Get("Label", reportDefinitionNamespace)).Value;
									dataSet.DataSource.Rows.Add(newRow);
								}

								definition.DataSets.Add(dataSet);
							}

							parameter.DataSetReference = new DataSetReference
							{
								DataSetName = dataSet.Name,
								ValueField = dataSet.DataSource.Columns[0].ColumnName,
								LabelField = dataSet.DataSource.Columns.Count > 1 ? dataSet.DataSource.Columns[1].ColumnName : dataSet.DataSource.Columns[0].ColumnName
							};
						}
					}
				}

				definition.ReportParameters.Add(parameter);
			}

			definition.LocalReport = new Microsoft.Reporting.WinForms.LocalReport();
			stream.Position = 0;
			definition.LocalReport.LoadReportDefinition(stream);

			stream.Close();
			stream.Dispose();

			return definition;
		}

		/// <summary>
		/// Loads the report definition based on the report definition file specified.
		/// </summary>
		/// <param name="reportPath">The file path of the report definition file.</param>
		/// <param name="connection">The <see cref="SqlConnection"/> used to initialize the reference 
		/// data for <see cref="ReportParameter"/>.</param>
		/// <returns>An instance of <see cref="ReportDefinition"/>.</returns>
		public static ReportDefinition LoadReportDefinition(string reportPath, SqlConnection connection)
		{
			if (string.IsNullOrWhiteSpace(reportPath)) throw new ArgumentNullException("reportPath");
			if (!File.Exists(reportPath)) throw new FileNotFoundException
			(
				"Unable to locate the report definition file.",
				reportPath
			);

			return LoadReportDefinition(File.OpenRead(reportPath), connection);
		}

		/// <summary>
		/// Render the report as comma-separated text file to be display on web page.
		/// </summary>
		/// <returns>A series of <see cref="byte"/>, which is the content of the report rendered as CSV file.</returns>
		public byte[] RenderAsCSV()
		{
			byte[] reportContent = null;
			MemoryStream stream = new MemoryStream();

			using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.Unicode))
			{
				foreach (DataTable dt in DataSets.Where(ds => ds.IsReportDataSource).Select(ds => ds.DataSource))
				{
					if (dt.Columns.Count > 0 && dt.Rows.Count > 0)
					{
						// Write row header.
						foreach (System.Data.DataColumn column in dt.Columns)
						{
							writer.Write
							(
								string.Format
								(
									@"{1}""{0}""",
									column.ColumnName,
									column.Ordinal.Equals(0) ? string.Empty : ","
								)
							);
						}
						writer.Write(Environment.NewLine);

						// Write content.
						foreach (System.Data.DataRow row in dt.Rows)
						{
							foreach (System.Data.DataColumn column in dt.Columns)
							{
								writer.Write
								(
									string.Format
									(
										@"{1}""{0}""",
										row.IsNull(column) ? string.Empty : row[column].ToString(),
										column.Ordinal.Equals(0) ? string.Empty : ","
									)
								);
							}
							writer.Write(Environment.NewLine);
						}
					}
				}

				stream.Position = 0;
				reportContent = stream.ToArray();
			}

			MIMEType = "text/csv";

			return reportContent;
		}

		/// <summary>
		/// Render the report as Excel to be display on web page.
		/// </summary>
		/// <returns>A series of <see cref="byte"/>, which is the content of the report rendered as Excel.</returns>
		public byte[] RenderAsExcel()
		{
			byte[] reportContent = null;
			// Refer http://msdn.microsoft.com/en-us/library/ms155069 for complete list of settings.
			string deviceInfo = string.Empty;

			var reportDataSource =
				from d in DataSets
				where d.IsReportDataSource
				select new Microsoft.Reporting.WinForms.ReportDataSource(d.Name, d.DataSource);

			foreach (Microsoft.Reporting.WinForms.ReportDataSource source in reportDataSource)
			{
				LocalReport.DataSources.Add(source);
			}

			var reportParams =
				from p in ReportParameters
				where p.Values != null
				select new Microsoft.Reporting.WinForms.ReportParameter(p.Name, p.Values.ToArray());

			LocalReport.SetParameters(reportParams);

			reportContent = LocalReport.Render("Excel", deviceInfo, out string mimeType, out string encoding, out string fileNameExtension, out string[] streams, out Microsoft.Reporting.WinForms.Warning[] warnings);
			MIMEType = mimeType;

			return reportContent;
		}

		/// <summary>
		/// Render the report as image to be display on web page.
		/// </summary>
		/// <returns>A series of <see cref="byte"/>, which is the content of the report rendered as image.</returns>
		public byte[] RenderAsImage()
		{
			byte[] reportContent = null;
			// Refer http://msdn.microsoft.com/en-us/library/ms155373.aspx for complete list of settings.
			string deviceInfo =
				"<DeviceInfo>" +
				"  <OutputFormat>JPEG</OutputFormat>" +
				"  <StartPage>0</StartPage>" +
				"</DeviceInfo>";

			var reportDataSource =
				from d in DataSets
				where d.IsReportDataSource
				select new Microsoft.Reporting.WinForms.ReportDataSource(d.Name, d.DataSource);

			foreach (Microsoft.Reporting.WinForms.ReportDataSource source in reportDataSource)
			{
				LocalReport.DataSources.Add(source);
			}

			var reportParams =
				from p in ReportParameters
				where p.Values != null
				select new Microsoft.Reporting.WinForms.ReportParameter(p.Name, p.Values.ToArray());

			LocalReport.SetParameters(reportParams);
			//reportContent = LocalReport.Render("Image", deviceInfo);
			reportContent = LocalReport.Render("Image", deviceInfo, out string mimeType, out string encoding, out string fileNameExtension, out string[] streams, out Microsoft.Reporting.WinForms.Warning[] warnings);

			MIMEType = mimeType;

			return reportContent;
		}

		/// <summary>
		/// Render the report as PDF file to be display on web page.
		/// </summary>
		/// <returns>A series of <see cref="byte"/>, which is the content of the report rendered as PDF.</returns>
		public byte[] RenderAsPDF()
		{
			byte[] reportContent = null;
			// Refer http://msdn.microsoft.com/en-us/library/ms154682 for complete list of settings.
			string deviceInfo =
				"<DeviceInfo>" +
				"  <StartPage>0</StartPage>" +
				"</DeviceInfo>";

			var reportDataSource =
				from d in DataSets
				where d.IsReportDataSource
				select new Microsoft.Reporting.WinForms.ReportDataSource(d.Name, d.DataSource);

			foreach (Microsoft.Reporting.WinForms.ReportDataSource source in reportDataSource)
			{
				LocalReport.DataSources.Add(source);
			}

			var reportParams =
				from p in ReportParameters
				where p.Values != null
				select new Microsoft.Reporting.WinForms.ReportParameter(p.Name, p.Values.ToArray());

			LocalReport.SetParameters(reportParams);

			reportContent = LocalReport.Render("PDF", deviceInfo, out string mimeType, out string encoding, out string fileNameExtension, out string[] streams, out Microsoft.Reporting.WinForms.Warning[] warnings);
			MIMEType = mimeType;

			return reportContent;
		}

		/// <summary>
		/// Sets the data source for the <see cref="DataSet"/>.
		/// </summary>
		/// <param name="dataSetName">The name of the <see cref="DataSet"/>.</param>
		/// <param name="dataTable">The data to be set to the <see cref="DataSet"/>.</param>
		public void SetDataSource(string dataSetName, DataTable dataTable)
		{
			if (string.IsNullOrWhiteSpace(dataSetName)) throw new ArgumentNullException("dataSetName");
			if (dataTable == null) throw new ArgumentNullException("dataTable");

			DataSet dataSet = GetDataSet(dataSetName);
			if (dataSet == null)
			{
				throw new ArgumentException(string.Format("The dataset '{0}' specified does not exist.", dataSetName));
			}
			else
			{
				dataSet.DataSource = dataTable;
			}
		}

		/// <summary>
		/// Sets the value for the <see cref="ReportParameter"/>.
		/// </summary>
		/// <param name="parameterName">The name of the <see cref="ReportParameter"/>.</param>
		/// <param name="value">The value of the <see cref="ReportParameter"/>.</param>
		public void SetReportParameterValue(string parameterName, string value)
		{
			if (string.IsNullOrWhiteSpace(parameterName)) throw new ArgumentNullException("parameterName");

			ReportParameter param = ReportParameters.SingleOrDefault(p => p.Name.Equals(parameterName)) ?? throw new ArgumentException(string.Format("The report parameter '{0}' does not exist.", parameterName));
			if (value == null && !param.Nullable) throw new InvalidOperationException("The report parameter is not nullable.");
			if (value == string.Empty && !param.AllowBlank) throw new InvalidOperationException("The report parameter can not be blank.");

			if (param.Values == null)
				param.Values = new List<string> { value };
			else
			{
				param.Values.Clear();
				param.Values.Add(value);
			}
		}

		/// <summary>
		/// Sets the value for the <see cref="ReportParameter"/>.
		/// </summary>
		/// <param name="parameterName">The name of the <see cref="ReportParameter"/>.</param>
		/// <param name="values">The value of the <see cref="ReportParameter"/>.</param>
		public void SetReportParameterValue(string parameterName, List<string> values)
		{
			if (string.IsNullOrWhiteSpace(parameterName)) throw new ArgumentNullException("parameterName");

			ReportParameter param = ReportParameters.Single(p => p.Name.Equals(parameterName)) ?? throw new ArgumentException(string.Format("The report parameter '{0}' does not exist.", parameterName));
			if (values == null && !param.Nullable) throw new InvalidOperationException("The report parameter is not nullable.");

			if (values.Exists(v => string.IsNullOrWhiteSpace(v)) && !param.AllowBlank) throw new InvalidOperationException("The report parameter can not be blank.");

			param.Values = values;
		}

		#endregion

	}

}
