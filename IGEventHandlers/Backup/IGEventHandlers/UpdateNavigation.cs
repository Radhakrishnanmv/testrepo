using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Navigation;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;
using System.Data;

namespace IGEventHandlers
{
    public class UpdateNavigation : SPItemEventReceiver
    {
        private static object NavigationLock = new object();

        #region events

        public override void ItemDeleting(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation ItemDeleting method starts");
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.PHASES)
            {
                DeletePhaseTasks(properties);
                DeletePahseTeam(properties);
            }
        }

        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation ItemAdded method starts");
            try
            {
                this.EventFiringEnabled = false;
                try
                {
                    System.Threading.Thread.Sleep(2000);
                    UpdateNavNodes(properties);
                }
                catch (Exception ex)
                {
                    DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
                    DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("Exception at UpdateNavNodes ItemAdded");
                    Log.LogMessage("Exception at UpdateNavNodes ItemAdded:" + ex.ToString());
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("UpdateNavigation ItemAdded method Exception:" + ex.ToString());

            }
            finally
            {
                this.EventFiringEnabled = true;
            }
        }

        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation ItemUpdated Method starts");
            try
            {
                this.EventFiringEnabled = false;

                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.PHASES)
                {   
                    UpdateNavNodes(properties);
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("UpdateNavigation ItemUpdated method Exception:" + ex.ToString());

            }
            finally
            {
                this.EventFiringEnabled = true;
            }
        }

        public override void ItemDeleted(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation ItemDeleted method starts");
            try
            {
                string preaviousPhaseID = string.Empty;

                if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.PHASES)
                {
                    DeletePhaseRecords(properties);
                    UpdateNavNodes(properties);
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("UpdateNavigation ItemDeleted method Exception:" + ex.ToString());
                throw ex;
            }
        }

        #endregion

        #region private method

        /// <summary>
        /// Deletes phase team
        /// </summary>
        /// <param name="properties"></param>
        private void DeletePahseTeam(SPItemEventProperties properties)
        {
            //TODO: Rename the method to DeletePhaseTeam
            Log.LogMessage("UpdateNavigation DeletePahseTeam method starts");
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite oSite = new SPSite(properties.SiteId))
                    {
                        using (SPWeb oWeb = oSite.OpenWeb(properties.RelativeWebUrl))
                        {
                            //TODO: Release the SPWeb object. Better to use USING block.
                            if (oWeb != null)
                            {
                                SPList oList = oWeb.Lists[IdeationConstant.IdeaSiteListNames.PHASES];
                                string phaseID = SharepointUtil.GetLookupValue(properties.ListItem["Phase"], true);

                                if (oList != null && !string.IsNullOrEmpty(phaseID))
                                {
                                    SPList lstTeam = oWeb.Lists.TryGetList(IdeationConstant.IdeaSiteListNames.TEAM_MEMBERS);
                                    Log.LogMessage("TeamMembers List:" + lstTeam.Title);
                                    if (lstTeam != null)
                                    {
                                        SPQuery query = new SPQuery();
                                        query.Query = @"<Where>
                                                  <Eq>
                                                     <FieldRef Name='Phase'  LookupId='TRUE'/>
                                                     <Value Type='Lookup'>" + phaseID + @"</Value>
                                                  </Eq>
                                               </Where>";

                                        SPListItemCollection collTasks = lstTeam.GetItems(query);
                                        Log.LogMessage("TeamMembers ListItem Coll:" + collTasks.Count);
                                        if (collTasks != null && collTasks.Count > 0)
                                        {
                                            foreach (SPListItem item in collTasks)
                                            {
                                                SPListItem itemToDelete = lstTeam.GetItemById(item.ID);
                                                oWeb.AllowUnsafeUpdates = true;
                                                itemToDelete.Delete();
                                                oWeb.AllowUnsafeUpdates = false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                CommonFunctions.LogError(ex);
                Log.LogMessage("UpdateNavigation DeletePahseTeam method Exception:" + ex.ToString());
            }
        }

        /// <summary>
        /// Deletes phase tasks
        /// </summary>
        /// <param name="properties"></param>
        private void DeletePhaseTasks(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation DeletePhaseTasks method starts");
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
               {
                   using (SPSite oSite = new SPSite(properties.SiteId))
                   {
                       using (SPWeb oWeb = oSite.OpenWeb(properties.RelativeWebUrl))
                       {
                           //TODO: Release the SPWeb object. Better to use USING block.

                           if (oWeb != null)
                           {
                               SPList oList = oWeb.Lists[IdeationConstant.IdeaSiteListNames.PHASES];
                               Log.LogMessage("Phases List:" + oList.Title);
                               string phaseID = SharepointUtil.GetLookupValue(properties.ListItem["Phase"], true);
                               Log.LogMessage("PhaseID" + phaseID);

                               if (oList != null && !string.IsNullOrEmpty(phaseID))
                               {
                                   SPList lstTasks = oWeb.Lists.TryGetList(IdeationConstant.IdeaSiteListNames.IDEA_TASKS);
                                   SPList lstMasterTasks = oWeb.ParentWeb.Lists.TryGetList(IdeationConstant.MasterDataListNames.IdeationMasterTask);

                                   if (lstTasks != null)
                                   {
                                       SPQuery query = new SPQuery();
                                       query.Query = @"<Where>
                                                  <Eq>
                                                     <FieldRef Name='Phase'  LookupId='TRUE'/>
                                                     <Value Type='Lookup'>" + phaseID + @"</Value>
                                                  </Eq>
                                               </Where>";
                                       try
                                       {
                                           if (lstMasterTasks != null)
                                           {
                                               SPListItemCollection collMatsterTasks = lstMasterTasks.GetItems(query);
                                               Log.LogMessage("IdeationMasterTask ListItem Count:" + collMatsterTasks.Count);
                                               if (collMatsterTasks != null && collMatsterTasks.Count > 0)
                                               {
                                                   foreach (SPListItem masterItem in collMatsterTasks)
                                                   {
                                                       SPFolderCollection oAttachFolder = oWeb.ParentWeb.Folders["Lists"].SubFolders[
                                                   IdeationConstant.MasterDataListNames.IdeationMasterTask].SubFolders["Attachments"].SubFolders;

                                                       foreach (SPFolder oFolder in oAttachFolder)
                                                       {
                                                           if (oFolder.Name == masterItem["ID"].ToString())
                                                           {
                                                               foreach (SPFile oFile in oFolder.Files)
                                                               {
                                                                   oWeb.AllowUnsafeUpdates = true;
                                                                   oWeb.Folders[IdeationConstant.IdeaSiteListNames.DOCUMENTS].Files.Delete(oFile.Name);
                                                                   oWeb.AllowUnsafeUpdates = false;
                                                               }

                                                               break;
                                                           }
                                                       }
                                                   }
                                               }
                                           }

                                       }
                                       catch (Exception ex)
                                       {
                                           Log.LogMessage("IdeationMasterTask ListItem Exception:" + ex.ToString());
                                           CommonFunctions.LogError(ex);
                                       }

                                       SPListItemCollection collTasks = lstTasks.GetItems(query);

                                       if (collTasks != null && collTasks.Count > 0)
                                       {
                                           Log.LogMessage("Tasks ListItem Not null");
                                           foreach (SPListItem item in collTasks)
                                           {
                                               SPListItem itemToDelete = lstTasks.GetItemById(item.ID);
                                               oWeb.AllowUnsafeUpdates = true;
                                               itemToDelete.Delete();
                                               oWeb.AllowUnsafeUpdates = false;
                                           }
                                       }
                                   }
                               }
                           }
                       }
                   }
               });
            }
            catch (Exception ex)
            {
                CommonFunctions.LogError(ex);
                Log.LogMessage("UpdateNavigation DeletePhaseTasks method Exception:" + ex.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        private void UpdateNavNodes(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation UpdateNavNodes method starts");
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite oSite = new SPSite(properties.SiteId))
                    {
                        SPWeb oWeb = null;

                        lock (NavigationLock)
                        {
                            try
                            {
                                int ideaId = -1;
                                oWeb = oSite.OpenWeb(properties.RelativeWebUrl);
                                oWeb.AllowUnsafeUpdates = true;

                                IdeationDataSet drIdea = IdeaExec.GetIdeaBySiteUrl(oWeb.ServerRelativeUrl.ToString());

                                if (drIdea.Tables["Idea"].Rows.Count > 0)
                                {
                                    ideaId = Int32.Parse(drIdea.Tables["Idea"].Rows[0]["IdeaID"].ToString());
                                }

                                SPNavigationNodeCollection oNavNodeColl = oWeb.Navigation.QuickLaunch;

                                SPList oList = oWeb.Lists[IdeationConstant.IdeaSiteListNames.PHASES];
                                SPQuery oQuery = new SPQuery();
                                oQuery.Query = "<OrderBy><FieldRef Name='ID' /></OrderBy>"; ;
                                SPListItemCollection collListItems = oList.GetItems(oQuery);
                                Log.LogMessage("Phases ListItem Count:" + collListItems.Count);
                                for (int Count = oNavNodeColl.Count - 1; Count >= 0; Count--)
                                {
                                    if (!oNavNodeColl[Count].Title.Contains("Additional Pages"))
                                        oNavNodeColl[Count].Delete();
                                    else
                                    {
                                        SPNavigationNode oLibNode = oNavNodeColl[Count];
                                        oLibNode.Properties["UrlFragment"] = "";
                                        oLibNode.Properties["NodeType"] = "Heading";
                                        oLibNode.Properties["BlankUrl"] = "True";
                                        oLibNode.Update();
                                    }
                                }

                                SPNavigationNode oNode = new SPNavigationNode("Phases", "");
                                oWeb.Navigation.QuickLaunch.AddAsFirst(oNode);

                                oNode.Properties["UrlFragment"] = "";
                                oNode.Properties["NodeType"] = "Heading";
                                oNode.Properties["BlankUrl"] = "True";
                                oNode.Update();

                                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("collListItems Count: " + collListItems.Count);
                                foreach (SPListItem oListItem in collListItems)
                                {

                                    if (oListItem.File.LockedByUser == null)
                                    {  
                                        DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("oListItem.File.Level: " + oListItem.File.Level);
                                        string PhaseName = Convert.ToString(oListItem["Phase"]);
                                        if (!string.IsNullOrEmpty(PhaseName))
                                        {
                                            PhaseName = PhaseName.Substring(PhaseName.IndexOf("#") + 1);
                                            DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("File PhaseName: " + PhaseName);                                            
                                            SPNavigationNode subMenuItem = new SPNavigationNode(PhaseName, oWeb.ServerRelativeUrl + "/" + oListItem.Url, true);
                                            oNavNodeColl[0].Children.AddAsLast(subMenuItem);
                                        }
                                    }
                                }

                                PublishingWeb pubWeb = PublishingWeb.GetPublishingWeb(oWeb);
                                pubWeb.Navigation.InheritCurrent = false;
                                List<PublishingPage> pages = new List<PublishingPage>();

                                foreach (PublishingPage page in pubWeb.GetPublishingPages())
                                {
                                    if (page.IncludeInCurrentNavigation)
                                        pages.Add(page);
                                }
                                pubWeb.Update();
                                pubWeb.Close();
                            }
                            catch (Exception ex)
                            {
                                
                            }
                            finally
                            {
                                if (oWeb != null)
                                    oWeb.Dispose();
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {                
                throw ex;
            }
            finally
            {

            }
        }

        /// <summary>
        /// Deletes phase inforamation from database
        /// </summary>
        /// <param name="properties"></param>
        private void DeletePhaseRecords(SPItemEventProperties properties)
        {
            Log.LogMessage("UpdateNavigation DeletePhaseRecords method starts");
            try
            {
                string preaviousPhaseID = string.Empty;
                IdeationDataSet drIdea = IdeaExec.GetIdeaBySiteUrl(properties.Web.ServerRelativeUrl.ToString());

                if (drIdea.Tables["Idea"].Rows.Count > 0)
                {
                    int currentPhaseID = int.Parse(properties.Web.Properties["CurrentPhaseID"]);
                    int ideaId = Int32.Parse(drIdea.Tables["Idea"].Rows[0]["IdeaID"].ToString());

                    if (properties.List.GetItems().Count > 0)
                    {
                        Log.LogMessage("Propereties ListItem not null");
                        //not first phase
                        DataSet dsPhase = PhaseExec.DeletePhase(ideaId, currentPhaseID);

                        if (dsPhase.Tables["Phase"].Rows.Count > 0)
                        {
                            preaviousPhaseID = dsPhase.Tables["Phase"].Rows[0]["PhaseID"].ToString();

                            #region update current phaseID property bag

                            //update currentphaseid value = previous phase idea.
                            if (!string.IsNullOrEmpty(preaviousPhaseID))
                            {
                                SPSecurity.RunWithElevatedPrivileges(delegate()
                                {
                                    using (SPSite oSiteImp = new SPSite(properties.SiteId))
                                    {
                                        using (SPWeb oWebImp = oSiteImp.OpenWeb(properties.RelativeWebUrl))
                                        {
                                            oWebImp.Properties["CurrentPhaseID"] = preaviousPhaseID.ToString();
                                            oWebImp.AllowUnsafeUpdates = true;
                                            oWebImp.Properties.Update();
                                            oWebImp.AllowUnsafeUpdates = false;
                                        }
                                    }
                                });
                            }

                            #endregion
                        }

                    }
                    else
                    {
                        //first phase
                        IdeaExec.DeleteIdea(ideaId);

                        //delete idea site
                        SPSecurity.RunWithElevatedPrivileges(delegate()
                        {
                            using (SPSite oSiteImp = new SPSite(properties.SiteId))
                            {
                                using (SPWeb oWebImp = oSiteImp.OpenWeb(properties.RelativeWebUrl))
                                {
                                    oWebImp.Delete();
                                }
                            }
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                CommonFunctions.LogError(ex);
                Log.LogMessage("UpdateNavigation DeletePhaseRecords method Exception:" + ex.ToString());
            }
        }

        #endregion
    }
}
