﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CoreDX.Common.Util.PropertyChangedExtensions;
using CoreDX.Domain.Core.Entity;

namespace CoreDX.Domain.Model.Entity
{
    /// <summary>
    /// 领域实体基类
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <typeparam name="TIdentityKey">身份主键类型</typeparam>
    /// <typeparam name="TIdentityUser">身份类型</typeparam>
    public abstract class DomainEntityBase<TKey, TIdentityKey, TIdentityUser> : DomainEntityBase<TKey, TIdentityKey>
        , ICreatorRecordable<TIdentityKey, TIdentityUser>
        , ILastModifierRecordable<TIdentityKey, TIdentityUser>
        where TKey : struct, IEquatable<TKey>
        where TIdentityKey : struct, IEquatable<TIdentityKey>
        where TIdentityUser : IEntity<TIdentityKey>
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public virtual TIdentityUser Creator { get; set; }

        /// <summary>
        /// 上次修改人
        /// </summary>
        public virtual TIdentityUser LastModifier { get; set; }
    }

    /// <summary>
    /// 领域实体基类
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <typeparam name="TIdentityKey">身份主键类型</typeparam>
    public abstract class DomainEntityBase<TKey, TIdentityKey> : DomainEntityBase<TKey>
        , ICreatorRecordable<TIdentityKey>
        , ILastModifierRecordable<TIdentityKey>
        where TKey : struct, IEquatable<TKey>
        where TIdentityKey : struct, IEquatable<TIdentityKey>
    {
        /// <summary>
        /// 创建人Id
        /// </summary>
        public virtual TIdentityKey? CreatorId { get; set; }

        /// <summary>
        /// 上次修改人Id
        /// </summary>
        public virtual TIdentityKey? LastModifierId { get; set; }
    }

    /// <summary>
    /// 领域实体基类
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    public abstract class DomainEntityBase<TKey> : IDomainEntity<TKey>
        , IOptimisticConcurrencySupported
        where TKey : struct, IEquatable<TKey>
    {
        /// <summary>
        /// 初始化用于跟踪属性变更所需的属性信息
        /// </summary>
        protected DomainEntityBase()
        {
            //判断类型是否已经加入字典
            //将未加入的类型添加进去（一般为该类对象首次初始化时）
            var type = this.GetType();
            if (!PropertyNamesDictionary.ContainsKey(type))
            {
                lock (Locker)
                {
                    if (!PropertyNamesDictionary.ContainsKey(type))
                    {
                        PropertyNamesDictionary.Add(type, type.GetProperties()
                            .OrderBy(property => property.Name)
                            .Select(property => property.Name).ToArray());
                    }
                }
            }

            //初始化属性变更掩码
            _propertyChangeMask = new BitArray(PropertyNamesDictionary[type].Length, false);

            //注册全局属性变更事件处理器
            if (PublicPropertyChangedEventHandler != null)
            {
                PropertyChanged += PublicPropertyChangedEventHandler;
            }
        }

        [Key]
        public virtual TKey Id { get; set; }

        #region IEntity成员

        /// <summary>
        /// 并发标记
        /// </summary>
        public virtual string ConcurrencyStamp { get; set; }

        /// <summary>
        /// 软删除标记
        /// </summary>
        public virtual bool IsDeleted { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public virtual DateTimeOffset CreationTime { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 上次修改时间
        /// </summary>
        public virtual DateTimeOffset LastModificationTime { get; set; } = DateTimeOffset.Now;

        #endregion

        #region IPropertyChangeTrackable成员

        private static readonly object Locker = new object();
        private static readonly Dictionary<Type, string[]> PropertyNamesDictionary = new Dictionary<Type, string[]>();

        private readonly BitArray _propertyChangeMask;

        /// <summary>
        /// 全局属性变更通知事件处理器（所有继承自<see cref="DomainEntityBase" />的类在实例化时都会自动注册）
        /// </summary>
        public static PropertyChangedEventHandler PublicPropertyChangedEventHandler { get; set; }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangedExtensionEventHandler PropertyChangedExtension;

        /// <summary>
        /// 内部属性变更事件处理器，将会由 PropertyChanged.Fody 在编译时自动注入到属性设置器
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        protected virtual void OnPropertyChanged(string propertyName, object oldValue, object newValue)
        {
            //Perform property validation

            _propertyChangeMask[Array.IndexOf(PropertyNamesDictionary[this.GetType()], propertyName)] = true;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PropertyChangedExtension?.Invoke(this, new PropertyChangedExtensionEventArgs(propertyName, oldValue, newValue));
        }

        /// <summary>
        /// 判断指定的属性或任意属性是否被变更过（<see cref="IPropertyChangeTrackable"/>接口的实现）
        /// </summary>
        /// <param name="names">指定要判断的属性名数组，如果为空(null)或空数组则表示判断任意属性。</param>
        /// <returns>
        ///	<para>如果指定的<paramref name="names"/>参数有值，当只有参数中指定的属性发生过更改则返回真(True)，否则返回假(False)；</para>
        ///	<para>如果指定的<paramref name="names"/>参数为空(null)或空数组，当实体中任意属性发生过更改则返回真(True)，否则返回假(False)。</para>
        ///	</returns>
        public bool HasChanges(params string[] names)
        {
            if (!(names?.Length > 0))
            {
                foreach (bool mask in _propertyChangeMask)
                {
                    if (mask == true)
                    {
                        return true;
                    }
                }

                return false;
            }

            var type = this.GetType();
            foreach (var name in names)
            {
                var index = Array.IndexOf(PropertyNamesDictionary[type], name);
                if (index >= 0 && _propertyChangeMask[index] == true)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取实体中发生过变更的属性集（<see cref="IPropertyChangeTrackable"/>接口的实现）
        /// </summary>
        /// <returns>如果实体没有属性发生过变更，则返回空白字典，否则返回被变更过的属性键值对</returns>
        public IDictionary<string, object> GetChanges()
        {
            Dictionary<string, object> changeDictionary = new Dictionary<string, object>();
            var type = this.GetType();
            for (int i = 0; i < _propertyChangeMask.Length; i++)
            {
                if (_propertyChangeMask[i] == true)
                {
                    changeDictionary.Add(PropertyNamesDictionary[type][i],
                        type.GetProperty(PropertyNamesDictionary[type][i])?.GetValue(this));
                }
            }

            return changeDictionary;
        }

        /// <summary>
        /// 重置指定的属性或任意属性变更状态（为未变更）（<see cref="IPropertyChangeTrackable"/>接口的实现）
        /// </summary>
        /// <param name="names">指定要重置的属性名数组，如果为空(null)或空数组则表示重置所有属性的变更状态（为未变更）</param>
        public void ResetPropertyChangeStatus(params string[] names)
        {
            if (names?.Length > 0)
            {
                var type = this.GetType();
                foreach (var name in names)
                {
                    var index = Array.IndexOf(PropertyNamesDictionary[type], name);
                    if (index >= 0)
                    {
                        _propertyChangeMask[index] = false;
                    }
                }
            }
            else
            {
                _propertyChangeMask.SetAll(false);
            }
        }

        #endregion
    }
}
