﻿using System;
using System.Collections.Generic;
using System.Linq;
using Comm.Cloud.RDS;
using Comm.Cloud.RDS.DTO;
using Comm.Global.DTO.News;
using Lfb.DataGrabBll.Model;
using Lib.Csharp.Tools;
using Comm.Tools.Utility;
using Log = Lib.Csharp.Tools.Log;

namespace Lfb.DataGrabBll
{
    public class DalNews
    {
        private static RdsNew Sql; //注意在TestFixtureSetUp后初始化

        static DalNews()
        {
            Sql = new RdsNew(RdsConfig, Comm.Global.Enum.Sys.DbType.MySql);
        }

        public static PublicCloudRdsConfig RdsConfig
        {
            get
            {
                var sqlInfo = Global.MySql1;
                var server = "localhost";
                var db = "News";
                var uid = "root";
                var pwd = "";
                var port = 3306;
                if (!string.IsNullOrWhiteSpace(sqlInfo))
                {
                    var arrStr = sqlInfo.Split(';');
                    if (arrStr != null && arrStr.Length > 0)
                    {
                        foreach (var item in arrStr)
                        { 
                            //Server=localhost;Database=News;Uid=root;Pwd=;Port=3306
                            if (item.Contains("Server="))
                            {
                                server = item.Replace("Server=", "");
                            }
                            if (item.Contains("Database="))
                            {
                                db = item.Replace("Database=", "");
                            }
                            if (item.Contains("Uid="))
                            {
                                uid = item.Replace("Uid=", "");
                            }
                            if (item.Contains("Pwd="))
                            {
                                pwd = item.Replace("Pwd=", "");
                            }
                            if (item.Contains("Port="))
                            {
                                port = StrHelper.ToInt32( item.Replace("Port=", ""));
                            }
                        }
                    }
                }
                var rdsConfig = new PublicCloudRdsConfig
                {
                    Server = server,
                    Database = db,
                    Uid = uid,
                    Pwd = pwd,
                    Port = port,
                };
                return rdsConfig;
            }
        }

        #region === news deal ===
        /// <summary>
        /// 添加一条新闻
        /// </summary>
        /// <param name="model">新闻实体</param>
        /// <returns></returns>
        public static int Insert(DtoNews model)
        {
            try
            {
                if (model.Author == null)
                    model.Author = "";
                if (model.Contents == null)
                    model.Contents = "";
                if (model.FromSiteName == null)
                    model.FromSiteName = "";
                if (model.PubTime == null)
                    model.PubTime = DateTime.Now;
                if (model.FromUrl == null)
                    model.FromUrl = "";
                if (model.LogoOriginalUrl == null)
                    model.LogoOriginalUrl = "";
                if (model.LogoUrl == null)
                    model.LogoUrl = "";
                if (model.Title == null)
                    model.Title = "";

                ////非图片的，且内容小于100的不入库
                //if (model.NewsTypeId != NewsTypeEnum.图片  && model.Contents.Length < 100)
                //{
                //    return -1;
                //}
                //if(string.IsNullOrWhiteSpace(model.Title.Trim()))
                //{
                //    return -1;
                //}

                var item = new T_News()
                {
                    Author = model.Author,
                    Contents = model.Contents,
                    //CreateTime = model.CreateTime,
                    FromSiteName = model.FromSiteName,
                    FromUrl = model.FromUrl,
                    IsShow = 0,
                    LogoOriginalUrl = model.LogoOriginalUrl,
                    LogoUrl = model.LogoUrl,
                    NewsTypeId = (int)model.NewsTypeId,
                    PubTime = model.PubTime,
                    Title = model.Title,
                    AuthorId = model.AuthorId,
                    TotalComments = model.TotalComments,
                    Tags = model.Tags,
                    NewsHotClass = model.NewsHotClass,
                    LastReadTimes = model.LastReadTimes,
                    LastDealTime = DateTime.Now,
                    IsHot = model.IsHot,
                    IsDeal = model.IsDeal,
                    IntervalMinutes = model.IntervalMinutes,
                    CurReadTimes = model.CurReadTimes,
                    CreateTime = DateTime.Now,
                };


                var id = Sql.InsertId<T_News>(item);

                return id;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                return -1;
            }
        }

