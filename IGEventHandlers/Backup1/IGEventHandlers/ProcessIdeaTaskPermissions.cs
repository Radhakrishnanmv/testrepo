using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using System.Data;
using DataLan.InnovaOPN.Ideation.DataAccess;
using System.Xml;

namespace IGEventHandlers
{
    public class ProcessIdeaTaskPermissions : SPItemEventReceiver
    {
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTaskPermissions ItemAdded method starts");
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
            {
                Log.LogMessage("properties.ListTitle :" + Convert.ToString(properties.ListTitle));
                Log.LogMessage("Tasks :" + Convert.ToString(properties.ListTitle) + "==" + IdeationConstant.IdeaSiteListNames.IDEA_TASKS);
                try
                {
                   
                    //this.EventFiringEnabled = false;
                    SetItemPermissionLevel(properties);

                    //if manually added
                    if (!SharepointUtil.RunWithHandler)
                    {
                        SPSecurity.RunWithElevatedPrivileges(delegate()
                        {
                            using (SPSite iSite = new SPSite(properties.WebUrl))
                            {
                                using (SPWeb iWeb = iSite.OpenWeb())
                                {
                                    string PhaseName = null;
                                    SPListItem item = iWeb.Lists[properties.ListTitle].GetItemById(properties.ListItemId);

                                    if (!string.IsNullOrEmpty(Convert.ToString(item["Phase"])))
                                    {
                                        Log.LogMessage("List Item not null");
                                        PhaseName = Convert.ToString(item["Phase"]).Split('#')[1];
                                        Actions.UpdateReadPermissionForTeamMembers(iWeb, PhaseName);
                                    }
                                }
                            }
                        });
                    }
                    
                }
                catch (Exception ex)
                {
                    Log.LogMessage("ProcessIdeaTaskPermissions ItemAdded method Exception:" + ex.ToString());
                    DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);

                }
            }

        }

        public static bool DoesPrincipalHasPermissions(SPListItem item, SPPrincipal principal)
        {
            Log.LogMessage("ProcessIdeaTaskPermissions DoesPrincipalHasPermissions method starts");
            SPRoleAssignment roleAssignment = null;
            try
            {
                
                roleAssignment = item.RoleAssignments.GetAssignmentByPrincipal(principal);

                return true;
            }
            catch
            {
                //if the user has no permission on the item (SPPrincipal is not in permissionlist -> item.RoleAssignments is empty), an exception is thrown.
                return false;
            }
           

        }

        private static void SetItemPermissionLevel(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTaskPermissions setItemPermissionLevel method starts");
            SPSecurity.RunWithElevatedPrivileges(delegate
            {
                using (SPSite spSite = new SPSite(properties.WebUrl))
                {
                    Log.LogMessage("properties.WebUrl :" + Convert.ToString(properties.WebUrl));
                    SPWeb spWeb = null;

                    spWeb = spSite.OpenWeb();

                    try
                    {
                        SPListItem spItem = spWeb.Lists[properties.ListId].GetItemById(properties.ListItemId);
                        Log.LogMessage("ADD" + IdeationConstant.SecurityGroups.INNOVATION_MANAGEMENT_TEAM + "Group " + IdeationConstant.PermissionsLevel.CONTRIBUTE + " for the list " + Convert.ToString(properties.ListTitle) + "ListItem starts");
                        AddGroupToListItemRoleAssignment2(IdeationConstant.SecurityGroups.INNOVATION_MANAGEMENT_TEAM,
                            IdeationConstant.PermissionsLevel.CONTRIBUTE, properties.ListTitle, ref spWeb, ref  spItem);


                        Log.LogMessage("ADD" + IdeationConstant.SecurityGroups.INNOVAOPN_GLOBAL_READERS + "Group " + IdeationConstant.PermissionsLevel.READ + " for the list " + Convert.ToString(properties.ListTitle) + "ListItem starts");
                        AddGroupToListItemRoleAssignment2(IdeationConstant.SecurityGroups.INNOVAOPN_GLOBAL_READERS,
                           IdeationConstant.PermissionsLevel.READ, properties.ListTitle, ref spWeb, ref  spItem);

                        if (!String.IsNullOrEmpty(Convert.ToString(properties.AfterProperties["AssignedTo"])))
                        {
                            int AssignedTo = Convert.ToInt32(properties.AfterProperties["AssignedTo"].ToString().Split(';')[0]);

                            if (AssignedTo > 0)
                            {
                                Log.LogMessage("AssignedTo not null");
                                SharepointUtil.AddUserToListItemRoleAssignment(AssignedTo,
                                   IdeationConstant.PermissionsLevel.UPDATEONLY, IdeationConstant.IdeaSiteListNames.IDEA_TASKS, ref spWeb, ref  spItem);

                            }
                        }
                        
                        spWeb.Update();
                    }
                    catch (Exception ex)
                    {
                        CommonFunctions.LogError(ex);
                        Log.LogMessage("ProcessIdeaTaskPermissions SetItemPermissionLevel method exception:" +ex.ToString());
                    }
                    finally
                    {
                        if (spWeb != null)
                            spWeb.Dispose();
                    }
                }
            });
        }

        public static void AddGroupToListItemRoleAssignment2(string group, string sPermissionName, string ListName, ref SPWeb oWeb, ref SPListItem oListItem)
        {
            StringBuilder log = new StringBuilder();
            log.Append("Method AddGroupToListItemRoleAssignment");
            try
            {
                oWeb.AllowUnsafeUpdates = true;

                oListItem.BreakRoleInheritance(false);
                Log.LogMessage("GroupName 2 :" + group.ToString());
                SPGroup spGroup = oWeb.SiteGroups[group];


                if (spGroup != null)
                {
                    Log.LogMessage("spGroup not null 2");
                    SPRoleDefinition spRole = oWeb.RoleDefinitions[sPermissionName];
                    if (spRole != null)
                    {

                        Log.LogMessage("spRole not null 2");
                        SPRoleAssignment roleAssignment = new SPRoleAssignment(spGroup);
                        roleAssignment.RoleDefinitionBindings.Add(spRole);
                        oListItem.RoleAssignments.Add(roleAssignment);
                        oWeb.AllowUnsafeUpdates = false;
                    }

                    else
                    {

                        Log.LogMessage("spRole null 2");
                    }
                }
                else
                {

                    Log.LogMessage("spGroup null 2");
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(log.ToString() + ex.Message);
            }

        }

        public override void ItemUpdating(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaTaskPermissions ItemUpdating Method starts");
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.IdeaSiteListNames.IDEA_TASKS)
            {
                try
                {
                    SPItemEventDataCollection oAfterProperties = properties.AfterProperties;
                    if (oAfterProperties["AssignedTo"] != null)
                    {
                        if (oAfterProperties["Phase"] != null)
                        {
                            String BeforeAssignTo = "";
                            String AfterAssignTo = "";

                            if (properties.ListItem["AssignedTo"] != null)
                                BeforeAssignTo = properties.ListItem["AssignedTo"].ToString();
                            Log.LogMessage("BeforeAssignTO:" + BeforeAssignTo);

                            if (oAfterProperties["AssignedTo"] != null)
                                AfterAssignTo = oAfterProperties["AssignedTo"].ToString();
                            Log.LogMessage("AfterAssignTo:" + AfterAssignTo);

                            if (BeforeAssignTo != "" && BeforeAssignTo.Contains(";#"))
                                BeforeAssignTo = BeforeAssignTo.Split(';')[0];

                            if (AfterAssignTo != BeforeAssignTo)
                            {
                                //  this.EventFiringEnabled = false;
                                SetItemPermissionLevel(properties);
                                // this.EventFiringEnabled = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                    Log.LogMessage("ProcessIdeaTaskPermissions ItemUpdating method exception:" + ex.ToString());
                }
            }
        }
    }
}
