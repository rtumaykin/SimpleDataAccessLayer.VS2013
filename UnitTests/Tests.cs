using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleDataAccessLayer_vs2013;
using SimpleDataAccessLayer_vs2013.CodeBuilder;
using Procedure = SimpleDataAccessLayer_vs2013.Procedure;

namespace UnitTests
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void ShouldGenerateCode()
        {
            var dal = new DalConfig()
            {
                ApplicationConnectionString = "appcon",
                DesignerConnection = new DesignerConnection()
                {
                    Authentication = new WindowsAuthentication()
                },
                Namespace = "testName.sds",
                Procedures = new List<Procedure>
                {
                    new Procedure
                    {
                        Alias = null,
                        ProcedureName = "BatchUpdate",
                        Schema = "asset"
                    },
                    new Procedure
                    {
                        Alias = null,
                        ProcedureName = "GetEngineId",
                        Schema = "calc"
                    },
                    new Procedure
                    {
                        Alias = null,
                        ProcedureName = "GetSampleData",
                        Schema = "calc"
                    }

                },
                Enums = new List<SimpleDataAccessLayer_vs2013.Enum>
                {
                    new SimpleDataAccessLayer_vs2013.Enum
                    {
                        Alias = "",
                        KeyColumn = "Name",
                        Schema = "asset",
                        TableName = "Status",
                        ValueColumn = "StatusId"
                    }
                }
            };

            var cStr = "Data Source=db-01;Initial Catalog=RulesRepository;Integrated Security=True;Asynchronous Processing=True";

#if DEBUG
            ISqlRepository sqlRepository = new SqlRepository(cStr);
            var code = string.Format("{0}{1}{2}{3}",
                new SimpleDataAccessLayer_vs2013.CodeBuilder.Common(dal).GetCode(),
                new SimpleDataAccessLayer_vs2013.CodeBuilder.TableValuedParameter(dal, sqlRepository).GetCode(),
                new SimpleDataAccessLayer_vs2013.CodeBuilder.Enum(dal, sqlRepository).GetCode(),
                new SimpleDataAccessLayer_vs2013.CodeBuilder.Procedure(dal, sqlRepository).GetCode()
                );


            var z = code;
#endif
        }
    }
}
