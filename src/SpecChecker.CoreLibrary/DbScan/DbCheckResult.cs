﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpecChecker.CoreLibrary.DbScan
{
	[Serializable]
	public class DbCheckResult : BaseScanResult
	{
		/// <summary>
		/// 不规范原因
		/// </summary>
		public string Reason { get; set; }

		/// <summary>
		/// 信息
		/// </summary>
		public string Informantion { get; set; }

		/// <summary>
		/// 表名
		/// </summary>
		public string TableName { get; set; }
	}
}
