using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation;
using DataLan.InnovaOPN.Ideation.Common;
using Microsoft.SharePoint.Utilities;
using DataLan.InnovaOPN.Ideation.Dataset;
using System.Data;
using DataLan.InnovaOPN.Ideation.DataAccess;
using System.Xml;
using System.Web;
using System.Collections;

namespace IGEventHandlers
{
    public class ProcessIdeaTasks : SPItemEventReceiver
    {
        public static string BeforeUpdate;
        private delegate bool AsyncSendEmail(String webUrl, String ActionName, String PhaseName,
           String DefaultToAddress, String DefaultCCAddress);

        public static object oLock = new object();

        public override void ItemAdding(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks ItemAdding Method starts");
            string AssignedTo = null, RoleIdBefore = null;
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
                {
                    if (properties.AfterProperties["AssignedTo"] != null)
                        AssignedTo = properties.AfterProperties["AssignedTo"].ToString().Split(';')[0];
                    Log.LogMessage("AssignedTo:" + AssignedTo);

                    if (properties.AfterProperties["InnovaRole"] != null)
                        RoleIdBefore = properties.AfterProperties["InnovaRole"].ToString();
                    Log.LogMessage("RoleIdBefore:" + RoleIdBefore);

                    if (string.IsNullOrEmpty(AssignedTo) && RoleIdBefore != null)
                    {
                        // this.EventFiringEnabled = false;
                        UpdateAssignedTo(properties);
                        // this.EventFiringEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessIdeaTasks ItemAdding method exception:" + ex.ToString());
            }
        }

        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks ItemAdded method starts");
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
                {
                    if (properties.ListItem["Task Type"] != null)
                    {
                        if (!string.IsNullOrEmpty(Convert.ToString(properties.ListItem["Task Type"]))
                            && string.Compare(properties.ListItem["Task Type"].ToString(), "Deliverable") == 0)
                        {
                            SPFieldUserValue fld = null;
                            if (properties.ListItem["AssignedTo"] != null)
                                fld = new SPFieldUserValue(properties.Web, properties.ListItem["AssignedTo"].ToString());
                            Log.LogMessage("FieldUserValue:" + fld.User);

                            // this.EventFiringEnabled = false;
                            SendTeamMemberEmail(properties, fld);
                        }
                    }


                    // this.EventFiringEnabled = false;
                    UpdateScheduledDate(properties);

                    ScoringTasks(properties);
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessIdeaTasks ItemAdded Method exception: " + ex.ToString());
            }
        }

