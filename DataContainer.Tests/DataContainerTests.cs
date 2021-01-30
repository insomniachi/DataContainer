﻿using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Threading;
using System.Runtime.Serialization;
using Xunit;
using KEI.Infrastructure;
using KEI.Infrastructure.Utils;
using DataContainer.Tests.TestData;
using KEI.Infrastructure.Helpers;
using System.ComponentModel;

namespace DataContainer.Tests
{
    public class DataContainerTests
    {
        [Theory]
        [InlineData("MyProperty")]
        [InlineData("MyProperty2")]
        [InlineData("My_Property")]
        [InlineData("MyProperty_1")]
        public void IdentifierExtensions_IsValidIdentifierReturnsTrue(string identifierName)
        {
            Assert.True(IdentifierExtensions.IsValidIdentifier(identifierName));
        }

        [Theory]
        [InlineData("My Property")]
        [InlineData("MyProperty 2")]
        [InlineData("2MyProperty")]
        [InlineData("My$Property")]
        [InlineData("MyProperty[0]")]
        public void IdentifierExtensions_IsValidIdentifierReturnsFalse(string identifierName)
        {
            Assert.False(IdentifierExtensions.IsValidIdentifier(identifierName));
        }

        #region Access And Manipulation

        [Fact]
        public void DataContainerBase_CanAccessChildDataFromRoot()
        {
            DataContainerBase Root = (DataContainerBase)DataContainerBuilder.Create("Root")
                .DataContainer("Child", b => b
                    .DataContainer("GrandChild", c => c
                        .Data("A", 1)
                        .Data("B", 2)))
                .Build();

            int a = Root.GetValue(new Key<int>("Child.GrandChild.A"));
            int b = Root.GetValue(new Key<int>("Child.GrandChild.B"));

            Assert.Equal(1, a);
            Assert.Equal(2, b);
        }

        [Theory]
        [InlineData(typeof(POCO))]
        public void DataContainerBase_Morph_MustRecreatesSameObject(Type t)
        {
            var obj = Activator.CreateInstance(t);

            DataContainerBase data = (DataContainerBase)DataContainerBuilder.CreateObject("Untitiled", obj);

            var morphedData = data.Morph();

            foreach (var prop in t.GetProperties())
            {
                var expected = prop.GetValue(obj);
                var actualData = prop.GetValue(morphedData);

                Assert.Equal(expected, actualData);
            }

        }


        [Theory]
        [InlineData(typeof(POCO))]
        public void DataContainerBase_Store_MustDeserialize(Type t)
        {
            var obj = Activator.CreateInstance(t);

            DataContainerBase data = (DataContainerBase)DataContainerBuilder.CreateObject("Untitiled", obj);

            var serializedData = XmlHelper.SerializeToString(data);

            var recreatedData = XmlHelper.DeserializeFromString<KEI.Infrastructure.DataContainer>(serializedData);

            Assert.NotNull(recreatedData);

            var morphedData = recreatedData.Morph();

            foreach (var prop in t.GetProperties())
            {
                var expected = prop.GetValue(obj);
                var actualData = prop.GetValue(morphedData);

                Assert.Equal(expected, actualData);
            }

        }

        [Fact]
        public void DataContainerBase_Get_MustGetCorrectValue()
        {
            const string PROP_NAME = "IntProperty";

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, 42)
                .Build();

            int prop = 0;
            bool containsProperty = property.GetValue(PROP_NAME, ref prop);

            Assert.True(containsProperty);
            Assert.NotEqual(0, prop);
            Assert.Equal(42, prop);

            int prop2 = property.GetValue<int>(PROP_NAME);

            Assert.NotEqual(0, prop2);
            Assert.Equal(42, prop2);
        }

        [Fact]
        public void DataContainerBase_Get_MustReturnDefaultIfNotPresent()
        {
            const string PROP_NAME = "IntProperty";

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, 42)
                .Build();

            int prop = 0;
            bool containsProperty = property.GetValue("blah", ref prop);

            Assert.False(containsProperty);
            Assert.Equal(default, prop);

            int prop2 = property.GetValue<int>("blah");

