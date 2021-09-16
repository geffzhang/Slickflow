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
using System.Threading;
using System.Data.Linq;
using System.Transactions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Slickflow.Data;
using Slickflow.Engine.Common;
using Slickflow.Engine.Utility;
using Slickflow.Engine.Core.Result;
using Slickflow.Engine.Xpdl;
using Slickflow.Engine.Xpdl.Node;
using Slickflow.Engine.Business.Entity;
using Slickflow.Engine.Business.Manager;

namespace Slickflow.Engine.Core.Runtime
{
    /// <summary>
    /// 运行时的创建类
    /// 静态方法：创建执行实例的运行者对象
    /// </summary>
    internal class WfRuntimeManagerFactory
    {
        #region WfRuntimeManager 创建启动运行时对象
        /// <summary>
        /// 启动流程
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时实例对象</returns>
        public static WfRuntimeManager CreateRuntimeInstanceStartup(WfAppRunner runner,
            ref WfExecutedResult result)
        {
            //检查流程是否可以被启动
            var rmins = new WfRuntimeManagerStartup();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            //正常流程启动
            var pim = new ProcessInstanceManager();
            ProcessInstanceEntity processInstance = pim.GetProcessInstanceCurrent(runner.AppInstanceID,
                runner.ProcessGUID);

            //不能同时启动多个主流程
            if (processInstance != null
                && processInstance.ParentProcessInstanceID == null
                && processInstance.ProcessState == (short)ProcessStateEnum.Running)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Started_IsRunningAlready;
                result.Message = "流程已经处于运行状态，如果要重新启动，请先终止当前流程实例！";
                return rmins;
            }

            rmins.AppRunner = runner;

            //获取流程第一个可办理节点
            rmins.ProcessModel = ProcessModelFactory.Create(runner.ProcessGUID, runner.Version);
            var startActivity = rmins.ProcessModel.GetStartActivity();
            var firstActivity = rmins.ProcessModel.GetFirstActivity();

            rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(firstActivity.ActivityGUID,
                runner.UserID,
                runner.UserName);
            rmins.ActivityResource = new ActivityResource(runner, rmins.AppRunner.NextActivityPerformers);

            return rmins;
        }

        /// <summary>
        /// 子流程启动
        /// </summary>
        /// <param name="runner">运行者</param>
        /// <param name="parentProcessInstance">父流程</param>
        /// <param name="subProcessNode">子流程节点</param>
        /// <param name="performerList">执行者列表</param>
        /// <param name="result">运行结果</param>
        /// <returns>运行时管理器</returns>
        public static WfRuntimeManager CreateRuntimeInstanceStartupSub(WfAppRunner runner,
            ProcessInstanceEntity parentProcessInstance,
            SubProcessNode subProcessNode,
            PerformerList performerList,
            ref WfExecutedResult result)
        {
            //检查流程是否可以被启动
            var rmins = new WfRuntimeManagerStartupSub();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            var pim = new ProcessInstanceManager();
            ProcessInstanceEntity processInstance = pim.GetProcessInstanceCurrent(runner.AppInstanceID,
                    subProcessNode.SubProcessGUID);

            //不能同时启动多个主流程
            if (processInstance != null
                && processInstance.ParentProcessInstanceID == null
                && processInstance.ProcessState == (short)ProcessStateEnum.Running)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Started_IsRunningAlready;
                result.Message = "流程已经处于运行状态，如果要重新启动，请先终止当前流程实例！";
                return rmins;
            }

            //processInstance 为空，此时继续执行启动操作
            rmins.AppRunner = runner;
            rmins.ParentProcessInstance = parentProcessInstance;
            rmins.InvokedSubProcessNode = subProcessNode;

            //获取流程第一个可办理节点
            rmins.ProcessModel = ProcessModelFactory.Create(runner.ProcessGUID, runner.Version);
            var startActivity = rmins.ProcessModel.GetStartActivity();
            var firstActivity = rmins.ProcessModel.GetFirstActivity();

            //子流程自动获取第一个办理节点上的人员列表
            rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(firstActivity.ActivityGUID, 
                performerList);
            rmins.ActivityResource = new ActivityResource(runner, rmins.AppRunner.NextActivityPerformers);

