using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataLan.InnovaOPN.Ideation.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;
using Microsoft.SharePoint;

namespace IGEventHandlers
{
    public class ProcessDBSynch : SPItemEventReceiver
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemAdded(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessDBSynch ItemAdded Method starts");
            SPWeb sbIdeation = null;
            string phaseID = string.Empty;
            string PhaseName = string.Empty;
            try
            {
                using (SPWeb spWeb = properties.OpenWeb())
                {
                    SPSecurity.RunWithElevatedPrivileges(delegate()
                    {
                        using (SPSite iSite = new SPSite(properties.WebUrl))
                        {
                            using (SPWeb iWeb = iSite.OpenWeb())
                            {
                                DBSynchActions.DBSynchUpdate(properties, iSite, iWeb);
                            }
                        }
                    });

                }
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("Exception at ProcessDBSynch ItemAdded");
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessDBSynch ItemAdded Method Exception:" + ex.ToString());
            }
            finally
            {
                if (sbIdeation != null)
                    sbIdeation.Dispose();
            }
        }
        /// <summary>
        /// </summary>
        /// <param name="properties"></param>
        public override void ItemUpdated(SPItemEventProperties properties)
        {
            Log.LogMessage("ProcessDBSynch ItemAdded Method starts");
            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite iSite = new SPSite(properties.WebUrl))
                    {
                        using (SPWeb iWeb = iSite.OpenWeb())
                        {
                            DBSynchActions.DBSynchUpdate(properties, iSite, iWeb);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogMessage("Exception at ProcessDBSynch ItemUpdated");
                DataLan.InnovaOPN.Ideation.Common.CommonFunctions.LogError(ex, properties);
                Log.LogMessage("ProcessDBSynch ItemUpdated Method exception:" + ex.ToString());
            }
        }
    }
}