            Assert.Equal(default, prop2);
        }

        [Fact]
        public void DataContainerBase_Set_MustSetValue()
        {
            const string PROP_NAME = "IntProperty";
            const int VALUE = 42;
            const int SET_VALUE = 14;

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, VALUE)
                .Build();

            property.SetValue(PROP_NAME, SET_VALUE);

            int value = 0;
            property.GetValue(PROP_NAME, ref value);
            int value2 = property.GetValue<int>(PROP_NAME);

            Assert.NotEqual(VALUE, value);
            Assert.NotEqual(VALUE, value2);

            Assert.Equal(SET_VALUE, value);
            Assert.Equal(SET_VALUE, value);
        }

        [Fact]
        public void DataContainerBase_Put_MustAddValueIfNotPresent()
        {
            const string PROP_NAME = "IntProperty";
            const int SET_VALUE = 14;

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create().Build();

            Assert.False(property.ContainsData(PROP_NAME));

            property.PutValue(PROP_NAME, SET_VALUE);

            Assert.True(property.ContainsData(PROP_NAME));

            int value = 0;
            property.GetValue(PROP_NAME, ref value);

            Assert.Equal(SET_VALUE, value);
        }

        [Fact]
        public void DataContainerBase_Put_MustUpdateValueIfPresent()
        {
            const string PROP_NAME = "IntProperty";
            const int VALUE = 42;
            const int SET_VALUE = 14;

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, VALUE)
                .Build();

            int originalValue = 0;
            property.GetValue(PROP_NAME, ref originalValue);

            Assert.True(property.ContainsData(PROP_NAME));

            property.PutValue(PROP_NAME, SET_VALUE);

            Assert.True(property.ContainsData(PROP_NAME));

            int value = 0;
            property.GetValue(PROP_NAME, ref value);

            Assert.NotEqual(originalValue, value);
            Assert.Equal(SET_VALUE, value);
        }

        [Fact]
        public void DataContainerBase_Morph_T_CanMoverToObject()
        {
            DataContainerBase DC = (DataContainerBase)DataContainerBuilder.Create("POCO")
                .Data(nameof(POCO.IntProperty), 12)
                .Data(nameof(POCO.StringProperty), "Hello")
                .Build();

            POCO morphed = DC.Morph<POCO>();

            Assert.Equal(12, morphed.IntProperty);
            Assert.Equal("Hello", morphed.StringProperty);
        }

        #endregion

        #region Binding

        [Fact]
        public void DataContainerBase_SetBinding_MustBindOneWay()
        {
            const string PROP_NAME = "IntProperty";
            const int VALUE = 42;
            const int NEW_VALUE = 14;

            IPropertyContainer pc = PropertyContainerBuilder.Create()
                .Property(PROP_NAME, VALUE)
                .Build();

            var bindingTarget = new BindingTestObject();

            Assert.NotEqual(VALUE, bindingTarget.IntProperty);

            pc.SetBinding(PROP_NAME, () => bindingTarget.IntProperty, BindingMode.OneWay);

            Assert.Equal(VALUE, bindingTarget.IntProperty);

            pc.SetValue(PROP_NAME, NEW_VALUE);

            Assert.Equal(NEW_VALUE, bindingTarget.IntProperty);

            bindingTarget.IntProperty = 32;

            int propertyValue = 0;
            pc.GetValue(PROP_NAME, ref propertyValue);

            pc.RemoveBinding(PROP_NAME, () => bindingTarget.IntProperty);

            Assert.NotEqual(32, propertyValue);
            Assert.Equal(NEW_VALUE, propertyValue);

        }

        [Fact]
        public void DataContainerBase_SetBinding_MustBindTwoWay()
        {
            const string PROP_NAME = "IntProperty";
            const int VALUE = 42;
            const int NEW_VALUE = 14;

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, VALUE)
                .Build();

            var bindingTarget = new BindingTestObject();

            Assert.NotEqual(VALUE, bindingTarget.IntProperty);

            property.SetBinding(PROP_NAME, () => bindingTarget.IntProperty, BindingMode.TwoWay);

            Assert.Equal(VALUE, bindingTarget.IntProperty);

            property.SetValue(PROP_NAME, NEW_VALUE);

            Assert.Equal(NEW_VALUE, bindingTarget.IntProperty);

            bindingTarget.IntProperty = 32;

            int propertyValue = 0;
            property.GetValue(PROP_NAME, ref propertyValue);

            property.RemoveBinding(PROP_NAME, () => bindingTarget.IntProperty);

            Assert.Equal(propertyValue, bindingTarget.IntProperty);
        }

        [Fact]
        public void DataContainerBase_SetBinding_MustBindOneWayToSource()
        {
            const string PROP_NAME = "IntProperty";
            const int VALUE = 42;
            const int NEW_VALUE = 14;

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, VALUE)
                .Build();

            var bindingTarget = new BindingTestObject();

            Assert.NotEqual(VALUE, bindingTarget.IntProperty);

            property.SetBinding(PROP_NAME, () => bindingTarget.IntProperty, BindingMode.OneWayToSource);

            Assert.Equal(VALUE, bindingTarget.IntProperty);

            property.SetValue(PROP_NAME, NEW_VALUE);

            Assert.NotEqual(NEW_VALUE, bindingTarget.IntProperty);
            Assert.Equal(VALUE, bindingTarget.IntProperty);

            bindingTarget.IntProperty = 32;

            int propertyValue = 0;
            property.GetValue(PROP_NAME, ref propertyValue);

            property.RemoveBinding(PROP_NAME, () => bindingTarget.IntProperty);

            Assert.Equal(propertyValue, bindingTarget.IntProperty);
        }

        [Fact]
        public void DataContainerBase_SetBinding_MustBindOneTime()
        {
            const string PROP_NAME = "IntProperty";
            const int VALUE = 42;
            const int NEW_VALUE = 14;

            DataContainerBase property = (DataContainerBase)PropertyContainerBuilder.Create()
                .Property(PROP_NAME, VALUE)
                .Build();

            var bindingTarget = new BindingTestObject();

            Assert.NotEqual(VALUE, bindingTarget.IntProperty);

            property.SetBinding(PROP_NAME, () => bindingTarget.IntProperty, BindingMode.OneTime);

            Assert.Equal(VALUE, bindingTarget.IntProperty);

            property.SetValue(PROP_NAME, NEW_VALUE);

            Assert.NotEqual(NEW_VALUE, bindingTarget.IntProperty);
            Assert.Equal(VALUE, bindingTarget.IntProperty);

            bindingTarget.IntProperty = 32;

            int propertyValue = 0;
            property.GetValue(PROP_NAME, ref propertyValue);

            property.RemoveBinding(PROP_NAME, () => bindingTarget.IntProperty);

            Assert.NotEqual(32, propertyValue);
            Assert.Equal(NEW_VALUE, propertyValue);
        }

        #endregion

        #region Set Operations

        [Fact]
        public void IDataContainer_Union_UsesFirstsNameForResultName()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 22)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("B", 33)
                .Build();

            IDataContainer AunionB = A.Union(B);
            IDataContainer BunionA = B.Union(A);

            Assert.Equal(A.Name, AunionB.Name);
            Assert.Equal(B.Name, BunionA.Name);
        }

        [Fact]
        public void IDataContainer_Union_UsesValuesFromFirstForSameProperty()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .Data("B", 1)
                .Build();

            IDataContainer AunionB = A.Union(B);
            IDataContainer BunionA = B.Union(A);

            int AA = (int)A["A"];
            int AB = (int)A["B"];

            int BA = (int)B["A"];
            int BB = (int)B["B"];

            int AUBA = (int)AunionB["A"];
            int AUBB = (int)AunionB["B"];
            int BUAA = (int)BunionA["A"];
            int BUAB = (int)BunionA["B"];

            Assert.Equal(AA, AUBA);
            Assert.Equal(AB, AUBB);

            Assert.Equal(BA, BUAA);
            Assert.Equal(BB, BUAB);
        }

        [Fact]
        public void IDataContainer_Union_MustWorkWithSingleLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("C", 3)
                .Data("E", 5)
                .Data("G", 7)
                .Data("Z", 26)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("A")
                .Data("B", 2)
                .Data("D", 4)
                .Data("F", 6)
                .Data("H", 8)
                .Data("Z", 28)
                .Build();

            IDataContainer AunionB = A.Union(B);

            Assert.Equal(9, AunionB.Count); // 4 + 4 + 1 (common)

            var keys = AunionB.GetKeys().ToList();
            keys.Sort();
            Assert.Equal("ABCDEFGHZ", string.Join("", keys)); // all keys are present.
        }

        [Fact]
        public void IDataContainer_Union_MustWorkWithMultiLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .DataContainer("AA", b => b
                    .Data("A1", 11)
                    .Data("A2", 12)
                    .Data("A3", 13))
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C1", 31)
                    .Data("C3", 33))
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 1)
                .Data("B", 2)
                .DataContainer("AA", b => b
                    .Data("A4", 14)
                    .Data("A5", 15)
                    .Data("A6", 16))
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C2", 32))
                .Build();

            IDataContainer AunionB = A.Union(B);

            Assert.Equal(5, AunionB.Count); // both have same keys, 5

            IDataContainer AA = (IDataContainer)AunionB["AA"];
            Assert.Equal(6, AA.Count); // 3 + 3

            IDataContainer CC = (IDataContainer)AunionB["CC"];
            Assert.Equal(3, CC.Count); // 2 + 1
        }

        [Fact]
        public void IDataContainer_Intersect_UsesValuesFromFirstForSameProperty()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .Data("B", 1)
                .Build();

            IDataContainer AintersectB = A.Intersect(B);
            IDataContainer BintersectA = B.Intersect(A);

            int AA = (int)A["A"];
            int AB = (int)A["B"];

            int BA = (int)B["A"];
            int BB = (int)B["B"];

            int AUBA = (int)AintersectB["A"];
            int AUBB = (int)AintersectB["B"];
            int BUAA = (int)BintersectA["A"];
            int BUAB = (int)BintersectA["B"];

            Assert.Equal(AA, AUBA);
            Assert.Equal(AB, AUBB);

            Assert.Equal(BA, BUAA);
            Assert.Equal(BB, BUAB);
        }

        [Fact]
        public void IDataContainer_Intersect_UsesFirstsNameForResultName()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 22)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 33)
                .Build();

            IDataContainer AintersectB = A.Intersect(B);
            IDataContainer BintersectA = B.Intersect(A);

            Assert.Equal(A.Name, AintersectB.Name);
            Assert.Equal(B.Name, BintersectA.Name);
        }

        [Fact]
        public void IDataContainer_Intersect_MustWorkWithSingleLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("C", 3)
                .Data("E", 5)
                .Data("G", 7)
                .Data("Z", 26)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("A")
                .Data("B", 2)
                .Data("D", 4)
                .Data("F", 6)
                .Data("H", 8)
                .Data("Z", 28)
                .Build();

            IDataContainer AintersectB = A.Intersect(B);

            Assert.Equal(1, AintersectB.Count); // only 1 common

            var keys = AintersectB.GetKeys().ToList();
            keys.Sort();
            Assert.Equal("Z", string.Join("", keys)); // all keys are present.
        }

        [Fact]
        public void IDataContainer_Intersect_MustWorkWithMultiLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .DataContainer("AA", b => b
                    .Data("A1", 11)
                    .Data("A2", 12)
                    .Data("A3", 13))
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C1", 31)
                    .Data("C3", 33))
                .DataContainer("BB", b => b
                    .Data("B1", 21)
                    .Data("B2", 22))
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 1)
                .Data("B", 2)
                .DataContainer("AA", b => b
                    .Data("A4", 14)
                    .Data("A5", 15)
                    .Data("A6", 16))
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C2", 32))
                .DataContainer("BB", b => b
                    .Data("B1", 23)
                    .Data("B2", 24)
                    .Data("B3", 25))
                .Build();

            IDataContainer AintersectB = A.Intersect(B);

            Assert.Equal(6, AintersectB.Count); // both have same keys, 6

            IDataContainer AA = (IDataContainer)AintersectB["AA"];
            Assert.Equal(0, AA.Count); // nothing common

            IDataContainer CC = (IDataContainer)AintersectB["CC"];
            Assert.Equal(0, CC.Count); // nothing common

            IDataContainer BB = (IDataContainer)AintersectB["BB"];
            Assert.Equal(2, BB.Count); // 2 common, 1 only in B
        }

        [Fact]
        public void IDataContainer_IsIdenticalReturnsTrueForSingleLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .Data("B", 3)
                .Data("C", 4)
                .Build();

            Assert.True(A.IsIdentical(B));
            Assert.True(B.IsIdentical(A));
        }

        [Fact]
        public void IDataContainer_IsIdenticalReturnsFalseForSingleLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .Data("B", 3)
                .Data("C", 4)
                .Build();

            Assert.False(A.IsIdentical(B));
            Assert.False(B.IsIdentical(A));
        }

        [Fact]
        public void IDataContainer_IsIdenticalReturnsTrueForMultiLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .DataContainer("AA", b => b
                    .Data("A1", 1)
                    .Data("A2", 2)
                    .Data("A3", 3))
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2))
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .DataContainer("AA", b => b
                    .Data("A1", 1)
                    .Data("A2", 2)
                    .Data("A3", 3))
                .Data("B", 3)
                .Data("C", 4)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2))
                .Build();

            Assert.True(A.IsIdentical(B));
            Assert.True(B.IsIdentical(A));
        }

        [Fact]
        public void IDataContainer_IsIdenticalReturnsFalseForMultiLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .DataContainer("AA", b => b
                    .Data("A1", 1)
                    .Data("A3", 3))
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2))
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .DataContainer("AA", b => b
                    .Data("A1", 1)
                    .Data("A2", 2)
                    .Data("A3", 3))
                .Data("B", 3)
                .Data("C", 4)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2))
                .Build();

            Assert.False(A.IsIdentical(B));
            Assert.False(B.IsIdentical(A));
        }

        [Fact]
        public void IDataContainer_IsIdenticalReturnsFalseForMultiLevelDataWithSameNumberOfKeys()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .DataContainer("AA", b => b
                    .Data("A1", 1)
                    .Data("A3", 3))
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2))
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .DataContainer("AA", b => b
                    .Data("A3", 3))
                .Data("B", 3)
                .Data("C", 4)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2)
                    .Data("C3", 3))
                .Build();

            Assert.False(A.IsIdentical(B));
            Assert.False(B.IsIdentical(A));
        }

        [Fact]
        public void IDataContainer_Difference_RemovesCommonProperties()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("AA", b => b
                    .Data("A1", 11)
                    .Data("A2", 12))
                .Data("D", 4)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("F", 1)
                .Data("B", 2)
                .Data("C", 3)
                .Data("X", 4)
                .Data("Y", 2)
                .Data("Z", 3)
                .Build();

            IDataContainer AdifferenceB = A.Except(B);
            IDataContainer BdifferenceA = B.Except(A);

            Assert.Equal(3, AdifferenceB.Count); // remove B,C from A
            Assert.Equal(4, BdifferenceA.Count); // remove B,C from B
        }

        [Fact]
        public void IDataContainer_Merge_MergesSingleLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("Z", 26)
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .Data("B", 3)
                .Data("C", 4)
                .Data("D", 4)
                .Build();

            A.Merge(B);

            Assert.Equal(5, A.Count);
        }

        [Fact]
        public void IDataContainer_Merge_MergesMultiLevelData()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .DataContainer("AA", b => b
                    .Data("A1", 1)
                    .Data("A3", 3))
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("CC", b => b
                    .Data("C5", 1)
                    .Data("C6", 2))
                .Build();

            IDataContainer B = DataContainerBuilder.Create("B")
                .Data("A", 2)
                .DataContainer("AA", b => b
                    .Data("A2", 3))
                .Data("B", 3)
                .Data("C", 4)
                .DataContainer("CC", b => b
                    .Data("C1", 1)
                    .Data("C2", 2)
                    .Data("C3", 3))
                .Build();

            A.Merge(B);

            Assert.Equal(5, A.Count);

            IDataContainer AA = (IDataContainer)A["AA"];
            Assert.Equal(3, AA.Count);

            IDataContainer CC = (IDataContainer)A["CC"];
            Assert.Equal(5, CC.Count);
        }

        #endregion

        #region Creation 

        [Theory]
        [InlineData(typeof(POCO), 6)]
        [InlineData(typeof(NestedPOCO), 7)]
        [InlineData(typeof(UninitializedPOCO), 4)] // Should not create data for null properties, or should it ??
        public void DataContainerBuilder_CreateObject_MustCreatesObjectWithCorrectCount(Type t, int count)
        {
            var obj = Activator.CreateInstance(t);

            IDataContainer data = DataContainerBuilder.CreateObject("Untitiled", obj);

            Assert.Equal(count, data.Count);
        }

        [Theory]
        [InlineData(typeof(POCO), 6)]
        [InlineData(typeof(NestedPOCO), 7)]
        [InlineData(typeof(UninitializedPOCO), 4)] // Should not create data for null properties, or should it ??
        public void PropertyContainerBuilder_CreateObject_MustCreatesObjectWithCorrectCount(Type t, int count)
        {
            var obj = Activator.CreateInstance(t);

            IPropertyContainer property = PropertyContainerBuilder.CreateObject("Untitled", obj);

            Assert.Equal(count, property.Count);
        }

        [Theory]
        [InlineData(typeof(POCO))]
        public void DataContainerBuilder_CreateObject_MustCreateObjectWithCorrectValue(Type t)
        {
            var obj = Activator.CreateInstance(t);

            IDataContainer data = DataContainerBuilder.CreateObject("Untitiled", obj);

            foreach (var item in data)
            {
                var expected = t.GetProperty(item.Name).GetValue(obj);
                var actual = item.GetValue();
                Assert.Equal(expected, actual);
            }
        }

        [Theory]
        [InlineData(typeof(POCO))]
        public void PropertyContainerBuilder_CreateObject_MustCreateObjectWithCorrectValue(Type t)
        {
            var obj = Activator.CreateInstance(t);

            IPropertyContainer property = PropertyContainerBuilder.CreateObject("Untitled", obj);

            foreach (var item in property)
            {
                var expected = t.GetProperty(item.Name).GetValue(obj);
                var actual = item.GetValue();
                Assert.Equal(expected, actual);
            }
        }

        [Theory]
        [InlineData(DataObjectType.Short, typeof(ShortDataObject), typeof(short))]
        [InlineData(DataObjectType.Integer, typeof(IntDataObject), typeof(int))]
        [InlineData(DataObjectType.Long, typeof(LongDataObject), typeof(long))]
        [InlineData(DataObjectType.UShort, typeof(UnsignedShortDataObject), typeof(ushort))]
        [InlineData(DataObjectType.UInteger, typeof(UnsignedIntDataObject), typeof(uint))]
        [InlineData(DataObjectType.ULong, typeof(UnsignedLongDataObject), typeof(ulong))]
        [InlineData(DataObjectType.Float, typeof(FloatDataObject), typeof(float))]
        [InlineData(DataObjectType.Double, typeof(DoubleDataObject), typeof(double))]
        [InlineData(DataObjectType.Char, typeof(CharDataObject), typeof(char))]
        [InlineData(DataObjectType.String, typeof(StringDataObject), typeof(string))]
        [InlineData(DataObjectType.Password, typeof(PasswordDataObject), typeof(string))]
        [InlineData(DataObjectType.DateTime, typeof(DateTimeDataObject), typeof(DateTime))]
        [InlineData(DataObjectType.TimeSpan, typeof(TimeSpanDataObject), typeof(TimeSpan))]
        [InlineData(DataObjectType.Color, typeof(ColorDataObject), typeof(Color))]
        [InlineData(DataObjectType.Point, typeof(PointDataObject), typeof(Point))]
        [InlineData(DataObjectType.Array1D, typeof(Array1DDataObject), typeof(int[]))]
        [InlineData(DataObjectType.Array2D, typeof(Array2DDataObject), typeof(int[,]))]
        public void DataObjectFactory_GetDataObject_CreatesCorrectObject(string type, Type resultType, Type valueType)
        {
            var valueProvider = new DefaultValueProvider();
            object defaultValue = valueProvider.GetValue(valueType);

            DataObject obj = DataObjectFactory.GetDataObject(type, "a", defaultValue);

            Assert.Equal(resultType, obj.GetType());
            Assert.Equal(defaultValue, obj.GetValue());
        }

        [Theory]
        [InlineData(DataObjectType.Short, typeof(ShortPropertyObject), typeof(short))]
        [InlineData(DataObjectType.Integer, typeof(IntPropertyObject), typeof(int))]
        [InlineData(DataObjectType.Long, typeof(LongPropertyObject), typeof(long))]
        [InlineData(DataObjectType.UShort, typeof(UnsignedShortPropertyObject), typeof(ushort))]
        [InlineData(DataObjectType.UInteger, typeof(UnsignedIntPropertyObject), typeof(uint))]
        [InlineData(DataObjectType.ULong, typeof(UnsignedLongPropertyObject), typeof(ulong))]
        [InlineData(DataObjectType.Float, typeof(FloatPropertyObject), typeof(float))]
        [InlineData(DataObjectType.Double, typeof(DoublePropertyObject), typeof(double))]
        [InlineData(DataObjectType.Char, typeof(CharPropertyObject), typeof(char))]
        [InlineData(DataObjectType.String, typeof(StringPropertyObject), typeof(string))]
        [InlineData(DataObjectType.File, typeof(FilePropertyObject), typeof(string))]
        [InlineData(DataObjectType.Folder, typeof(FolderPropertyObject), typeof(string))]
        [InlineData(DataObjectType.Password, typeof(PasswordPropertyObject), typeof(string))]
        [InlineData(DataObjectType.DateTime, typeof(DateTimePropertyObject), typeof(DateTime))]
        [InlineData(DataObjectType.TimeSpan, typeof(TimeSpanPropertyObject), typeof(TimeSpan))]
        [InlineData(DataObjectType.Color, typeof(ColorPropertyObject), typeof(Color))]
        [InlineData(DataObjectType.Point, typeof(PointPropertyObject), typeof(Point))]
        [InlineData(DataObjectType.Array1D, typeof(Array1DPropertyObject), typeof(int[]))]
        [InlineData(DataObjectType.Array2D, typeof(Array2DPropertyObject), typeof(int[,]))]
        public void DataObjectFactory_GetPropertyObject_CreatesCorrectObject(string type, Type resultType, Type valueType)
        {
            var valueProvider = new DefaultValueProvider();
            object defaultValue = valueProvider.GetValue(valueType);

            DataObject obj = DataObjectFactory.GetPropertyObject(type, "a", defaultValue);

            Assert.Equal(resultType, obj.GetType());
            Assert.Equal(defaultValue, obj.GetValue());
        }

        [Theory]
        [InlineData(DataObjectType.Short,typeof(short))]
        [InlineData(DataObjectType.Integer, typeof(int))]
        [InlineData(DataObjectType.Long, typeof(long))]
        [InlineData(DataObjectType.UShort, typeof(ushort))]
        [InlineData(DataObjectType.UInteger, typeof(uint))]
        [InlineData(DataObjectType.ULong, typeof(ulong))]
        [InlineData(DataObjectType.Float, typeof(float))]
        [InlineData(DataObjectType.Double, typeof(double))]
        [InlineData(DataObjectType.String, typeof(string))]
        [InlineData(DataObjectType.DateTime, typeof(DateTime))]
        [InlineData(DataObjectType.TimeSpan, typeof(TimeSpan))]
        [InlineData(DataObjectType.Point, typeof(Point))]
        [InlineData(DataObjectType.Color, typeof(Color))]
        [InlineData(DataObjectType.Array1D, typeof(int[]))]
        [InlineData(DataObjectType.Array2D, typeof(int[,]))]
        public void DataObjectFactory_GetDataObjectFor_CreatesCorrectObject(string dataObjectType, Type valueType)
        {
            var valueProvider = new DefaultValueProvider();
            object defaultValue = valueProvider.GetValue(valueType);

            DataObject obj = DataObjectFactory.GetDataObjectFor("name", defaultValue);
            Assert.Equal(dataObjectType, obj.Type);
        }

        [Theory]
        [InlineData(DataObjectType.Short, typeof(short))]
        [InlineData(DataObjectType.Integer, typeof(int))]
        [InlineData(DataObjectType.Long, typeof(long))]
        [InlineData(DataObjectType.UShort, typeof(ushort))]
        [InlineData(DataObjectType.UInteger, typeof(uint))]
        [InlineData(DataObjectType.ULong, typeof(ulong))]
        [InlineData(DataObjectType.Float, typeof(float))]
        [InlineData(DataObjectType.Double, typeof(double))]
        [InlineData(DataObjectType.String, typeof(string))]
        [InlineData(DataObjectType.DateTime, typeof(DateTime))]
        [InlineData(DataObjectType.TimeSpan, typeof(TimeSpan))]
        [InlineData(DataObjectType.Point, typeof(Point))]
        [InlineData(DataObjectType.Color, typeof(Color))]
        [InlineData(DataObjectType.Array1D, typeof(int[]))]
        [InlineData(DataObjectType.Array2D, typeof(int[,]))]
        public void DataObjectFactory_GetPropertyObjectFor_CreatesCorrectObject(string dataObjectType, Type valueType)
        {
            var valueProvider = new DefaultValueProvider();
            object defaultValue = valueProvider.GetValue(valueType);

            DataObject obj = DataObjectFactory.GetPropertyObjectFor("name", defaultValue);
            Assert.Equal(dataObjectType, obj.Type);
        }

        #endregion

        #region Change Notification

        [Fact]
        public void IDataContainer_ShouldRaisePropertyChanged()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .Build();

            var listener = new PropertyChangedListener(A);

            A["A"] = 55;
            Assert.Equal("A", listener.LastChangedProperty);
            Assert.Single(listener.PropertiesChanged);

            A["B"] = 23;
            Assert.Equal("B", listener.LastChangedProperty);
            Assert.Equal(2, listener.PropertiesChanged.Count);

            A["C"] = 29;
            Assert.Equal("C", listener.LastChangedProperty);
            Assert.Equal(3, listener.PropertiesChanged.Count);
            
            Assert.Contains("A", listener.PropertiesChanged);
            Assert.Contains("B", listener.PropertiesChanged);
            Assert.Contains("C", listener.PropertiesChanged);
        }

        [Fact]
        public void IDataContainer_ShouldRaisePropertyChangedForNestedContainers()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("AA", b => b
                    .Data("A1", 23)
                    .Data("A2", 24)
                    .DataContainer("AAA", b => b
                        .Data("AA1", 3)))
                .Build();

            var listener = new PropertyChangedListener(A);

            A["A"] = 55;
            Assert.Equal("A", listener.LastChangedProperty);
            Assert.Single(listener.PropertiesChanged);

            IDataContainer AA = (IDataContainer)A["AA"];
            AA["A2"] = 42;
            Assert.Equal("AA.A2", listener.LastChangedProperty);
            Assert.Equal(2, listener.PropertiesChanged.Count);

            IDataContainer AAA = (IDataContainer)AA["AAA"];
            AAA["AA1"] = 5;
            Assert.Equal("AA.AAA.AA1", listener.LastChangedProperty);
            Assert.Equal(3, listener.PropertiesChanged.Count);

        }

        [Fact]
        public void IDataContainer_ShouldNotRaisePropertyChangedIfValueSame()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .Build();

            var listener = new PropertyChangedListener(A);

            A["A"] = 1;
            Assert.True(string.IsNullOrEmpty(listener.LastChangedProperty));
            Assert.Empty(listener.PropertiesChanged);

            A["A"] = 2;
            Assert.Equal("A", listener.LastChangedProperty);
            Assert.NotEmpty(listener.PropertiesChanged);
        }

        [Fact]
        public void IDataContainer_ObjectDataObjectRaisesPropertyChangedWhenCLRObjectChanges()
        {
            BindingTestObject obj = new BindingTestObject();

            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("Obj1", obj, SerializationFormat.Container)
                .Data("Obj2", obj, SerializationFormat.Xml)
                .Data("Obj3", obj, SerializationFormat.Json)
                .Build();

            var listener = new PropertyChangedListener(A);

            obj.IntProperty = 44;

            Assert.Contains("Obj1", listener.PropertiesChanged); 
            Assert.Contains("Obj2", listener.PropertiesChanged);
            Assert.Contains("Obj3", listener.PropertiesChanged);
            Assert.Equal("Obj3", listener.LastChangedProperty);
        }

        #endregion

        #region AutoSave

        [Fact]
        public void DataContainerAutoSaver_ShouldAutoSave()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .Build();

            var tester = new AutoSaveTester(A);

            Assert.False(tester.SavingStartedInvoked);
            Assert.False(tester.SavingFinishedInvoked);

            A["B"] = 5;

            // wait timer
            Thread.Sleep((int)tester.AutoSaver.SaveDelay + 1000);

            Assert.True(tester.SavingStartedInvoked);
            Assert.True(tester.SavingFinishedInvoked);
        }

        [Fact]
        public void DataContainerAutoSaver_ShouldAutoSaveWithFilters()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .Build();

            var tester = new AutoSaveTester(A);
            tester.AutoSaver.UseFilters = true;
            tester.AutoSaver.AddFilter("D");

            Assert.False(tester.SavingStartedInvoked);
            Assert.False(tester.SavingFinishedInvoked);

            A["B"] = 5;

            // wait timer
            Thread.Sleep((int)tester.AutoSaver.SaveDelay + 1000);

            Assert.False(tester.SavingStartedInvoked);
            Assert.False(tester.SavingFinishedInvoked);

            A["D"] = 10;

            // wait timer
            Thread.Sleep((int)tester.AutoSaver.SaveDelay + 1000);

            Assert.True(tester.SavingStartedInvoked);
            Assert.True(tester.SavingFinishedInvoked);
        }

        [Fact]
        public void DataContainerAutoSaver_WillNotUseFiltersIfUseFilterIsFalse()
        {
            IDataContainer A = DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .Build();

            var tester = new AutoSaveTester(A);
            tester.AutoSaver.UseFilters = false;
            tester.AutoSaver.AddFilter("D");

            Assert.False(tester.SavingStartedInvoked);
            Assert.False(tester.SavingFinishedInvoked);

            A["B"] = 5;

            // wait timer
            Thread.Sleep((int)tester.AutoSaver.SaveDelay + 1000);

            Assert.True(tester.SavingStartedInvoked);
            Assert.True(tester.SavingFinishedInvoked);
        }

        #endregion

        #region AutoUpdate

        [Fact]
        public void DataContainerAutoUpdater_ShouldUpdateValues()
        {
            string path = Path.GetTempFileName();

            DataContainerBase A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .Build();

            A.FilePath = path;
            A.Store(path);

            var tester = new AutoUpateTester(A);

            Assert.False(tester.UpdateStartedInvoked);
            Assert.False(tester.UpdateFinishedInvoked);

            IDataContainer Updated = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 5)
                .Build();

            Updated.Store(path);

            // wait timer
            Thread.Sleep((int)tester.AutoUpdater.PollingInterval + 1000);

            Assert.True(tester.UpdateStartedInvoked);
            Assert.True(tester.UpdateFinishedInvoked);

            Assert.Equal(5, (int)A["D"]);
            Assert.Equal(Updated["D"], A["D"]);

            File.Delete(path);
        }

        [Fact]
        public void DataContainerAutoUpdater_ShouldUpdateValuesWithMultiLevelData()
        {
            string path = Path.GetTempFileName();

            DataContainerBase A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1))
                .Build();

            A.FilePath = path;
            A.Store(path);

            var tester = new AutoUpateTester(A);

            Assert.False(tester.UpdateStartedInvoked);
            Assert.False(tester.UpdateFinishedInvoked);

            IDataContainer Updated = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 2))
                .Build();

            Updated.Store(path);

            // wait timer
            Thread.Sleep((int)tester.AutoUpdater.PollingInterval + 1000);

            Assert.True(tester.UpdateStartedInvoked);
            Assert.True(tester.UpdateFinishedInvoked);

            IDataContainer AB = (IDataContainer)A["AB"];
            IDataContainer U_AB = (IDataContainer)Updated["AB"];

            Assert.Equal(2, (int)AB["AB1"]);
            Assert.Equal(U_AB["AB1"], AB["AB1"]);

            File.Delete(path);
        }

        [Fact]
        public void DataContainerAutoUpdater_ShouldAddNewValues()
        {
            string path = Path.GetTempFileName();

            DataContainerBase A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .Build();

            A.FilePath = path;
            A.Store(path);

            var tester = new AutoUpateTester(A);

            Assert.False(tester.UpdateStartedInvoked);
            Assert.False(tester.UpdateFinishedInvoked);

            // allow adding new items
            tester.AutoUpdater.CanAddItems = true;

            IDataContainer Updated = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .Data("D", 5)
                .Build();

            Updated.Store(path);

            // wait timer
            Thread.Sleep((int)tester.AutoUpdater.PollingInterval + 1000);

            Assert.True(tester.UpdateStartedInvoked);
            Assert.True(tester.UpdateFinishedInvoked);

            // 1 new property added
            Assert.Equal(4, A.Count);
            // new value is present
            Assert.True(A.ContainsData("C"));
            // has correct value
            Assert.Equal(3, (int)A["C"]);

            File.Delete(path);
        }

        [Fact]
        public void DataContainerAutoUpdater_ShouldAddNewValuesWithMultiLevelData()
        {
            string path = Path.GetTempFileName();

            DataContainerBase A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1))
                .Build();

            A.FilePath = path;
            A.Store(path);

            var tester = new AutoUpateTester(A);

            // allow adding
            tester.AutoUpdater.CanAddItems = true;

            Assert.False(tester.UpdateStartedInvoked);
            Assert.False(tester.UpdateFinishedInvoked);

            IDataContainer Updated = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1)
                    .Data("AB2", 2))
                .Build();

            Updated.Store(path);

            // wait timer
            Thread.Sleep((int)tester.AutoUpdater.PollingInterval + 1000);

            Assert.True(tester.UpdateStartedInvoked);
            Assert.True(tester.UpdateFinishedInvoked);

            IDataContainer AB = (IDataContainer)A["AB"];

            // 1 value added
            Assert.Equal(2, AB.Count);
            // check if value exist
            Assert.True(AB.ContainsData("AB2"));
            // check it has correct value
            Assert.Equal(2, (int)AB["AB2"]);

            File.Delete(path);
        }

        [Fact]
        public void DataContainerAutoUpdater_ShouldRemoveValues()
        {
            string path = Path.GetTempFileName();

            DataContainerBase A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .Build();

            A.FilePath = path;
            A.Store(path);

            var tester = new AutoUpateTester(A);

            Assert.False(tester.UpdateStartedInvoked);
            Assert.False(tester.UpdateFinishedInvoked);

            // allow removing items
            tester.AutoUpdater.CanRemoveItems = true;

            IDataContainer Updated = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("D", 5)
                .Build();

            Updated.Store(path);

            // wait timer
            Thread.Sleep((int)tester.AutoUpdater.PollingInterval + 1000);

            Assert.True(tester.UpdateStartedInvoked);
            Assert.True(tester.UpdateFinishedInvoked);

            // 1 new property removed
            Assert.Equal(2, A.Count);
            // new value is not present
            Assert.False(A.ContainsData("B"));

            File.Delete(path);
        }

        [Fact]
        public void DataContainerAutoUpdater_ShouldRemoveValuesWithMultiLevelData()
        {
            string path = Path.GetTempFileName();

            DataContainerBase A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1)
                    .Data("AB2", 2))
                .Build();

            A.FilePath = path;
            A.Store(path);

            var tester = new AutoUpateTester(A);

            // allow adding
            tester.AutoUpdater.CanRemoveItems = true;

            Assert.False(tester.UpdateStartedInvoked);
            Assert.False(tester.UpdateFinishedInvoked);

            IDataContainer Updated = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("D", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1))
                .Build();

            Updated.Store(path);

            // wait timer
            Thread.Sleep((int)tester.AutoUpdater.PollingInterval + 1000);

            Assert.True(tester.UpdateStartedInvoked);
            Assert.True(tester.UpdateFinishedInvoked);

            IDataContainer AB = (IDataContainer)A["AB"];

            // 1 value removed
            Assert.Equal(1, AB.Count);
            // check if value exist
            Assert.False(AB.ContainsData("AB2"));

            File.Delete(path);
        }


        #endregion

        #region DataObjects
        
        [Theory]
        [InlineData(typeof(ShortDataObject), (short)42)]
        [InlineData(typeof(IntDataObject), 42)]
        [InlineData(typeof(LongDataObject), (long)42)]
        [InlineData(typeof(UnsignedShortDataObject), (ushort)42)]
        [InlineData(typeof(UnsignedIntDataObject), (uint)42)]
        [InlineData(typeof(UnsignedLongDataObject), (ulong)42)]
        [InlineData(typeof(DoubleDataObject), 3.14)]
        [InlineData(typeof(FloatDataObject), 1.42f)]
        [InlineData(typeof(ByteDataObject), (byte)22)]
        [InlineData(typeof(BoolDataObject), true)]
        [InlineData(typeof(CharDataObject), '!')]
        [InlineData(typeof(EnumDataObject), EntityHandling.ExpandEntities)]
        [InlineData(typeof(ShortPropertyObject), (short)42)]
        [InlineData(typeof(IntPropertyObject), 42)]
        [InlineData(typeof(LongPropertyObject), (long)42)]
        [InlineData(typeof(UnsignedShortPropertyObject), (ushort)42)]
        [InlineData(typeof(UnsignedIntPropertyObject), (uint)42)]
        [InlineData(typeof(UnsignedLongPropertyObject), (ulong)42)]
        [InlineData(typeof(DoublePropertyObject), 3.14)]
        [InlineData(typeof(FloatPropertyObject), 1.42f)]
        [InlineData(typeof(BytePropertyObject), (byte)22)]
        [InlineData(typeof(BoolPropertyObject), true)]
        [InlineData(typeof(CharPropertyObject), '!')]
        [InlineData(typeof(EnumPropertyObject), EntityHandling.ExpandEntities)]
        public void DataObject_Serialization_Primitive(Type type, object value)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", value);

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            // assert equal
            Assert.Equal(orig.GetValue(), deserialized.GetValue());
        }

        [Theory]
        [InlineData(typeof(ColorDataObject), 255,255,255)]
        [InlineData(typeof(ColorPropertyObject), 255, 255, 255)]
        public void DataObject_Serialization_Color(Type type, byte R, byte G, byte B)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", new Color(R,G,B));

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            // assert equal
            Assert.Equal(orig.GetValue(), deserialized.GetValue());
        }

        [Theory]
        [InlineData(typeof(PointDataObject), 12, 55)]
        [InlineData(typeof(PointPropertyObject), 12, 55)]
        public void DataObject_Serialization_Point(Type type, double x, double y)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", new Point(x,y));

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            // assert equal
            Assert.Equal(orig.GetValue(), deserialized.GetValue());
        }

        [Theory]
        [InlineData(typeof(DateTimeDataObject))]
        [InlineData(typeof(DateTimePropertyObject))]
        public void DataObject_Serialization_DateTime(Type type)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", DateTime.Now);

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            DateTime origValue = (DateTime)orig.GetValue();
            DateTime serializedValue = (DateTime)deserialized.GetValue();

            // assert equal
            // check time and date separately, 
            Assert.Equal(origValue.Date, serializedValue.Date);
            Assert.Equal(origValue.TimeOfDay.Hours, serializedValue.TimeOfDay.Hours);
            Assert.Equal(origValue.TimeOfDay.Minutes, serializedValue.TimeOfDay.Minutes);
            Assert.Equal(origValue.TimeOfDay.Seconds, serializedValue.TimeOfDay.Seconds);

            //not serializing milliseconds so do check that
        }

        [Theory]
        [InlineData(typeof(TimeSpanDataObject))]
        [InlineData(typeof(TimeSpanPropertyObject))]
        public void DataObject_Serialization_TimeSpan(Type type)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", TimeSpan.FromSeconds(144));

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            // assert equal
            Assert.Equal(orig.GetValue(), deserialized.GetValue());
        }

        [Theory]
        [InlineData(typeof(Array1DDataObject), 5 , 10)]
        [InlineData(typeof(Array1DPropertyObject), 5, 10)]
        public void DataObject_Serialization_Array1D(Type type, int size, object value)
        {
            Array origValue = Array.CreateInstance(value.GetType(), size);
            for (int i = 0; i < size; i++)
            {
                origValue.SetValue(value, i);
            }

            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", origValue);

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            Array deserializedValue = deserialized.GetValue() as Array;

            // assert dimension
            Assert.Equal(origValue.Rank, deserializedValue.Rank);

            // assert values
            for (int i = 0; i < size; i++)
            {
                Assert.Equal(origValue.GetValue(i), deserializedValue.GetValue(i));
            }
        }

        [Theory]
        [InlineData(typeof(Array2DDataObject), 5, 10, 3)]
        [InlineData(typeof(Array2DPropertyObject), 5, 10, 3)]
        public void DataObject_Serialization_Array2D(Type type, int rows, int columns, object value)
        {
            Array origValue = Array.CreateInstance(value.GetType(), rows, columns);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    origValue.SetValue(value, i, j);
                }
            }

            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", origValue);

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            Array deserializedValue = deserialized.GetValue() as Array;

            // assert dimension
            Assert.Equal(origValue.Rank, deserializedValue.Rank);
            Assert.Equal(origValue.GetLength(0), deserializedValue.GetLength(0));
            Assert.Equal(origValue.GetLength(1), deserializedValue.GetLength(1));

            // assert values
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    Assert.Equal(origValue.GetValue(i, j), deserializedValue.GetValue(i, j));
                }
            }
        }

        [Theory]
        [InlineData(typeof(ContainerDataObject))]
        [InlineData(typeof(XmlDataObject))]
        [InlineData(typeof(JsonDataObject))]
        [InlineData(typeof(ContainerPropertyObject))]
        [InlineData(typeof(XmlPropertyObject))]
        [InlineData(typeof(JsonPropertyObject))]
        public void DataObject_Serialization_Object(Type type)
        {
            BindingTestObject origValue = new BindingTestObject();
            origValue.IntProperty = 35;

            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", origValue);

            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            BindingTestObject deserializedValue = deserialized.GetValue() as BindingTestObject;

            // assert equal
            Assert.Equal(origValue.IntProperty, deserializedValue.IntProperty);
        }

        [Theory]
        [InlineData(typeof(PasswordDataObject))]
        [InlineData(typeof(PasswordPropertyObject))]
        public void DataObject_Serialization_Password(Type type)
        {
            string password = "password";
            string encryptedPassword = EncryptionHelper.Encrypt(password);

            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", password);
           
            // create xml
            StringWriter sw = new StringWriter();
            var writer = new XmlTextWriter(sw);
            orig.WriteXml(writer);

            // deserialize xml
            DataObject deserialized = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader = new XmlTextReader(new StringReader(sw.ToString()));
            reader.Read();
            deserialized.ReadXml(reader);

            // read enrypted string
            DataObject deserialized2 = (DataObject)FormatterServices.GetUninitializedObject(type);
            var reader2 = new XmlTextReader(new StringReader(sw.ToString()));
            reader2.Read();
            string storedValue = reader2.GetAttribute("value");

            Assert.Equal(password, deserialized.GetValue());
            Assert.Equal(encryptedPassword, storedValue);
        }

        [Theory]
        [InlineData(typeof(ShortPropertyObject), (short)22, (short)5, (short)60)]
        [InlineData(typeof(IntPropertyObject), (int)22, (int)5, (int)60)]
        [InlineData(typeof(LongPropertyObject), (long)22, (long)5, (long)60)]
        [InlineData(typeof(UnsignedShortPropertyObject), (ushort)22, (ushort)5, (ushort)60)]
        [InlineData(typeof(UnsignedIntPropertyObject), (uint)22, (uint)5, (uint)60)]
        [InlineData(typeof(UnsignedLongPropertyObject), (ulong)22, (ulong)5, (ulong)60)]
        public void NumericDataObject_SetValueReturnsFalseIfMinMaxValidationFails(Type type, object value, object minFail, object maxFail)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", value);

            INumericPropertyObject numeric = (INumericPropertyObject)orig;
            TypeConverter converter = TypeDescriptor.GetConverter(value);
            
            numeric.Max = converter.ConvertTo(50, value.GetType());
            numeric.Min = converter.ConvertTo(10, value.GetType());

            Assert.False(orig.SetValue(minFail));
            Assert.False(orig.SetValue(maxFail));
        }

        [Theory]
        [InlineData(typeof(ShortPropertyObject), (short)22, (short)5, (short)60)]
        [InlineData(typeof(IntPropertyObject), (int)22, (int)5, (int)60)]
        [InlineData(typeof(LongPropertyObject), (long)22, (long)5, (long)60)]
        [InlineData(typeof(UnsignedShortPropertyObject), (ushort)22, (ushort)5, (ushort)60)]
        [InlineData(typeof(UnsignedIntPropertyObject), (uint)22, (uint)5, (uint)60)]
        [InlineData(typeof(UnsignedLongPropertyObject), (ulong)22, (ulong)5, (ulong)60)]
        public void NumericDataObject_SetValueReturnsTrueIfMinMaxValidationPasses(Type type, object value, object minFail, object maxFail)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", value);

            INumericPropertyObject numeric = (INumericPropertyObject)orig;
            TypeConverter converter = TypeDescriptor.GetConverter(value);

            numeric.Max = converter.ConvertTo(100, value.GetType());
            numeric.Min = converter.ConvertTo(0, value.GetType());

            Assert.True(orig.SetValue(minFail));
            Assert.True(orig.SetValue(maxFail));
        }

        [Theory]
        [InlineData(typeof(ShortPropertyObject), (short)22, (short)5, (short)60)]
        [InlineData(typeof(IntPropertyObject), (int)22, (int)5, (int)60)]
        [InlineData(typeof(LongPropertyObject), (long)22, (long)5, (long)60)]
        [InlineData(typeof(UnsignedShortPropertyObject), (ushort)22, (ushort)5, (ushort)60)]
        [InlineData(typeof(UnsignedIntPropertyObject), (uint)22, (uint)5, (uint)60)]
        [InlineData(typeof(UnsignedLongPropertyObject), (ulong)22, (ulong)5, (ulong)60)]
        public void NumericDataObject_SetValueReturnsTrueIfMinMaxIsNotSet(Type type, object value, object minFail, object maxFail)
        {
            DataObject orig = (DataObject)Activator.CreateInstance(type, "A", value);

            Assert.True(orig.SetValue(minFail));
            Assert.True(orig.SetValue(maxFail));
        }

        #endregion

        #region SnapShots

        [Fact]
        public void SnapShot_GetsAllValuesSingleLevelData()
        {
            IDataContainer A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .Data("D", 4)
                .Build();

            SnapShot snapShot = A.GetSnapShot();

            Assert.Equal(4, snapShot.Count());
            Assert.Contains("A", snapShot.Keys);
            Assert.Contains("B", snapShot.Keys);
            Assert.Contains("C", snapShot.Keys);
            Assert.Contains("D", snapShot.Keys);

            Assert.Equal(A["A"], snapShot["A"].Value);
            Assert.Equal(A["B"], snapShot["B"].Value);
            Assert.Equal(A["C"], snapShot["C"].Value);
            Assert.Equal(A["D"], snapShot["D"].Value);
        }

        [Fact]
        public void SnapShot_GetAllValuesMultiLevelData()
        {
            IDataContainer A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1)
                    .Data("AB2", 2))
                .Build();

            SnapShot snapShot = A.GetSnapShot();

            Assert.Equal(5, snapShot.Count());
            Assert.Contains("A", snapShot.Keys);
            Assert.Contains("B", snapShot.Keys);
            Assert.Contains("C", snapShot.Keys);
            Assert.Contains("AB.AB1", snapShot.Keys);
            Assert.Contains("AB.AB2", snapShot.Keys);

            Assert.Equal(A["A"], snapShot["A"].Value);
            Assert.Equal(A["B"], snapShot["B"].Value);
            Assert.Equal(A["C"], snapShot["C"].Value);
            Assert.Equal(A["AB.AB1"], snapShot["AB.AB1"].Value);
            Assert.Equal(A["AB.AB2"], snapShot["AB.AB2"].Value);
        }

        [Fact]
        public void SnapShot_ValuesShouldNotChangeWhenIDataContainerChanges()
        {
            IDataContainer A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Build();

            SnapShot snapShot = A.GetSnapShot();

            Assert.Single(snapShot);
            Assert.Equal(A["A"], snapShot["A"].Value);

            object origValue = A["A"];
            A["A"] = 3;

            Assert.NotEqual(A["A"], snapShot["A"].Value);
            Assert.Equal(origValue, snapShot["A"].Value);
        }

        [Fact]
        public void SnapShot_Difference_OnlyHasValuesWhichAreDifferent()
        {
            IDataContainer A = (DataContainerBase)DataContainerBuilder.Create("A")
                .Data("A", 1)
                .Data("B", 2)
                .Data("C", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1)
                    .Data("AB2", 2))
                .Build();

            IDataContainer B = (DataContainerBase)DataContainerBuilder.Create("B")
                .Data("A", 1)
                .Data("B", 22)
                .Data("C", 3)
                .DataContainer("AB", b => b
                    .Data("AB1", 1)
                    .Data("AB2", 21))
                .Build();

            SnapShotDiff diff = A.GetSnapShot() - B.GetSnapShot();

            Assert.Equal(2, diff.Count());
            Assert.Contains("B", diff.Keys);
            Assert.Contains("AB.AB2", diff.Keys);

            Assert.Equal(A["B"], diff["B"].Left);
            Assert.Equal(B["B"], diff["B"].Right);

            Assert.Equal(A["AB.AB2"], diff["AB.AB2"].Left);
            Assert.Equal(B["AB.AB2"], diff["AB.AB2"].Right);
        }

        #endregion
    }
}
