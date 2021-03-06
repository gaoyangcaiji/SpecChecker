﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ClownFish.Base.Xml;
using ClownFish.Web;
using SpecChecker.WebLib.ViewModel;

namespace SpecChecker.WebLib.Services
{
	/// <summary>
	/// 将扫描结果转换成QA要求的报表结构，
	/// 这里的转换是没有规律的，基本上是按照许畅的报表模板来呈现数据
	/// </summary>
	internal class QaReportTableConvert
	{
		/// <summary>
		/// 当天的小组汇总数据
		/// </summary>
		public List<GroupDailySummary2> TodaySummary { get; set; }

		/// <summary>
		/// 前一天的小组汇总数据
		/// </summary>
		public List<GroupDailySummary2> LastdaySummary { get; set; }


		// 顺序必须和页面输出上的标题一致
		internal static readonly string[] GroupNames =
			(from x in SpecChecker.CoreLibrary.Config.JobManager.Jobs
			 select x.Name).ToArray();


		private static readonly Dictionary<string, string> s_propertyDict = new Dictionary<string, string>(){
            {"编译结果", "BuildIsOK"},
            { "安全规范", "Security"},
			{"高性能规范", "Performance"},
			{"稳定性规范", "Stability"},
			{"数据库规范", "Database"},
			{"项目设置规范", "Project"},
			{"ERP特殊规范", "ErpRule"},
			{"命名规范", "ObjectName"},
			{"微软托管规则", "VsRule"},
			{IssueCategoryManager.DefaultCategory, "Others"},
			{"基础问题小计", "1"},
			{"注释规范", "Comment"},
			{"单测用例通过率", "UnitTest"},
			{"单测代码覆盖率", "CodeCover"},
			{"性能日志", "PerformanceLog"},
			{"异常日志", "ExceptionLog"}
			  };

		
		private bool _isExistUnitTestData = false;
		

		public QaReportTable ToTableData()
		{
			if( this.TodaySummary == null || this.LastdaySummary == null )
				return null;

			// 判断是否有单元测试数据。早期是没有单元测试采集的，所以对于早期数据，虽然数值是 0，但是用  -- 来展示
			_isExistUnitTestData = IsExistUnitTestData();
			
			//s_groupNames = (from x in this.TodaySummary select x.GroupName).ToArray();

			// 计算所有的规则类别
			string[] scanKinds = (from x in s_propertyDict select x.Key).ToArray();

			// 构造表格数据对象
			QaReportTable table = new QaReportTable();
			table.Rows = new QaReportDataRow[scanKinds.Length];

			int i = -1;
			foreach(string kind in scanKinds ) {
				i++;
				// 每次循环创建一行数据
				QaReportDataRow row = new QaReportDataRow();
				table.Rows[i] = row;

				row.ScanKind = kind;
				row.Cells = new QaReportDataCell[GroupNames.Length];


				if( kind == "基础问题小计" ) {
					for( int j = 0; j < GroupNames.Length; j++ )
						row.Cells[j] = CreateTotalCell(GroupNames[j]);
				}
				else if( kind == "单测用例通过率" ) {
					CreateUnitTestRow(row);
				}
				else if( kind == "单测代码覆盖率" ) {
					CreateCodeCoverRow(row);
				}
                else if( kind == "编译结果" ) {
                    CreateBuildRow(row);
                }
                else {
					for( int j = 0; j < GroupNames.Length; j++ ) 
						row.Cells[j] = CreateCell(kind, GroupNames[j]);
				}
			}

			return table;
		}




		private QaReportDataCell CreateCell(string scanKind, string groupName)
		{
			string propertyName = s_propertyDict[scanKind];
			PropertyInfo property = typeof(TotalSummary2).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);


			GroupDailySummary2 todaySummary = this.TodaySummary.Find(x => x.GroupName == groupName);
			int todayValue = todaySummary?.Data != null ? (int)property.GetValue(todaySummary.Data, null) : 0;


			GroupDailySummary2 lastdaySummary = this.LastdaySummary.Find(x => x.GroupName == groupName);

			int lastdayValue = lastdaySummary?.Data != null ? (int)property.GetValue(lastdaySummary.Data, null) : 0;

			return new QaReportDataCell(todayValue, lastdayValue);
		}


