﻿/*
 * Copyright 2016 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : ScadaData
 * Summary  : Handy and thread safe access to the client cache data
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2016
 * Modified : 2016
 */

using Scada.Data.Models;
using Scada.Data.Tables;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Utils;

namespace Scada.Client
{
    /// <summary>
    /// Handy and thread safe access to the client cache data
    /// <para>Удобный и потокобезопасный доступ к данным кеша клиентов</para>
    /// </summary>
    public class DataAccess
    {
        /// <summary>
        /// Кеш данных
        /// </summary>
        protected readonly DataCache dataCache;
        /// <summary>
        /// Журнал
        /// </summary>
        protected readonly Log log;


        /// <summary>
        /// Конструктор, ограничивающий создание объекта без параметров
        /// </summary>
        protected DataAccess()
        {
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        public DataAccess(DataCache dataCache, Log log)
        {
            if (dataCache == null)
                throw new ArgumentNullException("dataCache");
            if (log == null)
                throw new ArgumentNullException("log");

            this.dataCache = dataCache;
            this.log = log;
        }


        /// <summary>
        /// Получить кеш данных
        /// </summary>
        public DataCache DataCache
        {
            get
            {
                return dataCache;
            }
        }


        /// <summary>
        /// Получить наименование роли по идентификатору из базы конфигурации
        /// </summary>
        protected string GetRoleNameFromBase(int roleID, string defaultRoleName)
        {
            try
            {
                dataCache.RefreshBaseTables();
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    BaseTables.CheckColumnsExist(baseTables.RoleTable, true);
                    DataView viewRole = baseTables.RoleTable.DefaultView;
                    viewRole.Sort = "RoleID";
                    int rowInd = viewRole.Find(roleID);
                    return rowInd >= 0 ? (string)viewRole[rowInd]["Name"] : defaultRoleName;
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении наименования роли по идентификатору {0}" :
                    "Error getting role name by ID {0}", roleID);
                return defaultRoleName;
            }
        }


        /// <summary>
        /// Получить свойства входного канала по его номеру
        /// </summary>
        public InCnlProps GetCnlProps(int cnlNum)
        {
            try
            {
                dataCache.RefreshBaseTables();

                // необходимо сохранить ссылку, т.к. объект может быть пересоздан другим потоком
                InCnlProps[] cnlProps = dataCache.CnlProps;

                // поиск свойств заданного канала
                int ind = Array.BinarySearch(cnlProps, cnlNum, InCnlProps.IntComp);
                return ind >= 0 ? cnlProps[ind] : null;
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении свойств входного канала {0}" :
                    "Error getting input channel {0} properties", cnlNum);
                return null;
            }
        }

