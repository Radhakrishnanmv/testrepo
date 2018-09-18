using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;

namespace IGEventHandlers
{
    public class ProcessDisposition : SPItemEventReceiver
    {
        public override void ItemAdding(SPItemEventProperties properties)
        {
            base.ItemAdding(properties);
            try
            {
                Log.LogMessage("Process Disposition Item Adding Method starts");
                string phaseName = string.Empty;
                string afterlight = Convert.ToString(properties.AfterProperties["Light"]);
                int phaseID = Convert.ToInt32(SharepointUtil.GetLookupValue(properties.AfterProperties["Phase"], true));
                Log.LogMessage("PhaseID:" + phaseID);
                IdeationDataSet dsIdeation = PhaseExec.GetPhase(phaseID);

                foreach (IdeationDataSet.PhaseRow drPhase in dsIdeation.Phase.Rows)
                {
                    phaseName = drPhase.Name;
                }
                //check if entry is valid
                bool isExist = IsValid(properties);

                if (!isExist)
                {
                    properties.Cancel = true;
                    properties.ErrorMessage = "You can not add different Max and Min threshold values for " + afterlight + " light in " + phaseName + " phase";
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("Process Disposition Item Adding Method Exception: " + ex.ToString());
                CommonFunctions.LogError(ex);
            }
        }

        public override void ItemUpdating(SPItemEventProperties properties)
        {
            Log.LogMessage("Process Disposition Item Updating method starts");
            base.ItemUpdating(properties);
            try
            {                
                string phaseName = string.Empty;
                string afterlight = Convert.ToString(properties.AfterProperties["Light"]);
                Log.LogMessage("AfterLight: " + afterlight);
                //get phase id
                int phaseID = Convert.ToInt32(SharepointUtil.GetLookupValue(properties.AfterProperties["Phase"], true));
                Log.LogMessage("PhaseID: " + phaseID);
                //get phase by id
                IdeationDataSet dsIdeation = PhaseExec.GetPhase(phaseID);

                foreach (IdeationDataSet.PhaseRow drPhase in dsIdeation.Phase.Rows)
                {
                    phaseName = drPhase.Name;
                }

                //check if entry is valid
                bool isExist = IsValid(properties);

                if (!isExist)
                {
                    properties.Cancel = true;
                    properties.ErrorMessage = "You can not add different Max and Min threshold values for " + afterlight + " light in " + phaseName + "  phase";
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("Process Disposition Item Updating method Exception: " + ex.ToString());
                CommonFunctions.LogError(ex);
            }

        }

        /// <summary>
        /// Validates disposition entry
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        private bool IsValid(SPItemEventProperties properties)
        {
            Log.LogMessage("Process Disposition IsValid method starts");
            try
            {                
                string phaseID = SharepointUtil.GetLookupValue(properties.AfterProperties["Phase"], true);
                string light = Convert.ToString(properties.AfterProperties["Light"]);
                decimal max = Convert.ToDecimal(properties.AfterProperties["Max"]);
                decimal min = Convert.ToDecimal(properties.AfterProperties["Min"]);

                //get disposition route by phase and light
                StringBuilder oSbQuery = new StringBuilder();
                oSbQuery.Append("<Where>");
                oSbQuery.Append("<And>");
                oSbQuery.Append("<Eq>");
                oSbQuery.Append("<FieldRef Name='Phase'  LookupId='TRUE'/>");
                oSbQuery.Append("<Value Type='Lookup'>{0}</Value>");
                oSbQuery.Append("</Eq>");
                oSbQuery.Append("<Eq>");
                oSbQuery.Append("<FieldRef Name='Light' />");
                oSbQuery.Append("<Value Type='Choice'>{1}</Value>");
                oSbQuery.Append("</Eq>");
                oSbQuery.Append("</And>");
                oSbQuery.Append("</Where>");
                //pass values
                string strQuery = string.Format(oSbQuery.ToString(), phaseID, light);
                Log.LogMessage("Query:" + strQuery);
                //build spquery
                SPQuery spqRouts = new SPQuery();
                spqRouts.Query = strQuery;
                //get items
                SPListItemCollection itemColl = properties.List.GetItems(spqRouts);

                if (itemColl != null && itemColl.Count > 0)
                {
                    Log.LogMessage("ListItemCollection not null");
                    foreach (SPListItem spltRoute in itemColl)
                    {
                        if (spltRoute.ID != properties.ListItemId)
                        {
                            // if max and min values are diffent - cancel property with error 
                            if (Convert.ToDecimal(spltRoute["Max"]) != max ||
                                Convert.ToDecimal(spltRoute["Min"]) != min)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("Process Disposition IsValid method Exception: " + ex.ToString());
                CommonFunctions.LogError(ex);
            }

            return true;
        }
    }
}

