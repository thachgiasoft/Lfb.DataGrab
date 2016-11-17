﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Comm.Global.DTO.News;
using Comm.Global.Enum.Business;
using Lfb.DataGrabBll.Dto;
using Lib.Csharp.Tools;
using Lib.Csharp.Tools.Security;
using Newtonsoft.Json;

namespace Lfb.DataGrabBll
{
    /// <summary>
    /// 今日头条的抓取类
    /// </summary>
    public class ToutiaoGather
    {
        /// <summary>
        /// 频道的翻页计数
        /// </summary>
        public static int ChannelPageIndex;
        /// <summary>
        /// 组图的翻页计数
        /// </summary>
        public static int ZtPageIndex;

        /// <summary>
        /// 作者的翻页计数
        /// </summary>
        public static int AuthorPageIndex;

        public static Dictionary<string, int> DictUrl = new Dictionary<string, int>();

        #region === task 定时调用的方法 ===

        /// <summary>
        /// 从频道页抓取作者的主页地址
        /// </summary>
        /// <param name="newsListUrl"></param>
        /// <param name="newsType"></param>
        /// <returns></returns>
        public List<DtoNewsUrlList> AuthorUrlGathering(string newsListUrl, int newsType)
        {
            //重试过的移除
            if (DictUrl.ContainsKey(newsListUrl))
            {
                DictUrl.Remove(newsListUrl);
            }
            var strContent = "";
            try
            {
                newsListUrl = FormatUrlPcAs(newsListUrl);
                Log.Info(newsListUrl + " 抓取开始");
                strContent = HttpHelper.GetContentByMobileAgent(newsListUrl, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(strContent))
                {
                    //重新请求一次，因为用了代理后，经常会失败
                    strContent = HttpHelper.GetContentByMobileAgent(newsListUrl, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(strContent))
                    {
                        //HttpHelper.IsUseProxy = false;
                        //重新请求一次，因为用了代理后，经常会失败
                        strContent = HttpHelper.GetContentByMobileAgent(newsListUrl, Encoding.UTF8);
                        //HttpHelper.IsUseProxy = true;
                        if (string.IsNullOrWhiteSpace(strContent))
                        {
                            Log.Info(newsListUrl + " 未抓取到任何内容");
                            return null;
                        }
                    }
                }
                strContent = FormatJsonData(strContent);
                var data = JsonConvert.DeserializeObject<DtoTouTiaoJsData>(strContent);
                if (data != null)
                {
                    #region === 处理data中的数据，有作者的地址则存储 ===

                    if (data.data != null && data.data.Count > 0)
                    {
                        foreach (var item in data.data)
                        {
                            try
                            {
                                //item.media_url;
                                //"media_url": "http://toutiao.com/m3470331046/
                                if (!string.IsNullOrEmpty(item.media_url))
                                {
                                    var isAuthorUrl = Global.IsToutiaoAuthorUrl(item.media_url);
                                    if (isAuthorUrl)
                                    {
                                        //检查是否已存在，不在则入库
                                        DealAuthorUrl(item.media_url, item.group_id);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }

                    #endregion
                }
                else
                {
                    Log.Info(newsListUrl + " 未取到数据");
                    return null;
                }
                Log.Info(newsListUrl + " 抓取结束");
                var isHaveMore = data.has_more;

                Random rnd = new Random();
                //有更多数据，则继续抓取数据
                if (isHaveMore && ChannelPageIndex < Global.PageDepth)
                {
                    //sleep
                    //Thread.Sleep(rnd.Next(1000, 2500));
                    Thread.Sleep(200);
                    ChannelPageIndex++;
                    var maxBehotTime = data.next.max_behot_time.ToString();
                    //替换url中的max_behot_time
                    newsListUrl = ModifyUrlMax_behot_time(newsListUrl, maxBehotTime);
                    AuthorUrlGathering(newsListUrl, newsType);
                }
                else
                {
                    Log.Info("本频道抓取结束总页数" + ChannelPageIndex.ToString());
                    ChannelPageIndex = 0;
                    //Thread.Sleep(rnd.Next(2000, 5000));
                    Thread.Sleep(200);
                }

            }
            catch (Exception ex)
            {
                Log.Error("出错的url=" + newsListUrl);
                Log.Error(ex.Message + ex.StackTrace);
                Log.Debug("======strContent begin =========");
                Log.Debug(strContent);
                Log.Debug("======strContent end =========");
                //重试一次
                if (!DictUrl.ContainsKey(newsListUrl))
                {
                    DictUrl.Add(newsListUrl, 1);
                    AuthorUrlGathering(newsListUrl, newsType);
                }

            }
            return null;
        }

        /// <summary>
        /// 抓取作者主页的list,抓取文章阅读量等数据,
        /// </summary>
        /// <returns></returns>
        public int AuthorNewsGathering()
        {
            try
            {
                //取出待处理作者的数据，并置位isdeal=2 处理中
                var list = DalNews.GetNoDealAuthorList();
                #region === 取出待刷新的作者url数据 ===
                if (list != null && list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        if (!string.IsNullOrWhiteSpace(item.AuthorId))
                        {
                            var url = GetAuthorDataUrl(item.AuthorId);
                            DealAuthorData(url, item.AuthorId,item.GroupId);
                        }
                    }
                    //Thread.Sleep(5 * 1000);
                    Thread.Sleep(200);
                }
                else
                {
                    Log.Info("暂时没有要处理的作者url");
                    Thread.Sleep(60 * 1000);
                    AuthorNewsGathering();
                }
                #endregion
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 0;
        }

        /// <summary>
        /// 从新闻详细页抓取作者信息的，每个新闻处理一次即可
        /// </summary>
        /// <returns></returns>
        public int GatherAuthorFromNews()
        {
            try
            {
                //取出待处理作者的数据，并置位IsShow=2 处理中
                var list = DalNews.GetNoGatherAuthorUrlNewsList();
                if (list != null && list.Count > 0)
                {
                    foreach (var news in list)
                    {
                        var url = news.FromUrl;
                        Log.Info(url + " 抓取开始");
                        var strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);

                        #region === begin ===
                        if (string.IsNullOrWhiteSpace(strContent))
                        {
                            //重新请求一次，因为用了代理后，经常会失败
                            strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                            if (string.IsNullOrWhiteSpace(strContent))
                            {
                                //HttpHelper.IsUseProxy = false;
                                //重新请求一次，因为用了代理后，经常会失败
                                strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                                //HttpHelper.IsUseProxy = true;
                                if (string.IsNullOrWhiteSpace(strContent))
                                {
                                    Log.Info(url + " 未抓取到任何内容");
                                    continue;
                                }
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(strContent))
                        {
                            var hrefList = XpathHelper.GetAttrValueListByXPath(strContent, "//a", "href");
                            if (hrefList != null && hrefList.Count > 0)
                            {
                                foreach (var href in hrefList)
                                {
                                    var isAuthorUrl = Global.IsToutiaoAuthorUrl(href);
                                    if (isAuthorUrl)
                                    {
                                        //检查是否已存在，不在则入库,此处的没有groupid
                                        DealAuthorUrl(href,"");
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                        #endregion

                        //取到评论的用户，再从用户取得订阅作者
                        GatherAuthorFromUserSub(news.FromUrl, news.GroupId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 0;
        }

        /// <summary>
        /// 根据文章的刷新间隔取得该作者的主页来 抓取该作者文章阅读量等数据,
        /// </summary>
        /// <returns></returns>
        public int AuthorNewsByRefreshGathering()
        {
            try
            {
                //取出待处理作者的数据，并置位isdeal=2 处理中
                var list = DalNews.GetWaitRefreshAuthorList();
                #region === 取出待刷新的作者url数据 ===
                if (list != null && list.Count > 0)
                {
                    foreach (var item in list)
                    {
                        if (!string.IsNullOrWhiteSpace(item.AuthorId))
                        {
                            var url = GetAuthorDataUrl(item.AuthorId);
                            DealAuthorData(url, item.AuthorId,item.GroupId);
                        }
                    }
                    //Thread.Sleep(5 * 1000);
                    Thread.Sleep(200);
                }
                else
                {
                    Log.Info("暂时没有要处理的作者url");
                    Thread.Sleep(60 * 1000);
                    AuthorNewsGathering();
                }
                #endregion
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 0;
        }

        /// <summary>
        /// 从作者抓取相关新闻取出作者信息
        /// </summary>
        /// <returns></returns>
        public int GatherRelationNewsFromAuthor()
        {
            try
            {
                //取出待处理作者的数据，并置位IsShow=2 处理中
                var list = DalNews.GetNoRefreshAuthorList();
                if (list != null && list.Count > 0)
                {
                    foreach (var author in list)
                    {
                        var url = "http://www.toutiao.com/related_media/?media_id=" + author.AuthorId;
                        Log.Info(url + " 抓取开始");
                        var strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                        if (string.IsNullOrWhiteSpace(strContent))
                        {
                            //重新请求一次，因为用了代理后，经常会失败
                            strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                            if (string.IsNullOrWhiteSpace(strContent))
                            {
                                //HttpHelper.IsUseProxy = false;
                                //重新请求一次，因为用了代理后，经常会失败
                                strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                                //HttpHelper.IsUseProxy = true;
                                if (string.IsNullOrWhiteSpace(strContent))
                                {
                                    Log.Info(url + " 未抓取到任何内容");
                                    continue;
                                }
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(strContent))
                        {
                            strContent = FormatJsonData(strContent);
                            var result = JsonConvert.DeserializeObject<DtoTouTiaoRelationNewsJsData>(strContent);
                            if (result != null && result.data.related_media != null)
                            {
                                foreach (var item in result.data.related_media)
                                {
                                    if (!string.IsNullOrWhiteSpace(item.open_url))
                                    {
                                        var isAuthorUrl = Global.IsToutiaoAuthorUrl(item.open_url);
                                        var groupid = "";
                                        try
                                        {
                                            if (item.latest_article != null && item.latest_article.Count > 0)
                                            {
                                                if (item.latest_article[0].display_url != null)
                                                {
                                                    groupid =
                                                        Global.GetToutiaoGroupId(item.latest_article[0].display_url);
                                                }

                                            }
                                        }
                                        catch
                                        {
                                        }
                                        //item.latest_article[0].display_url
                                        if (isAuthorUrl)
                                        {
                                            //检查是否已存在，不在则入库
                                            DealAuthorUrl(item.open_url, groupid);
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 0;
        }


        /// <summary>
        /// 从组图抓取作者信息
        /// </summary>
        /// <returns></returns>
        public int GatherNewsFromZtRecent(string url)
        {
            try
            {
                #region === begin ===
                
                Log.Info(url + " 抓取开始");
                var strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(strContent))
                {
                    //重新请求一次，因为用了代理后，经常会失败
                    strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(strContent))
                    {
                        //HttpHelper.IsUseProxy = false;
                        //重新请求一次，因为用了代理后，经常会失败
                        strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                        //HttpHelper.IsUseProxy = true;
                        if (string.IsNullOrWhiteSpace(strContent))
                        {
                            Log.Info(url + " 未抓取到任何内容");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(strContent))
                {
                    strContent = FormatJsonData(strContent);
                    var data = JsonConvert.DeserializeObject<DtoTouTiaoZtJsData>(strContent);
                    if (data != null)
                    {
                        #region === 处理data中的数据，有作者的地址则存储 ===

                        if (data.data != null && data.data.Count > 0)
                        {
                            foreach (var item in data.data)
                            {
                                try
                                {
                                    //item.media_url;
                                    //"media_url": "http://toutiao.com/m3470331046/
                                    if (!string.IsNullOrEmpty(item.media_url))
                                    {
                                        var isAuthorUrl = Global.IsToutiaoAuthorUrl(item.media_url);
                                        if (isAuthorUrl)
                                        {
                                            //检查是否已存在，不在则入库
                                            DealAuthorUrl(item.media_url,item.group_id);
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                }
                            }
                        }

                        #endregion
                    }
                    else
                    {
                        Log.Info(url + " 未取到数据");
                        return 0;
                    }
                    
                    var isHaveMore = data.has_more;

                    Random rnd = new Random();
                    //有更多数据，则继续抓取数据
                    if (isHaveMore && ChannelPageIndex < Global.PageDepth)
                    {
                        //sleep
                        //Thread.Sleep(rnd.Next(1000, 2500));
                        Thread.Sleep(200);
                        ZtPageIndex++;
                        var maxBehotTime = data.next.max_behot_time.ToString();
                        //替换url中的max_behot_time
                        url = ModifyUrlMax_behot_time(url, maxBehotTime);
                        GatherNewsFromZtRecent(url);
                    }
                    else
                    {
                        Log.Info("本组图抓取结束总页数" + ChannelPageIndex.ToString());
                        ZtPageIndex = 0;
                        //Thread.Sleep(rnd.Next(2000, 5000));
                        Thread.Sleep(10*1000);
                    }
                }
                #endregion

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 0;
        }

        
        #endregion


        #region === 辅助方法 ===
        /// <summary>
        /// 从用户订阅抓取作者信息
        /// </summary>
        /// <returns></returns>
        private int GatherAuthorFromUserSub(string itemUrl,string groupId)
        {
            try
            {
                #region === begin ===

                var url = "";

                var strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(strContent))
                {
                    //重新请求一次，因为用了代理后，经常会失败
                    strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(strContent))
                    {
                        //HttpHelper.IsUseProxy = false;
                        //重新请求一次，因为用了代理后，经常会失败
                        strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                        //HttpHelper.IsUseProxy = true;
                        if (string.IsNullOrWhiteSpace(strContent))
                        {
                            Log.Info(url + " 未抓取到任何内容");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(strContent))
                {
                    strContent = FormatJsonData(strContent);
                    var data = JsonConvert.DeserializeObject<DtoTouTiaoZtJsData>(strContent);
                    if (data != null)
                    {
                        #region === 处理data中的数据，有作者的地址则存储 ===

                        if (data.data != null && data.data.Count > 0)
                        {
                            foreach (var item in data.data)
                            {
                                try
                                {
                                    //item.media_url;
                                    //"media_url": "http://toutiao.com/m3470331046/
                                    if (!string.IsNullOrEmpty(item.media_url))
                                    {
                                        var isAuthorUrl = Global.IsToutiaoAuthorUrl(item.media_url);
                                        if (isAuthorUrl)
                                        {
                                            //检查是否已存在，不在则入库
                                            DealAuthorUrl(item.media_url, item.group_id);
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                }
                            }
                        }

                        #endregion
                    }
                    else
                    {
                        Log.Info(url + " 未取到数据");
                        return 0;
                    }

                    var isHaveMore = data.has_more;

                    Random rnd = new Random();
                    //有更多数据，则继续抓取数据
                    if (isHaveMore && ChannelPageIndex < Global.PageDepth)
                    {
                        //sleep
                        //Thread.Sleep(rnd.Next(1000, 2500));
                        Thread.Sleep(200);
                        ZtPageIndex++;
                        var maxBehotTime = data.next.max_behot_time.ToString();
                        //替换url中的max_behot_time
                        url = ModifyUrlMax_behot_time(url, maxBehotTime);
                        //GatherAuthorFromUserSub();
                    }
                    else
                    {
                        Log.Info("本组图抓取结束总页数" + ChannelPageIndex.ToString());
                        ZtPageIndex = 0;
                        //Thread.Sleep(rnd.Next(2000, 5000));
                        Thread.Sleep(10 * 1000);
                    }
                }
                #endregion

            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
            }
            return 0;
        }

        public int DealAuthorData(string url, string authorId,string groupId)
        {
            var strContent = "";
            if (string.IsNullOrWhiteSpace(groupId))
            {
                groupId = "";
            }
            try
            {
                Log.Info(url + " 抓取开始");
                strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(strContent))
                {
                    //重新请求一次，因为用了代理后，经常会失败
                    strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(strContent))
                    {
                        //HttpHelper.IsUseProxy = false;
                        //重新请求一次，因为用了代理后，经常会失败
                        strContent = HttpHelper.GetContentByMobileAgent(url, Encoding.UTF8);
                        //HttpHelper.IsUseProxy = true;
                        if (string.IsNullOrWhiteSpace(strContent))
                        {
                            Log.Info(url + " 未抓取到任何内容");
                            return 0;
                        }
                    }
                }
                strContent = FormatJsonData(strContent);
                var data = JsonConvert.DeserializeObject<DtoTouTiaoAuthorJsData>(strContent);
                if (data != null)
                {
                    #region === 处理data中的数据，存储新闻信息 ===

                    if (data.data != null && data.data.Count > 0)
                    {
                        foreach (var subItem in data.data)
                        {
                            try
                            {
                                var newsId = DalNews.IsExistsNews(authorId, subItem.title);
                                if (newsId < 1)
                                {
                                    #region === 不存在的插入===
                                    var model = new DtoNews()
                                    {
                                        Author = "",
                                        AuthorId = authorId,
                                        Contents = "",
                                        CreateTime = DateTime.Now,
                                        CurReadTimes = subItem.go_detail_count,
                                        FromSiteName = "toutiao",
                                        FromUrl = subItem.source_url,
                                        IntervalMinutes = 60,
                                        IsDeal = 0,
                                        IsHot = 0,
                                        IsShow = 1,
                                        LastDealTime = DateTime.Now,
                                        LastReadTimes = subItem.go_detail_count,
                                        LogoOriginalUrl = subItem.pc_image_url,
                                        LogoUrl = subItem.pc_image_url,
                                        NewsHotClass = 7,
                                        NewsTypeId = (int)NewsTypeEnum.新闻,
                                        PubTime = subItem.datetime,
                                        Tags = "",
                                        Title = subItem.title,
                                        TotalComments = subItem.comments_count,
                                        RefreshTimes = 0,
                                        GroupId = groupId
                                    };
                                    DalNews.Insert(model);
                                    #endregion
                                }
                                else
                                {
                                    #region === 存在的则更新数据 ===
                                    var oldNews = DalNews.GetNews(newsId);
                                    if (string.IsNullOrWhiteSpace(oldNews.GroupId))
                                    {
                                        oldNews.GroupId = groupId;
                                    }
                                    if (oldNews != null)
                                    {
                                        //b、变化数据，如果是当天发稿的文章，每15分钟刷新一次阅读量，如果5、6、7级，则改为小时更新；
                                        //7天内发稿的文章，每一小时更新一次阅读数；
                                        //7天以上，每天刷新；
                                        //（这个可以按欢迎度级别优化，如15分钟阅读增加在10000以上为1级，5000以上为2级，2500以上为3级，1000以上为4级，500以上为5级，100以上为6级，100以下为7级）
                                        var isHot = 0;
                                        var minutes = (DateTime.Now - oldNews.LastDealTime).TotalMinutes;
                                        var newsClassId = 7;
                                        var addReads = subItem.go_detail_count - oldNews.CurReadTimes;
                                        var intervalMinutes = 24 * 60;
                                        if (addReads > 0)
                                        {
                                            if (minutes > 60)
                                            {
                                                var perHourReads = addReads / (minutes / 60.0);
                                                if (perHourReads > 10000)
                                                {
                                                    isHot = 1;
                                                }
                                            }
                                            else
                                            {
                                                if (addReads > 10000)
                                                {
                                                    isHot = 1;
                                                }
                                            }
                                            #region === 15分钟阅读量分析　===
                                            var per15MinutesReads = addReads / (minutes / 15.0);
                                            if (per15MinutesReads > 10000)
                                            {
                                                newsClassId = 1;
                                                isHot = 1;
                                                intervalMinutes = 15;
                                            }
                                            else if (per15MinutesReads > 5000)
                                            {
                                                newsClassId = 2;
                                                isHot = 1;
                                                intervalMinutes = 15;
                                            }
                                            else if (per15MinutesReads > 2500)
                                            {
                                                newsClassId = 3;
                                                intervalMinutes = 15;
                                            }
                                            else if (per15MinutesReads > 1000)
                                            {
                                                newsClassId = 4;
                                                intervalMinutes = 15;
                                            }
                                            else if (per15MinutesReads > 500)
                                            {
                                                newsClassId = 5;
                                                intervalMinutes = 60;
                                            }
                                            else if (per15MinutesReads > 100)
                                            {
                                                newsClassId = 6;
                                                intervalMinutes = 60;
                                            }
                                            else
                                            {
                                                newsClassId = 7;
                                                intervalMinutes = 60;
                                            }
                                            #endregion
                                        }
                                        if (oldNews.PubTime.AddHours(24) < DateTime.Now)
                                        {
                                            //不是今天发布的
                                            intervalMinutes = 24 * 60;
                                        }

                                        //如果原来是爆文的不修改ishot
                                        if (oldNews.IsHot == 1)
                                        {
                                            isHot = 1;
                                        }
                                        if (oldNews.NewsHotClass < newsClassId)
                                        {
                                            newsClassId = oldNews.NewsHotClass;
                                        }
                                        var model = new DtoNews()
                                        {
                                            Id = newsId,
                                            LastReadTimes = oldNews.CurReadTimes,
                                            CurReadTimes = subItem.go_detail_count,
                                            IsHot = isHot,
                                            IsDeal = 1,
                                            TotalComments = subItem.comments_count,
                                            IntervalMinutes = intervalMinutes,
                                            NewsHotClass = newsClassId,
                                            LastDealTime = DateTime.Now,
                                            RefreshTimes = oldNews.RefreshTimes + 1,
                                        };
                                        if (!string.IsNullOrWhiteSpace(oldNews.GroupId))
                                        {
                                            model.GroupId = oldNews.GroupId;
                                        }
                                        DalNews.UpdateNews(model);

                                        //暂不更新作者表的刷新时间，没用上
                                        //DalNews.UpdateAuthorInterval(authorId, intervalMinutes);
                                    }
                                    #endregion
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    Log.Info(url + " 未取到数据");

                }
                Log.Info(url + " 抓取结束");
                var isHaveMore = data.has_more;

                Random rnd = new Random();
                //有更多数据，则继续抓取数据
                if (isHaveMore && AuthorPageIndex < Global.PageDepth)
                {
                    //sleep
                    //Thread.Sleep(rnd.Next(1000, 2500));
                    Thread.Sleep(200);
                    AuthorPageIndex++;
                    var maxBehotTime = data.next.max_behot_time.ToString();
                    //替换url中的max_behot_time
                    url = ModifyUrlMax_behot_time(url, maxBehotTime);
                    DealAuthorData(url, authorId,groupId);
                }
                else
                {
                    Log.Info("本作者抓取结束总页数" + AuthorPageIndex);
                    //置位状态
                    DalNews.UpdateAuthorIsDeal(authorId, 1);
                    AuthorPageIndex = 0;
                    //Thread.Sleep(rnd.Next(2000, 5000));
                    Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message + ex.StackTrace);
                Log.Debug("======strContent begin =========");
                Log.Debug(strContent);
                Log.Debug("======strContent end =========");
            }
            return 1;
        }

        /// <summary>
        /// 处理url,替换其中的时间参数，用以读取下一页数据
        /// </summary>
        /// <param name="url">原url</param>
        /// <param name="newMaxBehotTime">时间</param>
        /// <returns></returns>
        public string ModifyUrlMax_behot_time(string url, string newMaxBehotTime)
        {
            var reStr = url;
            try
            {
                //http://www.toutiao.com/api/article/feed/?category=__all__&utm_source=toutiao&widen=0&max_behot_time=1477194899&max_behot_time_tmp=1477194899&as=A135F8A09C7897E&cp=580C08C9477EAE1

                var arrStr = url.Split('&');
                if (arrStr.Length > 0)
                {
                    foreach (var item in arrStr)
                    {
                        if (item.ToLower().Contains("max_behot_time") && !item.ToLower().Contains("max_behot_time_tmp"))
                        {
                            reStr = reStr.Replace(item, "max_behot_time=" + newMaxBehotTime);
                        }
                        if (item.ToLower().Contains("max_behot_time_tmp"))
                        {
                            reStr = reStr.Replace(item, "max_behot_time_tmp=" + newMaxBehotTime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return reStr;
        }

        /// <summary>
        /// 处理作者首页url,不存在则入库
        /// </summary>
        /// <param name="authorUrl"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public int DealAuthorUrl(string authorUrl,string groupId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(authorUrl))
                {
                    return 0;
                }
                var authorId = Global.GetToutiaoAuthorId(authorUrl);
                var isExists = DalNews.IsExistsAuthor(authorId);
                if (!isExists)
                {
                    var model = new DtoAuthor()
                    {
                        Author = "",
                        AuthorId = authorId,
                        IsDeal = 0,
                        LastDealTime = DateTime.Now,
                        Url = authorUrl,
                        IntervalMinutes = 60,
                        RefreshTimes = 0,
                        GroupId = groupId
                    };
                    var id = DalNews.Insert(model);
                    return id;
                }
            }
            catch (Exception ex)
            { }
            return 0;
        }

        /// <summary>
        /// 获取作者的取数据的url
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>
        public string GetAuthorDataUrl(string authorId)
        {
            var url = string.Format("http://www.toutiao.com/pgc/ma/?media_id={0}&page_type=1&max_behot_time=0&count=10&version=2&platform=pc&as=&cp=", authorId);
            url = FormatUrlPcAs(url);
            return url;
        }

        /// <summary>
        /// 格式化抓取数据的url的cp,as参数
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string FormatUrlPcAs(string url)
        {
            //var reStr = FormatUrlPcAsDefault(url);
            //return reStr;

            var reStr = url;
            //先处理cp=***,as=****
            var arrStr = url.Split('&');
            if (arrStr.Length > 0)
            {
                foreach (var item in arrStr)
                {
                    if (item.ToLower().Contains("cp="))
                    {
                        reStr = reStr.Replace(item, "cp=");
                    }
                    if (item.ToLower().Contains("as="))
                    {
                        reStr = reStr.Replace(item, "as=");
                    }
                }
            }

            string strCp;
            string strAs;
            GetCpandAs(out strAs, out strCp);
            reStr = reStr.Replace("as=", "as=" + strAs);
            reStr = reStr.Replace("cp=", "cp=" + strCp);

            return reStr;

        }

        /// <summary>
        /// 格式化抓取数据的url的cp,as参数,用于格式化后还不行的情况，用默认参数
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string FormatUrlPcAsDefault(string url)
        {
            var reStr = url;
            //先处理cp=***,as=****
            var arrStr = url.Split('&');
            if (arrStr.Length > 0)
            {
                foreach (var item in arrStr)
                {
                    if (item.ToLower().Contains("cp="))
                    {
                        reStr = reStr.Replace(item, "cp=");
                    }
                    if (item.ToLower().Contains("as="))
                    {
                        reStr = reStr.Replace(item, "as=");
                    }
                }
            }

            string strCp = "7E0AC8874BB0985";
            string strAs = "479BB4B7254C150";

            reStr = reStr.Replace("as=", "as=" + strAs);
            reStr = reStr.Replace("cp=", "cp=" + strCp);
            return reStr;
        }

        public void GetCpandAs(out string strAs, out string strCp)
        {

            var date1 = new DateTime(1970, 1, 1, 0, 0, 0);
            var t1 = GetIntFromTime(DateTime.Now);

            string t = t1.ToString("x8").ToUpper();
            var e = StringSecurityHelper.Md5(t1.ToString(), true);
            if (t.Length != 8)
            {
                strAs = "479BB4B7254C150";
                strCp = "7E0AC8874BB0985";
                return;
            }
            string[] s = new string[5];
            string[] a = new string[5];

            for (var j = 0; j < 5; j++)
            {
                s[j] = e.Substring(j, 1);
                a[j] = e.Substring(e.Length - 5 + j, 1);
            }
            var o = "";
            var ta = t.ToCharArray();
            for (var j = 0; j < 5; j++)
            {
                o += s[j] + ta[j].ToString();
            }
            var c = "";
            for (var j = 0; j < 5; j++)
            {
                c += ta[j + 3].ToString() + a[j];
            }
            strAs = "A1" + o + t.Substring(t.Length - 3, 3);
            strCp = t.Substring(0, 3) + c + "E1";

        }
        private long lLeft = 621355968000000000;
        public long GetIntFromTime(DateTime dt)
        {

            DateTime dt1 = dt.ToUniversalTime();
            long sticks = (long)Math.Floor((dt1.Ticks - lLeft) / 10000000.0);
            return sticks;

        }

        public string FormatJsonData(string contents)
        {
            var str = contents;
            try
            {
                str = str.Replace("\"show_play_effective_count\": true,", "\"show_play_effective_count\": 0,").Replace("\"show_play_effective_count\": false,", "\"show_play_effective_count\": 0,");
            }
            catch (Exception ex)
            {

            }
            return str;
        }
        #endregion

    }
}
