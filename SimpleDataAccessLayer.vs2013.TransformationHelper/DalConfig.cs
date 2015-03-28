﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace SimpleDataAccessLayer.vs2013.TransformationHelper
{
	public enum AuthenticationType
	{
		Windows = 0,
		Sql = 1
	}
	//
	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "Authentication")]
	[KnownType(typeof(SqlAuthentication))]
	[KnownType(typeof(WindowsAuthentication))]
	public abstract class Authentication
	{
		private AuthenticationType _type;
		public Authentication(AuthenticationType type)
		{
			_type = type;
		}
	}
	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "SqlAuthentication")]
	public class SqlAuthentication : Authentication
	{
	    [DataMember(IsRequired = true)]
		public string UserName { get; set; }

	    [DataMember(IsRequired = true)]
		public string Password { get; set; }

	    public SqlAuthentication(string userName, string password)
			: base(AuthenticationType.Sql)
		{
			UserName = userName;
			Password = password;
		}
	}

	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "WindowsAuthentication")]
	public class WindowsAuthentication : Authentication
	{
		public WindowsAuthentication()
			: base(AuthenticationType.Windows)
		{ }
	}
	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "DesignerConnection")]
	public class DesignerConnection
	{
		[DataMember]
		public Authentication Authentication { get; set; }
	}

	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "Enum")]
	public class Enum
	{
		[DataMember(IsRequired = true)]
		public string Schema { get; set; }
		[DataMember(IsRequired = true)]
		public string TableName { get; set; }
		[DataMember(IsRequired = true)]
		public string KeyColumn { get; set; }
		[DataMember(IsRequired = true)]
		public string ValueColumn { get; set; }
		[DataMember(IsRequired = true)]
		public string Alias { get; set; }
	}
	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "Procedure")]
	public class Procedure
	{
		[DataMember(IsRequired = true)]
		public string Schema { get; set; }
		[DataMember(IsRequired = true)]
		public string ProcedureName { get; set; }
		[DataMember(IsRequired = false)]
		public string Alias { get; set; }
	}

	[DataContract(Namespace = "SimpleDataAcessLayer", Name = "DalConfig")]
	[KnownType(typeof(Enum))]
	[KnownType(typeof(Authentication))]
	[KnownType(typeof(DesignerConnection))]
	[KnownType(typeof(WindowsAuthentication))]
	[KnownType(typeof(SqlAuthentication))]
	public class DalConfig
	{
		[DataMember(IsRequired = true)]
		public DesignerConnection DesignerConnection { get; set; }
		[DataMember(IsRequired = true)]
		public String Namespace { get; set; }
		[DataMember(IsRequired = true)]
		public String ApplicationConnectionString { get; set; }
		[XmlElement("Enums")]
		[DataMember(IsRequired = true)]
		public List<Enum> Enums { get; set; }
		[XmlElement("Procedures")]
		[DataMember(IsRequired = true)]
		public List<Procedure> Procedures { get; set; }
	}
}
