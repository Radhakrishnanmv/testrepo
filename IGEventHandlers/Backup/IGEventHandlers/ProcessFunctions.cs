using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Common;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.WebPartPages;
using System.Web.UI.WebControls.WebParts;
using System.Globalization;

namespace IGEventHandlers
{
    public class ProcessFunctions : SPItemEventReceiver
    {
        public override void ItemAdded(SPItemEventProperties properties)
         {
            try
            {
                Log.LogMessage("ProcessFunctions ItemAdded method starts");
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Functions)
                {
                    if (!string.IsNullOrEmpty(properties.ListItem["Create Function Site"].ToString()))
                        if (properties.ListItem["Create Function Site"].ToString().Equals("True"))
                            CreateFunctionSite(properties);
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessFunctions ItemAdded method Exception: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
            }
        }

        /// <summary>
        /// This function is used to update the Roles information
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdating(SPItemEventProperties properties)
        {
            try
            {
                Log.LogMessage("ProcessFunctions ItemUpdating method starts");
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Functions)
                {
                    if (!string.IsNullOrEmpty(properties.ListItem["Create Function Site"].ToString()))
                        if (properties.ListItem["Create Function Site"].ToString().Equals("True"))
                            UpdateFunctionSite(properties);
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessFunctions ItemUpdating method Exception: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
            }
        }