        public override void ItemUpdating(SPItemEventProperties properties)
        {
            string RoleIdBefore = null, RoleIdAfter = null;
            Log.LogMessage("ProcessIdeaTasks ItemUpdating method starts");
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
                {
                    if (properties.ListItem != null)
                        if (properties.ListItem["InnovaRole"] != null)
                            RoleIdBefore = properties.ListItem["InnovaRole"].ToString().Split(';')[0];
                    Log.LogMessage("RoleIdBefore:" + RoleIdBefore);

                    if (properties.AfterProperties["InnovaRole"] != null)
                        RoleIdAfter = properties.AfterProperties["InnovaRole"].ToString().Split(';')[0];
                    Log.LogMessage("RoleIdAfter:" + RoleIdAfter);

                    if (RoleIdBefore != RoleIdAfter && !String.IsNullOrEmpty(RoleIdAfter))
                    {
                        // this.EventFiringEnabled = false;
                        UpdateAssignedTo(properties);
                        //  this.EventFiringEnabled = true;
                    }
                    BeforeUpdate = Convert.ToString(properties.ListItem["Status"]);

                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessIdeaTasks ItemUpdaing method exception: " + ex.ToString());
            }
        }


        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks ItemUpdated method starts");
            try
            {

                if (properties.ListItem != null)
                {
                    Log.LogMessage("Properties ListItem not null");
                    string Phase = "", Title = "", IterationNumber = "";
                    if (Convert.ToString(properties.AfterProperties["Phase"]).Trim() != "")
                    {
                        Phase = Convert.ToString(properties.AfterProperties["Phase"]);
                    }
                    else
                    {
                        Phase = Convert.ToString(properties.ListItem["Phase"]);
                    }
                    if (Convert.ToString(properties.AfterProperties["Title"]).Trim() != "")
                    {
                        Title = Convert.ToString(properties.AfterProperties["Title"]);
                    }
                    else
                    {
                        Title = Convert.ToString(properties.ListItem["Title"]);
                    }

                    if (string.Compare(Convert.ToString(properties.ListItem["Task Type"]), "Deliverable", true) == 0 && string.Compare(Convert.ToString(properties.ListItem["Status"]), "Completed", true) == 0 && string.Compare(Convert.ToString(BeforeUpdate), "Completed", true) != 0)
                    {
                        Actions.AddTaskQueueEntry(properties.SiteId, properties.Web.ServerRelativeUrl, properties.Web.Url, properties.Web.Title, Phase, Title);

                    }
                }


                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
                {
                    bool IsContainAllowedChilds = false;
                    SPList TaskQueue = properties.Web.ParentWeb.Lists.TryGetList("TaskQueue");

                    SPQuery TaskQueueQuery = new SPQuery();
                    string siteurl = properties.Web.Url;
                    string RelativeSiteUrl;
                    if (siteurl.LastIndexOf('/') == siteurl.Length - 1)
                    {
                        siteurl = siteurl.Substring(0, siteurl.Length - 1);
                    }
                    RelativeSiteUrl = siteurl;
                    RelativeSiteUrl = RelativeSiteUrl.Substring(RelativeSiteUrl.IndexOf("/") + 1);
                    RelativeSiteUrl = RelativeSiteUrl.Substring(RelativeSiteUrl.IndexOf("/") + 1);
                    RelativeSiteUrl = RelativeSiteUrl.Substring(RelativeSiteUrl.IndexOf("/"));

                    //siteurl = siteurl.Substring(siteurl.LastIndexOf('/'), siteurl.Length - siteurl.LastIndexOf('/'));
                    Log.LogMessage("SiteURL :" + RelativeSiteUrl.ToString());


                    TaskQueueQuery.Query = "<Where><And><Contains><FieldRef Name='Site' /><Value Type='URL'>" + RelativeSiteUrl + "</Value></Contains><Eq><FieldRef Name='Processed' /><Value Type='Boolean'>0</Value></Eq></And></Where>";
                    Log.LogMessage("<Where><And><Contains><FieldRef Name='Site' /><Value Type='URL'>" + RelativeSiteUrl + "</Value></Contains><Eq><FieldRef Name='Processed' /><Value Type='Boolean'>0</Value></Eq></And></Where>");
                    SPListItemCollection TaskqueueItemCollection = TaskQueue.GetItems(TaskQueueQuery);
                    Log.LogMessage("After Got Task queue Item Collection");
                    if (TaskqueueItemCollection != null)
                    {
                        Log.LogMessage("Tasqueue Count :" + Convert.ToString(TaskqueueItemCollection.Count));
                        Log.LogMessage("ParentWeb :" + Convert.ToString(properties.Web.ParentWeb.Url));
                        SPList list1 = properties.Web.ParentWeb.Lists.TryGetList("Ideation Master Task");
                        Log.LogMessage("ListName :" + Convert.ToString(list1.Title));
                        SPList lstTeamMembers = properties.Web.Lists.TryGetList("Team Members");
                        if (TaskqueueItemCollection.Count > 0)
                        {
                            foreach (SPListItem taskQueueItm in TaskqueueItemCollection)
                            {
                                Log.LogMessage("taskQueueItm :" + taskQueueItm.Title.ToString());
                                foreach (SPFieldLookupValue fieldLookupValue in (List<SPFieldLookupValue>)taskQueueItm["Master Task"])
                                {
                                    string str1 = "", str2 = "";
                                    Log.LogMessage("Master Task Taskqueue not null");
                                    Log.LogMessage("Taskqueue Master Task value :" + Convert.ToString(fieldLookupValue));
                                    string lookupValue2 = SharepointUtil.GetLookupValue(list1.GetItemById(fieldLookupValue.LookupId)["Role"], true);
                                    Log.LogMessage("Role value for the" + Convert.ToString(fieldLookupValue) + " from ideation master tasks list");

                                    IdeationDataSet.IdeaRow drIdea = (IdeationDataSet.IdeaRow)IGDBSynchExec.GetIdeaBySiteUrl(properties.Web.ServerRelativeUrl).Idea.Rows[0];
                                    try
                                    {


                                        if (lookupValue2 != null)
                                            str1 = lookupValue2;
                                        if (properties.ListItem["Phase"] != null)
                                            str2 = SharepointUtil.GetLookupValue(properties.ListItem["Phase"], true);
                                        Log.LogMessage("Str1=" + str1 + "Str2=" + str2);
                                        SPQuery qry = new SPQuery();
                                        qry.Query = @"<Where><And><Eq><FieldRef Name='Phase' LookupId='TRUE' /><Value Type='LookupMulti'>" + str2 + @"</Value></Eq>
                                             <Eq><FieldRef Name='InnovaRole' LookupId='TRUE' /><Value Type='LookupMulti'>" + str1 + @"</Value></Eq>
                                            </And></Where>";
                                        SPListItemCollection TeamMembersItmColl = lstTeamMembers.GetItems(qry);
                                        if (TeamMembersItmColl.Count > 0)
                                        {
                                            string filterQuery = CommonFunctions.GetFilterQuery(properties.Web.ParentWeb.Url, "Ideation Master Task", 1, drIdea.IdeaID, new Hashtable()
          {
            {
              (object) 0,
              (object) "Title"
            },
            {
              (object) 1,
              (object) "Priority"
            },
            {
              (object) 2,
              (object) "Status"
            },
            {
              (object) 3,
              (object) "Assigned To"
            },
            {
              (object) 4,
              (object) "Task Group"
            },
            {
              (object) 5,
              (object) "Description"
            },
            {
              (object) 6,
              (object) "Start Date"
            },
            {
              (object) 7,
              (object) "Due"
            },
            {
              (object) 8,
              (object) "Phase"
            },
            {
              (object) 9,
              (object) "Role"
            },
            {
              (object) 10,
              (object) "Dimension"
            },
            {
              (object) 11,
              (object) "Task Type"
            },
            {
              (object) 12,
              (object) "Parent Task"
            }
          });
                                            StringBuilder stringBuilder1 = new StringBuilder();
                                            stringBuilder1.Append("<And>");
                                            stringBuilder1.Append("<And>");
                                            stringBuilder1.Append("<Eq><FieldRef Name='Phase' LookupId='TRUE' /><Value Type='LookupMulti'>{0}</Value></Eq>");
                                            stringBuilder1.Append("<Eq><FieldRef Name='Role' LookupId='TRUE' /><Value Type='LookupMulti'>{1}</Value></Eq>");
                                            stringBuilder1.Append("</And>");
                                            stringBuilder1.Append("<Eq><FieldRef Name='ID' /><Value Type='Counter'>{2}</Value></Eq>");
                                            stringBuilder1.Append("</And>");
                                            string str4 = string.Format(stringBuilder1.ToString(), (object)str2, (object)str1, (object)fieldLookupValue.LookupId);
                                            if (filterQuery.Length > 0)
                                                str4 = "<And>" + str4 + filterQuery + "</And>";
                                            string str5 = "<OrderBy><FieldRef Name='Relationship_x0020_Type' Ascending='False' /></OrderBy><Where><And>" + str4 + "<Eq><FieldRef Name='Copy_x0020_On_x0020_Manual_x0020' /><Value Type='bit'>1</Value></Eq></And></Where>";
                                            SPList list = properties.Web.ParentWeb.Lists.TryGetList("Ideation Master Task");
                                            SPListItemCollection MasterTaskitems = list.GetItems(new SPQuery()
                                            {
                                                Query = str5
                                            });
                                            Log.LogMessage(Convert.ToString(MasterTaskitems.Count));
                                            if (MasterTaskitems.Count > 0)
                                            {
                                                IsContainAllowedChilds = true;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {

                                    }

                                }

                            }
                        }
                    }
                    if (TaskqueueItemCollection == null || TaskqueueItemCollection.Count == 0 || !IsContainAllowedChilds)
                    {
                        ScoringTasks(properties);
                        CommonFunctions.LogMessage("Action AddItemToIdeaLog 1");
                        //Updated // Sep15
                        SPSecurity.RunWithElevatedPrivileges(delegate()
                        {
                            using (SPSite oSite = new SPSite(properties.WebUrl))
                            {
                                using (SPWeb oWebImp = oSite.OpenWeb())
                                {
                                    try
                                    {
                                        CommonFunctions.LogMessage("Action AddItemToIdeaLog 2");
                                        if (oWebImp.Properties["CurrentPhaseID"] != null)
                                        {
                                            Log.LogMessage("Current PhaseID not null");
                                            int phaseID = Convert.ToInt32(oWebImp.Properties["CurrentPhaseID"]);
                                            IdeationDataSet dsIdea;
                                            IdeationDataSet dsIdeaDisposition;
                                            IdeationDataSet.PhaseRow drPhase;
                                            string PhaseName = "";
                                            dsIdea = PhaseExec.GetPhase(phaseID);
                                            if (dsIdea.Phase.Rows.Count > 0)
                                            {
                                                drPhase = (IdeationDataSet.PhaseRow)dsIdea.Phase.Rows[0];
                                                PhaseName = drPhase.Name;
                                            }
                                            CommonFunctions.LogMessage("Action AddItemToIdeaLog 3 PhaseName: " + PhaseName);
                                            Log.LogMessage("PhaseName:" + PhaseName);
                                            bool IsAllTasksCompleted = CommonFunctions.IsAllTasksCompleted(properties.Web.Url, IdeationConstant.IdeaSiteListNames.IDEA_TASKS, PhaseName);
                                            CommonFunctions.LogMessage("Action AddItemToIdeaLog 3 IsAllTasksCompleted: " + IsAllTasksCompleted);
                                            Log.LogMessage("IsAllTasksCompleted:" + IsAllTasksCompleted);
                                            if (IsAllTasksCompleted)
                                            {
                                                bool IsReadyForProceed = false;
                                                try
                                                {
                                                    dsIdeaDisposition = IdeaApplication.IdeaDisposition(properties.Web.Url, phaseID);
                                                    dsIdeaDisposition.Merge(IdeaApplication.ScoringDispositionAction(properties.Web.Url, phaseID, oWebImp.Url.ToString()));
                                                    dsIdeaDisposition.FinalScoring.Columns.Add("Disposition");
                                                    dsIdeaDisposition.FinalScoring.AcceptChanges();


                                                    //add disposition column to display ready to proceed image
                                                    foreach (IdeationDataSet.ActionMenuDataRow drAction in dsIdeaDisposition.ActionMenuData.Rows)
                                                    {
                                                        if (drAction.MenuData.Contains("ProceedToNextStage.aspx") ||
                                                            drAction.MenuData.Contains("ProceedtoNthStage.aspx") ||
                                                            drAction.MenuData.Contains("VaultIdea.aspx"))
                                                        {
                                                            IsReadyForProceed = true;
                                                            break;
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Log.LogMessage("Exception at  IsReadyForProceed from Task Complete:" + ex.ToString());
                                                    CommonFunctions.LogMessage("Exception at  IsReadyForProceed from Task Complete");
                                                    CommonFunctions.LogError(ex);
                                                }
                                                CommonFunctions.LogMessage("Action AddItemToIdeaLog from Task Complete Starts");
                                                Log.LogMessage("Action AddItemToIdeaLog from Task Complete Starts");
                                                SPList lstIdeaLog = oWebImp.Lists.TryGetList("Idealog");
                                                if (lstIdeaLog != null)
                                                {
                                                    SPListItem item = null;
                                                    if (lstIdeaLog.Items.Count > 0)
                                                        item = lstIdeaLog.Items[0];
                                                    else
                                                        item = lstIdeaLog.Items.Add();

                                                    item["Scores_x0020_Complete"] = true;
                                                    if (item.Fields.ContainsField("Ready for Disposition"))
                                                    {
                                                        item["Ready_x0020_for_x0020_Dispositio"] = IsReadyForProceed;
                                                    }

                                                    item.Update();
                                                    lstIdeaLog.Update();
                                                }
                                                CommonFunctions.LogMessage("Action AddItemToIdeaLog from Task Complete Ends");
                                                Log.LogMessage("Action AddItemToIdeaLog from Task Complete Ends");
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.LogMessage("Exception at  AddItemToIdeaLog from Task Complete: " + ex.ToString());
                                        CommonFunctions.LogMessage("Exception at  AddItemToIdeaLog from Task Complete");
                                        CommonFunctions.LogError(ex);
                                    }

                                    //Updated // Sep15
                                    DBSynchActions.DBSynchUpdate(properties, oSite, oWebImp);
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                CommonFunctions.LogMessage("Exception at  Tasks Item Updated");
                Log.LogMessage("Exception at  Tasks Item Updated: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
            }
        }

        public override void ItemDeleted(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks Itemdeleted Method starts");
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
                {
                    // this.EventFiringEnabled = false;
                    ScoringTasks(properties);
                    // this.EventFiringEnabled = true;
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessIdeaTasks Itemdeleted Method exception: " + ex.ToString());
            }
        }


        private void UpdateScheduledDate(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks UpdateScheduledDate Method starts");
            try
            {
                this.DisableEventFiring();
                using (SPWeb spWeb = properties.OpenWeb())
                {
                    SPList spTaskList = spWeb.Lists[properties.ListId];
                    Log.LogMessage("List Name:" + spTaskList.Title);
                    String ScheduledStart = "", ScheduledEnd = "";

                    if (!String.IsNullOrEmpty(Convert.ToString(properties.AfterProperties["StartDate"])))
                        ScheduledStart = SPUtility.CreateISO8601DateTimeFromSystemDateTime(Convert.ToDateTime(properties.AfterProperties["StartDate"]));

                    if (!String.IsNullOrEmpty(Convert.ToString(properties.AfterProperties["DueDate"])))
                        ScheduledEnd = SPUtility.CreateISO8601DateTimeFromSystemDateTime(Convert.ToDateTime(properties.AfterProperties["DueDate"]));

                    SPListItem spItem = spTaskList.GetItemById(properties.ListItemId);
                    spItem["Scheduled Start"] = String.IsNullOrEmpty(ScheduledStart) ? null : ScheduledStart;
                    spItem["Scheduled End"] = String.IsNullOrEmpty(ScheduledEnd) ? null : ScheduledEnd;
                    spItem.SystemUpdate();
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessIdeaTasks UpdateScheduledDate Method Exception: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
            }
            finally
            {
                this.EnableEventFiring();
            }
        }

        private void ScoringTasks(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks ScoringTasks Method starts");
            try
            {
                IdeationDataSet dsIdeaData = null;
                IdeationDataSet.IdeaRow drIdea = null;
                IdeationDataSet.ScorePhaseRow drScorePhase = null;

                string PhaseName = "";
                int PhaseId;
                short DaysToScore = 0;

                using (SPWeb oWeb = properties.OpenWeb())
                {
                    PhaseId = Convert.ToInt32(oWeb.Properties["CurrentPhaseID"]);
                    Log.LogMessage("PhaseId:" + PhaseId.ToString());
                    IdeationDataSet.PhaseRow drPhase;

                    dsIdeaData = IdeaExec.GetIdeaBySiteUrl(properties.RelativeWebUrl);
                    dsIdeaData.EnforceConstraints = false;
                    PhaseExec.GetPhase(PhaseId, dsIdeaData);

                    if (dsIdeaData.Idea.Rows.Count > 0)
                        drIdea = dsIdeaData.Idea[0];

                    if (dsIdeaData.Phase.Rows.Count > 0)
                    {
                        drPhase = dsIdeaData.Phase[0];
                        PhaseName = drPhase.Name;
                    }

                    bool IsDeliverableTaskCompleted = false;

                    lock (oLock)
                    {

                        IsDeliverableTaskCompleted = DataLan.InnovaOPN.Ideation.Common.CommonFunctions.CheckPhaseDeliverableTasks(properties.WebUrl, properties.ListTitle, PhaseName);

                        if (IsDeliverableTaskCompleted)
                        {
                            ScorePhaseExec.GetScorePhase("PhaseID=" + PhaseId.ToString(), null, dsIdeaData);

                            if (dsIdeaData.ScorePhase.Rows.Count > 0)
                            {
                                Log.LogMessage("ScorePhase dataset not null");
                                drScorePhase = (IdeationDataSet.ScorePhaseRow)dsIdeaData.ScorePhase.Rows[0];

                                if (drScorePhase.MinScorers > 0)
                                {
                                    DaysToScore = (short)drScorePhase.DaysToScore;
                                    dsIdeaData.Merge(RoleExec.GetScorerRolesByPhaseId(PhaseId));

                                    Actions.AddScoringTasks(properties.WebUrl, drIdea.Title, PhaseName, DaysToScore);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                //throw;
            }
            finally
            {
                this.EnableEventFiring();
            }
        }

        private void UpdateAssignedTo(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTasks UpdateAssignedTo method starts");
            string RoleName = null, PhaseName = null;
            SSOSharepointWorkflowHelper sso = new SSOSharepointWorkflowHelper();

            try
            {
                sso.LoadCredentials();
                sso.Impersonater.Impersonate();

                using (SPSite oSiteImp = new SPSite(properties.WebUrl))
                {
                    SPWeb oWebImp = null;
                    SPList spTaskList = null;

                    try
                    {
                        if (properties.AfterProperties["InnovaRole"] != null)
                            RoleName = properties.AfterProperties["InnovaRole"].ToString();
                        Log.LogMessage("RoleName:" + RoleName);
                        if (properties.AfterProperties["Phase"] != null)
                            PhaseName = properties.AfterProperties["Phase"].ToString();
                        Log.LogMessage("Phasename:" + PhaseName);
                        oWebImp = oSiteImp.OpenWeb();
                        spTaskList = oWebImp.Lists[properties.ListId];
                        Log.LogMessage("List:" + spTaskList);
                        string PhaseCriteria = String.Format(
                        @"<Where><And><Eq><FieldRef Name='Phase'  LookupId='True'/><Value Type='Lookup'>{0}</Value></Eq><Eq><FieldRef Name='InnovaRole'  LookupId='True'/><Value Type='Lookup'>{1}</Value></Eq></And></Where>", HttpUtility.HtmlEncode(PhaseName), HttpUtility.HtmlEncode(RoleName));

                        SPQuery oFilterQuery = new SPQuery();
                        oFilterQuery.Query = PhaseCriteria;

                        SPListItemCollection spTeams = oWebImp.Lists[IdeationConstant.IdeaSiteListNames.TEAM_MEMBERS].GetItems(oFilterQuery);

                        if (spTeams.Count > 0)
                        {
                            Log.LogMessage("Team Members ListItem Coll not null");
                            SPFieldUserValue fldValue = new SPFieldUserValue(oWebImp, spTeams[0]["Member_x0020_Name"].ToString());
                            properties.AfterProperties["AssignedTo"] = fldValue;

                            for (int j = 1; j <= spTeams.Count - 1; j++)
                            {
                                SPListItem spTaskItem = spTaskList.Items.Add();

                                foreach (SPField spFld in spTaskList.Fields)
                                {
                                    if (!spFld.Hidden && !spFld.ReadOnlyField && spFld.Title != "Content Type" &&
                                        spFld.InternalName != "WorkflowName" && spFld.InternalName != "Attachments")
                                    {
                                        if (!String.IsNullOrEmpty(Convert.ToString(properties.AfterProperties[spFld.InternalName])))
                                            spTaskItem[spFld.InternalName] = properties.AfterProperties[spFld.InternalName];
                                    }
                                }

                                spTaskItem["AssignedTo"] = new SPFieldUserValue(oWebImp, spTeams[j]["Member_x0020_Name"].ToString());
                                spTaskItem.Update();
                            }

                        }
                        else
                            properties.AfterProperties["AssignedTo"] = null;
                    }
                    catch
                    {
                        //throw;
                    }
                    finally
                    {
                        if (oWebImp != null)
                            oWebImp.Dispose();
                    }
                }
            }
            catch
            {
                //throw;
            }
            finally
            {
                sso.Impersonater.Undo();
            }
        }

        public static bool DoesPrincipalHasPermissions(SPListItem item, SPPrincipal principal)
        {
            Log.LogMessage("ProcessIdeaTasks DoesPrincipalHasPermissions method starts");
            SPRoleAssignment roleAssignment = null;
            try
            {
                roleAssignment = item.RoleAssignments.GetAssignmentByPrincipal(principal);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        private void SendTeamMemberEmail(SPItemEventProperties properties, SPFieldUserValue MemberIdAfter)
        {
            Log.LogMessage("ProcessIdeaTasks SendTeamMemberEmail method starts");
            IdeationDataSet dsIdea;
            IdeationDataSet.PhaseRow drPhase;
            string PhaseName = null, Members = "";

            try
            {
                using (SPWeb oWeb = properties.OpenWeb())
                {
                    if (properties.AfterProperties["Phase"] != null)
                        PhaseName = properties.AfterProperties["Phase"].ToString();
                    Log.LogMessage("PhaseName:" + PhaseName);
                    dsIdea = PhaseExec.GetPhase(Convert.ToInt32(PhaseName));

                    if (dsIdea.Phase.Rows.Count > 0)
                    {
                        Log.LogMessage("Phase Dataset not null");
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
                                        //do not send emails to group members ==== ROHIT 17/5

                                        SPGroup oGroup = oWebImp.SiteGroups.GetByID(MemberIdAfter.LookupId);
                                        foreach (SPUser oGpUser in oGroup.Users)
                                        {
                                            if (oGpUser.Email.Length > 0)
                                                Members += oGpUser.Email + ";";
                                        }
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
                Log.LogMessage("ProcessIdeaTasks SendTeamMemberEmail method exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                throw ex;
            }
        }
    }
}
