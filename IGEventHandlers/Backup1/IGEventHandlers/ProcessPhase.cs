using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using System.Data;
using DataLan.InnovaOPN.Ideation;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;

namespace IGEventHandlers
{
    public class ProcessPhase : SPItemEventReceiver
    {
        /// <summary>
        /// This event is trigger when added a new item to the List
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase ItemAdded Method starts");
            Log.LogMessage("ListTitle:" + Convert.ToString(properties.ListTitle));
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Phases)
            {
                base.ItemAdded(properties);
                InsertData(properties);
            }
        }

        /// <summary>
        /// This event is trigger when adding a item to the List
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdding(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase ItemAdding method starts");
            Log.LogMessage("ListTitle:" + Convert.ToString(properties.ListTitle));
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Phases)
            {
                base.ItemAdding(properties);
                CheckPreviousPhase(properties);
            }
        }

        /// <summary>
        /// This event is trigger when updating a item to the List
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdating(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase ItemUpdating method starts");
            Log.LogMessage("ListTitle:" + Convert.ToString(properties.ListTitle));
            base.ItemUpdating(properties);
            if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Phases)
            {
                string PreviousPhaseId = properties.ListItem["Previous_x0020_Phase"].ToString().Split(';')[0];
                Log.LogMessage("PreviousPhaseId:" + PreviousPhaseId);
                if (PreviousPhaseId != properties.AfterProperties["Previous_x0020_Phase"].ToString())
                    CheckPreviousPhase(properties);

                if (properties.Cancel != true)
                    UpdateData(properties);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemDeleting(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase ItemDeleting method starts");
            Log.LogMessage("ListTitle:" + Convert.ToString(properties.ListTitle));
            base.ItemDeleting(properties);
            try
            {
                if (Convert.ToString(properties.ListTitle) == IdeationConstant.MasterDataListNames.Phases)
                {
                    Int32 PhaseId = Convert.ToInt32(properties.ListItem["ID"].ToString());

                    PhaseExec.Delete(PhaseId);
                }
            }
            catch (Exception ex)
            {
                properties.Cancel = true;
                properties.ErrorMessage = ex.ToString();
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessPhase ItemDeleting method Exception:" + ex.ToString());
            }
        }

        /// <summary>
        /// For checking the already mapped Phases
        /// </summary>
        /// <param name="properties"></param>
        private void CheckPreviousPhase(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase CheckPreviousPhase Method starts");
            IdeationDataSet dsIdeation = null;

            String PreviousPhase = properties.AfterProperties["Previous_x0020_Phase"].ToString();
            Log.LogMessage("PreviousPhase:" + PreviousPhase);
            try
            {
                if (PreviousPhase.Length == 0)
                    PreviousPhase = "0";

                dsIdeation = PhaseExec.GetPhase("PreviousPhaseID=" + PreviousPhase, null);

                if (dsIdeation.Phase.Rows.Count > 0)
                {
                    //IdeationDataSet.PhaseRow drPhase = dsIdeation.Phase.Select("PhaseID=
                    if (properties.EventType == SPEventReceiverType.ItemAdding)
                    {
                        properties.Cancel = true;
                        properties.ErrorMessage = "The previous phase value must be unique.";
                    }
                    else if (properties.EventType == SPEventReceiverType.ItemUpdating)
                    {
                        int PhaseID = properties.ListItemId;

                        foreach (IdeationDataSet.PhaseRow drPhase in dsIdeation.Phase.Rows)
                        {
                            if (drPhase.PhaseID != PhaseID)
                            {
                                properties.Cancel = true;
                                properties.ErrorMessage = "The previous phase value must be unique.";
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessPhase CheckPreviousPhase Method exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                throw ex;
            }

        }

        /// <summary>
        /// For inserting new item in Database
        /// </summary>
        /// <param name="properties"></param>
        public void InsertData(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase InsertData method starts");
            IdeationDataSet dsIdeation = null;

            try
            {
                dsIdeation = new IdeationDataSet();

                IdeationDataSet.PhaseRow drPhase = dsIdeation.Phase.NewPhaseRow();

                drPhase.Name = properties.ListItem["Title"].ToString();
                drPhase.ProcessID = 1;
                drPhase.PhaseID = properties.ListItemId;
                drPhase.ShowPhaseDate = bool.Parse(properties.ListItem["ShowPhaseDate"].ToString());
                if (properties.ListItem["Previous_x0020_Phase"] == null)
                    drPhase.SetPreviousPhaseIDNull();
                else
                    drPhase.PreviousPhaseID = Convert.ToInt32(properties.ListItem["Previous_x0020_Phase"].ToString().Split(';')[0]);

                dsIdeation.Phase.Rows.Add(drPhase);

                PhaseExec.InsertData(dsIdeation);

            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessPhase InsertData method exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                throw ex;
            }
        }

        /// <summary>
        /// For Updating a existing a item in DataBase
        /// </summary>
        /// <param name="properties"></param>
        public void UpdateData(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessPhase UpdateData method starts");
            IdeationDataSet dsIdeation = null;

            try
            {
                dsIdeation = PhaseExec.GetPhase(properties.ListItemId);

                if (dsIdeation.Phase.Rows.Count > 0)
                {
                    IdeationDataSet.PhaseRow drPhase = (IdeationDataSet.PhaseRow)dsIdeation.Phase.Rows[0];
                    drPhase.Name = properties.AfterProperties["Title"].ToString();
                    drPhase.ProcessID = 1;
                    drPhase.ShowPhaseDate = bool.Parse(properties.AfterProperties["ShowPhaseDate"].ToString());

                    if (properties.AfterProperties["Previous_x0020_Phase"].ToString().Length == 0)
                        drPhase.SetPreviousPhaseIDNull();
                    else
                        drPhase.PreviousPhaseID = Convert.ToInt32(properties.AfterProperties["Previous_x0020_Phase"]);

                    PhaseExec.UpdateData(dsIdeation);
                }
                else
                {
                    InsertData(properties);
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessPhase UpdateData method Exception:" + ex.ToString());
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                throw ex;
            }
        }
    }
}