        /// <summary>
        /// Updates existing rounting site with new title
        /// </summary>
        /// <param name="BeforeTitle"></param>
        /// <param name="AfterTitle"></param>
        private void UpdateFunctionSite(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessFunctions UpdateFunctionSite method starts");
            SPWeb parentRountingWeb = null;

            String BeforeTitle = "", AfterTitle = "";

            if (properties.ListItem["Title"] != null)
                BeforeTitle = properties.ListItem["Title"].ToString();
            Log.LogMessage("BeforeTitle: " + BeforeTitle);
            if (properties.AfterProperties["Title"] != null)
                AfterTitle = properties.AfterProperties["Title"].ToString();
            Log.LogMessage("AfterTitle: " + AfterTitle);
            //create functional site if it is not created already
            CreateFunctionSite(properties);

            if (BeforeTitle != AfterTitle)
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite spSite = new SPSite(properties.Web.ParentWeb.Url))
                    {
                        using (SPWeb spWeb = spSite.OpenWeb())
                        {
                            parentRountingWeb = spWeb.Webs.Where(x => string.Compare(x.Title, IdeationConstant.Functional_Sites, true) == 0).FirstOrDefault();

                            if (parentRountingWeb != null)
                            {
                                SPWeb newWeb = null;

                                try
                                {
                                    if (parentRountingWeb.GetSubwebsForCurrentUser()[BeforeTitle].Exists)
                                    {
                                        newWeb = parentRountingWeb.GetSubwebsForCurrentUser()[BeforeTitle];
                                        PublishingWeb pubWeb = PublishingWeb.GetPublishingWeb(newWeb);
                                        SPListItemCollection oLstItmColl = pubWeb.PagesList.Items;
                                        Log.LogMessage("ListItem Collections: " + oLstItmColl.Count);
                                        foreach (SPListItem listItem in oLstItmColl)
                                        {
                                            if (PublishingPage.IsPublishingPage(listItem))
                                            {
                                                PublishingPage page = PublishingPage.GetPublishingPage(listItem);
                                                page.CheckOut();
                                                //update category name in the upper left corner at an 18 point font  
                                                string pageContent = string.Format(CultureInfo.InvariantCulture, "<span style='font-size: 18pt'>" + AfterTitle + "</span>");
                                                page.ListItem["Page Content"] = pageContent;
                                                page.Update();
                                                page.CheckIn("");
                                                SPFile pageFile = page.ListItem.File;
                                                pageFile.Publish("");
                                            }
                                        }

                                        UpdateSecurityGroupTitle(spWeb, BeforeTitle, AfterTitle);

                                        UpdateWebPartTitle(newWeb, AfterTitle, BeforeTitle);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.LogMessage("ProcessFunctions UpdateFunctionSite method Exception: " + ex.ToString());
                                    throw ex;
                                }
                                finally
                                {
                                    if (newWeb != null)
                                        newWeb.Dispose();

                                    if (parentRountingWeb != null)
                                        parentRountingWeb.Dispose();
                                }
                            }
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Updates category group title
        /// </summary>
        /// <param name="group"></param>
        /// <param name="title"></param>
        private void UpdateSecurityGroupTitle(SPWeb web, string beforeTitle, string afterTitle)
        {
            try
            {
                Log.LogMessage("ProcessFunctions UpdateSecurityGroupTitle method starts");
                var categoryGroups = web.SiteGroups.Cast<SPGroup>().Where(x => x.Name.Contains(beforeTitle));

                if (categoryGroups != null)
                {
                    Log.LogMessage("CategoryGroups not null");
                    foreach (var group in categoryGroups)
                    {
                        SPGroup spGroup = web.SiteGroups.GetByID(group.ID);
                        if (spGroup != null)
                        {
                            spGroup.Name = spGroup.Name.Replace(beforeTitle, afterTitle);
                            web.AllowUnsafeUpdates = true;
                            spGroup.Update();
                            web.AllowUnsafeUpdates = false;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessFunctions UpdateSecurityGroupTitle method Exception:" + ex.ToString());
                CommonFunctions.LogError(ex);
            }
        }

        /// <summary>
        /// This method updates webpart title in functional sites and updates webpart peroprties
        /// </summary>
        /// <param name="newWeb"></param>
        /// <param name="functionName"></param>
        private void UpdateWebPartTitle(SPWeb newWeb, string functionName, string beforeTitle)
        {
            Log.LogMessage("ProcessFunctions UpdateWebPartTitle method starts");
            using (SPSite subSite = new SPSite(newWeb.Url))
            {
                using (SPWeb subWeb = subSite.OpenWeb())
                {

                    if (isListExists(subWeb, IdeationConstant.IdeaSiteListNames.SitePagesFunctions))
                    {
                        SPFile file = subWeb.GetFile(subWeb.ServerRelativeUrl + "/SitePages/default.aspx");
                        subWeb.AllowUnsafeUpdates = true;

                        file.CheckOut();
                        subWeb.Title = functionName;

                        SPLimitedWebPartManager coll = file.GetLimitedWebPartManager(PersonalizationScope.Shared);

                        for (int i = 0; i < coll.WebParts.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(beforeTitle))
                            {
                                if (coll.WebParts[i].Title.ToLower().Contains(beforeTitle))
                                {
                                    coll.WebParts[i].Title = coll.WebParts[i].Title.Replace(beforeTitle, functionName);
                                }
                            }
                            else
                            {
                                if (coll.WebParts[i].Title.ToLower().EndsWith("for"))
                                {
                                    //append function name in webpart title
                                    coll.WebParts[i].Title = coll.WebParts[i].Title + " " + functionName;
                                }
                            }

                            //set up show risk webpart property
                            System.Reflection.PropertyInfo pinRiskProperty = coll.WebParts[i].GetType().GetProperty("ShowRisk");

                            if (pinRiskProperty != null)
                            {
                                pinRiskProperty.SetValue(coll.WebParts[i], false, null);
                            }

                            //set up function webpart property
                            System.Reflection.PropertyInfo pinProperty = coll.WebParts[i].GetType().GetProperty("Function");

                            if (pinProperty != null)
                                pinProperty.SetValue(coll.WebParts[i], functionName, null);

                            coll.SaveChanges(coll.WebParts[i]);
                        }
                        coll.Web.Dispose();
                        file.CheckIn("");                        
                    }

                    subWeb.Update();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oWeb"></param>
        /// <param name="listName"></param>
        /// <returns></returns>
        private bool isListExists(SPWeb oWeb, string listName)
        {
            return oWeb.Lists.Cast<SPList>().Any(list => string.Equals(list.Title, listName));
        }

        /// <summary>
        /// Creates functinal site for field option
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="category"></param>
        private void CreateFunctionSite(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessFunctions CreateFunctionSite Method starts");
            SPUser spUser = null;
            UInt32 nLocalID = Convert.ToUInt32(1033);
            SPWeb parentFunctinonalWeb = null;           

            SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                using (SPSite spSite = new SPSite(properties.Web.ParentWeb.Url))
                {
                    using (SPWeb spWeb = spSite.OpenWeb())
                    {
                        parentFunctinonalWeb = spWeb.Webs.Where(x => string.Compare(x.Title, IdeationConstant.Functional_Sites, true) == 0).FirstOrDefault();

                        if (parentFunctinonalWeb == null)
                        {
                            Log.LogMessage("Web is null");
                            CreateFunctionalRootSite(spWeb);
                            parentFunctinonalWeb = spWeb.Webs.Where(x => string.Compare(x.Title, IdeationConstant.Functional_Sites, true) == 0).FirstOrDefault();
                        }

                        if (parentFunctinonalWeb != null)
                        {
                            Log.LogMessage("Web is not null");
                            SPWeb rountingWeb = null;
                            spUser = spWeb.CurrentUser;

                            try
                            {
                                string functionName = Convert.ToString(properties.ListItem["Title"]);

                                parentFunctinonalWeb.AllowUnsafeUpdates = true;

                                int count = parentFunctinonalWeb.GetSubwebsForCurrentUser().Count(p => String.Compare(p.Title, functionName) == 0);

                                if (count == 0)
                                {
                                    SPWebTemplateCollection webTemplates = spSite.RootWeb.GetAvailableWebTemplates(nLocalID);

                                    SPWebTemplate oNewIdeaSiteTemplate = (from SPWebTemplate t in webTemplates
                                                                          where (t.Title.ToLower() == "functional sites" || t.Name.ToLower() == "functional sites")
                                                                          select t).FirstOrDefault();
                                    if (oNewIdeaSiteTemplate == null)
                                        throw new Exception("Site template not found");

                                    rountingWeb = parentFunctinonalWeb.Webs.Add(functionName, functionName, "", 1033, oNewIdeaSiteTemplate, false, false);

                                    if (!spWeb.SiteGroups.Cast<SPGroup>().Any(group => string.Equals(group.Name, "Function_" + functionName)))
                                    {
                                        spWeb.SiteGroups.Add("Function_" + functionName, (SPMember)spUser, spUser, "");
                                    }

                                    rountingWeb.AllowUnsafeUpdates = true;

                                    rountingWeb.Navigation.UseShared = true;

                                    SharepointUtil.SetInheritsMasterPage(ref rountingWeb);
                                    rountingWeb.Update();

                                    //get pages library
                                    SPList lstPages = rountingWeb.Lists.TryGetList("Site Pages");
                                    //Fixing Error: The Site Is Not Valid The ‘Pages’ Document Library is Missing in SharePoint 2010.
                                    Guid uniqueID = lstPages.ID;
                                    rountingWeb.Properties["__PagesListId"] = uniqueID.ToString();
                                    rountingWeb.Properties.Update();
                                    rountingWeb.Update();

                                    SPFile spDefaultFile = rountingWeb.GetFile(rountingWeb.Url + "/SitePages/default.aspx");

                                    SPFolder oFolder = rountingWeb.RootFolder;
                                    oFolder.WelcomePage = spDefaultFile.ToString();
                                    oFolder.Update();
                                    rountingWeb.Dispose();
                                    UpdateWebPartTitle(rountingWeb, functionName, null);

                                    //get management group for category
                                    SPGroup functionalGroup = spWeb.SiteGroups.Cast<SPGroup>().Where(group => string.Equals(group.Name, "Function_" + functionName)).FirstOrDefault();

                                    if (functionalGroup != null)
                                    {
                                        Log.LogMessage("Functional Group not null");
                                        //commiting uncommeted changes
                                        parentFunctinonalWeb.Update();
                                        //add read permissions to the root rounting site
                                        DataLan.InnovaOPN.Ideation.Common.SharepointUtil.AddGroupToCategorySite(functionalGroup.Name, DataLan.InnovaOPN.Ideation.Common.IdeationConstant.PermissionsLevel.READ, parentFunctinonalWeb);
                                        //add contribute permissions to the management site
                                        DataLan.InnovaOPN.Ideation.Common.SharepointUtil.AddGroupToCategorySite(functionalGroup.Name, DataLan.InnovaOPN.Ideation.Common.IdeationConstant.PermissionsLevel.CONTRIBUTE, rountingWeb);

                                        //add contribute permissions to the management site
                                        DataLan.InnovaOPN.Ideation.Common.SharepointUtil.AddGroupToCategorySite(DataLan.InnovaOPN.Ideation.Common.IdeationConstant.SecurityGroups.INNOVATION_MANAGEMENT_TEAM, DataLan.InnovaOPN.Ideation.Common.IdeationConstant.PermissionsLevel.CONTRIBUTE, parentFunctinonalWeb);

                                        //add contribute permissions to the management site
                                        DataLan.InnovaOPN.Ideation.Common.SharepointUtil.AddGroupToCategorySite(DataLan.InnovaOPN.Ideation.Common.IdeationConstant.SecurityGroups.INNOVATION_MANAGEMENT_TEAM, DataLan.InnovaOPN.Ideation.Common.IdeationConstant.PermissionsLevel.CONTRIBUTE, rountingWeb);

                                    }

                                }
                            }
                            catch (Exception ex)
                            {
                                Log.LogMessage("ProcessFunctions CreateFunctionSite Method Exception: " + ex.ToString());
                                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
                                throw ex;
                            }
                            finally
                            {
                                if (rountingWeb != null)
                                    rountingWeb.Dispose();

                                if (parentFunctinonalWeb != null)
                                    parentFunctinonalWeb.Dispose();
                            }
                        }

                    }
                }
            });
        }

        /// <summary>
        /// Cretates parent site for functions
        /// </summary>
        /// <param name="dsForms"></param>
        private void CreateFunctionalRootSite(SPWeb rootWeb)
        {
            Log.LogMessage("ProcessFunctions CreateFunctionalRootSite method starts");
            SPWeb newWeb = null;
            try
            {                
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {

                    //check if category site already exist
                    int count = rootWeb.GetSubwebsForCurrentUser().Count(p => String.Compare(p.Title, IdeationConstant.Functional_Sites, true) == 0);

                    if (count == 0)
                    {
                        UInt32 nLocalID = Convert.ToUInt32(1033);
                        string description = "Functions parent site";

                        SPWebTemplateCollection webTemplates = rootWeb.GetAvailableWebTemplates(nLocalID);

                        //get category root site template
                        SPWebTemplate oNewIdeaSiteTemplate = (from SPWebTemplate t in webTemplates
                                                              where (t.Title.ToLower() == "function" || t.Name.ToLower() == "function")
                                                              select t).FirstOrDefault();

                        newWeb = rootWeb.Webs.Add(IdeationConstant.Functional_Sites, IdeationConstant.Functional_Sites, description, nLocalID, oNewIdeaSiteTemplate, false, false);

                        //TODO: Release the SPWeb Object. Better use USING block.

                        newWeb.AllowUnsafeUpdates = true;
                        newWeb.Navigation.UseShared = true;
                        //set IO master page
                        SharepointUtil.SetInheritsMasterPage(ref newWeb);
                        newWeb.Update();

                        //get pages library
                        SPList lstPages = newWeb.Lists.TryGetList("SitePages");
                        if (lstPages != null)
                        {
                            //Fixing Error: The Site Is Not Valid The ‘Pages’ Document Library is Missing in SharePoint 2010.
                            Guid uniqueID = lstPages.ID;
                            newWeb.Properties["__SitePagesListId"] = uniqueID.ToString();
                            newWeb.Properties.Update();
                            newWeb.Update();
                        }

                        try
                        {
                            PublishingWeb publishingWeb = PublishingWeb.GetPublishingWeb(newWeb);
                            // Global Navigation 
                            //Show Subsites 
                            publishingWeb.Navigation.GlobalIncludeSubSites = true;

                            // Maximum number of dynamic items to show within this level of navigation: 
                            publishingWeb.Navigation.GlobalDynamicChildLimit = 60;

                            //Update the changes
                            publishingWeb.Update();
                        }
                        catch (Exception ex)
                        {
                            CommonFunctions.LogError(ex);
                            Log.LogMessage("Global Navigation Exception:" + ex.ToString());
                        }



                        //set default page
                        SPFile spDefaultFile = newWeb.GetFile(newWeb.Url + "/SitePages/default.aspx");
                        SPFolder oFolder = newWeb.RootFolder;
                        oFolder.WelcomePage = spDefaultFile.ToString();
                        oFolder.Update();
                        newWeb.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessFunctions CreateFunctionalRootSite method Exception: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex);
            }
            finally
            {
                newWeb.Dispose();
            }
        }
    }
}
