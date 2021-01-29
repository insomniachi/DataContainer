﻿using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

namespace KEI.Infrastructure
{
    public static class DataContainerExtensions
    {
        #region DataContainer Get/Put Extensions

        public static T GetValue<T>(this IDataContainer dc, string key)
        {
            var retValue = default(T);

            dc.GetValue(key, ref retValue);

            return retValue;
        }

        public static T GetValue<T>(this IDataContainer dc, Key<T> key)
        {
            T value = key.DefaultValue;
            
            dc.GetValue(key.Name, ref value);
            
            return value;
        }

        public static bool SetValue<T>(this IDataContainer dc, Key<T> key, T value) => dc.SetValue(key.Name, value);

        public static void PutValue(this IDataContainer dc, string key, object value)
        {
            if (dc.ContainsData(key))
            {
                dc.SetValue(key, value);
            }
            else
            {
                if (dc is IPropertyContainer)
                {
                    dc.Add(DataObjectFactory.GetPropertyObjectFor(key, value));
                }
                else
                {
                    dc.Add(DataObjectFactory.GetDataObjectFor(key, value));
                }
            }
        }

        public static void PutValue<T>(this IDataContainer dc, Key<T> key, T value) => dc.PutValue(key.Name, value);

        public static DataObject FindRecursive(this IDataContainer container, string key)
        {
            var split = key.Split('.');

            if (split.Length == 1)
            {
                return container.Find(key);
            }
            else
            {
                object temp = null;
                container.GetValue(split.First(), ref temp);

                if (temp is IDataContainer dc)
                {
                    return dc.FindRecursive(string.Join(".", split.Skip(1)));
                }
                else
                {
                    throw new Exception("Nested configs should be of type IDataContainer");
                }
            }
        }

        #endregion

        #region Set Operations

        /// <summary>
        /// Takes in 2 <see cref="IDataContainer"/> returns a new instance of <see cref="IDataContainer"/>
        /// which contains all the properties in <paramref name="lhs"/> and <paramref name="rhs"/>
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"> LHS V RHS</param>
        /// <returns></returns>
        public static IDataContainer Union(this IDataContainer lhs, IDataContainer rhs)
        {
            IDataContainer union = lhs is IPropertyContainer
                ? (IDataContainer)new PropertyContainer() 
                : (IDataContainer)new DataContainer();

            union.Name = lhs.Name;

            foreach (DataObject obj in lhs)
            {
                union.Add(obj);
            }

            foreach (DataObject obj in rhs)
            {
                if(union.ContainsData(obj.Name) == false)
                {
                    union.Add(obj);
                }
                else
                {
                    // in case of nested IDataContainer
                    if (obj.GetValue() is IDataContainer dc)
                    {
                        var unionObj = union.Find(obj.Name);

                        IDataContainer unionDC = unionObj.GetValue() as IDataContainer;

                        if (unionDC.IsIdentical(dc) == false)
                        {
                            unionObj.SetValue(unionDC.Union(dc)); 
                        }
                    }
                }
            }

            return union;
        }

        /// <summary>
        /// Takes in 2 <see cref="IDataContainer"/> returns new instance of <see cref="IDataContainer"/>
        /// which containes properties which are both in LHS and RHS, the values will be from LHS
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns>LHS ^ RHS</returns>
        public static IDataContainer Intersect(this IDataContainer lhs, IDataContainer rhs)
        {
            IDataContainer intersect = lhs is IPropertyContainer
                ? (IDataContainer)new PropertyContainer()
                : (IDataContainer)new DataContainer();

            intersect.Name = lhs.Name;

            var lhsKeys = lhs.GetKeys();
            var rhsKeys = rhs.GetKeys();

            var intersectKeys = lhsKeys.Intersect(rhsKeys);

            foreach (var key in intersectKeys)
            {
                DataObject first = lhs.Find(key);
                
                // in case of nested IDataContainer
                if (first.GetValue() is IDataContainer dc)
                {
                    var second = rhs.Find(first.Name).GetValue() as IDataContainer;

                    if (dc.IsIdentical(second) == false)
                    {
                        first.SetValue(dc.Intersect(second)); 
                    }
                }

                intersect.Add(first);
            }

            return intersect;
        }

        /// <summary>
        /// Takes in 2 <see cref="IDataContainer"/> returns a new instance of <see cref="IDataContainer"/>
        /// Which contains all the properties in LHS and RHS exception the ones that are common to both
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static IDataContainer Except(this IDataContainer lhs, IDataContainer rhs)
        {
            IDataContainer difference = lhs is IPropertyContainer
                ? (IDataContainer)new PropertyContainer()
                : (IDataContainer)new DataContainer();

            difference.Name = lhs.Name;

            foreach (var data in lhs)
            {
                difference.Add(data);
            }

            var lhsKeys = lhs.GetKeys();
            var rhsKeys = rhs.GetKeys();

            var intersectKeys = lhsKeys.Intersect(rhsKeys);

            // no need to handle nested IDataContainer, the root will be removed, no need to worry about children.
            foreach (var key in intersectKeys)
            {
                difference.Remove(lhs.Find(key));
            }

            return difference;
        }

