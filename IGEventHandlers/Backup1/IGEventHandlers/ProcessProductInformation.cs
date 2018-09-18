using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.DataAccess;
using DataLan.InnovaOPN.Ideation.Dataset;
using System.Data;


namespace IGEventHandlers
{
    public class ProcessProductInformation : SPItemEventReceiver
    {
        public struct IdeationFieldValue
        {
            public int fieldID;
            public int fieldOptionID;
            public string FieldName;
            public string FieldVaue;
        }

        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessProductInformation ItemUpdated Method starts");
            IdeationDataSet dsIdea = null;
            Dictionary<int, IdeationFieldValue> fieldValue = new Dictionary<int, IdeationFieldValue>();


            try
            {

                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite oSite = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb oWeb = oSite.OpenWeb())
                        {
                            SPListItem item = oWeb.Lists[IdeationConstant.MasterDataListNames.ProductInformation].GetItemById(properties.ListItem.ID);

                            //Get idea inforamtion
                            dsIdea = IdeaExec.GetIdeaBySiteUrl(oWeb.ServerRelativeUrl);

                            //update ideaTitle
                            foreach (DataRow row in dsIdea.Tables["Idea"].Rows)
                            {
                                if (!string.IsNullOrEmpty(Convert.ToString(item["Title"])))
                                {
                                    row["Title"] = Convert.ToString(item["Title"]);
                                }
                            }

                            //get field values 
                            foreach (IdeationDataSet.IdeaRow drIdea in dsIdea.Idea)
                            {
                                //get existing idea field values 
                                dsIdea.IdeaFieldValues.Merge(IdeaFieldValuesExec.GetIdeaFieldValues("IdeaID=" + drIdea.IdeaID, null).IdeaFieldValues);

                                FormsDataset dsForms = new FormsDataset();
                                dsForms.Field.Clear();
                                dsForms.Form.Clear();
                                dsForms.FormFields.Clear();

                                dsForms.Field.Merge(FieldExec.GetField("1=1", null).Field);
                                dsForms.Form.Merge(FormExec.GetForm("1=1", null).Form);
                                dsForms.FormFields.Merge(FormFieldsExec.GetFormFields("1=1", null).FormFields);
                                dsForms.FieldOptions.Merge(FieldOptionsExec.GetFieldOptions("1=1", null).FieldOptions);

                                int key = 0;
                                foreach (FormsDataset.FormFieldsRow drRoutingField in dsForms.FormFields)
                                {
                                    if (item.Fields.ContainsField(drRoutingField.FieldName))
                                    {
                                        IdeationFieldValue fieldValues = new IdeationFieldValue();
                                        fieldValues.FieldName = drRoutingField.FieldName;
                                        fieldValues.FieldVaue = Convert.ToString(item[drRoutingField.FieldName]);
                                        fieldValues.fieldID = drRoutingField.FieldID;

                                        FormsDataset.FieldOptionsRow[] drFldOptions = (FormsDataset.FieldOptionsRow[])dsForms.FieldOptions.Select("FieldID=" + drRoutingField.FieldID);

                                        foreach (FormsDataset.FieldOptionsRow drFldOption in drFldOptions)
                                        {
                                            if (string.Compare(drFldOption.Name, Convert.ToString(item[drRoutingField.FieldName])) == 0)
                                            {
                                                fieldValues.fieldOptionID = drFldOption.FieldOptionsID;
                                            }
                                        }

                                        fieldValue.Add(key, fieldValues);
                                        key++;
                                    }
                                }

                                foreach (DataRow row in dsIdea.IdeaFieldValues.Rows)
                                {
                                    foreach (KeyValuePair<int, IdeationFieldValue> value in fieldValue)
                                    {
                                        if (value.Value.fieldID == int.Parse(row["FieldID"].ToString()))
                                        {
                                            if (value.Value.fieldOptionID != 0)
                                            {
                                                row["Value"] = value.Value.fieldOptionID;
                                            }
                                            else
                                            {
                                                row["Value"] = value.Value.FieldVaue;
                                            }
                                        }
                                    }
                                }
                            }

                            IdeaSystem.SaveData(dsIdea);
                        }
                    }


                });

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessProductInformation ItemUpdated Method Exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
            }
        }
    }
}
