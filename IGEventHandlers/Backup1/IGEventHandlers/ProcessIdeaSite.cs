using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SharePoint;
using DataLan.InnovaOPN.Ideation.Dataset;
using DataLan.InnovaOPN.Ideation.DataAccess;
using DataLan.InnovaOPN.Ideation.Common;

namespace IGEventHandlers
{
    public class ProcessIdeaSite : SPWebEventReceiver
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        public override void WebDeleting(SPWebEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaSite WebDeleting Method starts");
            try
            {                
                int ideaId = -1;
                SPSecurity.RunWithElevatedPrivileges(delegate
                {
                    SPWeb oWeb = properties.Web;
                    if (oWeb != null)
                    {
                        Log.LogMessage("Web not null");
                        IdeationDataSet drIdea = IdeaExec.GetIdeaBySiteUrl(oWeb.ServerRelativeUrl.ToString());
                        if (drIdea.Tables["Idea"].Rows.Count > 0)
                        {
                            Log.LogMessage("IdeaTable count not null");
                            ideaId = Int32.Parse(drIdea.Tables["Idea"].Rows[0]["IdeaID"].ToString());

                            if (ideaId != -1)
                            {
                                IdeaExec.DeleteIdea(ideaId);
                            }
                        }
                    }
                    if (oWeb != null)
                    {
                        oWeb.Dispose();
                    }

                });

                //delete purhase orders from My Po list---Conair crisp
                DeleteMyPo(properties);
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessIdeaSite WebDeleting Method Exception:" + ex.ToString());
                CommonFunctions.LogError(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="properties"></param>
        private void DeleteMyPo(SPWebEventProperties properties)
        {
            Log.LogMessage("ProcessIdeaSite DeleteMyPo method starts");
            SPWeb rootWeb = null;
            try
            {
                
                SPSecurity.RunWithElevatedPrivileges(delegate()
            {
                using (SPSite site = new SPSite(properties.Web.Url))
                {
                    using (SPWeb web = site.OpenWeb())
                    {   
                        rootWeb = web.ParentWeb.ParentWeb;

                        SPList lstMyPo = rootWeb.Lists.TryGetList("My PO");
                        Log.LogMessage("List Name:"+ lstMyPo.Title);
                        if (lstMyPo != null)
                        {
                            List<SPListItem> lstSitePos = lstMyPo.Items.Cast<SPListItem>().Where(x => Convert.ToString(x["Job URL"]).ToLower().Contains(web.Url.ToLower())).ToList();

                            if (lstSitePos.Count > 0)
                            {
                                foreach (SPListItem item in lstSitePos)
                                {
                                    //delete item
                                    web.AllowUnsafeUpdates = true;
                                    lstMyPo.GetItemById(item.ID).Delete();
                                }
                            }

                        }
                    }
                }
            });
            }
            catch (Exception ex)
            {
                Log.LogMessage("ProcessIdeaSite DeleteMyPo method Exception: " + ex.ToString());
                CommonFunctions.LogError(ex);
            }
            finally
            {
                if (rootWeb != null)
                    rootWeb.Dispose();
            }
        }

    }
}