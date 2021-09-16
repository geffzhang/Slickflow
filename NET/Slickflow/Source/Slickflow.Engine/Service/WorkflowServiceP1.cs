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
using System.Text;
using System.Data;
using System.Threading;
using Slickflow.Module.Resource;
using Slickflow.Engine.Common;
using Slickflow.Engine.Utility;
using Slickflow.Data;
using Slickflow.Engine.Business.Entity;
using Slickflow.Engine.Business.Manager;
using Slickflow.Engine.Xpdl;

namespace Slickflow.Engine.Service
{
    /// <summary>
    /// 工作流服务（数据查询）
    /// </summary>
    public partial class WorkflowService : IWorkflowService
    {
        #region 流程实例信息获取
        /// <summary>
        /// 获取流程实例数据
        /// </summary>
        /// <param name="processInstanceID">流程实例ID</param>
        /// <returns>流程实例实体</returns>
        public ProcessInstanceEntity GetProcessInstance(int processInstanceID)
        {
            var pim = new ProcessInstanceManager();
            var instance = pim.GetById(processInstanceID);
            return instance;
        }

        /// <summary>
        /// 获取流程实例数据
        /// </summary>
        /// <param name="activityInstanceID">活动实例ID</param>
        /// <returns>流程实例实体</returns>
        public ProcessInstanceEntity GetProcessInstanceByActivity(int activityInstanceID)
        {
            var pim = new ProcessInstanceManager();
            var instance = pim.GetByActivity(activityInstanceID);
            return instance;
        }