        public static int UpdateNews(DtoNews model)
        {
            try
            {
                var news = new T_News()
                {
                    Id = model.Id,
                    CurReadTimes = model.CurReadTimes,
                    LastDealTime = DateTime.Now,
                    LastReadTimes = model.LastReadTimes,
                    IsHot = model.IsHot,
                    IsDeal = 1,
                    TotalComments = model.TotalComments,
                    NewsHotClass = model.NewsHotClass,
                    IntervalMinutes = model.IntervalMinutes,
                };
                return Sql.Update(news, "Id={0}".Formats(model.Id));
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 1;
        }
        /// <summary>
        /// 判断某条新闻是否已存在
        /// </summary>
        /// <param name="title">新闻标题</param>
        /// <returns></returns>
        public static bool IsExistsNews(string title)
        {
            try
            {
                var sql = "select Id from T_News where Title=?";

                var id = Sql.ExecuteScalar(0, sql, title);

                return id > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }

            return false;
        }

        /// <summary>
        /// 判断某条新闻是否已存在
        /// </summary>
        /// <param name="authorId">新闻作者</param>
        /// <param name="title">新闻标题</param>
        /// <returns></returns>
        public static int IsExistsNews(string authorId, string title)
        {
            try
            {
                var sql = "select Id from T_News where AuthorId=? and Title=?";

                var id = Sql.ExecuteScalar(0, sql, authorId, title);

                return id;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }

            return 0;
        }

        public static bool DelNews(int id)
        {
            try
            {
                var sql = string.Format("delete from T_News where Id={0}", id);

                var result = Sql.ExecuteSql(sql);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }

            return false;
        }

        /// <summary>
        /// 添加一条新闻
        /// </summary>
        /// <param name="model">新闻实体</param>
        /// <returns></returns>
        public static int InsertMedia(DtoNewsMedia model)
        {
            try
            {
                if (model.Description == null)
                    model.Description = "";
                if (model.PicOriginalUrl == null)
                    model.PicOriginalUrl = "";
                if (model.PicUrl == null)
                    return -1;
                if (model.NewsId < 1)
                    return -1;

                //var item = new T_NewsMedia()
                //{
                //    Description = model.Description,
                //    IsShow = 1,
                //    NewsId = model.NewsId,
                //    Orders = model.Orders,
                //    PicOriginalUrl = model.PicOriginalUrl,
                //    PicUrl = model.PicUrl,
                //    //ThumbnailUrl = model.ThumbnailUrl,

                //};

                //var id = item.InsertIdNoTrans();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                return -1;
            }
        }

        /// <summary>
        /// 获取30条图片未处理的记录
        /// </summary>
        /// <returns></returns>
        public static List<DtoNewsAll> GetNoDealNewsList()
        {
            try
            {
                var sql = "select top 30 * from T_News where ImgFlag=0 order By Id DESC";
                var list = Sql.Select<DtoNewsAll>(sql);

                if (list != null && list.Count > 0)
                {
                    var ids = list.Select(p => p.Id).Join(",");
                    if (ids.Length == 0)
                    {
                        ids = "0";
                    }
                    var list2 = GetNewsMedia(ids);

                    #region === 循环取每个回复 ===

                    foreach (var item in list)
                    {

                        if (list2 == null || list2.Count <= 0) continue;
                        var medias = list2.Where(p => p.NewsId == item.Id).ToList();
                        item.NewsMedia = medias;

                    }

                    #endregion
                }

                return list;
            }
            catch (Exception ex)
            {
                Lib.Csharp.Tools.Log.Error(ex.Message + ex.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// 获取指定id的图片信息
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        private static List<DtoNewsMedia> GetNewsMedia(string ids)
        {
            try
            {
                var sql = @"SELECT * FROM dbo.T_NewsMedia WHERE NewsId in({0})".Formats(ids);

                var list = Sql.Select<DtoNewsMedia>(sql);
                return list;
            }
            catch (Exception e)
            {
                Log.Error(e.Message + e.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// 获取某一个新闻
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static T_News GetNews(int id)
        {
            try
            {
                var sql = @"SELECT  * FROM T_News WHERE Id={0}".Formats(id);

                var list = Sql.Select<T_News>(sql);
                if (list != null && list.Count > 0)
                {
                    return list[0];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// 更新新闻的图片处理状态
        /// </summary>
        /// <param name="id">新闻id</param>
        /// <param name="imgFlag">图片处理标识 0=no,1=yes</param>
        /// <returns></returns>
        public static bool UpdateImgFlag(int id, int imgFlag)
        {
            try
            {
                if (imgFlag > 1)
                    imgFlag = 1;
                if (imgFlag < 0)
                    imgFlag = 0;

                var sql = string.Format("update T_News set ImgFlag={0} where id={1}", imgFlag, id);

                var result = Sql.ExecuteSql(sql);
                return result;
            }
            catch (Exception e)
            {
                Log.Error(e.Message + e.StackTrace);
                return false;
            }
        }

        #endregion

        #region === author deal ===
        /// <summary>
        /// 添加一条新闻
        /// </summary>
        /// <param name="model">新闻实体</param>
        /// <returns></returns>
        public static int Insert(DtoAuthor model)
        {
            try
            {
                if (model.Author == null)
                    model.Author = "";
                if (model.AuthorId == null)
                    return -1;
                if (model.Url == null)
                    return -1;

                var item = new T_Author()
                {
                    Author = model.Author,
                    CreateTime = DateTime.Now,
                    AuthorId = model.AuthorId,
                    IsDeal = model.IsDeal,
                    LastDealTime = DateTime.Now,
                    Url = model.Url
                };


                var id = Sql.InsertId<T_Author>(item);

                return id;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                return -1;
            }
        }

        /// <summary>
        /// 判断某个作者是否已存在
        /// </summary>
        /// <param name="authorId">作者id</param>
        /// <returns></returns>
        public static bool IsExistsAuthor(string authorId)
        {
            try
            {
                var sql = "select Id from T_Author where AuthorId=?";

                var id = Sql.ExecuteScalar(0, sql, authorId);

                return id > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }

            return false;
        }

        /// <summary>
        /// 更新作者的处理状态
        /// </summary>
        /// <param name="id">作者表id</param>
        /// <param name="isDeal">处理标识 0=no,1=yes</param>
        /// <returns></returns>
        public static bool UpdateAuthorIsDeal(int id, int isDeal)
        {
            try
            {
                if (isDeal > 1)
                    isDeal = 1;
                if (isDeal < 0)
                    isDeal = 0;

                var sql = string.Format("update T_Author set IsDeal={0} where Id={1}", isDeal, id);

                var result = Sql.ExecuteSql(sql);
                return result;
            }
            catch (Exception e)
            {
                Log.Error(e.Message + e.StackTrace);
                return false;
            }
        }
        public static bool UpdateAuthorInterval(string authorId, int intervalMinutes)
        {
            try
            {
                var sql = string.Format("update T_Author set IntervalMinutes={0} where AuthorId='{1}'", intervalMinutes, authorId);

                var result = Sql.ExecuteSql(sql);
                return result;
            }
            catch (Exception e)
            {
                Log.Error(e.Message + e.StackTrace);
                return false;
            }
        }
        public static bool UpdateAuthorIsDeal(string authorId, int isDeal)
        {
            try
            {
                var sql = string.Format("update T_Author set IsDeal={0} where AuthorId='{1}'", isDeal, authorId);

                var result = Sql.ExecuteSql(sql);
                return result;
            }
            catch (Exception e)
            {
                Log.Error(e.Message + e.StackTrace);
                return false;
            }
        }
        /// <summary>
        /// 获取100条未处理的作者记录
        /// </summary>
        /// <returns></returns>
        public static List<DtoAuthor> GetNoDealAuthorList()
        {
            try
            {
                var sql = "select * from T_Author where (IsDeal=0 or IsDeal=1) order By Id DESC limit 0,100";
                var list = Sql.Select<DtoAuthor>(sql);

                if (list != null && list.Count > 0)
                {
                    var ids = list.Select(p => p.Id).Join(",");
                    if (ids.Length == 0)
                    {
                        ids = "0";
                    }
                    //取出后置位isdeal 正在处理状态　isdeal=2
                    sql = "update T_Author set IsDeal=2 where Id in({0})".Formats(ids);
                    Sql.ExecuteSql(sql);

                }

                return list;
            }
            catch (Exception ex)
            {
                Lib.Csharp.Tools.Log.Error(ex.Message + ex.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// 获取需要刷新的作者记录
        /// </summary>
        /// <returns></returns>
        public static List<DtoAuthor> GetWaitRefreshAuthorList()
        {
            try
            {
                //取一个月内抓取且已到刷新时间的新闻的作者列表
                var sql = "select * from t_author where (IsDeal=0 or IsDeal=1) and AuthorId in(SELECT DISTINCT AuthorId from t_news WHERE  DATE_ADD(CreateTime,INTERVAL 30 DAY)>'{0}' and DATE_ADD(LastDealTime,INTERVAL IntervalMinutes MINUTE)>'{0}') order By Id DESC limit 0,100".Formats(DateTime.Now);
                var list = Sql.Select<DtoAuthor>(sql);

                if (list != null && list.Count > 0)
                {
                    var ids = list.Select(p => p.Id).Join(",");
                    if (ids.Length == 0)
                    {
                        ids = "0";
                    }
                    //取出后置位isdeal 正在处理状态　isdeal=2
                    sql = "update T_Author set IsDeal=2 where Id in({0})".Formats(ids);
                    Sql.ExecuteSql(sql);

                }

                return list;
            }
            catch (Exception ex)
            {
                Lib.Csharp.Tools.Log.Error(ex.Message + ex.StackTrace);
            }

            return null;
        }

        /// <summary>
        /// 获取100条未处理的新闻记录 未处理是指没有抓取该新闻详细页，从中取出作者url
        /// </summary>
        /// <returns></returns>
        public static List<DtoNews> GetNoGatherAuthorUrlNewsList()
        {
            try
            {
                var sql = "select * from T_News where IsShow=0 order By Id DESC limit 0,100";
                var list = Sql.Select<DtoNews>(sql);

                if (list != null && list.Count > 0)
                {
                    var ids = list.Select(p => p.Id).Join(",");
                    if (ids.Length == 0)
                    {
                        ids = "0";
                    }
                    //取出后置位IsShow 正在处理状态　IsShow=2
                    sql = "update T_News set IsShow=2 where Id in({0})".Formats(ids);
                    Sql.ExecuteSql(sql);

                }

                return list;
            }
            catch (Exception ex)
            {
                Lib.Csharp.Tools.Log.Error(ex.Message + ex.StackTrace);
            }

            return null;
        }
        #endregion
    }
}