        /// <summary>
        /// Checks whether to <see cref="IDataContainer"/> instances contains same set of properties
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool IsIdentical(this IDataContainer lhs, IDataContainer rhs)
        {
            List<string> lhsKeys = new List<string>();
            List<string> rhsKeys = new List<string>();

            lhs.GetAllKeys(lhsKeys);
            rhs.GetAllKeys(rhsKeys);

            if(lhsKeys.Count != rhsKeys.Count)
            {
                return false;
            }

            List<string> lhsKeysCopy = new List<string>(lhsKeys);
            List<string> rhsKeysCopy = new List<string>(rhsKeys);

            foreach (var key in rhsKeys)
            {
                lhsKeysCopy.Remove(key);
            }

            foreach (var key in lhsKeys)
            {
                rhsKeysCopy.Remove(key);
            }

            return lhsKeysCopy.Count == 0 && rhsKeysCopy.Count == 0;
        }

        /// <summary>
        /// Get all keys recursively if some of the children hold IDataContainers
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="keys"></param>
        /// <param name="root"></param>
        public static void GetAllKeys(this IDataContainer dc, List<string> keys, string root ="")
        {
            foreach (var data in dc)
            {
                if(data.GetValue() is IDataContainer dcInner)
                {
                    dcInner.GetAllKeys(keys, dcInner.Name);
                }
                else
                {
                    keys.Add(string.IsNullOrEmpty(root) ? data.Name : $"{root}.{data.Name}");
                }
            }
        }

        /// <summary>
        /// Add new properties from second to first, if they already exist, keep the values.
        /// Same as <see cref="Union(IDataContainer, IDataContainer)"/> but does operation inplace instead of returning new intance;
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        public static void Merge(this IDataContainer lhs, IDataContainer rhs)
        {
            foreach (var data in rhs)
            {
                if(lhs.ContainsData(data.Name) == false)
                {
                    lhs.Add(data);
                }
                else
                {
                    DataObject lhsData = lhs.Find(data.Name);
                    if(lhsData.GetValue() is IDataContainer dc)
                    {
                        IDataContainer rhsDC = data.GetValue() as IDataContainer;
                        dc.Merge(rhsDC);
                    }
                }
            }
        }


        /// <summary>
        /// Removes the properties from first which are common to first and second.
        /// Same as <see cref="Except(IDataContainer, IDataContainer)"/> but does operation in place instead of returning new instance
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        public static void Remove(this IDataContainer lhs, IDataContainer rhs)
        {
            foreach (var data in rhs)
            {
                if(lhs.Find(data.Name) is DataObject obj)
                {
                    lhs.Remove(obj);
                }
            }
        }

        /// <summary>
        /// Removes all the properties that are not in second and in first from first
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        public static void InvertedRemove(this IDataContainer lhs, IDataContainer rhs)
        {
            var toRemove = new List<DataObject>();

            foreach (var data in lhs)
            {
                if (rhs.ContainsData(data.Name) == false)
                {
                    toRemove.Add(data);
                }
                else if (data.GetValue() is IDataContainer dc)
                {
                    var rhsDC = (IDataContainer)rhs[data.Name];
                    dc.InvertedRemove(rhsDC);
                }
            }

            foreach (var item in toRemove)
            {
                lhs.Remove(item);
            }
        }


        #endregion

        /// <summary>
        /// Reload values from file again
        /// Doesn't need new properties if added, only updates existing ones
        /// </summary>
        /// <param name="dc"></param>
        public static void Refresh(this IDataContainer dc, IDataContainer changed)
        {
            foreach (var data in changed)
            {
                object value = data.GetValue();

                if (value is IDataContainer changedChild)
                {
                    DataObject dcChild = dc.Find(data.Name);
                    IDataContainer dcChildValue = dcChild.GetValue() as IDataContainer;
                    dcChildValue.Refresh(changedChild);
                }
                else
                {
                    dc.SetValue(data.Name, value);
                }
            }
        }

        /// <summary>
        /// Get instace of <see cref="DataContainerAutoSaver"/>
        /// Always returns new instance.
        /// </summary>
        /// <param name="dc"></param>
        /// <returns></returns>
        public static DataContainerAutoSaver GetAutoSaver(this IDataContainer dc) => new DataContainerAutoSaver(dc);

        /// <summary>
        /// Get instance of <see cref="DataContainerAutoUpdater"/>
        /// Always returns new instance.
        /// </summary>
        /// <param name="dc"></param>
        /// <returns></returns>
        public static DataContainerAutoUpdater GetAutoUpdater(this IDataContainer dc) => new DataContainerAutoUpdater(dc);

        /// <summary>
        /// Gets the current values in the datacontainer
        /// </summary>
        /// <param name="dc"></param>
        /// <returns></returns>
        public static SnapShot GetSnapShot(this IDataContainer dc) => new SnapShot(dc);

        /// <summary>
        /// Restore values from snapshot
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="s"></param>
        public static void Restore(this IDataContainer dc, SnapShot s)
        {
            foreach (var key in s.Keys)
            {
                if (s[key].Value is object obj)
                {
                    dc.SetValue(key, obj); 
                }
            }
        }

        public static void WriteBytes(this IDataContainer container, Stream stream)
        {
            var writer = new BinaryWriter(stream);

            foreach (var data in container)
            {
                if (data is IWriteToBinaryStream wbs)
                {
                    wbs.WriteBytes(writer);
                }
            }

        }
    }
}
