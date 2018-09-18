using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;
using DataLan.InnovaOPN.Ideation.Common;
using System.Data;
namespace IGEventHandlers
{
    public class ProcessDocuments : SPItemEventReceiver
    {
        /// <summary>
        /// ItemAdded event 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            try
            {
                Log.LogMessage("ProcessDocuments Item Added method starts");
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite oSite = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb oWeb = oSite.OpenWeb())
                        {
                            //verify if event is trigrd by timer job.
                            if (oWeb.Properties.ContainsKey("DOCUMENTUPLOAD") &&
                                 oWeb.Properties["DOCUMENTUPLOAD"] == "False")
                            {
                                SPListItem itemToupdate = oWeb.Lists[properties.ListTitle].GetItemById(properties.ListItemId);

                                if (itemToupdate != null)
                                {
                                    Log.LogMessage("List Item Not null");
                                    SPFile file = itemToupdate.File;

                                    if (!string.IsNullOrEmpty(file.Name))
                                    {
                                        itemToupdate["Title"] = file.Name;
                                    }
                                    else if (!string.IsNullOrEmpty(file.Title))
                                    {
                                        itemToupdate["Title"] = file.Title;
                                    }

                                    oWeb.AllowUnsafeUpdates = true;
                                    this.EventFiringEnabled = false;
                                    itemToupdate.Update();
                                    this.EventFiringEnabled = true;
                                    oWeb.AllowUnsafeUpdates = false;

                                }
                            }
                        }
                    }
                });

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessDocuments Item Added method Exception: " + ex.ToString());
                CommonFunctions.LogError(ex);
            }

        }
        /// <summary>
        /// ItemUpdated event 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessDocuments Item Updated Method starts");
            int ideaId = -1;
            string category = string.Empty;
            int fieldId = -1;
            IdeationDataSet drIdea = null;
            IdeationDataSet dsFields = null;

            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite oSite = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb oWeb = oSite.OpenWeb())
                        {
                            //verify if event is trigrd by timer job.
                            if (oWeb.Properties.ContainsKey("DOCUMENTUPLOAD") &&
                                 oWeb.Properties["DOCUMENTUPLOAD"] == "False")
                            {
                                //Get idea inforamtion
                                drIdea = IdeaExec.GetIdeaBySiteUrl(oWeb.ServerRelativeUrl);

                                if (drIdea != null)
                                {
                                    Log.LogMessage("Dataset not null");
                                    if (drIdea.Tables["Idea"].Rows.Count > 0)
                                    {
                                        ideaId = Int32.Parse(drIdea.Tables["Idea"].Rows[0]["IdeaID"].ToString());
                                    }

                                    //Get form field configuration from database
                                    FormsDataset dsForm = FormFieldsExec.GetFormFields("FormId=" + 1, null);

                                    if (dsForm.Tables["FormFields"].Rows.Count > 0 && ideaId != -1)
                                    {
                                        Log.LogMessage("FormsDataset not null");
                                        foreach (DataRow row in dsForm.Tables["FormFields"].Rows)
                                        {
                                            //get field id for category field.
                                            if (Convert.ToString(row["FieldName"]) == "Category")
                                            {
                                                fieldId = Convert.ToInt32(row["FieldID"].ToString());
                                                break;
                                            }
                                        }

                                        if (fieldId != -1)
                                        {
                                            //get field value by field id
                                            dsFields = IdeaFieldValuesExec.GetIdeaFieldValues(ideaId, fieldId);

                                            if (dsFields.Tables["IdeaFieldValues"].Rows.Count > 0)
                                            {
                                                Log.LogMessage("IdeaFieldValues Table not null");
                                                category = Convert.ToString(dsFields.Tables["IdeaFieldValues"].Rows[0]["Value"]);
                                            }
                                        }

                                    }

                                    if (!string.IsNullOrEmpty(category))
                                    {
                                        SPListItem item = oWeb.Lists[properties.List.Title].GetItemById(properties.ListItemId);
                                        item[IdeationConstant.SiteColumns.IdeaCategory] = category;
                                        item["Title"] = item.File.Name;
                                        oWeb.AllowUnsafeUpdates = true;
                                        this.EventFiringEnabled = false;
                                        item.Update();
                                        this.EventFiringEnabled = true;
                                        oWeb.AllowUnsafeUpdates = false;
                                    }
                                }
                            }
                        }
                    }

                });

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessDocuments Item Updated Method Exception: " + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
            }
        }
    }
}
