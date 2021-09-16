﻿/*
* Slickflow 软件遵循自有项目开源协议，也可联系作者获取企业版商业授权和技术支持；
* 除此之外的使用则视为不正当使用，请您务必避免由此带来的一切商业版权纠纷和损失。
* 
The Slickflow Open License (SfPL 1.0)
Copyright (C) 2014  .NET Workflow Engine Library

1. Slickflow software must be legally used, and should not be used in violation of law, 
   morality and other acts that endanger social interests;
2. Non-transferable, non-transferable and indivisible authorization of this software;
3. The source code can be modified to apply Slickflow components in their own projects 
   or products, but Slickflow source code can not be separately encapsulated for sale or 
   distributed to third-party users;
4. The intellectual property rights of Slickflow software shall be protected by law, and
   no documents such as technical data shall be made public or sold.
5. The enterprise, ultimate and universe version can be provided with commercial license, 
   technical support and upgrade service.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.Security;
using System.Web.Http;
using System.Web.Http.Controllers;
using Slickflow.Engine.Common;
using Slickflow.Engine.Utility;
using Slickflow.Engine.Core;
using Slickflow.Engine.Core.Result;
using Slickflow.Data;
using Slickflow.Engine.Business.Entity;
using Slickflow.Engine.Business.Manager;
using Slickflow.Engine.Service;
using Slickflow.WebApi.Utility;

namespace Slickflow.WebApi.Controllers
{
    //数据库表: WfProcess
    //流程记录ID：188
    //流程名称：并行分支流程测试
    //GUID: a0f15aad-81d3-467b-8a85-ab865ec4b3ab

    //startup process:
    //{"UserID":"6","UserName":"XiaoMing","AppName":"Multiple And-Split","AppInstanceID":"123","ProcessGUID":"a0f15aad-81d3-467b-8a85-ab865ec4b3ab"}

    //run process app:
    ////并行分支
    //{"AppName":"Multiple And-Split","AppInstanceID":"123","ProcessGUID":"a0f15aad-81d3-467b-8a85-ab865ec4b3ab", "UserID":"6","UserName":"XiaoMing", "NextActivityPerformers":{"d467834b-996c-42d7-fe27-1fff16d92460":[{"UserID":4,"UserName":"MissLi"}, {"UserID":24,"UserName":"MrsLang"}]}}


    //AndSplit 之后，并行任务的执行
    //带任务ID的Json数据，用于AndSplit产生的个人用户同时存在多个任务的案例
    //{"AppName":"Multiple And-Split","AppInstanceID":"123","ProcessGUID":"a0f15aad-81d3-467b-8a85-ab865ec4b3ab","TaskID":"1781","UserID":"4", "UserName":"MissLi", "NextActivityPerformers":{"2cd8ff3f-fd36-4508-cee5-44dd985618ab":[{"UserID":10,"UserName":"Long"}]}}
    //{"AppName":"Multiple And-Split","AppInstanceID":"123","ProcessGUID":"a0f15aad-81d3-467b-8a85-ab865ec4b3ab", "TaskID":"1782","UserID":"24", "UserName":"MrsLang", "NextActivityPerformers":{"2cd8ff3f-fd36-4508-cee5-44dd985618ab":[{"UserID":31,"UserName":"Jade"}]}}


    //run process app:
    ////并行汇合--> 归档
    //{"AppName":"Multiple And-Split","AppInstanceID":"123","ProcessGUID":"a0f15aad-81d3-467b-8a85-ab865ec4b3ab","UserID":"10","UserName":"Long", "NextActivityPerformers":{"e3bfbd48-df18-4e8c-a02f-9ccdfb1c8e4d":[{"UserID":10,"UserName":"Long"}]}}



    //撤销流程: WithdrawProcess
    //退回流程：SendBackProcess
    //返签流程：ReverseProcess
    //取消运行流程：CancelProcess
    //废弃所有流程实例：DiscardProcess
    /// <summary>
    /// </summary>
    public class WfAndSplitMIController : ApiController
    {
        //
        // GET: /WorkflowPL/

        #region Workflow Api访问操作
        [HttpPost]
        [AllowAnonymous]
        public ResponseResult StartProcess(WfAppRunner starter)
        {
            IWorkflowService wfService = new WorkflowService();
            IDbConnection conn = SessionFactory.CreateConnection();

            IDbTransaction trans = null;
            try
            {
                trans = conn.BeginTransaction();
                WfExecutedResult result = wfService.StartProcess(conn, starter, trans);
                trans.Commit();

                int newProcessInstanceID = result.ProcessInstanceIDStarted;
                if (result.Status == WfExecutedStatus.Success)
                {
                    return ResponseResult.Success();
                }
                else
                {
                    return ResponseResult.Error(result.Message);
                }
            }
            catch (WorkflowException w)
            {
                trans.Rollback();
                return ResponseResult.Error(w.Message);
            }
            finally
            {
                trans.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public ResponseResult RunProcessApp(WfAppRunner runner)
        {
            IWorkflowService wfService = new WorkflowService();
            IDbConnection conn = SessionFactory.CreateConnection();

            IDbTransaction trans = null;
            try
            {
                trans = conn.BeginTransaction();
                var result = wfService.RunProcessApp(conn, runner, trans);

                if (result.Status == WfExecutedStatus.Success)
                {
                    trans.Commit();
                    return ResponseResult.Success();
                }
                else
                {
                    trans.Rollback();
                    return ResponseResult.Error(result.Message);
                }
            }
            catch (WorkflowException w)
            {
                trans.Rollback();
                return ResponseResult.Error(w.Message);
            }
            finally
            {
                trans.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public ResponseResult WithdrawProcess(WfAppRunner runner)
        {
            IWorkflowService wfService = new WorkflowService();
            IDbConnection conn = SessionFactory.CreateConnection();

            IDbTransaction trans = null;
            try
            {
                trans = conn.BeginTransaction();
                var result = wfService.WithdrawProcess(conn, runner, trans);

                if (result.Status == WfExecutedStatus.Success)
                {
                    trans.Commit();
                    return ResponseResult.Success();
                }
                else
                {
                    trans.Rollback();
                    return ResponseResult.Error(result.Message);
                }
            }
            catch (WorkflowException w)
            {
                trans.Rollback();
                return ResponseResult.Error(w.Message);
            }
            finally
            {
                trans.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public ResponseResult SendBackProcess(WfAppRunner runner)
        {
            IWorkflowService wfService = new WorkflowService();
            IDbConnection conn = SessionFactory.CreateConnection();

            IDbTransaction trans = null;
            try
            {
                trans = conn.BeginTransaction();
                var result = wfService.SendBackProcess(conn, runner, trans);
                trans.Commit();

                if (result.Status == WfExecutedStatus.Success)
                    return ResponseResult.Success();
                else
                    return ResponseResult.Error(result.Message);
            }
            catch (WorkflowException w)
            {
                trans.Rollback();
                return ResponseResult.Error(w.Message);
            }
            finally
            {
                trans.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public ResponseResult ReverseProcess(WfAppRunner runner)
        {
            IWorkflowService wfService = new WorkflowService();
            IDbConnection conn = SessionFactory.CreateConnection();

            IDbTransaction trans = null;
            try
            {
                trans = conn.BeginTransaction();
                var result = wfService.ReverseProcess(conn, runner, trans);
                trans.Commit();

                if (result.Status == WfExecutedStatus.Success)
                    return ResponseResult.Success();
                else
                    return ResponseResult.Error(result.Message);
            }
            catch (WorkflowException w)
            {
                trans.Rollback();
                return ResponseResult.Error(w.Message);
            }
            finally
            {
                trans.Dispose();
                if (conn.State == ConnectionState.Open)
                    conn.Close();
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public ResponseResult DiscardProcess(WfAppRunner discarder)
        {
            IWorkflowService service = new WorkflowService();
            var result = service.DiscardProcess(discarder);

            return ResponseResult.Success();
        }

        [HttpPost]
        [AllowAnonymous]
        public ResponseResult GetNextActivityRoleUserTree(WfAppRunner nexter)
        {
            IWorkflowService service = new WorkflowService();
            var nodeViewList = service.GetNextActivityRoleUserTree(nexter, nexter.Conditions);

            return ResponseResult.Success(nodeViewList.Count().ToString());
        }
        #endregion

    }
}
