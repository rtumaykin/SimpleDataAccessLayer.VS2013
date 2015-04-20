using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace SimpleDataAccessLayer_vs2013.CodeBuilder
{
#if DEBUG 
    public class Enum
#else 
    internal class Enum
#endif
    {
        private readonly DalConfig _config;
        private readonly string _designerConnectionString;

        public Enum(DalConfig config, string designerConnectionString)
        {
            _config = config;
            _designerConnectionString = designerConnectionString;
		}

        public string GetCode()
        {
            if (_config == null || _config.Enums == null)
                return "";

            return string.Join("", _config.Enums.Select(e => e.Schema)
                .Distinct().Select(ns =>
                    string.Format("namespace {0}.Enums.{1} {{{2}}}", _config.Namespace, ns,
                        GetEnumsCodeForNamespace(ns))));

        }

        private string GetEnumsCodeForNamespace(string ns)
        {
            return string.Join("",
                _config.Enums.Where(e => e.Schema == ns)
                    .Select(
                        e =>
                            string.Format("public enum {0}{{{1}}}",
                                Tools.CleanName(string.IsNullOrWhiteSpace(e.Alias) ? e.TableName : e.Alias), GetEnumBodyCode(e))));
        }

        private string GetEnumBodyCode(SimpleDataAccessLayer_vs2013.Enum enumInfo)
        {
            return string.Join(",",
                GetKeyValues(enumInfo.Schema, enumInfo.TableName, enumInfo.ValueColumn, enumInfo.KeyColumn)
                    .Select(kv => string.Format("{0} = {1}", kv.Key, kv.Value)));
        }

        private IList<EnumKeyValue> GetKeyValues(string objectSchemaName, string objectName, string valueColumnName,
            string keyColumnName)
        {
            var retValue = new List<EnumKeyValue>();
            var fullObjectName = Tools.QuoteName(objectSchemaName) + "." + Tools.QuoteName(objectName);

            using (var conn = new SqlConnection(_designerConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_executesql";

                    cmd.Parameters.Add(new SqlParameter("@stmt",
                        String.Format("SELECT CONVERT(bigint, {0}) AS [Value], {1} AS [Key] FROM {2} ORDER BY {0}",
                            valueColumnName, keyColumnName, fullObjectName)));

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            retValue.Add(new EnumKeyValue((string) reader["Key"], (long) reader["Value"]));
                        }
                    }
                }
            }
            return retValue;
        }
    }
}