		private QaReportDataCell CreateTotalCell(string groupName)
		{
			GroupDailySummary2 todaySummary = this.TodaySummary.Find(x => x.GroupName == groupName);
			int todayValue = 0;
			if( todaySummary?.Data != null ) {
				todayValue = todaySummary.Data.BaseTotal;
			}

			GroupDailySummary2 lastdaySummary = this.LastdaySummary.Find(x => x.GroupName == groupName);
			int lastdayValue = 0;
			if( lastdaySummary?.Data != null ) {
				lastdayValue =lastdaySummary.Data.BaseTotal;
			}

			return new QaReportDataCell(todayValue, lastdayValue);
		}


		private bool IsExistUnitTestData()
		{
			foreach( var data in this.TodaySummary ) {
				if( data.Data != null && data.Data.UnitTestTotal > 0 ) {
					return true;
				}
			}

			return false;
		}
		

		private void CreateUnitTestRow(QaReportDataRow row)
		{
			// 增加单元测试结果行
			for( int j = 0; j < GroupNames.Length; j++ ) {
				// 取各小组的扫描数据
				GroupDailySummary2 summary = this.TodaySummary.FirstOrDefault(x => x.GroupName == GroupNames[j]);

				if( summary != null ) {
					string text = $"{summary.Data.UnitTestPassed}/{summary.Data.UnitTestTotal}";

					// 单元测试如果执行失败，就以 -1 表示
					if( summary.Data.UnitTestTotal < 0 )
						row.Cells[j] = new QaReportDataCell("ERROR", "red");

					else if( summary.Data.UnitTestTotal == 0 ) {
						if( _isExistUnitTestData )
							row.Cells[j] = new QaReportDataCell("0", "red");
						else
							// 老数据中，没有采集单元数据，需要特殊处理
							row.Cells[j] = new QaReportDataCell("--", "#999");
					}

					else {
						if( summary.Data.UnitTestPassed == summary.Data.UnitTestTotal )
							row.Cells[j] = new QaReportDataCell(text, "green");

						else // 如果单元测试不能 100% 通过，就用红字显示
							row.Cells[j] = new QaReportDataCell(text, "red");
					}
				}
				else {
					// 未知场景，也按没有采集数据来处理
					row.Cells[j] = new QaReportDataCell("--", "#999");
				}
			}
		}


		private void CreateCodeCoverRow(QaReportDataRow row)
		{
			// 增加单元测试结果行
			for( int j = 0; j < GroupNames.Length; j++ ) {
				// 取各小组的扫描数据
				GroupDailySummary2 summary = this.TodaySummary.FirstOrDefault(x => x.GroupName == GroupNames[j]);

				if( summary != null ) {
					string text = summary.Data.CodeCover.ToString() + "%";

					// 单元测试如果执行失败，就以 -1 表示
					if( summary.Data.CodeCover < 0 )
						row.Cells[j] = new QaReportDataCell("ERROR", "red");

					else if( summary.Data.CodeCover == 0 ) {
						if( _isExistUnitTestData )
							row.Cells[j] = new QaReportDataCell("0", "red");
						else
							// 老数据中，没有采集单元数据，需要特殊处理
							row.Cells[j] = new QaReportDataCell("--", "#999");
					}


					else if( summary.Data.CodeCover < 60 )
						row.Cells[j] = new QaReportDataCell(text, "red");

					else if( summary.Data.CodeCover >= 80 )
						row.Cells[j] = new QaReportDataCell(text, "green");

					else
						row.Cells[j] = new QaReportDataCell(text, null);
				}
				else {
					// 未知场景，也按没有采集数据来处理
					row.Cells[j] = new QaReportDataCell("--", "#999");
				}
			}
		}


        private void CreateBuildRow(QaReportDataRow row)
        {
            // 增加单元测试结果行
            for( int j = 0; j < GroupNames.Length; j++ ) {
                // 取各小组的扫描数据
                GroupDailySummary2 summary = this.TodaySummary.FirstOrDefault(x => x.GroupName == GroupNames[j]);

                if( summary != null ) {
                    // 早期没有数据，或者没有启用编译任务
                    if( summary.Data.BuildIsOK.HasValue == false)
                        row.Cells[j] = new QaReportDataCell("--", "#999");

                    else if( summary.Data.BuildIsOK.Value )
                        row.Cells[j] = new QaReportDataCell("PASS", "green");

                    // 编译失败
                    else
                        row.Cells[j] = new QaReportDataCell("ERROR", "red");
                }
                else {
                    // 未知场景，也按没有采集数据来处理
                    row.Cells[j] = new QaReportDataCell("--", "#999");
                }
            }
        }

    }
}
