using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;

namespace IGEventHandlers
{
    public class ProcessIteration : SPItemEventReceiver
    {
        /// <summary>
        /// Item ading event handler
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdding(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIteration ItemAdding method starts");
            string errorMessage = string.Empty;
            try
            {
                string iterationPrefix = Convert.ToString(properties.AfterProperties[IdeationConstant.SiteColumns.COL_INTERNAL_TITLE]);
                //verify if iteration is 2 characters only
                if (iterationPrefix.Length > 2)
                {
                    errorMessage = "Iteration # should be 2 characters only";
                }

                //The entered # needs to be alphabets excluding A, B, F and R
                if (iterationPrefix.ToLower().Contains("a") ||
                    iterationPrefix.ToLower().Contains("b") ||
                    iterationPrefix.ToLower().Contains("f") ||
                    iterationPrefix.ToLower().Contains("r"))
                {
                    errorMessage = "Invalid Iteration #";
                }

                //verify for duplicate iteration # 
                SPListItemCollection itemColl = properties.List.Items;
                string iterationNo = properties.Web.Title.Split(':')[0].Trim() + iterationPrefix;
                var uniqueItems = itemColl.Cast<SPListItem>().Where(x => string.Compare(Convert.ToString(x[IdeationConstant.SiteColumns.COL_INTERNAL_TITLE]), iterationNo, true) == 0);

                if (uniqueItems.Count() > 0)
                {
                    errorMessage = "Iteration # already exist in the list";
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    properties.Cancel = true;
                    properties.ErrorMessage = errorMessage;
                }

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessIteration ItemAdding method exception :" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
            }
        }

        /// <summary>
        /// Item added event receiver
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIteration ItemAdded method starts");
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite site = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb web = site.OpenWeb())
                        {
                            //get trails list
                            SPList lstTrails = web.Lists.TryGetList(IdeationConstant.IdeaSiteListNames.Iteration);
                            Log.LogMessage("List: " + lstTrails.Title);
                            if (lstTrails != null)
                            {
                                SPListItem item = lstTrails.GetItemById(properties.ListItemId);

                                if (item != null)
                                {
                                    Log.LogMessage("List Item Not Null");
                                    //Update the Iteration # to have R999999XX format. 
                                    string iterationNo = web.Title.Split(':')[0].Trim() + Convert.ToString(item["Title"]);
                                    item["Title"] = iterationNo;

                                    if (lstTrails.Items.Count == 1)
                                    {
                                        //Add default iteration = yes for first item in the list.
                                        item[IdeationConstant.SiteColumns.Default_Iteration] = true;
                                    }
                                    else
                                    {
                                        //Query for any other Iteration item which has Default Iteration=”yes. If found, update its Default iteration=”no”.
                                        if (Convert.ToBoolean(Convert.ToString(item[IdeationConstant.SiteColumns.Default_Iteration])))
                                        {
                                            foreach (SPListItem lstItem in lstTrails.Items)
                                            {
                                                if (lstItem.ID != properties.ListItemId &&
                                                    Convert.ToBoolean(Convert.ToString(item[IdeationConstant.SiteColumns.Default_Iteration])))
                                                {
                                                    lstItem[IdeationConstant.SiteColumns.Default_Iteration] = false;
                                                    web.AllowUnsafeUpdates = true;
                                                    lstItem.Update();
                                                    web.AllowUnsafeUpdates = false;
                                                }
                                            }
                                        }
                                    }

                                    web.AllowUnsafeUpdates = true;
                                    item.Update();
                                    web.AllowUnsafeUpdates = false;

                                    //Add Iteration Class object to SP Persisted object with Iteration #, Phase, Special activity value, TaskType and project URL
                                    DataLan.InnovaOPN.Ideation.Common.BusinessEntities.Iteration iteration = new DataLan.InnovaOPN.Ideation.Common.BusinessEntities.Iteration();

                                    IdeationDataSet drIdea = IdeaExec.GetIdeaBySiteUrl(properties.Web.ServerRelativeUrl);
                                    if (drIdea.Tables["Idea"].Rows.Count > 0)
                                    {
                                        long ideaId = Int32.Parse(drIdea.Tables["Idea"].Rows[0]["IdeaID"].ToString());

                                        if (ideaId != -1)
                                        {
                                            iteration.IdeaID = ideaId;
                                        }
                                    }
                                    iteration.SiteID = properties.Web.ID;
                                    iteration.IdeaSiteUrl = properties.Web.ServerRelativeUrl;
                                    iteration.IterationNo = iterationNo;
                                    iteration.PhaseID = Convert.ToInt32(web.Properties["CurrentPhaseID"]);
                                    iteration.SpecialActivity = SharepointUtil.GetLookupValue(item[IdeationConstant.SiteColumns.SpcialActivities], true);
                                    IterationCommon.UpdateIterationObjects(iteration, site.RootWeb.Site.Url);
                                }
                            }
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessIteration ItemAdded method Exception: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
            }
        }
    }
}
