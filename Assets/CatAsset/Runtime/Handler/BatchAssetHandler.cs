﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Object = System.Object;

namespace CatAsset.Runtime
{
    /// <summary>
    /// 批量资源加载完毕回调方法的原型
    /// </summary>
    public delegate void BatchAssetLoadedCallback(BatchAssetHandler handler);

    /// <summary>
    /// 批量资源句柄
    /// </summary>
    public class BatchAssetHandler : BaseHandler ,IBindableHandler
    {
        /// <summary>
        /// 需要加载的资源数量
        /// </summary>
        private int assetCount;

        /// <summary>
        /// 加载结束的资源数量
        /// </summary>
        private int loadedCount;

        /// <summary>
        /// 资源句柄列表
        /// </summary>
        public List<AssetHandler<object>> Handlers { get; } = new List<AssetHandler<object>>();

        /// <summary>
        /// 资源加载完毕回调
        /// </summary>
        internal readonly AssetLoadedCallback<object> OnAssetLoadedCallback;

        /// <summary>
        /// 批量资源加载完毕回调
        /// </summary>
        private BatchAssetLoadedCallback onLoadedCallback;

        /// <summary>
        /// 批量资源加载完毕回调
        /// </summary>
        public event BatchAssetLoadedCallback OnLoaded
        {
            add
            {
                if (State == HandlerState.InValid)
                {
                    Debug.LogError($"在无效的{GetType().Name}：{Name}上添加了OnLoaded回调");
                    return;
                }

                if (State != HandlerState.Doing)
                {
                    value?.Invoke(this);
                    return;
                }

                onLoadedCallback += value;
            }

            remove
            {
                if (State == HandlerState.InValid)
                {
                    Debug.LogError($"在无效的{GetType().Name}：{Name}上移除了OnLoaded回调");
                    return;
                }

                onLoadedCallback -= value;
            }
        }
        
        public BatchAssetHandler()
        {
            OnAssetLoadedCallback = OnAssetLoaded;
        }

        /// <summary>
        /// 资源加载完毕回调
        /// </summary>
        private void OnAssetLoaded(AssetHandler<object> handler)
        {
            loadedCount++;
            
            CheckLoaded();
        }

        /// <summary>
        /// 检查所有资源是否已加载完毕
        /// </summary>
        internal void CheckLoaded()
        {
            if (Token != default && Token.IsCancellationRequested)
            {
                InternalUnload();
                return;
            }
            
            if (loadedCount == assetCount)
            {
                Task = null;
                State = HandlerState.Success;

                onLoadedCallback?.Invoke(this);
                ContinuationCallBack?.Invoke();
            }
        }

        /// <summary>
        /// 添加资源句柄
        /// </summary>
        internal void AddAssetHandler(AssetHandler<object> handler)
        {
            Handlers.Add(handler);
        }
        
        /// <inheritdoc />
        protected override void Cancel()
        {
            //此Handler的取消行为等同于卸载行为
            InternalUnload();
        }

        /// <inheritdoc />
        protected override void InternalUnload()
        {
            foreach (AssetHandler<object> assetHandler in Handlers)
            {
                //加载中的取消 加载成功的卸载 加载失败的释放
                assetHandler.Unload();
            }

            //释放自身
            Release();
        }

        /// <summary>
        /// 获取可等待对象
        /// </summary>
        public HandlerAwaiter<BatchAssetHandler> GetAwaiter()
        {
            return new HandlerAwaiter<BatchAssetHandler>(this);
        }
        
        public static BatchAssetHandler Create(int assetCount = 0,CancellationToken token = default)
        {
            BatchAssetHandler handler = ReferencePool.Get<BatchAssetHandler>();
            handler.CreateBase(nameof(BatchAssetHandler),token);
            handler.assetCount = assetCount;
            
            handler.CheckLoaded();
            
            return handler;
        }

        public override void Clear()
        {
            base.Clear();

            assetCount = default;
            loadedCount = default;
            Handlers.Clear();
        }
    }
}
