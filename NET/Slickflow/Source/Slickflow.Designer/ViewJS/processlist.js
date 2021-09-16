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

var processlist = (function () {
	function processlist() {
	}

	processlist.pselectedProcessEntity = null;
    processlist.afterCreated = new slick.Event();
	processlist.afterOpened = new slick.Event();

	//#region Process DataGrid
	processlist.getProcessList = function () {
		$('#loading-indicator').show();

		jshelper.ajaxGet('api/Wf2Xml/GetProcessListSimple', null, function (result) {
			if (result.Status === 1) {
				var divProcessGrid = document.querySelector('#myProcessGrid');
				$(divProcessGrid).empty();

				var gridOptions = {
					columnDefs: [
						{ headerName: 'ID', field: 'ID', width: 50 },
						{ headerName: '流程GUID', field: 'ProcessGUID', width: 120 },
						{ headerName: '流程名称', field: 'ProcessName', width: 200 },
						{ headerName: '版本', field: 'Version', width: 40 },
						{ headerName: '状态', field: 'IsUsing', width: 40 },
						{ headerName: '创建日期', field: 'CreatedDateTime', width: 120 }
					],
					rowSelection: 'single',
					onSelectionChanged: onSelectionChanged,
					onRowDoubleClicked: onRowDoubleClicked
				};

				new agGrid.Grid(divProcessGrid, gridOptions);
				gridOptions.api.setRowData(result.Entity);

				$('#loading-indicator').hide();

				function onSelectionChanged() {
					var selectedRows = gridOptions.api.getSelectedRows();
					var selectedProcessID = 0;
					selectedRows.forEach(function (selectedRow, index) {
						processlist.pselectedProcessEntity = selectedRow;
					});
				}

				function onRowDoubleClicked(e, args) {
					processlist.editProcess();
				}
			}
		});
	}

	processlist.createProcess = function () {
        processlist.pselectedProcessEntity = null;

		BootstrapDialog.show({
			title: '流程新建',
            message: $('<div></div>').load('process/edit'),
            draggable: true
		});
	}

	processlist.loadProcess = function () {
		var entity = processlist.pselectedProcessEntity;
		if (entity !== null) {
			$("#txtProcessGUID").val(entity.ProcessGUID);
			$("#txtProcessName").val(entity.ProcessName);
			$("#txtVersion").val(entity.Version);
			$("#ddlIsUsing").val(entity.IsUsing);
			$("#txtDescription").val(entity.Description);
		} else {
			$("#txtProcessGUID").val("");
			$("#txtProcessName").val("");
			$("#txtVersion").val("1");
			$("#ddlIsUsing").val();
			$("#txtDescription").val("");
		}
	}

	processlist.editProcess = function () {
		var entity = processlist.pselectedProcessEntity;
		if (entity == null) {
			$.msgBox({
				title: "Designer / Process",
				content: "请先选择流程记录！",
				type: "alert"
			});
			return false;
		}

		BootstrapDialog.show({
			title: '流程编辑',
            message: $('<div></div>').load('process/edit'),
            draggable: true
		});
	}

	processlist.saveProcess = function () {
		if ($("#txtProcessName").val() == ""
			|| $("#txtVersion").val() == "") {
					$.msgBox({
						title: "Designer / Process",
						content: "请输入流程基本信息！",
						type: "alert"
					});
					return false;
				}

		var entity = {
			"ProcessGUID": $("#txtProcessGUID").val(),
			"ProcessName": $("#txtProcessName").val(),
			"Version": $("#txtVersion").val(),
			"IsUsing": $("#ddlIsUsing").val(),
			"Description": $("#txtDescription").val()
		};

		if (processlist.pselectedProcessEntity === null) {
			processapi.create(entity, function (result) {
				if (result.Status == 1) {
                    processlist.pselectedProcessEntity = result.Entity;
                    //render process into graph canvas
					if (processlist.afterCreated) {
						slick.trigger(processlist.afterCreated, {
                            "ProcessEntity": result.Entity
                        });
					}
				}
			});
		}
		else
			processapi.update(entity);
	}

	processlist.deleteProcess = function () {
		$.msgBox({
			title: "Are You Sure",
			content: "确实要删除流程定义记录吗? ",
			type: "confirm",
			buttons: [{ value: "Yes" }, { value: "Cancel" }],
			success: function (result) {
				if (result == "Yes") {
					var entity = {
						"ProcessGUID": processlist.pselectedProcessEntity.ProcessGUID,
						"Version": processlist.pselectedProcessEntity.Version
					};
					processapi.delete(entity);
					return;
				}
			}
		});
	}

	processlist.sure = function () {
		//render process into graph canvas
		if (processlist.pselectedProcessEntity !== null) {
			if (processlist.afterOpened) {
				slick.trigger(processlist.afterOpened, { 
                    "ProcessEntity": processlist.pselectedProcessEntity
                });
			}
		}
	}

	return processlist;
})()

//process api
var processapi = (function () {
	function processapi() {
	}

	processapi.create = function (entity, callback) {
		jshelper.ajaxPost('api/Wf2Xml/CreateProcess',
            JSON.stringify(entity),
            function (result) {
            	if (result.Status == 1) {
            		$.msgBox({
            			title: "Designer / Process",
            			content: "新创建流程记录成功保存！",
            			type: "info"
            		});
            	} else {
            		$.msgBox({
            			title: "Designer / Process",
            			content: result.Message,
            			type: "error",
            			buttons: [{ value: "Ok" }],
            		});
            	}

            	//execute render in processlist
            	callback(result);
            });
	}

	processapi.update = function (entity) {
		jshelper.ajaxPost('api/Wf2Xml/UpdateProcess',
            JSON.stringify(entity),
            function (result) {
            	if (result.Status == 1) {
            		$.msgBox({
            			title: "Designer / Process",
            			content: "流程记录成功保存！",
            			type: "info"
            		});
            	} else {
            		$.msgBox({
            			title: "Ooops",
            			content: result.Message,
            			type: "error",
            			buttons: [{ value: "Ok" }],
            		});
            	}
            });
	}

	processapi.delete = function (entity) {
		//delete the selected row
		jshelper.ajaxPost('api/Wf2Xml/DeleteProcess',
            JSON.stringify(entity),
            function (result) {
            	if (result.Status == 1) {
            		$.msgBox({
            			title: "Designer / Process",
            			content: "流程记录已经删除！",
            			type: "info"
            		});

            		//refresh
            		processlist.getProcessList();
            	} else {
            		$.msgBox({
            			title: "Ooops",
            			content: result.Message,
            			type: "error",
            			buttons: [{ value: "Ok" }],
            		});
            	}
            });
	}

	processapi.queryProcessFile = function (query, callback) {
		jshelper.ajaxPost('api/Wf2Xml/QueryProcessFile',
            JSON.stringify(query),
            function (result) {
            	callback(result);
            }
        );
	}

	processapi.saveProcessFile = function (entity) {
		jshelper.ajaxPost('api/Wf2Xml/SaveProcessFile', JSON.stringify(entity), function (result) {
			if (result.Status == "1") {
				$.msgBox({
					title: "Designer / Index",
					content: "流程XML内容保存成功！",
					type: "info"
				});
			} else {
				$.msgBox({
					title: "Designer / Index",
					content: "流程XML内容保存失败！错误信息：" + result.Message,
					type: "info"
				});
			}
		});
	}

	return processapi;
})()