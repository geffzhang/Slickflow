﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slickflow.Engine.Xpdl.Schedule
{
    /// <summary>
    /// 中间事件节点下一步调度
    /// </summary>
    internal class NextActivityScheduleIntermediate : NextActivityScheduleBase
    {
        internal NextActivityScheduleIntermediate(IProcessModel processModel) :
            base(processModel)
        {

        }

        /// <summary>
        /// 获取下一步节点列表
        /// </summary>
        /// <param name="fromTransition">起始转移</param>
        /// <param name="currentGatewayActivity">当前节点</param>
        /// <param name="conditionKeyValuePair">条件对</param>
        /// <param name="resultType">结果类型</param>
        /// <returns>下一步组件类型</returns>
        internal override NextActivityComponent GetNextActivityListFromGateway(TransitionEntity fromTransition,
            ActivityEntity currentGatewayActivity, 
            IDictionary<string, string> conditionKeyValuePair, 
            out NextActivityMatchedType resultType)
        {
            NextActivityComponent child = null;
            NextActivityComponent gatewayComponent = null;
            resultType = NextActivityMatchedType.Unknown;

            var transitionList = this.ProcessModel.GetForwardTransitionList(currentGatewayActivity.ActivityGUID).ToList();
            foreach (TransitionEntity transition in transitionList)
            {
                bool isValidTransition = base.ProcessModel.IsValidTransition(transition, conditionKeyValuePair);
                if (isValidTransition)
                {
                    child = GetNextActivityListFromGatewayCore(transition,
                        conditionKeyValuePair,
                        out resultType);

                    gatewayComponent = AddChildToGatewayComponent(fromTransition, currentGatewayActivity, gatewayComponent, child);
                    //退出循环
                    break;
                }
            }

            if (gatewayComponent == null)
            {
                resultType = NextActivityMatchedType.NoneTransitionFilteredByCondition;
            }
            return gatewayComponent;
        }
    }
}
