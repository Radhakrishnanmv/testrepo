using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using System.Data;
using DataLan.InnovaOPN.Ideation;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;
using Microsoft.SharePoint.Publishing;
using System.Collections;
using System.Linq;

namespace IGEventHandlers
{
    public class ProcessRole : SPItemEventReceiver
    {
        /// <summary>
        /// This function is used to add the Roles information to the database.
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole ItemAdded method starts");
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Roles)
                {
                    InsertData(properties);
                    UpdateEmailConfigurationChoice(properties);
                }
            }
            catch (Exception ex)
            {
                properties.Cancel = true;
                properties.Status = SPEventReceiverStatus.CancelWithError;
                properties.ErrorMessage = ex.Message.ToString();
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessRole ItemAdded method exception:" + ex.ToString());
            }
        }

        /// <summary>
        /// This function is used to update the Roles information
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdating(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole ItemUpdating method starts");
            base.ItemUpdating(properties);
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Roles)
                {
                    UpdateData(properties);
                }
            }
            catch (Exception ex)
            {
                properties.Cancel = true;
                properties.Status = SPEventReceiverStatus.CancelWithError;
                properties.ErrorMessage = ex.Message.ToString();
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessRole ItemUpdating method exception:" + ex.ToString());
            }
        }

        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole ItemUpdated method starts");
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Roles)
            {
                base.ItemUpdated(properties);
                UpdateEmailConfigurationChoice(properties);
            }
        }

        /// <summary>
        /// This function is used to delete the Roles record in the database
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemDeleting(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole ItemDeleting method starts");
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Roles)
                {
                    DeleteData(properties);
                }
            }
            catch (Exception ex)
            {
                properties.Cancel = true;
                properties.Status = SPEventReceiverStatus.CancelWithError;

                if (ex.Message.Contains("REFERENCE constraint"))
                    properties.ErrorMessage = "Cannot delete this role. The role has been linked with transaction tables. Please check the event log for more information";
                else
                    properties.ErrorMessage = ex.ToString();

                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessRole ItemDeleting method exception:" + ex.ToString());
            }
        }

        public override void ItemDeleted(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole ItemDeleted method starts");
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Roles)
            {
                base.ItemDeleted(properties);
                UpdateEmailConfigurationChoice(properties);
            }
        }

        private bool isListExists(SPWeb oWeb, string listName)
        {
            return oWeb.Lists.Cast<SPList>().Any(list => string.Equals(list.Title, listName));
        }

        private void UpdateEmailConfigurationChoice(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole updateEmailConfigurationChoice method starts");
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite spSite = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb spWeb = spSite.OpenWeb())
                        {
                            SPList listRoles = null;
                            SPList listEConfig = null;

                            if (isListExists(spWeb, "Email Configuration"))
                                listEConfig = spWeb.Lists["Email Configuration"];
                            Log.LogMessage("EmailConfiguration list:" + listEConfig.Title);

                            if (isListExists(spWeb, properties.ListTitle))
                                listRoles = spWeb.Lists[properties.ListTitle];
                            Log.LogMessage("Roles List:" + listRoles.Title);

                            if (listRoles != null && listEConfig != null)
                            {
                                spWeb.AllowUnsafeUpdates = true;
                                SPFieldMultiChoice choiceTo = (SPFieldMultiChoice)listEConfig.Fields["To"];
                                SPFieldMultiChoice choiceCC = (SPFieldMultiChoice)listEConfig.Fields["CC"];

                                choiceTo.Choices.Clear();
                                choiceCC.Choices.Clear();

                                foreach (SPListItem item in listRoles.Items)
                                {
                                    choiceTo.Choices.Add("Role: " + item["Title"].ToString());
                                    choiceCC.Choices.Add("Role: " + item["Title"].ToString());
                                }

                                choiceTo.Update();
                                choiceCC.Update();

                                listEConfig.Update();

                                spWeb.Update();
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// For inserting new item in Database
        /// </summary>
        /// <param name="properties"></param>
        private void InsertData(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole InsertData method starts");
            IdeationDataSet dsIdeation = null;

            try
            {
                dsIdeation = new IdeationDataSet();

                IdeationDataSet.RoleRow drRole = dsIdeation.Role.NewRoleRow();

                drRole.Name = properties.ListItem["Title"].ToString();
                drRole.ID = properties.ListItemId;
                dsIdeation.Role.Rows.Add(drRole);

                RoleExec.InsertData(dsIdeation);

            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessRole InsertData method exception:" + ex.ToString());
                throw ex;
            }
        }



        private void DeleteData(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessRole DeleteData method starts");
            try
            {
                RoleExec.Delete(properties.ListItemId);
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessRole DeleteData method Exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                throw ex;
            }
        }

        /// <summary>
        /// For Updating a existing a item in DataBase
        /// </summary>
        /// <param name="properties"></param>
        private void UpdateData(SPItemEventProperties properties)
        {
            IdeationDataSet dsIdeation = null;
            Log.LogMessage("ProcessRole UpdateData method starts");
            try
            {
                dsIdeation = RoleExec.GetRole(properties.ListItemId);

                if (dsIdeation.Role.Rows.Count > 0)
                {
                    IdeationDataSet.RoleRow drRole = (IdeationDataSet.RoleRow)dsIdeation.Role.Rows[0];
                    drRole.Name = properties.AfterProperties["Title"].ToString();
                    RoleExec.UpdateData(dsIdeation);
                }
                else
                {
                    InsertData(properties);
                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessRole UpdateData method exception:" + ex.ToString());
                throw ex;
            }
        }
    }
}
