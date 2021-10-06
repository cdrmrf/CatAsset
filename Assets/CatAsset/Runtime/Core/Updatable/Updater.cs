﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CatAsset
{
    /// <summary>
    /// 资源更新器
    /// </summary>
    public class Updater
    {
        /// <summary>
        /// 重新生成一次读写区资源清单所需的下载字节数
        /// </summary>
        private static long generateManifestLength = 1024 * 1024 * 10;  //10M

        /// <summary>
        /// 从上一次重新生成读写区资源清单到现在下载的字节数
        /// </summary>
        private static long deltaUpatedLength;

        /// <summary>
        /// 需要更新的资源列表
        /// </summary>
        public List<AssetBundleManifestInfo> UpdateList = new List<AssetBundleManifestInfo>();

        /// <summary>
        /// 需要更新的资源组
        /// </summary>
        public string UpdateGroup;

        /// <summary>
        /// 需要更新的资源总数
        /// </summary>
        public int totalCount;

        /// <summary>
        /// 需要更新的资源长度
        /// </summary>
        public long totalLength;

        /// <summary>
        /// 已更新资源文件数量
        /// </summary>
        public int updatedCount;

        /// <summary>
        /// 已更新资源文件长度
        /// </summary>
        public long updatedLength;

        /// <summary>
        /// 是否被暂停了
        /// </summary>
        public bool paused;

        /// <summary>
        /// 资源文件更新回调，每次下载资源文件后调用
        /// </summary>
        private Action<int, long, int, long,string, string> onFileDownloaded;



        /// <summary>
        /// 更新资源
        /// </summary>
        public void UpdateAsset(Action<int, long, int, long, string, string> onFileDownloaded)
        {
            foreach (AssetBundleManifestInfo updateABInfo in UpdateList)
            {
                string localFilePath = Util.GetReadWritePath(updateABInfo.AssetBundleName);
                string downloadUri = Path.Combine(CatAssetUpdater.UpdateUriPrefix, updateABInfo.AssetBundleName);
                DownloadFileTask task = new DownloadFileTask(CatAssetManager.taskExcutor, downloadUri, updateABInfo,this, localFilePath, downloadUri, OnDownloadFinished);
                CatAssetManager.taskExcutor.AddTask(task);
            }

            this.onFileDownloaded = onFileDownloaded;
        }

        /// <summary>
        /// 资源文件下载完毕的回调
        /// </summary>
        private void OnDownloadFinished(bool success, string error, AssetBundleManifestInfo abInfo)
        {

            if (!success)
            {
                Debug.LogError($"下载文件{abInfo.AssetBundleName}失败：" + error);
                return;
            }

            //更新已下载资源信息
            updatedCount++;
            updatedLength += abInfo.Length;
            deltaUpatedLength += abInfo.Length;

           
            GroupInfo groupInfo = CatAssetManager.GetGroupInfo(abInfo.Group);
            if (!groupInfo.localAssetBundles.Contains(abInfo.AssetBundleName))
            {
                //没有被另一个Updater下载过

                //将下载好的ab信息添加到RuntimeInfo中
                CatAssetManager.AddRuntimeInfo(abInfo, true);

                //更新读写区资源信息列表
                CatAssetUpdater.readWriteManifestInfoDict[abInfo.AssetBundleName] = abInfo;

                //更新资源组本地资源信息
                groupInfo.localAssetBundles.Add(abInfo.AssetBundleName);
                groupInfo.localCount++;
                groupInfo.localLength += abInfo.Length;


                if (updatedCount >= totalCount || deltaUpatedLength >= generateManifestLength)
                {
                    //所有资源下载完毕 或者已下载字节数达到要求 就重新生成一次读写区资源清单
                    deltaUpatedLength = 0;
                    CatAssetUpdater.GenerateReadWriteManifest();
                }
            }

            onFileDownloaded(updatedCount, updatedLength,totalCount,totalLength,abInfo.AssetBundleName,UpdateGroup);
        }
    }

}