            return rmins;
        }
        #endregion

        #region WfRuntimeManager 创建应用执行运行时对象
        /// <summary>
        /// 创建运行时实例对象
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时实例对象</returns>
        public static WfRuntimeManager CreateRuntimeInstanceAppRunning(WfAppRunner runner,
            ref WfExecutedResult result)
        {
            //检查传人参数是否有效
            var rmins = new WfRuntimeManagerAppRunning();
            rmins.WfExecutedResult = result = new WfExecutedResult();
            if (string.IsNullOrEmpty(runner.AppName)
                || String.IsNullOrEmpty(runner.AppInstanceID)
                || runner.ProcessGUID == null)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.RunApp_ErrorArguments;
                result.Message = "方法参数错误，无法运行流程！";
                return rmins;
            }

            //传递runner变量
            rmins.AppRunner = runner;

            var aim = new ActivityInstanceManager();
            TaskViewEntity taskView = null;
            var runningNode = aim.GetRunningNode(runner, out taskView);

            //判断是否是当前登录用户的任务
            if (runningNode.AssignedToUserIDs.Contains(runner.UserID.ToString()) == false)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.RunApp_HasNoTask;
                result.Message = "当前没有登录用户要办理的任务，无法运行流程！";
                return rmins;
            }

            var processModel = ProcessModelFactory.Create(taskView.ProcessGUID, taskView.Version);
            var activityResource = new ActivityResource(runner,
                runner.NextActivityPerformers,
                runner.Conditions,
                runner.DynamicVariables);

            rmins.TaskView = taskView;
            rmins.RunningActivityInstance = runningNode;
            rmins.ProcessModel = processModel;
            rmins.ActivityResource = activityResource;

            return rmins;
        }
        #endregion

        #region WfRuntimeManager 创建跳转运行时对象
        /// <summary>
        /// 创建跳转实例信息
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时实例对象</returns>
        public static WfRuntimeManager CreateRuntimeInstanceJump(WfAppRunner runner,
            ref WfExecutedResult result)
        {
            var rmins = new WfRuntimeManagerJump();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            if (string.IsNullOrEmpty(runner.AppName)
               || String.IsNullOrEmpty(runner.AppInstanceID)
               || runner.ProcessGUID == null
               || runner.NextActivityPerformers == null)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Jump_ErrorArguments;
                result.Message = "方法参数错误，无法运行流程！";
                return rmins;
            }

            //流程跳转时，只能跳转到一个节点
            if (runner.NextActivityPerformers.Count() > 1)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Jump_OverOneStep;
                result.Message = string.Format("不能跳转到多个节点！节点数:{0}",
                    runner.NextActivityPerformers.Count());
                return rmins;
            }

            //获取当前运行节点信息
            var aim = new ActivityInstanceManager();
            TaskViewEntity taskView = null;
            var runningNode = aim.GetRunningNode(runner, out taskView);

            //传递runner变量
            rmins.TaskView = taskView;
            rmins.AppRunner = runner;
            rmins.AppRunner.AppName = runner.AppName;
            rmins.AppRunner.AppInstanceID = runner.AppInstanceID;
            rmins.AppRunner.ProcessGUID = runner.ProcessGUID;
            rmins.AppRunner.UserID = runner.UserID;
            rmins.AppRunner.UserName = runner.UserName;

            var processModel = ProcessModelFactory.Create(taskView.ProcessGUID, taskView.Version);
            rmins.ProcessModel = processModel;

            #region 不考虑回跳方式
            ////获取跳转节点信息
            //var jumpActivityGUID = runner.NextActivityPerformers.First().Key;
            //var jumpActivityInstanceList = aim.GetActivityInstance(runner.AppInstanceID, runner.ProcessGUID, jumpActivityGUID);

            //if (jumpActivityInstanceList != null
            //    && jumpActivityInstanceList.Count > 0)
            //{
            //    //跳转到曾经执行过的节点上,可以作为跳回方式处理
            //    rmins.IsBackward = true;
            //    rmins.BackwardContext.ProcessInstance = (new ProcessInstanceManager()).GetById(runningNode.ProcessInstanceID);
            //    rmins.BackwardContext.BackwardToTaskActivity = processModel.GetActivity(jumpActivityGUID);

            //    //获取当前运行节点的上一步节点
            //    bool hasGatewayNode = false;
            //    var tim = new TransitionInstanceManager();
            //    var lastTaskTransitionInstance = tim.GetLastTaskTransition(runner.AppName,
            //        runner.AppInstanceID, runner.ProcessGUID);
            //    var previousActivityInstance = tim.GetPreviousActivityInstance(runningNode, true,
            //        out hasGatewayNode).ToList()[0];

            //    //仅仅是回跳到上一步节点，即按SendBack方式处理
            //    if (previousActivityInstance.ActivityGUID == jumpActivityGUID)
            //    {
            //        rmins.BackwardContext.BackwardToTaskActivityInstance = previousActivityInstance;
            //        rmins.BackwardContext.BackwardToTargetTransitionGUID =
            //            hasGatewayNode == false ? lastTaskTransitionInstance.TransitionGUID : System.Guid.Empty;        //如果中间有Gateway节点，则没有直接相连的TransitonGUID

            //        rmins.BackwardContext.BackwardFromActivity = processModel.GetActivity(runningNode.ActivityGUID);
            //        rmins.BackwardContext.BackwardFromActivityInstance = runningNode;
            //        rmins.BackwardContext.BackwardTaskReciever = WfBackwardTaskReciever.Instance(
            //            previousActivityInstance.ActivityName,
            //            previousActivityInstance.EndedByUserID.Value,
            //            previousActivityInstance.EndedByUserName);

            //        rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(
            //            previousActivityInstance.ActivityGUID,
            //            previousActivityInstance.EndedByUserID.Value,
            //            previousActivityInstance.EndedByUserName);
            //    }
            //    else
            //    {
            //        //回跳到早前节点
            //        var jumptoActivityInstance = jumpActivityInstanceList[0];
            //        if (jumptoActivityInstance.ActivityState != (short)ActivityStateEnum.Completed)
            //        {
            //            result.Status = WfExecutedStatus.Exception;
            //            result.Exception = WfJumpException.NotActivityBackCompleted;
            //            result.Message = string.Format("回跳到的节点不在完成状态，无法重新回跳！");

            //            return rmins;
            //        }

            //        rmins.BackwardContext.BackwardToTaskActivityInstance = jumptoActivityInstance;

            //        //判断两个节点是否有Transition的定义存在
            //        var transition = processModel.GetForwardTransition(runningNode.ActivityGUID, runner.JumpbackActivityGUID.Value);
            //        rmins.BackwardContext.BackwardToTargetTransitionGUID = transition != null ? transition.TransitionGUID : System.Guid.Empty;

            //        rmins.BackwardContext.BackwardFromActivity = processModel.GetActivity(runningNode.ActivityGUID);
            //        rmins.BackwardContext.BackwardFromActivityInstance = runningNode;
            //        rmins.BackwardContext.BackwardTaskReciever = WfBackwardTaskReciever.Instance(
            //            jumptoActivityInstance.ActivityName,
            //            jumptoActivityInstance.EndedByUserID.Value,
            //            jumptoActivityInstance.EndedByUserName);

            //        rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(
            //            jumptoActivityInstance.ActivityGUID,
            //            jumptoActivityInstance.EndedByUserID.Value,
            //            jumptoActivityInstance.EndedByUserName);
            //    }
            //    //获取资源数据
            //    var activityResourceBack = new ActivityResource(rmins.AppRunner, 
            //        rmins.AppRunner.NextActivityPerformers, 
            //        runner.Conditions);
            //    rmins.ActivityResource = activityResourceBack;
            //}
            //else
            //{
            //    //跳转到从未执行过的节点上
            //    var activityResource = new ActivityResource(runner, runner.NextActivityPerformers, runner.Conditions);
            //    rmins.ActivityResource = activityResource;
            //    rmins.RunningActivityInstance = runningNode;
            //}
            #endregion

            //跳转到从未执行过的节点上
            var activityResource = new ActivityResource(runner, runner.NextActivityPerformers, runner.Conditions);
            rmins.ActivityResource = activityResource;
            rmins.RunningActivityInstance = runningNode;

            return rmins;
        }
        #endregion

        #region WfRuntimeManager 创建撤销运行时对象
        /// <summary>
        /// 撤销操作
        /// 包括：
        /// 1) 正常流转
        /// 2) 多实例节点流转
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时实例对象</returns>
        internal static WfRuntimeManager CreateRuntimeInstanceWithdraw(WfAppRunner runner,
            ref WfExecutedResult result)
        {
            WfRuntimeManager rmins = new WfRuntimeManagerWithdraw();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            var aim = new ActivityInstanceManager();
            var runningActivityInstanceList = aim.GetRunningActivityInstanceList(runner.AppInstanceID, runner.ProcessGUID).ToList();

            WithdrawOperationTypeEnum withdrawOperation = WithdrawOperationTypeEnum.Default;

            //当前没有运行状态的节点存在，流程不存在，或者已经结束或取消
            if (runningActivityInstanceList == null || runningActivityInstanceList.Count() == 0)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Withdraw_NotInReady;
                result.Message = "当前没有运行状态的节点存在，流程不存在，或者已经结束或取消";

                return rmins;
            }

            if (runningActivityInstanceList.Count() == 1)      //如果只有1个运行节点
            {
                //先判断节点的状态是否是有效状态
                var runningNode = runningActivityInstanceList[0];
                if (runningNode.ActivityState != (short)ActivityStateEnum.Ready
                    && runningNode.ActivityState != (short)ActivityStateEnum.Suspended)        //只有准备或挂起状态的节点可以撤销
                {
                    result.Status = WfExecutedStatus.Exception;
                    result.ExceptionType = WfExceptionType.Withdraw_NotInReady;
                    result.Message = string.Format("无法撤销到上一步，因为要撤销的节点为空，或不在【待办/挂起】状态，当前状态: {0}",
                        runningNode.ActivityState);//，节点状态：{0}    runningNode.ActivityState     为空报错20150514

                    return rmins;
                } 
                //当前运行节点是普通节点模式
                withdrawOperation = WithdrawOperationTypeEnum.Normal;
            }

            //根据不同分支场景，创建不同撤销运行时管理器
            return CreateRuntimeInstanceWithdrawByCase(runningActivityInstanceList, withdrawOperation, runner, ref result);
        }

        /// <summary>
        /// 根据不同撤销场景创建运行时管理器
        /// </summary>
        /// <param name="runningActivityInstanceList">运行节点列表</param>
        /// <param name="withdrawOperation">撤销类型</param>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时管理器</returns>
        private static WfRuntimeManager CreateRuntimeInstanceWithdrawByCase(
            List<ActivityInstanceEntity> runningActivityInstanceList,
            WithdrawOperationTypeEnum withdrawOperation,
            WfAppRunner runner,
            ref WfExecutedResult result)
        {
            WfRuntimeManager rmins = new WfRuntimeManagerWithdraw();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            //根据当前运行节点获取
            ActivityInstanceEntity runningNode = runningActivityInstanceList[0];
            ProcessInstanceEntity processInstance = (new ProcessInstanceManager()).GetById(runningNode.ProcessInstanceID);
            IProcessModel processModel = ProcessModelFactory.Create(processInstance.ProcessGUID, processInstance.Version);

            //不同撤销的分支场景处理
            var aim = new ActivityInstanceManager();

            //以下处理，需要知道上一步是独立节点的信息
            //获取上一步流转节点信息，可能经过And, Or等路由节点
            var tim = new TransitionInstanceManager();
            bool hasGatewayNode = false;
            var currentNode = runningNode;

            if (runningNode.MIHostActivityInstanceID != null)
            {
                //如果当前运行节点是多实例子节点，则需要找到它的主节点的Transiton记录
                currentNode = aim.GetById(runningNode.MIHostActivityInstanceID.Value);
            }
            var lastActivityInstanceList = tim.GetPreviousActivityInstanceList(currentNode, false, out hasGatewayNode).ToList();

            if (lastActivityInstanceList == null || lastActivityInstanceList.Count > 1)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Withdraw_HasTooMany;
                result.Message = "当前没有可以撤销回去的节点，或者有多个可以撤销回去的节点，无法选择！";

                return rmins;
            }

            TransitionInstanceEntity lastTaskTransitionInstance = null;
            if (hasGatewayNode == false)
            {
                lastTaskTransitionInstance = tim.GetLastTaskTransition(runner.AppName,
                    runner.AppInstanceID, runner.ProcessGUID);
                if (lastTaskTransitionInstance.TransitionType == (short)TransitionTypeEnum.Loop)
                {
                    result.Status = WfExecutedStatus.Exception;
                    result.ExceptionType = WfExceptionType.Withdraw_IsLoopNode;
                    result.Message = "当前流转是自循环，无需撤销！";

                    return rmins;
                }
            }

            var previousActivityInstance = lastActivityInstanceList[0];
            if (previousActivityInstance.EndedByUserID != runner.UserID)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Withdraw_NotCreatedByMine;
                result.Message = string.Format("上一步节点的任务办理人跟当前登录用户不一致，无法撤销回上一步！节点办理人：{0}",
                    previousActivityInstance.EndedByUserName);

                return rmins;
            }

            if (previousActivityInstance.ActivityType == (short)ActivityTypeEnum.EndNode)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Withdraw_PreviousIsEndNode;
                result.Message = "上一步是结束节点，无法撤销！";

                return rmins;
            }

            //当前运行节点是普通节点
            if (withdrawOperation == WithdrawOperationTypeEnum.Normal)
            {
                    //简单串行模式下的退回
                    rmins = new WfRuntimeManagerWithdraw();
                    rmins.WfExecutedResult = result = new WfExecutedResult();

                    rmins.ProcessModel = processModel;
                    rmins.AppRunner.ProcessGUID = runner.ProcessGUID;
                    rmins.BackwardContext.ProcessInstance = processInstance;
                    rmins.BackwardContext.BackwardToTargetTransitionGUID =
                        hasGatewayNode == false ? lastTaskTransitionInstance.TransitionGUID : String.Empty;
                    rmins.BackwardContext.BackwardToTaskActivity = processModel.GetActivity(previousActivityInstance.ActivityGUID);
                    rmins.BackwardContext.BackwardToTaskActivityInstance = previousActivityInstance;
                    rmins.BackwardContext.BackwardFromActivity = processModel.GetActivity(runningNode.ActivityGUID);
                    rmins.BackwardContext.BackwardFromActivityInstance = runningNode; //准备状态的接收节点
                    rmins.BackwardContext.BackwardTaskReceiver = WfBackwardTaskReceiver.Instance(
                        previousActivityInstance.ActivityName,
                        previousActivityInstance.EndedByUserID,
                        previousActivityInstance.EndedByUserName);

                    //封装AppUser对象
                    rmins.AppRunner.AppName = runner.AppName;
                    rmins.AppRunner.AppInstanceID = runner.AppInstanceID;
                    rmins.AppRunner.UserID = runner.UserID;
                    rmins.AppRunner.UserName = runner.UserName;
                    rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(
                        previousActivityInstance.ActivityGUID,
                        runner.UserID,
                        runner.UserName);
                    rmins.ActivityResource = new ActivityResource(runner, rmins.AppRunner.NextActivityPerformers);

                    return rmins;
            }

            //如果有其它模式，没有处理到，则直接抛出异常
            throw new WorkflowException("未知的撤销场景，请报告给技术支持人员！");
        }
        #endregion

        #region WfRuntimeManager 创建退回运行时对象
        /// <summary>
        /// 退回操作
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时实例对象</returns>
        internal static WfRuntimeManager CreateRuntimeInstanceSendBack(WfAppRunner runner,
            ref WfExecutedResult result)
        {
            var rmins = new WfRuntimeManagerSendBack();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            var sendbackOperation = SendbackOperationTypeEnum.Default;

            //先查找当前用户正在办理的运行节点
            var aim = new ActivityInstanceManager();
            var runningNode = runner.TaskID != null ? aim.GetByTask(runner.TaskID.Value) 
                : aim.GetRunningNode(runner);

            if (runningNode == null)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Sendback_NotInRunning;
                result.Message = "当前没有运行状态的节点存在，流程不存在，或者已经结束或取消";

                return rmins;
            }

            if (aim.IsMineTask(runningNode, runner.UserID) == false)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Sendback_NotMineTask;
                result.Message = "不是当前登录用户的任务，无法退回！";
                return rmins;
            }

                var activityType = EnumHelper.ParseEnum<ActivityTypeEnum>(runningNode.ActivityType.ToString());
                if (XPDLHelper.IsSimpleComponentNode(activityType) == false)
                {
                    result.Status = WfExecutedStatus.Exception;
                    result.ExceptionType = WfExceptionType.Sendback_NotTaskNode;
                    result.Message = "当前节点不是任务类型的节点，无法退回上一步节点！";
                    return rmins;
                }

            //获取当前运行主节点信息
            var currentNode = runningNode;
            if (runningNode.MIHostActivityInstanceID != null)
            {
                currentNode = aim.GetById(runningNode.MIHostActivityInstanceID.Value);
            }

            //获取上一步流转节点信息，可能经过And, Or等路由节点
            //判断前置步骤是否经过Gateway节点
            var hasGatewayPassed = false;
            var processInstance = (new ProcessInstanceManager()).GetById(runningNode.ProcessInstanceID);
            var processModel = ProcessModelFactory.Create(processInstance.ProcessGUID, processInstance.Version);
            var previousActivityList = aim.GetPreviousActivityList(runningNode, processModel, out hasGatewayPassed);

            if (previousActivityList == null
                || previousActivityList.Count == 0)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Sendback_IsNull;
                result.Message = "当前没有可以退回的节点，请检查流程数据！";

                return null;
            }

            //多个退回节点存在的有效退回判断
            IList<ActivityEntity> sendbackPreviousActivityList = new List<ActivityEntity>();
            if (previousActivityList.Count > 1)
            {
                if (runner.NextActivityPerformers == null
                        || runner.NextActivityPerformers.Count == 0)
                {
                    result.Status = WfExecutedStatus.Exception;
                    result.ExceptionType = WfExceptionType.Sendback_IsTooManyPrevious;
                    result.Message = "当前有多个可以退回的节点，无法选择，请明确指定！";

                    return rmins;
                }

                //明确指定的退回节点，需要判断处理
                var isInvalidSendBack = IsInvalidStepsInPrevousActivityList(previousActivityList, 
                    runner.NextActivityPerformers, 
                    sendbackPreviousActivityList);

                if (isInvalidSendBack == true)
                {
                    result.Status = WfExecutedStatus.Exception;
                    result.ExceptionType = WfExceptionType.Sendback_NotContainedInPreviousOrStartNode;
                    result.Message = "指定的退回节点不在上一步节点运行列表中，或者上一步是开始节点，无法退回！";

                    return rmins;
                }
            }
            else
            {
                //只有一个要退回去的节点
                sendbackPreviousActivityList.Add(previousActivityList[0]);
            }

            //判断当前节点是否是多实例节点
            if (runningNode.MIHostActivityInstanceID != null)
            {
                if (runningNode.CompleteOrder == 1)
                {
                    //只有串行模式下有CompleteOrder的值为 1
                    //串行模式多实例的第一个执行节点，此时可退回到上一步
                    sendbackOperation = SendbackOperationTypeEnum.MISFirstOneIsRunning;
                }
                else if (runningNode.CompleteOrder > 1)
                {
                    //已经是中间节点，只能退回到上一步多实例子节点
                    sendbackOperation = SendbackOperationTypeEnum.MISOneIsRunning;
                }
                else if (runningNode.CompleteOrder == -1)
                {
                    sendbackOperation = SendbackOperationTypeEnum.MIPOneIsRunning;
                }
            }

            if (hasGatewayPassed == true)
                sendbackOperation = SendbackOperationTypeEnum.GatewayFollowedByParalleledNodes;
            else
                sendbackOperation = SendbackOperationTypeEnum.Normal;

            //根据不同分支场景，创建不同撤销运行时管理器
            return CreateRuntimeInstanceSendbackByCase(runningNode, processModel, processInstance, 
                sendbackPreviousActivityList, hasGatewayPassed, sendbackOperation, runner, ref result);
        }

        private static WfRuntimeManager CreateRuntimeInstanceSendbackByCase(ActivityInstanceEntity runningNode,
            IProcessModel processModel,
            ProcessInstanceEntity processInstance,
            IList<ActivityEntity> sendbackPreviousActivityList,
            Boolean hasGatewayPassed,
            SendbackOperationTypeEnum sendbackOperation,
            WfAppRunner runner,
            ref WfExecutedResult result)
        {
            WfRuntimeManager rmins = new WfRuntimeManagerSendBack();
            rmins.WfExecutedResult = result = new WfExecutedResult();

            var aim = new ActivityInstanceManager();


            //以下处理，需要知道上一步是独立节点的信息
            var tim = new TransitionInstanceManager();
            TransitionInstanceEntity lastTaskTransitionInstance = tim.GetLastTaskTransition(runner.AppName,
            runner.AppInstanceID, runner.ProcessGUID);

            if (lastTaskTransitionInstance.TransitionType == (short)TransitionTypeEnum.Loop)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Sendback_IsLoopNode;
                result.Message = "当前流转是自循环，无需退回！";

                return rmins;
            }

            //设置退回节点的相关信息
            var previousActivityGUID = sendbackPreviousActivityList[0].ActivityGUID;
            var previousActivityInstance = aim.GetActivityInstanceLatest(runner.AppInstanceID, runner.ProcessGUID, previousActivityGUID);

            //普通模式的退回
            if (sendbackOperation == SendbackOperationTypeEnum.Normal)
            {
                    //简单串行模式下的退回
                    rmins = new WfRuntimeManagerSendBack();
                    rmins.WfExecutedResult = result = new WfExecutedResult();

                    rmins.ProcessModel = processModel;
                    rmins.BackwardContext.ProcessInstance = processInstance;
                    rmins.BackwardContext.BackwardToTaskActivity = processModel.GetActivity(previousActivityInstance.ActivityGUID);
                    rmins.BackwardContext.BackwardToTaskActivityInstance = previousActivityInstance;
                    rmins.BackwardContext.BackwardToTargetTransitionGUID =
                        hasGatewayPassed == false ? lastTaskTransitionInstance.TransitionGUID : WfDefine.WF_XPDL_GATEWAY_BYPASS_GUID;        //如果中间有Gateway节点，则没有直接相连的TransitonGUID

                    rmins.BackwardContext.BackwardFromActivity = processModel.GetActivity(runningNode.ActivityGUID);
                    rmins.BackwardContext.BackwardFromActivityInstance = runningNode;
                    rmins.BackwardContext.BackwardTaskReceiver = WfBackwardTaskReceiver.Instance(previousActivityInstance.ActivityName,
                        previousActivityInstance.EndedByUserID, previousActivityInstance.EndedByUserName);

                    //封装AppUser对象
                    rmins.AppRunner.AppName = runner.AppName;
                    rmins.AppRunner.AppInstanceID = runner.AppInstanceID;
                    rmins.AppRunner.ProcessGUID = runner.ProcessGUID;
                    rmins.AppRunner.UserID = runner.UserID;
                    rmins.AppRunner.UserName = runner.UserName;
                    rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(previousActivityInstance.ActivityGUID,
                        previousActivityInstance.EndedByUserID,
                        previousActivityInstance.EndedByUserName);
                    rmins.ActivityResource = new ActivityResource(runner, rmins.AppRunner.NextActivityPerformers);

                    return rmins;
            }

            //如果有其它模式，没有处理到，则直接抛出异常
            throw new WorkflowException("未知的退回场景，请报告给技术支持人员！");
        }

        /// <summary>
        /// 判断传递的步骤是否在列表中
        /// </summary>
        /// <param name="previousActivityList">步骤列表</param>
        /// <param name="steps">要检查的步骤</param>
        /// <param name="sendbackPreviousActivityList">要退回的节点列表</param>
        /// <returns>是否没有包含</returns>
        private static Boolean IsInvalidStepsInPrevousActivityList(IList<ActivityEntity> previousActivityList, 
            IDictionary<string, PerformerList> steps,
            IList<ActivityEntity> sendbackPreviousActivityList)
        {
            var isInvalid = false;
            foreach (var key in steps.Keys)
            {
                var activity = previousActivityList.Single(s => s.ActivityGUID == key);
                if (activity == null)
                {
                    isInvalid = true;
                    break;
                }
                else if (activity.ActivityType == ActivityTypeEnum.StartNode)
                {
                    isInvalid = true;
                    break;
                }
                else
                {
                    sendbackPreviousActivityList.Add(activity);
                }
            }
            return isInvalid;
        }
        #endregion

        #region WfRuntimeManager 创建返签运行时对象
        /// <summary>
        /// 流程返签，先检查约束条件，然后调用wfruntimeinstance执行
        /// </summary>
        /// <param name="runner">执行者</param>
        /// <param name="result">结果对象</param>
        /// <returns>运行时实例对象</returns>
        public static WfRuntimeManager CreateRuntimeInstanceReverse(WfAppRunner runner,
            ref WfExecutedResult result)
        {
            var rmins = new WfRuntimeManagerReverse();
            rmins.WfExecutedResult = result = new WfExecutedResult();
            var pim = new ProcessInstanceManager();
            var processInstance = pim.GetProcessInstanceLatest(runner.AppInstanceID, runner.ProcessGUID);
            if (processInstance == null || processInstance.ProcessState != (short)ProcessStateEnum.Completed)
            {
                result.Status = WfExecutedStatus.Exception;
                result.ExceptionType = WfExceptionType.Reverse_NotInCompleted;
                result.Message = string.Format("当前应用:{0}，实例ID：{1}, 没有完成的流程实例，无法让流程重新运行！",
                    runner.AppName, runner.AppInstanceID);
                return rmins;
            }

            var tim = new TransitionInstanceManager();
            var endTransitionInstance = tim.GetEndTransition(runner.AppName, runner.AppInstanceID, runner.ProcessGUID);

            var processModel = ProcessModelFactory.Create(processInstance.ProcessGUID, processInstance.Version);
            var endActivity = processModel.GetActivity(endTransitionInstance.ToActivityGUID);

            var aim = new ActivityInstanceManager();
            var endActivityInstance = aim.GetById(endTransitionInstance.ToActivityInstanceID);

            bool hasGatewayNode = false;
            var lastTaskActivityInstance = tim.GetPreviousActivityInstanceList(endActivityInstance, false,
                out hasGatewayNode).ToList()[0];
            var lastTaskActivity = processModel.GetActivity(lastTaskActivityInstance.ActivityGUID);

            //封装返签结束点之前办理节点的任务接收人
            rmins.AppRunner.NextActivityPerformers = ActivityResource.CreateNextActivityPerformers(lastTaskActivityInstance.ActivityGUID,
                lastTaskActivityInstance.EndedByUserID,
                lastTaskActivityInstance.EndedByUserName);

            rmins.ActivityResource = new ActivityResource(runner, rmins.AppRunner.NextActivityPerformers);
            rmins.AppRunner.AppName = runner.AppName;
            rmins.AppRunner.AppInstanceID = runner.AppInstanceID;
            rmins.AppRunner.ProcessGUID = runner.ProcessGUID;
            rmins.AppRunner.UserID = runner.UserID;
            rmins.AppRunner.UserName = runner.UserName;

            rmins.BackwardContext.ProcessInstance = processInstance;
            rmins.BackwardContext.BackwardToTaskActivity = lastTaskActivity;
            rmins.BackwardContext.BackwardToTaskActivityInstance = lastTaskActivityInstance;
            rmins.BackwardContext.BackwardToTargetTransitionGUID =
                hasGatewayNode == false ? endTransitionInstance.TransitionGUID : String.Empty;
            rmins.BackwardContext.BackwardFromActivity = endActivity;
            rmins.BackwardContext.BackwardFromActivityInstance = endActivityInstance;
            rmins.BackwardContext.BackwardTaskReceiver = WfBackwardTaskReceiver.Instance(lastTaskActivityInstance.ActivityName,
                lastTaskActivityInstance.EndedByUserID,
                lastTaskActivityInstance.EndedByUserName);

            return rmins;
        }
        #endregion
    }
}