        /// <summary>
        /// Получить свойства канала управления по его номеру
        /// </summary>
        public CtrlCnlProps GetCtrlCnlProps(int ctrlCnlNum)
        {
            try
            {
                dataCache.RefreshBaseTables();

                // необходимо сохранить ссылку, т.к. объект может быть пересоздан другим потоком
                CtrlCnlProps[] ctrlCnlProps = dataCache.CtrlCnlProps;

                // поиск свойств заданного канала
                int ind = Array.BinarySearch(ctrlCnlProps, ctrlCnlNum, CtrlCnlProps.IntComp);
                return ind >= 0 ? ctrlCnlProps[ind] : null;
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении свойств канала управления {0}" :
                    "Error getting output channel {0} properties", ctrlCnlNum);
                return null;
            }
        }

        /// <summary>
        /// Получить свойства статуса входного канала по значению статуса
        /// </summary>
        public CnlStatProps GetCnlStatProps(int stat)
        {
            try
            {
                dataCache.RefreshBaseTables();
                CnlStatProps cnlStatProps;
                return dataCache.CnlStatProps.TryGetValue(stat, out cnlStatProps) ?
                    cnlStatProps : null;
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении цвета по статусу {0}" :
                    "Error getting color by status {0}", stat);
                return null;
            }
        }

        /// <summary>
        /// Привязать свойства входных каналов и каналов управления к элементам представления
        /// </summary>
        public void BindCnlProps(BaseView view)
        {
            try
            {
                dataCache.RefreshBaseTables();
                DateTime baseAge = dataCache.BaseAge;
                if (view != null && view.BaseAge != baseAge && baseAge > DateTime.MinValue)
                {
                    lock (view.SyncRoot)
                    {
                        view.BaseAge = baseAge;
                        view.BindCnlProps(dataCache.CnlProps);
                        view.BindCtrlCnlProps(dataCache.CtrlCnlProps);
                    }
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при привязке свойств каналов к элементам представления" :
                    "Error binding channel properties to the view elements");
            }
        }

        /// <summary>
        /// Получить свойства представления по идентификатору
        /// </summary>
        /// <remarks>Используется таблица объектов интерфейса</remarks>
        public ViewProps GetViewProps(int viewID)
        {
            try
            {
                dataCache.RefreshBaseTables();

                // необходимо сохранить ссылку, т.к. объект может быть пересоздан другим потоком
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    BaseTables.CheckColumnsExist(baseTables.InterfaceTable, true);
                    DataView viewInterface = baseTables.InterfaceTable.DefaultView;
                    viewInterface.Sort = "ItfID";
                    int rowInd = viewInterface.Find(viewID);

                    if (rowInd >= 0)
                    {
                        ViewProps viewProps = new ViewProps(viewID);
                        viewProps.FileName = (string)viewInterface[rowInd]["Name"];
                        string ext = Path.GetExtension(viewProps.FileName);
                        viewProps.ViewTypeCode = ext == null ? "" : ext.TrimStart('.');
                        return viewProps;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении свойств представления по ид.={0}" :
                    "Error getting view properties by ID={0}", viewID);
                return null;
            }
        }

        /// <summary>
        /// Получить права на представления по идентификатору роли
        /// </summary>
        public Dictionary<int, EntityRights> GetViewRights(int roleID)
        {
            Dictionary<int, EntityRights> viewRightsDict = new Dictionary<int, EntityRights>();

            try
            {
                dataCache.RefreshBaseTables();
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    BaseTables.CheckColumnsExist(baseTables.RightTable, true);
                    DataView viewRight = baseTables.RightTable.DefaultView;
                    viewRight.Sort = "RoleID";

                    foreach (DataRowView rowView in viewRight.FindRows(roleID))
                    {
                        int viewID = (int)rowView["ItfID"];
                        EntityRights rights = new EntityRights((bool)rowView["ViewRight"], (bool)rowView["CtrlRight"]);
                        viewRightsDict[viewID] = rights;
                    }
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении прав на представления для роли с ид.={0}" :
                    "Error getting view access rights for the role with ID={0}", roleID);
            }

            return viewRightsDict;
        }

        /// <summary>
        /// Получить права на контент по идентификатору роли
        /// </summary>
        public Dictionary<string, EntityRights> GetContentRights(int roleID)
        {
            Dictionary<string, EntityRights> contentRightsDict = new Dictionary<string, EntityRights>();

            try
            {
                dataCache.RefreshBaseTables();
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    DataTable tblInterface = baseTables.InterfaceTable;
                    DataTable tblRight = baseTables.RightTable;
                    BaseTables.CheckColumnsExist(tblInterface, true);
                    BaseTables.CheckColumnsExist(tblRight, true);
                    DataView viewRight = tblRight.DefaultView;
                    viewRight.Sort = "ItfID, RoleID";

                    foreach (DataRow itfRow in tblInterface.Rows)
                    {
                        int contentTypeID = (int)itfRow["ItfID"];
                        string contentTypeCode = (string)itfRow["Name"];

                        if (string.IsNullOrEmpty(Path.GetExtension(contentTypeCode)))
                        {
                            int rightRowInd = viewRight.Find(new object[] { contentTypeID, roleID });
                            if (rightRowInd >= 0)
                            {
                                DataRowView rightRowView = viewRight[rightRowInd];
                                EntityRights rights = new EntityRights(
                                    (bool)rightRowView["ViewRight"], (bool)rightRowView["CtrlRight"]);
                                contentRightsDict[contentTypeCode] = rights;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении прав на контент для роли с ид.={0}" :
                    "Error getting content access rights for the role with ID={0}", roleID);
            }

            return contentRightsDict;
        }

        /// <summary>
        /// Получить идентификатор пользователя по имени
        /// </summary>
        public int GetUserID(string username)
        {
            try
            {
                username = username ?? "";
                dataCache.RefreshBaseTables();
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    BaseTables.CheckColumnsExist(baseTables.UserTable, true);
                    DataView viewUser = baseTables.UserTable.DefaultView;
                    viewUser.Sort = "Name";
                    int rowInd = viewUser.Find(username);
                    return rowInd >= 0 ? (int)viewUser[rowInd]["UserID"] : BaseValues.EmptyDataID;
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении идентификатора пользователя по имени \"{0}\"" :
                    "Error getting user ID by name \"{0}\"", username);
                return BaseValues.EmptyDataID;
            }
        }

        /// <summary>
        /// Получить наименование объекта по номеру
        /// </summary>
        public string GetObjName(int objNum)
        {
            try
            {
                dataCache.RefreshBaseTables();
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    BaseTables.CheckColumnsExist(baseTables.ObjTable, true);
                    DataView viewObj = baseTables.ObjTable.DefaultView;
                    viewObj.Sort = "ObjNum";
                    int rowInd = viewObj.Find(objNum);
                    return rowInd >= 0 ? (string)viewObj[rowInd]["Name"] : "";
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении наименования объекта по номеру {0}" :
                    "Error getting object name by number {0}", objNum);
                return "";
            }
        }

        /// <summary>
        /// Получить наименование КП по номеру
        /// </summary>
        public string GetKPName(int kpNum)
        {
            try
            {
                dataCache.RefreshBaseTables();
                BaseTables baseTables = dataCache.BaseTables;

                lock (baseTables.SyncRoot)
                {
                    BaseTables.CheckColumnsExist(baseTables.ObjTable, true);
                    DataView viewObj = baseTables.KPTable.DefaultView;
                    viewObj.Sort = "KPNum";
                    int rowInd = viewObj.Find(kpNum);
                    return rowInd >= 0 ? (string)viewObj[rowInd]["Name"] : "";
                }
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении наименования КП по номеру {0}" :
                    "Error getting device name by number {0}", kpNum);
                return "";
            }
        }

        /// <summary>
        /// Получить наименование роли по идентификатору
        /// </summary>
        public string GetRoleName(int roleID)
        {
            string roleName = BaseValues.Roles.GetRoleName(roleID); // стандартное имя роли
            return BaseValues.Roles.Custom <= roleID && roleID < BaseValues.Roles.Err ?
                GetRoleNameFromBase(roleID, roleName) :
                roleName;
        }


        /// <summary>
        /// Получить текущие данные входного канала
        /// </summary>
        public SrezTableLight.CnlData GetCurCnlData(int cnlNum)
        {
            DateTime dataAge;
            return GetCurCnlData(cnlNum, out dataAge);
        }

        /// <summary>
        /// Получить текущие данные входного канала
        /// </summary>
        public SrezTableLight.CnlData GetCurCnlData(int cnlNum, out DateTime dataAge)
        {
            try
            {
                SrezTableLight.Srez snapshot = dataCache.GetCurSnapshot(out dataAge);
                SrezTableLight.CnlData cnlData;
                return snapshot != null && snapshot.GetCnlData(cnlNum, out cnlData) ? 
                    cnlData : SrezTableLight.CnlData.Empty;
            }
            catch (Exception ex)
            {
                log.WriteException(ex, Localization.UseRussian ?
                    "Ошибка при получении текущих данных входного канала {0}" :
                    "Error getting current data of the input channel {0}", cnlNum);

                dataAge = DateTime.MinValue;
                return SrezTableLight.CnlData.Empty;
            }
        }
    }
}
