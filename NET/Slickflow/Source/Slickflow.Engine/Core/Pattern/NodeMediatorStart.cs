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
using System.Reflection;
using System.Diagnostics;
using Slickflow.Engine.Common;
using Slickflow.Engine.Utility;
using Slickflow.Data;
using Slickflow.Engine.Xpdl;
using Slickflow.Engine.Xpdl.Node;
using Slickflow.Engine.Business.Entity;
using Slickflow.Engine.Business.Manager;

namespace Slickflow.Engine.Core.Pattern
{
    /// <summary>
    /// 开始节点执行器
    /// </summary>
    internal class NodeMediatorStart : NodeMediator
    {
        internal NodeMediatorStart(ActivityForwardContext forwardContext, IDbSession session)
            : base(forwardContext, session)
        {
            
        }

        /// <summary>
        /// 执行开始节点
        /// </summary>
        internal override void ExecuteWorkItem()
        {
            try
            {
                //写入流程实例
                ProcessInstanceManager pim = new ProcessInstanceManager();
                var newID = pim.Insert(this.Session.Connection, ActivityForwardContext.ProcessInstance,
                    this.Session.Transaction);

                ActivityForwardContext.ProcessInstance.ID = newID;
                
                CompleteAutomaticlly(ActivityForwardContext.ProcessInstance,
                    ActivityForwardContext.ActivityResource,
                    this.Session);

                //执行开始节点之后的节点集合
                ContinueForwardCurrentNode(false);
            }
            catch (System.Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 执行外部操作的方法
        /// </summary>
        /// <param name="actionList"></param>
        /// <param name="actionMethodParameters"></param>
        public void ExecteActionList(IList<ActionEntity> actionList, IDictionary<string, ActionParameterInternal> actionMethodParameters)
        {
            if (actionList != null && actionList.Count > 0)
            {
                var actionExecutor = new ActionExecutor();
                actionExecutor.ExecteActionList(actionList, actionMethodParameters);
            }
        }

        /// <summary>
        /// 置开始节点为结束状态
        /// </summary>
        /// <param name="processInstance"></param>
        /// <param name="activityResource"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        private GatewayExecutedResult CompleteAutomaticlly(ProcessInstanceEntity processInstance,
            ActivityResource activityResource,
            IDbSession session)
        {
            //开始节点没前驱信息
            var fromActivityInstance = base.CreateActivityInstanceObject(base.Linker.FromActivity, processInstance, activityResource.AppRunner);

            base.ActivityInstanceManager.Insert(fromActivityInstance, session);

            base.ActivityInstanceManager.Complete(fromActivityInstance.ID,
                activityResource.AppRunner,
                session);

            fromActivityInstance.ActivityState = (short)ActivityStateEnum.Completed;
            base.Linker.FromActivityInstance = fromActivityInstance;

            GatewayExecutedResult result = GatewayExecutedResult.CreateGatewayExecutedResult(GatewayExecutedStatus.Successed);
            return result;
        }
    }
}
