using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataLan.InnovaOPN.Ideation.Dataset;
using System.Data.Common;
using DataLan.InnovaOPN.Ideation.Dataset;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.EnterpriseLibrary.Common;
using System.Data;

namespace IGEventHandlers
{
    class IGDBSynchExec
    {
       
            /// <summary>
            /// 
            /// </summary>
            /// <param name="whereCondition"></param>
            /// <param name="orderByExpression"></param>
            /// <param name="dsFormFields"></param>
            /// <returns>FormsDataset</returns>
            public static FormsDataset GetFormFields(string whereCondition, string orderByExpression)
            {                
                FormsDataset dsFormFields = null;
                try
                {
                    dsFormFields = new FormsDataset();
                    GetFormFields(whereCondition, orderByExpression, dsFormFields);
                    return dsFormFields;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            public static IdeationDataSet GetIdeaBySiteUrl(string SiteUrl)
            {
                IdeationDataSet dsIdea = null;
                try
                {
                    dsIdea = new IdeationDataSet();

                    Database oDb = DatabaseFactory.CreateDatabase("IdeationConnectionString");

                    DbCommand dbCommand = oDb.GetStoredProcCommand("usp_Idea_SelectBySiteUrl");

                    oDb.AddInParameter(dbCommand, "SiteUrl", DbType.String, SiteUrl);

                    oDb.LoadDataSet(dbCommand, dsIdea, "Idea");

                    return dsIdea;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            public static bool UpdateFormData(int IdeaID, int FieldID, string value)
            {
                Database oDb = DatabaseFactory.CreateDatabase("IdeationConnectionString");
                using (DbConnection oDbCon = oDb.CreateConnection())
                {
                    oDbCon.Open();
                    DbTransaction oTrans = oDbCon.BeginTransaction();

                    try
                    {
                        DbCommand oDbCommand = oDb.GetStoredProcCommand("usp_DBSynchIdeaFieldValues_Insert");

                        oDb.AddInParameter(oDbCommand, "IdeaID", DbType.Int64, IdeaID);
                        oDb.AddInParameter(oDbCommand, "FieldID", DbType.Int32, FieldID);

                        if (string.IsNullOrEmpty(value))
                            oDb.AddInParameter(oDbCommand, "Value", DbType.String, null);
                        else
                            oDb.AddInParameter(oDbCommand, "Value", DbType.String, value);

                        oDb.ExecuteDataSet(oDbCommand, oTrans);

                        oTrans.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        oTrans.Rollback();
                        return false;


                    }
                    finally
                    {
                        oDbCon.Close();
                    }
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="whereCondition"></param>
            /// <param name="orderByExpression"></param>
            /// <returns>FormsDataset</returns>
            public static void GetFormFields(string whereCondition, string orderByExpression, FormsDataset dsFormFields)
            {
                try
                {
                    Database oDb = DatabaseFactory.CreateDatabase("IdeationConnectionString");

                    DbCommand dbCommand = oDb.GetStoredProcCommand("usp_FormFields_SelectByCriteria");

                    oDb.AddInParameter(dbCommand, "WhereCondition", DbType.String, whereCondition);

                    if (orderByExpression == null)
                        oDb.AddInParameter(dbCommand, "OrderByExpression", DbType.String, null);
                    else
                        oDb.AddInParameter(dbCommand, "OrderByExpression", DbType.String, orderByExpression);

                    oDb.LoadDataSet(dbCommand, dsFormFields, "FormFields");

                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
    }