        /// <summary>
        /// 获取流程正常实例数据
        /// </summary>
        /// <param name="runner"></param>
        /// <returns></returns>
        public ProcessInstanceEntity GetProcessInstance(WfAppRunner runner, IDbConnection conn = null)
        {
            if (conn == null)
            {
                conn = SessionFactory.CreateConnection();
            }

            try
            {
                var pim = new ProcessInstanceManager();
                var processInstance = pim.GetProcessInstanceLatest(runner.AppInstanceID, runner.ProcessGUID);
                return processInstance;
            }
            catch
            {
                throw;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        /// 获取运行中的流程实例
        /// </summary>
        /// <param name="runner">运行者</param>
        /// <returns>流程实例实体</returns>
        public ProcessInstanceEntity GetRunningProcessInstance(WfAppRunner runner)
        {
            ProcessInstanceEntity entity = null;
            IDbConnection conn = SessionFactory.CreateConnection();
            try
            {
                var pim = new ProcessInstanceManager();
                entity = pim.GetRunningProcessInstance(conn, runner.AppInstanceID, runner.ProcessGUID);
            }
            catch
            {
                throw;
            }
            finally
            {
                conn.Close();
            }
            return entity;
        }

        /// <summary>
        /// 判断流程实例是否存在
        /// </summary>
        /// <param name="processGUID">流程定义ID</param>
        /// <param name="version">流程定义版本</param>
        /// <returns>流程实例记录数</returns>
        public Int32 GetProcessInstanceCount(string processGUID, string version)
        {
            IDbConnection conn = SessionFactory.CreateConnection();
            try
            {
                var pim = new ProcessInstanceManager();
                return pim.GetProcessInstanceCount(conn, processGUID, version);
            }
            catch
            {
                throw;
            }
            finally
            {
                conn.Close();
            }
        }

        /// <summary>
        /// 获取流程发起人信息
        /// </summary>
        /// <param name="processInstanceID">流程实例Id</param>
        /// <returns>执行者</returns>
        public User GetProcessInitiator(int processInstanceID)
        {
            User initiator = null;
            try
            {
                var pim = new ProcessInstanceManager();
                initiator = pim.GetProcessInitiator(processInstanceID);
            }
            catch
            {
                throw;
            }
            return initiator;
        }

        /// <summary>
        /// 获取活动实例数据
        /// </summary>
        /// <param name="activityInstanceID"></param>
        /// <returns></returns>
        public ActivityInstanceEntity GetActivityInstance(int activityInstanceID)
        {
            var aim = new ActivityInstanceManager();
            var instance = aim.GetById(activityInstanceID);
            return instance;
        }

        /// <summary>
        /// 获取一个流程实例下的所有活动实例
        /// </summary>
        /// <param name="processInstanceID"></param>
        /// <returns></returns>
        public IList<ActivityInstanceEntity> GetActivityInstances(int processInstanceID)
        {
            var aim = new ActivityInstanceManager();
            var session = SessionFactory.CreateSession();
            try
            {
                return aim.GetActivityInstances(processInstanceID, session);
            }
            catch
            {
                throw;
            }
            finally
            {
                session.Dispose();
            }
        }

        /// <summary>
        /// 获取当前节点的下一步已经发出的活动实例列表(transition实例表)
        /// </summary>
        /// <param name="fromActivityInstanceID">活动实例ID</param>
        /// <returns></returns>
        public IList<ActivityInstanceEntity> GetNextActivityInstanceList(int fromActivityInstanceID)
        {
            var tim = new TransitionInstanceManager();
            return tim.GetNextActivityInstanceList(fromActivityInstanceID);
        }

        /// <summary>
        /// 获取当前等待办理节点的任务分配人列表
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <returns>执行者列表</returns>
        public IList<Performer> GetTaskPerformers(WfAppRunner runner)
        {
            var tm = new TaskManager();
            var tasks = tm.GetReadyTaskOfApp(runner).ToList();

            Performer performer;
            IList<Performer> performerList = new List<Performer>();
            foreach (var task in tasks)
            {
                performer = new Performer(task.AssignedToUserID, task.AssignedToUserName);
                performerList.Add(performer);
            }
            return performerList;
        }

        /// <summary>
        /// 创建新的委托任务
        /// </summary>
        /// <param name="entrusted">被委托任务信息</param>
        /// <param name="cancalOriginalTask">是否取消原委托任务办理</param>
        /// <returns></returns>
        public Boolean EntrustTask(TaskEntrustedEntity entrusted, bool cancalOriginalTask = true)
        {
            var tm = new TaskManager();
            return tm.Entrust(entrusted, cancalOriginalTask);
        }

        /// <summary>
        /// 获取流程当前运行节点信息
        /// </summary>
        /// <param name="runner"></param>
        /// <returns></returns>
        public ActivityInstanceEntity GetRunningNode(WfAppRunner runner)
        {
            var aim = new ActivityInstanceManager();
            var entity = aim.GetRunningNode(runner);

            return entity;
        }

        /// <summary>
        /// 判断是否是我的任务
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool IsMineTask(ActivityInstanceEntity entity, string userID)
        {
            var aim = new ActivityInstanceManager();
            bool isMine = aim.IsMineTask(entity, userID);
            return isMine;
        }

        /// <summary>
        /// 获取正在运行中的活动实例
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public IList<ActivityInstanceEntity> GetRunningActivityInstance(TaskQuery query)
        {
            var aim = new ActivityInstanceManager();
            var list = aim.GetRunningActivityInstanceList(query.AppInstanceID, query.ProcessGUID).ToList();
            return list;
        }
        #endregion

        #region 角色资源数据获取
        /// <summary>
        /// 获取所有角色数据
        /// </summary>
        /// <returns></returns>
        public IList<Role> GetRoleAll()
        {
            return ResourceService.GetRoleAll();
        }

        /// <summary>
        /// 获取流程定义文件中的角色信息
        /// </summary>
        /// <param name="processGUID">流程定义GUID</param>
        /// <param name="version">版本</param>
        /// <returns>角色列表</returns>
        public IList<Role> GetRoleByProcess(string processGUID, string version)
        {
            var processModel = ProcessModelFactory.Create(processGUID, version);
            var roleList = processModel.GetRoles();

            return roleList;
        }

        /// <summary>
        /// 获取流程文件中角色用户的列表数据
        /// </summary>
        /// <param name="processGUID">流程定义GUID</param>
        /// <param name="version">版本</param>
        /// <returns>角色列表</returns>
        public IList<Role> GetRoleUserListByProcess(string processGUID, string version)
        {
            var processModel = ProcessModelFactory.Create(processGUID, version);
            var roleList = processModel.GetRoles();
            var idsin = roleList.Select(r => r.ID).ToList().ToArray();

            var newRoleList = ResourceService.FillUsersIntoRoles(idsin);

            return newRoleList;
        }

       

        /// <summary>
        /// 根据角色获取用户列表
        /// </summary>
        /// <param name="roleID"></param>
        /// <returns></returns>
        public IList<User> GetUserListByRole(string roleID)
        {
            return ResourceService.GetUserListByRole(roleID);
        }

        /// <summary>
        /// 获取节点上的执行者列表
        /// </summary>
        /// <param name="nextNode"></param>
        /// <returns></returns>
        public PerformerList GetPerformerList(NodeView nextNode)
        {
            var performerList = PerformerBuilder.CreatePerformerList(nextNode.Roles);
            return performerList;
        }

        #endregion
    }
}
