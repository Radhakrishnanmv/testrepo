using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;
using System.Threading;
using System.Web;
using System.Collections;
using System.Linq;
using System.Data;

namespace IGEventHandlers
{
    public class ProcessTeamMembers : SPItemEventReceiver
    {
        private delegate bool AsyncSendEmail(String webUrl, String ActionName, String PhaseName,
            String DefaultToAddress, String DefaultCCAddress);
        public static object oLock = new object();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessTeamMembers ItemAdded method starts");
            lock (oLock)
            {
                SPWeb sbIdeation = null;
                long ideaID = -1;
                string phaseID = string.Empty;
                string PhaseName = string.Empty;
                SPFieldUserValue fld = null;

                try
                {
                    if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.TEAM_MEMBERS)
                    {
                        UpdateTeamMemberPermission(properties, true);

                        using (SPWeb spWeb = properties.OpenWeb())
                        {                            
                            UpdateTaskUserOnAdding(properties, fld);

                            //if manually added
                            if (!SharepointUtil.RunWithHandler)
                            {

                                // add tasks and set permissions for manually added team member

                                if (properties.ListItem["Phase"] != null)
                                {
                                    PhaseName = properties.ListItem["Phase"].ToString().Split('#')[1];
                                    phaseID = SharepointUtil.GetLookupValue(properties.ListItem["Phase"], true);
                                }

                                SPSecurity.RunWithElevatedPrivileges(delegate()
                                {
                                    using (SPSite iSite = new SPSite(properties.WebUrl))
                                    {
                                        using (SPWeb iWeb = iSite.OpenWeb())
                                        {
                                            //get ideation web
                                            sbIdeation = iWeb.ParentWeb;

                                            SPListItem item = iWeb.Lists[properties.ListTitle].GetItemById(properties.ListItemId);
                                            SPList lstTasks = iWeb.Lists.TryGetList(IdeationConstant.IdeaSiteListNames.IDEA_TASKS);
                                            Log.LogMessage("IdeaTasks List:" + lstTasks.Title);
                                            SharepointUtil.RunWithHandler = true;
                                            try
                                            {
                                                SPListItemCollection tasks = CommonFunctions.GetTaskForTeamMember(iWeb, item);

                                                if (tasks != null && tasks.Count > 0)
                                                {
                                                    Log.LogMessage("Tasks ListItem Coll not null");
                                                    foreach (SPListItem task in tasks)
                                                    {


                                                        bool isExist = CommonFunctions.IsDuplicateTask(iWeb, task, item);

                                                        if (!isExist)
                                                        {
                                                            bool canProcess = true;
                                                            //check if dependent task has multiple predecessors associted
                                                            SPFieldLookupValueCollection collPredcessors = (SPFieldLookupValueCollection)task[IdeationConstant.ListColumns.Predecessors];

                                                            if (collPredcessors != null && collPredcessors.Count > 0)
                                                            {
                                                                //check if all predecessors tasks are completed 
                                                                foreach (SPFieldLookupValue fldTask in collPredcessors)
                                                                {
                                                                    //check all predecessors except current one 

                                                                    StringBuilder strbParentTasks = new StringBuilder();
                                                                    strbParentTasks.Append("<Where>");
                                                                    strbParentTasks.Append("<And>");
                                                                    strbParentTasks.Append("<Eq>");
                                                                    strbParentTasks.Append("<FieldRef Name='Phase' LookupId='TRUE' />");
                                                                    strbParentTasks.Append("<Value Type='Lookup'>{0}</Value>");
                                                                    strbParentTasks.Append("</Eq>");
                                                                    strbParentTasks.Append("<Eq>");
                                                                    strbParentTasks.Append("<FieldRef Name='Title' />");
                                                                    strbParentTasks.Append("<Value Type='Text'>{1}</Value>");
                                                                    strbParentTasks.Append("</Eq>");
                                                                    strbParentTasks.Append("</And>");
                                                                    strbParentTasks.Append("</Where>");
                                                                    //pass params - pahse and task title
                                                                    string strQueryTask = string.Format(strbParentTasks.ToString(), phaseID, fldTask.LookupValue);

                                                                    SPQuery spqParentTasks = new SPQuery();
                                                                    spqParentTasks.Query = strQueryTask;
                                                                    SPListItemCollection parentTaskColl = lstTasks.GetItems(spqParentTasks);

                                                                    if (parentTaskColl != null && parentTaskColl.Count > 0)
                                                                    {
                                                                        //check if task is completed
                                                                        foreach (SPListItem parentTask in parentTaskColl)
                                                                        {
                                                                            if (string.Compare(Convert.ToString(parentTask[IdeationConstant.ListColumns.Status]), "Completed", true) != 0)
                                                                            {
                                                                                canProcess = false;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        canProcess = false;

                                                                    }

                                                                }

                                                            }

                                                            if (canProcess)
                                                            {
                                                                Actions.AddTaskForTeamMemberBasePredecessor(iWeb, IdeationConstant.IdeaSiteListNames.IDEA_TASKS, task, item);                                                               
                                                            }
                                                        }

                                                    }
                                                }

                                                //Get idea inforamtion
                                                IdeationDataSet dsIdea = IdeaExec.GetIdeaBySiteUrl(iWeb.ServerRelativeUrl);
                                                foreach (IdeationDataSet.IdeaRow drIdea in dsIdea.Idea.Rows)
                                                {
                                                    ideaID = drIdea.IdeaID;
                                                }
                                                //add discussions for team member
                                                Actions.AddDiscussionsForTeamMember(iWeb, sbIdeation, ideaID, Convert.ToInt32(phaseID), item);

                                            }
                                            catch (Exception ex)
                                            {
                                                CommonFunctions.LogError(ex);
                                                Log.LogMessage("Exception at tasks completion:" + ex.ToString());
                                            }

                                            SharepointUtil.RunWithHandler = false;

                                            Actions.UpdateReadPermissionForTeamMembers(iWeb, PhaseName);

                                            SetSocialFactorPermissions(iWeb, item);
                                        }
                                    }
                                });
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                    Log.LogMessage("ProcessTeamMembers ItemAdded method Exception:" + ex.ToString());
                    //throw ex;
                }
                finally
                {
                    if (sbIdeation != null)
                        sbIdeation.Dispose();
                }
            }
        }

        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdating(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessTeamMembers ItemUpdating method starts");
            try
            {
                String MemberIdBefore = "", MemberIdAfter = "";

                SPFieldUserValue fld = null;

                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.TEAM_MEMBERS)
                {
                    SPItemEventDataCollection oBeforeProperties = properties.BeforeProperties;
                    SPItemEventDataCollection oAfterProperties = properties.AfterProperties;

                    if (properties.ListItem != null)
                        if (properties.ListItem["Member Name"] != null)
                            MemberIdBefore = properties.ListItem["Member Name"].ToString().Split(';')[0];
                    Log.LogMessage("MemberIdBefore:" + MemberIdBefore);

                    if (oAfterProperties["Member_x0020_Name"] != null)
                        MemberIdAfter = oAfterProperties["Member_x0020_Name"].ToString().Split(';')[0];
                    Log.LogMessage("MemberIdAfter:" + MemberIdAfter);


                    if (MemberIdBefore != MemberIdAfter && MemberIdAfter != "")
                    {
                        SPUser spUser = null;
                        SPSecurity.RunWithElevatedPrivileges(delegate
                        {
                            using (SPSite spSite = new SPSite(properties.WebUrl))
                            {
                                using (SPWeb spWeb = spSite.OpenWeb())
                                {
                                    fld = new SPFieldUserValue(spWeb, oAfterProperties["Member_x0020_Name"].ToString());

                                    if (fld.LookupValue.Contains("\\"))
                                        spUser = spWeb.EnsureUser(fld.LookupValue);

                                    if (spUser != null)
                                        MemberIdAfter = spUser.ID.ToString();
                                    else if (fld.User != null)
                                    {
                                        MemberIdAfter = fld.User.ID.ToString();
                                    }
                                    else
                                        MemberIdAfter = Convert.ToString(fld.LookupId);
                                }
                            }
                        });

                        UpdateTeamMemberPermission(properties, false);
                        UpdateTeamMemberPermission(properties, true);
                        UpdateTaskForTeamMember(properties, MemberIdBefore, MemberIdAfter);
                        SendTeamMemberEmail(properties, fld);
                    }
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessTeamMembers ItemUpdating method Exception :" + ex.ToString());
                throw ex;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessTeamMembers ItemUpdated method starts");
            try
            {
                //setting permissions for manually changed role 
                string PhaseName = null;
                if (properties.ListItem["Phase"] != null)
                    PhaseName = properties.ListItem["Phase"].ToString().Split('#')[1];
                Log.LogMessage("PhaseName:" + PhaseName);

                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite iSite = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb iWeb = iSite.OpenWeb())
                        {  
                            Actions.UpdateReadPermissionForTeamMembers(iWeb, PhaseName);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessTeamMembers ItemUpdated method Exception:" + ex.ToString());
                throw ex;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemDeleting(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessTeamMembers ItemDeleting method starts");
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.TEAM_MEMBERS)
                {
                    UpdateTeamMemberPermission(properties, false);
                    RemoveUserTasks(properties);
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessTeamMembers ItemDeleting method Exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                throw ex;
            }
        }

        /// <summary>
        /// Justin Asked us to Rollback the Task Addition for Team member
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="MemberIdAfter"></param>
        private void AddTasksForTeamMember(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessTeamMembers AddTasksForTeamMember method starts");
            string RoleName = null, PhaseNameLookup = null, PhaseName = null;

            SSOSharepointWorkflowHelper sso = new SSOSharepointWorkflowHelper();
            Hashtable htParrentTasks = new Hashtable();

            try
            {
                htParrentTasks.Clear();

                sso.LoadCredentials();
                sso.Impersonater.Impersonate();

                using (SPSite oSiteImp = new SPSite(properties.WebUrl))
                {
                    SPWeb oWebImp = null;
                    SPWeb oParentWeb = null;

                    oWebImp = oSiteImp.OpenWeb();
                    oParentWeb = oWebImp.ParentWeb;

                    IdeationDataSet dsIdea = IdeaExec.GetIdeaBySiteUrl(oWebImp.ServerRelativeUrl);
                    IdeationDataSet.IdeaRow drIdea = (IdeationDataSet.IdeaRow)dsIdea.Idea.Rows[0];

                    SPFieldUserValue spFldMemberVal = null;

                    try
                    {
                        if (properties.ListItem["Member Name"] != null)
                            spFldMemberVal = new SPFieldUserValue(oWebImp, properties.ListItem["Member Name"].ToString());

                        if (properties.ListItem["InnovaRole"] != null)
                            RoleName = properties.ListItem["InnovaRole"].ToString().Split('#')[1];

                        if (properties.ListItem["Phase"] != null)
                            PhaseName = properties.ListItem["Phase"].ToString().Split('#')[1];

                        CommonFunctions oFn = new CommonFunctions();

                        SPListItemCollection oItems = oFn.GetPhase( ref oParentWeb, PhaseName);
                        if (oItems.Count > 0)
                            PhaseNameLookup = oItems[0]["ID"].ToString() + ";#" + oItems[0]["Title"].ToString();

                        Hashtable htField = new Hashtable();
                        htField.Add(0, "Title");
                        htField.Add(1, "Priority");
                        htField.Add(2, "Status");
                        htField.Add(3, "Assigned To");
                        htField.Add(4, "Task Group");
                        htField.Add(5, "Description");
                        htField.Add(6, "Start Date");
                        htField.Add(7, "Due");
                        htField.Add(8, "Phase");
                        htField.Add(9, "InnovaRole");
                        htField.Add(10, "Dimension");
                        htField.Add(11, "Task Type");
                        htField.Add(12, "Parent Task");


                        string FilterQuery = CommonFunctions.GetFilterQuery(oParentWeb.Url,
                            IdeationConstant.MasterDataListNames.IdeationMasterTask, 1, drIdea.IdeaID, htField);

                        string PhaseCriteria = String.Format(
                            @"<And><Eq><FieldRef Name='Phase' /><Value Type='LookupMulti'>{0}</Value></Eq><Eq><FieldRef Name='Role' /><Value Type='LookupMulti'>{1}</Value></Eq></And>", HttpUtility.HtmlEncode(PhaseName), HttpUtility.HtmlEncode(RoleName));

                        if (FilterQuery.Length > 0)
                            PhaseCriteria = "<And>" + PhaseCriteria + FilterQuery + "</And>";

                        SPQuery oFilterQuery = new SPQuery();
                        oFilterQuery.Query = "<OrderBy><FieldRef Name='Relationship_x0020_Type' Ascending='False' /></OrderBy><Where><And>" + PhaseCriteria + "<Eq><FieldRef Name='Copy_x0020_On_x0020_Manual_x0020' /><Value Type='bit'>1</Value></Eq></And></Where>";

                        SPListItemCollection oTaskItems = oParentWeb.Lists[IdeationConstant.MasterDataListNames.IdeationMasterTask].GetItems(oFilterQuery);

                        if (oTaskItems.Count > 0)
                        {
                            Log.LogMessage("IdeationMasterTask ListItem Coll not null");
                            foreach (SPListItem oTaskItem in oTaskItems)
                            {
                                SPQuery chkQuery = new SPQuery();
                                
                                chkQuery.Query = "<Where><And><And><And><Eq><FieldRef Name='AssignedTo' /><Value Type='User'>" + properties.ListItem["Member Name"].ToString().Split('#')[1] + "</Value></Eq><Eq><FieldRef Name='InnovaRole' /><Value Type='Lookup'>" + RoleName + "</Value></Eq></And><Eq><FieldRef Name='Phase' /><Value Type='Lookup'>" + PhaseName + "</Value></Eq></And><Eq><FieldRef Name='Title' /><Value Type='Text'>" + oTaskItem["Title"].ToString() + "</Value></Eq></And></Where>";

                                SPListItemCollection collection = properties.Web.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].GetItems(chkQuery);

                                if (collection == null || collection.Count == 0)
                                {
                                    SPFolderCollection oAttachFolder = oParentWeb.Folders["Lists"].SubFolders[
                                        IdeationConstant.MasterDataListNames.IdeationMasterTask].SubFolders["Attachments"].SubFolders;

                                    if (oParentWeb != null)
                                        oParentWeb.Dispose();

                                    foreach (SPFolder oFolder in oAttachFolder)
                                    {
                                        if (oFolder.Name == oTaskItem["ID"].ToString())
                                        {
                                            foreach (SPFile oFile in oFolder.Files)
                                            {
                                                SPFileStream oReadFile = null;
                                                byte[] oByteArrayIn;

                                                oReadFile = (SPFileStream)oFile.OpenBinaryStream();
                                                oByteArrayIn = new byte[oReadFile.Length];

                                                oReadFile.Read(oByteArrayIn, 0, Convert.ToInt32(oReadFile.Length));
                                                oReadFile.Close();

                                                oWebImp.Folders[IdeationConstant.IdeaSiteListNames.DOCUMENTS].Files.Add(oFile.Name, oByteArrayIn, true);

                                                oReadFile.Dispose();
                                            }
                                            break;
                                        }
                                    }


                                    DateTime? TaskStartDate = IdeaPhaseExec.GetCurrentPhaseTaskStartDate(drIdea.IdeaID);

                                    XDateTime sDueDate = null;
                                    XDateTime sStartDate = null;

                                    if (TaskStartDate.HasValue)
                                    {
                                        short TaskStartOffset;
                                        if (oTaskItem["Task Start Offset"] == null)
                                            TaskStartOffset = 0;
                                        else
                                            TaskStartOffset = Convert.ToInt16(oTaskItem["Task Start Offset"]);

                                        sStartDate = new XDateTime(TaskStartDate.GetValueOrDefault());
                                        sStartDate.AddBusinessDays(TaskStartOffset);

                                        TaskStartDate = sStartDate.Date;

                                        sDueDate = new XDateTime(TaskStartDate.GetValueOrDefault());
                                    }
                                    else
                                        sDueDate = new XDateTime();

                                    DateTime dtDueDate;

                                    if (oTaskItem["Due"] != null)
                                    {
                                        short Due = Convert.ToInt16(oTaskItem["Due"].ToString().Split('.')[0]);
                                        if (Due >= 1)
                                            Due = (short)(Due - 1);
                                        sDueDate.AddBusinessDays(Due);
                                    }
                                    dtDueDate = sDueDate.Date;



                                    if (SharepointUtil.isSharepointGroup(ref oWebImp, spFldMemberVal.LookupValue))
                                    {
                                        SPUserCollection oUsers = null;

                                        SPSecurity.RunWithElevatedPrivileges(delegate()
                                        {
                                            using (SPSite oSiteElev = new SPSite(oSiteImp.ID))
                                            {
                                                using (SPWeb oWeboElev = oSiteElev.OpenWeb(oWebImp.ID))
                                                {
                                                    oUsers = oWeboElev.Groups[spFldMemberVal.LookupValue].Users;
                                                }
                                            }
                                        });

                                        if (oUsers != null)
                                            foreach (SPUser oUser in oUsers)
                                            {
                                                SPListItem item = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].Items.Add();

                                                item["Title"] = oTaskItem["Title"].ToString();
                                                item["AssignedTo"] = oUser.ID;
                                                item["Status"] = oTaskItem["Status"].ToString();

                                                if (TaskStartDate.HasValue)
                                                    item["Start Date"] = TaskStartDate.GetValueOrDefault().ToShortDateString();

                                                item["DueDate"] = dtDueDate.ToShortDateString();
                                                item["Task Type"] = "Deliverable";
                                                item["Priority"] = oTaskItem["Priority"].ToString();
                                                item["Body"] = oTaskItem["Body"].ToString();

                                                if (oTaskItem["Dimension"] != null)
                                                    item["Dimension"] = oTaskItem["Dimension"].ToString();

                                                item["Phase"] = PhaseNameLookup;
                                                item["InnovaRole"] = oTaskItem["Role"].ToString();

                                                item["Relationship Type"] = Convert.ToString(oTaskItem["Relationship Type"]);

                                                item.Update();

                                                System.Threading.Thread.Sleep(3000);

                                                if (Convert.ToString(oTaskItem["Relationship Type"]) == "Parent")
                                                {
                                                    if (!htParrentTasks.ContainsKey(item.ID))
                                                        htParrentTasks.Add(item["Title"], item.ID);
                                                }

                                                if (oTaskItem["Parent Task"] != null)
                                                {
                                                    item = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].GetItemById(item.ID);
                                                    item["Parent Task"] = Actions.GetParentTaskId(htParrentTasks, oTaskItem["Parent Task"].ToString());
                                                    item["AssignedTo"] = oUser.ID;
                                                    item.Update();

                                                    System.Threading.Thread.Sleep(3000);
                                                }

                                            }
                                    }
                                    else
                                    {
                                        SPListItem item = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].Items.Add();

                                        item["Title"] = oTaskItem["Title"].ToString();
                                        item["AssignedTo"] = spFldMemberVal.LookupId;
                                        item["Status"] = oTaskItem["Status"].ToString();
                                        item["DueDate"] = dtDueDate.ToShortDateString();

                                        if (TaskStartDate.HasValue)
                                            item["Start Date"] = TaskStartDate.GetValueOrDefault().ToShortDateString();

                                        item["Task Type"] = "Deliverable";
                                        item["Priority"] = oTaskItem["Priority"].ToString();

                                        if (oTaskItem["Body"] != null)
                                            item["Body"] = oTaskItem["Body"].ToString();

                                        if (oTaskItem["Dimension"] != null)
                                            item["Dimension"] = oTaskItem["Dimension"].ToString();

                                        item["Phase"] = PhaseNameLookup;
                                        item["InnovaRole"] = oTaskItem["InnovaRole"].ToString();

                                        item["Relationship Type"] = Convert.ToString(oTaskItem["Relationship Type"]);

                                        if (oTaskItem["Parent Task"] != null)
                                            item["Parent Task"] = Actions.GetParentTaskId(htParrentTasks, oTaskItem["Parent Task"].ToString());

                                        item.Update();

                                        System.Threading.Thread.Sleep(3000);

                                        if (Convert.ToString(oTaskItem["Relationship Type"]) == "Parent")
                                        {
                                            if (!htParrentTasks.ContainsKey(item["Title"]))
                                                htParrentTasks.Add(item["Title"], item.ID);
                                        }

                                        if (oTaskItem["Parent Task"] != null)
                                        {
                                            SPFieldLookupValue spFld = new SPFieldLookupValue(oTaskItem["Parent Task"].ToString());

                                            if (spFld.LookupId == spFld.LookupId)
                                            {
                                                item = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].GetItemById(item.ID);
                                                item["Parent Task"] = item.ID;
                                                item["InnovaRole"] = oTaskItem["Role"].ToString();
                                                item["AssignedTo"] = spFldMemberVal.LookupId;
                                                item.Update();

                                                System.Threading.Thread.Sleep(3000);
                                            }

                                        }

                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        if (oParentWeb != null)
                            oParentWeb.Dispose();

                        if (oWebImp != null)
                            oWebImp.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                sso.Impersonater.Undo();
            }
        }

        private void UpdateTaskForTeamMember(SPItemEventProperties properties, string MemberIdBefore, string MemberIdAfter)
        {
            Log.LogMessage("ProcessTeamMembers UpdateTaskForTeamMember method starts");
            string RoleName = null, PhaseName = null;

            SPSecurity.RunWithElevatedPrivileges(delegate
            {
                try
                {
                    using (SPSite oSiteImp = new SPSite(properties.WebUrl))
                    {
                        SPWeb oWebImp = null;
                        oWebImp = oSiteImp.OpenWeb();

                        try
                        {
                            if (properties.ListItem["InnovaRole"] != null)
                                RoleName = properties.ListItem["InnovaRole"].ToString().Split('#')[1];

                            if (properties.ListItem["Phase"] != null)
                                PhaseName = properties.ListItem["Phase"].ToString().Split('#')[1];

                            string query = String.Format(
                                @"<Where><And><Eq><FieldRef Name='AssignedTo' LookupId='TRUE'/><Value Type='User'>{0}</Value></Eq><And><Eq><FieldRef Name='Phase' /><Value Type='Lookup'>{1}</Value></Eq><Eq><FieldRef Name='InnovaRole' /><Value Type='Lookup'>{2}</Value></Eq></And></And></Where>", MemberIdBefore, HttpUtility.HtmlEncode(PhaseName), HttpUtility.HtmlEncode(RoleName));

                            SPQuery oFilterQuery = new SPQuery();
                            oFilterQuery.Query = query;

                            SPListItemCollection oTaskItems = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].GetItems(oFilterQuery);

                            if (oTaskItems.Count > 0)
                            {
                                Log.LogMessage("Tasks ListItem Coll not null");
                                foreach (SPListItem oTaskItem in oTaskItems)
                                {
                                    oTaskItem["AssignedTo"] = MemberIdAfter;
                                    oTaskItem["Phase"] = oTaskItem["Phase"];

                                    oTaskItem.Update();
                                }
                            }
                        }
                        catch
                        {
                            if (oWebImp != null)
                                oWebImp.Dispose();
                        }
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                }
            });
        }

        private void UpdateTaskUserOnAdding(SPItemEventProperties properties, SPFieldUserValue usr)
        {
            Log.LogMessage("ProcessTeamMembers UpdateTaskUserOnAdding method starts");
            SPSecurity.RunWithElevatedPrivileges(delegate
            {
                try
                {
                    using (SPSite oSiteImp = new SPSite(properties.WebUrl))
                    {
                        SPWeb oWebImp = null;
                        oWebImp = oSiteImp.OpenWeb();

                        try
                        {
                            string PhaseName = null;
                            if (properties.ListItem["Phase"] != null)
                                PhaseName = properties.ListItem["Phase"].ToString().Split('#')[1];
                            Log.LogMessage("PhaseName:" + PhaseName);

                            SPListItem teamIteam = oWebImp.Lists[properties.ListTitle].GetItemById(properties.ListItemId);
                            string roleName = SharepointUtil.GetLookupValue(teamIteam["InnovaRole"], true);
                            Log.LogMessage("RoleName:" + roleName);

                            //get list of tasks with blank assigned to fields for current phase
                            List<SPListItem> oTaskItems = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].Items.Cast<SPListItem>().Where(x => string.IsNullOrEmpty(Convert.ToString(x["AssignedTo"])) &&
                                                                                                                                                string.Compare(SharepointUtil.GetLookupValue(x["Phase"], false), PhaseName, true) == 0).ToList();
                            

                            if (oTaskItems.Count > 0)
                            {
                                Log.LogMessage("Tasks ListItem Coll not null");
                                foreach (SPListItem oTaskItem in oTaskItems)
                                {
                                    //get task role
                                    string taskRole = SharepointUtil.GetLookupValue(oTaskItem["InnovaRole"], true);
                                    if (string.Compare(taskRole, roleName, true) == 0)
                                    {
                                        oTaskItem["AssignedTo"] = teamIteam["Member Name"];
                                        oWebImp.AllowUnsafeUpdates = true;
                                        oTaskItem.Update();
                                    }
                                    else
                                    {
                                        oTaskItem["AssignedTo"] = usr;
                                        oTaskItem.SystemUpdate(false);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            if (oWebImp != null)
                                oWebImp.Dispose();
                        }
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        private void RemoveUserTasks(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessTeamMembers RemoveUserTasks method starts");
            SPListItem spTaskUpdate = null;
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {

                    using (SPSite oSiteImp = new SPSite(properties.SiteId))
                    {
                        SPWeb oWebImp = null;
                        try
                        {
                            oWebImp = oSiteImp.OpenWeb(properties.RelativeWebUrl);

                            string MemberName = null, RoleName = null;
                            string PhaseName = null;

                            if (properties.ListItem["Member Name"] != null)
                                MemberName = properties.ListItem["Member Name"].ToString();

                            if (properties.ListItem["InnovaRole"] != null)
                                RoleName = properties.ListItem["InnovaRole"].ToString().Split('#')[1];

                            if (properties.ListItem["Phase"] != null)
                                PhaseName = properties.ListItem["Phase"].ToString().Split('#')[1];

                            String[] MemberInfo = MemberName.Split('#');

                            int UserId = Convert.ToInt32(MemberInfo[0].Replace(";", ""));
                            string MemberFullName = MemberInfo[1];

                            SPQuery spquery = new SPQuery();
                            spquery.Query = String.Format(
                            @"<Where><And><And><Eq><FieldRef Name='AssignedTo' /><Value Type='User'>{0}</Value></Eq><Eq><FieldRef Name='Phase' /><Value Type='Lookup'>{1}</Value></Eq></And><Eq><FieldRef Name='InnovaRole' /><Value Type='Lookup'>{2}</Value></Eq></And></Where>", MemberFullName, PhaseName, RoleName);

                            SPListItemCollection Tasks = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].GetItems(spquery);
                            Log.LogMessage("Task ListItem ount:" + Tasks.Count);
                            for (int i = Tasks.Count - 1; i >= 0; i--)
                            {
                                Tasks[i]["AssignedTo"] = null;
                                Tasks[i].Update();
                            }

                            foreach (SPListItem spTaskItem in Tasks)
                            {
                                spTaskUpdate = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.IDEA_TASKS].GetItemById(Convert.ToInt32(spTaskItem["ID"]));
                                spTaskUpdate["AssignedTo"] = null;
                                spTaskUpdate.Update();
                            }
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                        finally
                        {
                            if (oWebImp != null)
                                oWebImp.Dispose();
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
                Log.LogMessage("ProcessTeamMembers RemoveUserTasks method Exception:" + ex.ToString());
                throw ex;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        private void SendTeamMemberEmail(SPItemEventProperties properties, SPFieldUserValue MemberIdAfter)
        {
            Log.LogMessage("ProcessTeamMembers SendTeamMemberEmail method starts");
            IdeationDataSet dsIdea;
            IdeationDataSet.PhaseRow drPhase;
            string PhaseName = null, Members = "";

            try
            {
                using (SPWeb oWeb = properties.OpenWeb())
                {
                    if (properties.AfterProperties["Phase"] != null)
                        PhaseName = properties.AfterProperties["Phase"].ToString();

                    dsIdea = PhaseExec.GetPhase(Convert.ToInt32(PhaseName));

                    if (dsIdea.Phase.Rows.Count > 0)
                    {
                        Log.LogMessage("Phase Dataset Not null");
                        drPhase = (IdeationDataSet.PhaseRow)dsIdea.Phase.Rows[0];

                        SPSecurity.RunWithElevatedPrivileges(delegate()
                            {
                                using (SPSite oSiteImp = new SPSite(oWeb.Url))
                                {
                                    using (SPWeb oWebImp = oSiteImp.OpenWeb())
                                    {
                                        SPUser spUser = null;

                                        if (MemberIdAfter.LookupValue.Contains("\\"))
                                            spUser = oWebImp.EnsureUser(MemberIdAfter.LookupValue);

                                        if (spUser != null)
                                        {
                                            if (spUser.Email.Length > 0)
                                                Members += spUser.Email + ";";
                                        }
                                        else if (MemberIdAfter.User != null)
                                        {
                                            if (MemberIdAfter.User.Email.Length > 0)
                                                Members += MemberIdAfter.User.Email + ";";
                                        }
                                        else
                                        {
                                            
                                        }
                                    }
                                }
                            });

                        if (!string.IsNullOrEmpty(Members))
                        {
                            IAsyncResult CallResult;
                            AsyncSendEmail asyncMethod = new AsyncSendEmail(Actions.SendEmail);
                            CallResult = asyncMethod.BeginInvoke(oWeb.Url,
                            DataLan.InnovaOPN.Ideation.Common.IdeationConstant.EmailConfigActions.DeliverableTask,
                                drPhase.Name, Members, null, null, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessTeamMembers SendTeamMemberEmail method Exception:" + ex.ToString());
                throw ex;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="isAdd"></param>
        private void UpdateTeamMemberPermission(SPItemEventProperties properties, bool isAdd)
        {
            Log.LogMessage("ProcessTeamMembers UpdateTeamMemberPermission method starts");
            IdeationDataSet dsListRoles, dsIdea;
            IdeationDataSet.PhaseRow drPhase;
            List<string> lstRoles = new List<string>();
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {

                    using (SPSite oSiteImp = new SPSite(properties.SiteId))
                    {
                        SPWeb oWebImp = null;
                        SPFieldUserValue fldMemberValue = null;

                        try
                        {
                            //TODO: Could have used USING block
                            oWebImp = oSiteImp.OpenWeb(properties.RelativeWebUrl);

                            string MemberFullName = string.Empty, RoleName = string.Empty;
                            string PhaseName = string.Empty;

                            if (!isAdd)
                            {
                                if (properties.ListItem["Member Name"] != null)
                                    fldMemberValue = new SPFieldUserValue(oWebImp, Convert.ToString(properties.ListItem["Member Name"]));

                                if (properties.ListItem["InnovaRole"] != null)
                                    RoleName = Convert.ToString(properties.ListItem["InnovaRole"]).Split('#')[1];

                                if (properties.ListItem["Phase"] != null)
                                    PhaseName = Convert.ToString(properties.ListItem["Phase"]).Split('#')[1];

                                dsIdea = PhaseExec.GetPhase("Name='" + DataLan.InnovaOPN.Ideation.Common.CommonFunctions.MakeSafeQuery(PhaseName) + "'", null);
                            }
                            else
                            {
                                if (properties.AfterProperties["Member_x0020_Name"] != null)
                                {
                                    fldMemberValue = new SPFieldUserValue(oWebImp, properties.AfterProperties["Member_x0020_Name"].ToString());
                                }

                                if (properties.AfterProperties["InnovaRole"] != null)
                                {
                                    RoleName = Convert.ToString(properties.AfterProperties["InnovaRole"]);

                                    SPFieldLookup fldRole = (SPFieldLookup)oWebImp.Lists[properties.ListId].Fields["InnovaRole"];

                                    RoleName = oSiteImp.OpenWeb(fldRole.LookupWebId).Lists[
                                        new Guid(fldRole.LookupList)].GetItemById(Convert.ToInt32(RoleName)).Title;
                                }

                                if (properties.AfterProperties["Phase"] != null)
                                    PhaseName = Convert.ToString(properties.AfterProperties["Phase"]);

                                dsIdea = PhaseExec.GetPhase("PhaseID=" + DataLan.InnovaOPN.Ideation.Common.CommonFunctions.MakeSafeQuery(PhaseName), null);
                            }

                            oWebImp.Update();


                            int UserId = fldMemberValue.LookupId;

                            if (fldMemberValue.User != null)
                                MemberFullName = fldMemberValue.User.Name;
                            else
                            {
                                if (fldMemberValue.LookupValue.Contains("\\"))
                                {
                                    SPUser spMemberUser = oWebImp.EnsureUser(fldMemberValue.LookupValue);
                                    UserId = spMemberUser.ID;
                                    MemberFullName = fldMemberValue.LookupValue;
                                }
                                else if (String.IsNullOrEmpty(fldMemberValue.LookupValue))
                                {
                                    SPGroup spGroup = oWebImp.SiteGroups.GetByID(fldMemberValue.LookupId);
                                    MemberFullName = spGroup.Name;
                                }
                                else
                                    MemberFullName = fldMemberValue.LookupValue;
                            }
                            CommonFunctions.LogMessage("UpdateTeamMemberPerm - IsAdd: " + isAdd + " | " + "UserName: " + MemberFullName + " | " + "PhaseName: " + PhaseName + " | " + "RoleName: " + RoleName);
                            Log.LogMessage("UpdateTeamMemberPerm - IsAdd: " + isAdd + " | " + "UserName: " + MemberFullName + " | " + "PhaseName: " + PhaseName + " | " + "RoleName: " + RoleName);
                            if (!isAdd)
                            {
                                string query = String.Format(@"<Where>
      <And>
         <Eq>
            <FieldRef Name='Member_x0020_Name' />
            <Value Type='User'>{0}</Value>
         </Eq>
         <And>
            <Eq>
               <FieldRef Name='Phase' />
               <Value Type='Lookup'>{1}</Value>
            </Eq>
            <Neq>
               <FieldRef Name='InnovaRole' />
               <Value Type='Lookup'>{2}</Value>
            </Neq>
         </And>
      </And>
   </Where>", MemberFullName, PhaseName, RoleName);

                                SPQuery oFilterQuery = new SPQuery();
                                oFilterQuery.Query = query;

                                SPListItemCollection olstTeamItems = oWebImp.Lists[DataLan.InnovaOPN.Ideation.Common.IdeationConstant.IdeaSiteListNames.TEAM_MEMBERS].GetItems(oFilterQuery);
                                Log.LogMessage("TeamMembers ListItem Count:" + olstTeamItems.Count);
                                if (olstTeamItems != null && olstTeamItems.Count > 0)
                                {
                                    foreach (SPListItem teamItem in olstTeamItems)
                                    {
                                        if (teamItem["InnovaRole"] != null)
                                        {
                                            lstRoles.Add(Convert.ToString(teamItem["InnovaRole"]).Split('#')[1]);
                                            CommonFunctions.LogMessage("UpdateTeamMemberPerm - Existing Roles: " + Convert.ToString(teamItem["InnovaRole"]).Split('#')[1]);
                                        }
                                    }
                                }
                                else
                                    CommonFunctions.LogMessage("UpdateTeamMemberPerm -No Existing Roles for the User: " + MemberFullName);
                                
                            }

                            if (dsIdea.Phase.Rows.Count > 0 && RoleName != null && PhaseName != null)
                            {
                                drPhase = (IdeationDataSet.PhaseRow)dsIdea.Phase.Rows[0];

                                dsListRoles = ListRolesExec.GetListRolesByCriteria("RoleName = '" + DataLan.InnovaOPN.Ideation.Common.CommonFunctions.MakeSafeQuery(RoleName) + "' and PhaseID=" + drPhase.PhaseID, null);

                                if (SharepointUtil.isSharepointGroup(ref oWebImp, MemberFullName))
                                {
                                    if (!isAdd)
                                    {
                                        if (lstRoles.Count == 0)
                                            SharepointUtil.RemoveSiteGroupPermission(MemberFullName, ref oWebImp);
                                    }
                                    else
                                    {
                                        SharepointUtil.AddGroupToWebWithPermission(MemberFullName, IdeationConstant.PermissionsLevel.READ, ref oWebImp);
                                    }

                                    foreach (IdeationDataSet.ListRolesRow drListRole in dsListRoles.ListRoles.Rows)
                                    {
                                        try
                                        {
                                            SharepointUtil.AddGroupToListwithPermission(MemberFullName, drListRole.Permission, drListRole.ListName, ref oWebImp);

                                        }
                                        catch (Exception innerEx)
                                        {
                                            DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage(MemberFullName.ToString());
                                            DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(innerEx);
                                            throw innerEx;
                                        }
                                    }
                                }
                                else
                                {
                                    if (!isAdd)
                                    {
                                        if (lstRoles.Count == 0)
                                            SharepointUtil.RemoveSiteUser(UserId, ref oWebImp);
                                    }
                                    else
                                    {
                                        SharepointUtil.AddUserWithIDToWebWithPermission(MemberFullName, IdeationConstant.PermissionsLevel.READ, ref oWebImp);
                                    }

                                    foreach (IdeationDataSet.ListRolesRow drListRole in dsListRoles.ListRoles.Rows)
                                    {
                                        try
                                        {
                                            if (!isAdd)
                                            {
                                                SharepointUtil.RemoveUserListPermission(UserId, drListRole.ListName.Trim(), ref oWebImp);
                                            }
                                            else
                                            {
                                                SharepointUtil.AddUserToListPermission(UserId, drListRole.Permission.Trim(), drListRole.ListName.Trim(), ref oWebImp);                                                
                                            }
                                        }
                                        catch (Exception innerEx)
                                        {
                                            Log.LogMessage("UserPermission Exception:" + innerEx.ToString());
                                            DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage(MemberFullName.ToString());
                                            DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(innerEx);
                                            
                                        }
                                    }
                                    if (!isAdd)
                                    {
                                        if (lstRoles.Count > 0)
                                        {
                                            Log.LogMessage("RolesList Item not null");
                                            for (int i = 0; i < lstRoles.Count; i++)
                                            {
                                                dsListRoles = ListRolesExec.GetListRolesByCriteria("RoleName = '" + DataLan.InnovaOPN.Ideation.Common.CommonFunctions.MakeSafeQuery(lstRoles[i]) + "' and PhaseID=" + drPhase.PhaseID, null);
                                                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("RoleName = '" + DataLan.InnovaOPN.Ideation.Common.CommonFunctions.MakeSafeQuery(lstRoles[i]) + "' and PhaseID=" + drPhase.PhaseID);
                                                foreach (IdeationDataSet.ListRolesRow drListRole in dsListRoles.ListRoles.Rows)
                                                {
                                                    try
                                                    {
                                                        SharepointUtil.AddUserToListPermission(UserId, drListRole.Permission.Trim(), drListRole.ListName.Trim(), ref oWebImp);
                                                    }
                                                    catch (Exception innerEx)
                                                    {
                                                        DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("Exception at AddUserToListPermission for User: " + MemberFullName.ToString());
                                                        DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(innerEx);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                oWebImp.Update();
                            }
                        }
                        catch (Exception ex)
                        {
                            DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                            Log.LogMessage("Exception at UpdateTeamMemberPermission method:" + ex.ToString());
                        }
                        finally
                        {
                            if (oWebImp != null)
                                oWebImp.Dispose();
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
                Log.LogMessage("ProcessTeamMembers UpdateTeamMemberPermission method Exception:" + ex.ToString());
                throw ex;
            }

        }

        /// <summary>
        /// Adds team members to Team Memgers group of idea site.
        /// </summary>
        /// <param name="webUrl"></param>
        private void SetSocialFactorPermissions(SPWeb oWebImp, SPListItem item)
        {
            Log.LogMessage("ProcessTeamMembers SetSocialFactorPermissions method starts");
            try
            {
                Guid conversationSpaceFeature = new Guid(IdeationConstant.SocialFactorFeatureID);

                if (oWebImp.Features[conversationSpaceFeature] != null)
                {
                    oWebImp.AllowUnsafeUpdates = true;

                    SPFieldUser assignedTo = (SPFieldUser)item.Fields["Member Name"];
                    Log.LogMessage("AssignedTo:" + assignedTo.Title);
                    SPRoleDefinition roleDefinition = oWebImp.RoleDefinitions["ConversationSpace Contribute"];
                    SPRoleAssignment spRoleAssignement = null;

                    if (assignedTo != null)
                    {
                        SPFieldUserValue user = (SPFieldUserValue)assignedTo.GetFieldValue(item["Member Name"].ToString());

                        if (user != null)
                        {
                            SPUser userObject = user.User;
                            Log.LogMessage("User:" + userObject.Name);
                            if (userObject != null)
                            {
                                spRoleAssignement = oWebImp.RoleAssignments.GetAssignmentByPrincipal(userObject);

                                if (!spRoleAssignement.RoleDefinitionBindings.Contains(roleDefinition))
                                {
                                    spRoleAssignement.RoleDefinitionBindings.Add(roleDefinition);
                                    spRoleAssignement.Update();
                                }
                            }
                            else
                            {
                                string groupName = user.LookupValue;
                                Log.LogMessage("GroupName:" + groupName);
                                if (!string.IsNullOrEmpty(groupName))
                                {
                                    SPGroup group = oWebImp.SiteGroups[groupName];
                                    spRoleAssignement = oWebImp.RoleAssignments.GetAssignmentByPrincipal(group);

                                    if (!spRoleAssignement.RoleDefinitionBindings.Contains(roleDefinition))
                                    {
                                        spRoleAssignement.RoleDefinitionBindings.Add(roleDefinition);
                                        spRoleAssignement.Update();
                                    }
                                }
                            }
                        }
                    }

                    oWebImp.AllowUnsafeUpdates = false;

                }
            }

            catch (Exception ex)
            {
                CommonFunctions.LogError(ex);
                Log.LogMessage("ProcessTeamMembers SetSocialFactorPermissions method Exception:" + ex.ToString());
            }


        }
    }
}
