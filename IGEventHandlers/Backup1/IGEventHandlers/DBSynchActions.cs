using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataLan.InnovaOPN.Ideation.Dataset;
using Microsoft.SharePoint;
using System.Data;

namespace IGEventHandlers
{
    class DBSynchActions
    {      
        public static void DBSynchUpdate(SPItemEventProperties properties, SPSite iSite, SPWeb iWeb)
        {
            try
            {
                Log.LogMessage("DBSynchActions DBSynchUpdate Method Starts");
                
                
                SPSite site = new SPSite(iSite.Url);
                SPWeb web = site.OpenWeb();
                
                SPList list = web.Lists.TryGetList("Database Synch");
                Log.LogMessage("DBSynchListName: " + list.Title);
                if (list != null)
                {
                    SPQuery query = new SPQuery();
                    query.Query = string.Format("<Where><Eq><FieldRef Name='Title' /><Value Type='Text'>{0}</Value></Eq></Where>", properties.List.Title);
                    
                    SPListItemCollection coll = list.GetItems(query);
                    Log.LogMessage("ItemCollections: " + coll.Count);
                    if (coll != null && coll.Count > 0)
                    {

                        foreach (SPListItem item in coll)
                        {
                            try
                            {
                                string listName = Convert.ToString(item["Title"]);
                                string columnname = Convert.ToString(item["Check Column"]);
                                string columnValue = Convert.ToString(item["Check Column Value"]);
                                string columntoCopyToDB = Convert.ToString(item["Column to Copy to DB"]);
                                string destinationDBFld = Convert.ToString(item["Destination DB Field"]);


                                if (listName.ToLower() == properties.ListTitle.ToLower())
                                {
                                    if (properties.List.Fields.ContainsField(columnname))
                                    {
                                        if (properties.ListItem[columnname] != null)
                                        {
                                            Log.LogMessage("Properties Columnname not null");
                                            string currentItemvalue = "";
                                            if (properties.ListItem[columnname].ToString().Contains('#'))
                                                currentItemvalue = properties.ListItem[columnname].ToString().Split('#')[1];
                                            else
                                                currentItemvalue = properties.ListItem[columnname].ToString();
                                            if (currentItemvalue.ToLower() == columnValue.ToLower() || columnValue.Contains('*'))
                                            {

                                                IdeationDataSet dsIdeaInfo = IGDBSynchExec.GetIdeaBySiteUrl(iWeb.ServerRelativeUrl);
                                                if (dsIdeaInfo.Idea.Rows.Count > 0)
                                                {
                                                    Log.LogMessage("Dataset is not null");
                                                    IdeationDataSet.IdeaRow drIdea = (IdeationDataSet.IdeaRow)dsIdeaInfo.Idea.Rows[0];

                                                    FormsDataset formFields = IGDBSynchExec.GetFormFields("FormName='List Fields'", " RowOrder, ColumnOrder");
                                                    if (formFields != null)
                                                    {
                                                        DataRow[] formField = formFields.FormFields.Select("FieldName =" + "'" + destinationDBFld + "'");
                                                        Log.LogMessage("FormFields dataset not null");
                                                        if (formField != null)
                                                        {
                                                            try
                                                            {
                                                                string fldId = Convert.ToString(formField[0]["FieldID"]);

                                                                string DBColumnValue = "";
                                                                bool IsDateTimeColumn = false;
                                                                if (columntoCopyToDB.Contains('+'))
                                                                {

                                                                    string[] colToCopy = columntoCopyToDB.Split('+');
                                                                    for (int i = 0; i < colToCopy.Length; i++)
                                                                    {
                                                                        IsDateTimeColumn = false;
                                                                        if (properties.List.Fields[colToCopy[i]].Type == SPFieldType.DateTime)
                                                                            IsDateTimeColumn = true;

                                                                        string val = Convert.ToString(properties.ListItem[colToCopy[i]]);
                                                                        if (val.Contains("#"))
                                                                        {
                                                                            DBColumnValue = val.Split('#')[1];
                                                                        }
                                                                        else
                                                                        {
                                                                            try
                                                                            {
                                                                                if (IsDateTimeColumn)
                                                                                    val = Convert.ToDateTime(val).ToString("MM/dd/yy");
                                                                            }
                                                                            catch (Exception ex)
                                                                            {
                                                                                Log.LogMessage("Exception: " + ex.ToString());
                                                                            }
                                                                        }
                                                                        DBColumnValue += val;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    if (properties.List.Fields[columntoCopyToDB].Type == SPFieldType.DateTime)
                                                                        IsDateTimeColumn = true;

                                                                    string val = Convert.ToString(properties.ListItem[columntoCopyToDB]);
                                                                    if (val.Contains("#"))
                                                                        DBColumnValue = val.Split('#')[1];
                                                                    else
                                                                    {
                                                                        try
                                                                        {
                                                                            if (IsDateTimeColumn)
                                                                                val = Convert.ToDateTime(val).ToString("MM/dd/yy");
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            Log.LogMessage("DateTimeColumn Exception: " + ex.ToString());
                                                                        }
                                                                        DBColumnValue = val;
                                                                    }
                                                                }

                                                                bool IsSuccess = IGDBSynchExec.UpdateFormData(Convert.ToInt32(drIdea.IdeaID), Convert.ToInt32(fldId), DBColumnValue);

                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                Log.LogMessage("FormFields Dataset Exception: " + ex.ToString());
                                                            }
                                                        }
                                                        else
                                                        {
                                                            Log.LogMessage("FormField is null");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Log.LogMessage("FormFields Dataset is null");
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            IdeationDataSet dsIdeaInfo = IGDBSynchExec.GetIdeaBySiteUrl(iWeb.ServerRelativeUrl);
                                            if (dsIdeaInfo.Idea.Rows.Count > 0)
                                            {

                                                Log.LogMessage("Idea Dataset not null");
                                                IdeationDataSet.IdeaRow drIdea = (IdeationDataSet.IdeaRow)dsIdeaInfo.Idea.Rows[0];

                                                FormsDataset formFields = IGDBSynchExec.GetFormFields("FormName='List Fields'", " RowOrder, ColumnOrder");
                                                if (formFields != null)
                                                {
                                                    DataRow[] formField = formFields.FormFields.Select("FieldName =" + "'" + destinationDBFld + "'");

                                                    if (formField != null)
                                                    {
                                                        try
                                                        {
                                                            string fldId = Convert.ToString(formField[0]["FieldID"]);
                                                            Log.LogMessage("Field ID:" + fldId);
                                                            bool IsSuccess = IGDBSynchExec.UpdateFormData(Convert.ToInt32(drIdea.IdeaID), Convert.ToInt32(fldId), null);
                                                        }
                                                        catch (Exception ex)
                                                        {                                                            
                                                            throw ex;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Log.LogMessage("Properties Does not contain Column Name");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.LogMessage("List Item Exception: " + ex.ToString());
                            }
                        }
                    }
                    else
                    {
                        Log.LogMessage("List Item Collection Null");
                    }
                        
                }
                else
                {
                    Log.LogMessage("List not Found");
                }

            }
            catch (Exception ex)
            {
                Log.LogMessage("DBSynchActions DBSynchUpdate Exception: " + ex.ToString());
            }
          
        }
    }
}
  