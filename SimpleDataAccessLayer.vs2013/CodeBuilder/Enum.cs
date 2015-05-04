using System;
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
        private readonly ISqlRepository _sqlRepository;

        public Enum(DalConfig config, ISqlRepository sqlRepository)
        {
            _config = config;
            if (sqlRepository == null)
                throw new ArgumentNullException("sqlRepository");
            _sqlRepository = sqlRepository;
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
                                Tools.ValidIdentifier(string.IsNullOrWhiteSpace(e.Alias) ? e.TableName : e.Alias), GetEnumBodyCode(e))));
        }

        private string GetEnumBodyCode(SimpleDataAccessLayer_vs2013.Enum enumInfo)
        {
            return string.Join(",",
                _sqlRepository.GetEnumKeyValues(enumInfo.Schema, enumInfo.TableName, enumInfo.ValueColumn, enumInfo.KeyColumn)
                    .Select(kv => string.Format("{0} = {1}", Tools.ValidIdentifier(kv.Key), kv.Value)));
        }
    }
}
