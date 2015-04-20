using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleDataAccessLayer_vs2013;

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
                        ProcedureName = "GetSomething",
                        Schema = "common"
                    }
                },
                Enums = new List<SimpleDataAccessLayer_vs2013.Enum>
                {
                    new SimpleDataAccessLayer_vs2013.Enum
                    {
                        Alias = "",
                        KeyColumn = "colName",
                        Schema = "common",
                        TableName = "Enum1",
                        ValueColumn = "colId"
                    }
                }
            };

            var cStr = "Data Source=db-01;Initial Catalog=scratch;Integrated Security=True;Asynchronous Processing=True";

            var code = string.Format("{0}{1}{2}{3}",
                new SimpleDataAccessLayer_vs2013.CodeBuilder.Common(dal, cStr).GetCode(),
                new SimpleDataAccessLayer_vs2013.CodeBuilder.TableValuedParameter(dal, cStr).GetCode(),
                new SimpleDataAccessLayer_vs2013.CodeBuilder.Enum(dal, cStr).GetCode(),
                new SimpleDataAccessLayer_vs2013.CodeBuilder.Procedure(dal, cStr).GetCode()
                );

            var z = code;
        }
    }
}
